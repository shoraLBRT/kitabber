<script setup lang="ts">
// The management of a topic (E11-12, FR-TOPIC, docs/ui/topic.md, ADR-0121) at /topics/<id>/manage. Everyone reads the
// rules and the moderators. The owner edits the cover (BlogCover.vue, passed in by the page), the description and the
// rules, appoints moderators by the nickname of their blog and removes them, and offers the topic to another person or
// cancels the offer. The person the topic is offered to accepts or declines here; the topic is theirs only then. What
// moderators may do comes with the moderation (E15). Every change answers with the whole management, shown at once.
import { computed, ref, watch } from 'vue'
import BlogCover, { type CoverActions } from '../blog/BlogCover.vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import { useToast } from '../../composables/useToast'
import { descriptionMaxLength, profileTextLength, type Profile, type ProfileCover } from '../../api/profiles'
import {
  memberName,
  memberPath,
  rulesMaxLength,
  settingsInputErrors,
  type ManagementAction,
  type ManagementSave,
  type TopicManagement,
  type TopicManagementApi,
} from '../../api/topic-management'
import { t } from '../../i18n'

const props = defineProps<{
  management: TopicManagement
  api: TopicManagementApi
  cover?: ProfileCover | null
  coverActions?: CoverActions
}>()
const emit = defineEmits<{
  changed: [management: TopicManagement]
  cover: [profile: Profile]
  unauthorized: []
}>()

const toast = useToast()
const owner = computed(() => props.management.role === 'owner')

const description = ref(props.management.description ?? '')
const rules = ref(props.management.rules ?? '')
const moderatorNickname = ref('')
const recipientNickname = ref('')
/** Which change is on its way: its button shows the indicator. */
const busy = ref<string>()
const errors = ref<Record<string, string>>({})
const sectionError = ref<{ section: string; message: string }>()

watch(
  () => props.management,
  (management) => {
    description.value = management.description ?? ''
    rules.value = management.rules ?? ''
  },
)

const settingsErrors = computed<Record<string, string | undefined>>(() => ({
  description: errors.value['settings.description'],
  rules: errors.value['settings.rules'],
  ...settingsInputErrors({ description: description.value, rules: rules.value }),
}))

async function run(section: string, change: ManagementAction, done?: string): Promise<boolean> {
  busy.value = section
  sectionError.value = undefined
  errors.value = {}
  const result: ManagementSave = await props.api.change(change)
  busy.value = undefined
  switch (result.status) {
    case 'saved':
      emit('changed', result.management)
      if (done) toast.show(done, { tone: 'success' })
      return true
    case 'invalid':
      errors.value = Object.fromEntries(
        Object.entries(result.errors).map(([field, message]) => [`${section}.${field}`, message]),
      )
      return false
    case 'refused':
      sectionError.value = { section, message: result.message }
      return false
    case 'unauthorized':
      emit('unauthorized')
      return false
    default:
      sectionError.value = { section, message: t('topicManage.failed') }
      return false
  }
}

async function saveSettings() {
  if (Object.keys(settingsInputErrors({ description: description.value, rules: rules.value })).length > 0)
    return
  await run(
    'settings',
    { action: 'settings', description: description.value.trim() || null, rules: rules.value.trim() || null },
    t('topicManage.saved'),
  )
}

async function appoint() {
  if (await run('moderators', { action: 'appoint', nickname: moderatorNickname.value.trim() }))
    moderatorNickname.value = ''
}

async function offer() {
  if (await run('transfer', { action: 'offer', nickname: recipientNickname.value.trim() }))
    recipientNickname.value = ''
}
</script>

<template>
  <div class="manage" data-testid="topic-management">
    <section
      v-if="management.transferToYou"
      class="manage__card manage__card--offer"
      data-testid="transfer-offer"
    >
      <p class="manage__text">
        {{ t('topicManage.offerToYou', { name: management.owner ? memberName(management.owner) : '' }) }}
      </p>
      <p v-if="sectionError?.section === 'offer'" class="manage__error" role="alert">
        {{ sectionError.message }}
      </p>
      <div class="manage__actions">
        <UiButton
          :loading="busy === 'offer'"
          data-testid="transfer-accept"
          @click="run('offer', { action: 'accept' }, t('topicManage.accepted'))"
          >{{ t('topicManage.accept') }}</UiButton
        >
        <UiButton
          variant="secondary"
          :disabled="busy === 'offer'"
          data-testid="transfer-decline"
          @click="run('offer', { action: 'withdraw' }, t('topicManage.declined'))"
          >{{ t('topicManage.decline') }}</UiButton
        >
      </div>
    </section>

    <section v-if="owner" class="manage__card" aria-labelledby="manage-looks" data-testid="manage-looks">
      <h2 id="manage-looks" class="manage__heading">{{ t('topicManage.looks') }}</h2>
      <template v-if="coverActions">
        <BlogCover
          :cover="cover ?? null"
          :actions="coverActions"
          @saved="emit('cover', $event)"
          @unauthorized="emit('unauthorized')"
        />
        <p class="manage__muted">{{ t('topicManage.coverHint') }}</p>
      </template>
      <form class="manage__form" novalidate data-testid="manage-settings" @submit.prevent="saveSettings">
        <UiField
          v-model="description"
          :label="t('topicManage.description')"
          multiline
          :rows="3"
          :hint="`${profileTextLength(description)}/${descriptionMaxLength}`"
          :error="settingsErrors.description"
          data-field="description"
        />
        <UiField
          v-model="rules"
          :label="t('topicManage.rules')"
          multiline
          :rows="6"
          :hint="`${profileTextLength(rules)}/${rulesMaxLength}`"
          :error="settingsErrors.rules"
          data-field="rules"
        />
        <p v-if="sectionError?.section === 'settings'" class="manage__error" role="alert">
          {{ sectionError.message }}
        </p>
        <div class="manage__actions">
          <UiButton type="submit" :loading="busy === 'settings'" data-testid="manage-settings-save">{{
            t('topicManage.save')
          }}</UiButton>
        </div>
      </form>
    </section>

    <section v-else class="manage__card" aria-labelledby="manage-rules" data-testid="manage-rules">
      <h2 id="manage-rules" class="manage__heading">{{ t('topicManage.rules') }}</h2>
      <p v-if="management.rules" class="manage__rules" data-testid="manage-rules-text">
        {{ management.rules }}
      </p>
      <p v-else class="manage__muted">{{ t('topicManage.noRules') }}</p>
    </section>

    <section class="manage__card" aria-labelledby="manage-moderators" data-testid="manage-moderators">
      <h2 id="manage-moderators" class="manage__heading">{{ t('topicManage.moderators') }}</h2>
      <ul class="manage__list">
        <li v-if="management.owner" class="manage__member" data-testid="manage-owner">
          <NuxtLink
            v-if="memberPath(management.owner)"
            :to="memberPath(management.owner)!"
            class="manage__name"
            >{{ memberName(management.owner) }}</NuxtLink
          >
          <span v-else class="manage__name">{{ memberName(management.owner) }}</span>
          <span class="manage__muted">{{ t('topicManage.owner') }}</span>
        </li>
        <li
          v-for="moderator in management.moderators"
          :key="moderator.accountId"
          class="manage__member"
          data-testid="manage-moderator"
        >
          <NuxtLink v-if="memberPath(moderator)" :to="memberPath(moderator)!" class="manage__name">{{
            memberName(moderator)
          }}</NuxtLink>
          <span v-else class="manage__name">{{ memberName(moderator) }}</span>
          <UiButton
            v-if="owner"
            variant="ghost"
            size="sm"
            :disabled="busy === 'moderators'"
            :aria-label="t('topicManage.removeModerator', { name: memberName(moderator) })"
            data-testid="manage-moderator-remove"
            @click="run('moderators', { action: 'remove-moderator', accountId: moderator.accountId })"
            >{{ t('topicManage.remove') }}</UiButton
          >
        </li>
      </ul>
      <p v-if="management.moderators.length === 0" class="manage__muted" data-testid="manage-no-moderators">
        {{ t('topicManage.noModerators') }}
      </p>
      <form
        v-if="owner"
        class="manage__inline"
        novalidate
        data-testid="manage-appoint"
        @submit.prevent="appoint"
      >
        <UiField
          v-model="moderatorNickname"
          :label="t('topicManage.nickname')"
          :error="errors['moderators.nickname']"
          autocomplete="off"
          autocapitalize="off"
          spellcheck="false"
          data-field="moderator"
        />
        <UiButton
          type="submit"
          variant="secondary"
          :loading="busy === 'moderators'"
          :disabled="!moderatorNickname.trim()"
          data-testid="manage-appoint-submit"
          >{{ t('topicManage.appoint') }}</UiButton
        >
      </form>
      <p v-if="sectionError?.section === 'moderators'" class="manage__error" role="alert">
        {{ sectionError.message }}
      </p>
      <p v-if="owner" class="manage__muted">{{ t('topicManage.moderatorsHint') }}</p>
    </section>

    <section
      v-if="owner"
      class="manage__card"
      aria-labelledby="manage-transfer"
      data-testid="manage-transfer"
    >
      <h2 id="manage-transfer" class="manage__heading">{{ t('topicManage.transfer') }}</h2>
      <p class="manage__muted">{{ t('topicManage.transferHint') }}</p>
      <template v-if="management.transfer">
        <p class="manage__text" data-testid="manage-transfer-pending">
          {{ t('topicManage.offered', { name: memberName(management.transfer.to) }) }}
        </p>
        <div class="manage__actions">
          <UiButton
            variant="secondary"
            :loading="busy === 'transfer'"
            data-testid="manage-transfer-cancel"
            @click="run('transfer', { action: 'withdraw' })"
            >{{ t('topicManage.cancelOffer') }}</UiButton
          >
        </div>
      </template>
      <form v-else class="manage__inline" novalidate data-testid="manage-offer" @submit.prevent="offer">
        <UiField
          v-model="recipientNickname"
          :label="t('topicManage.nickname')"
          :error="errors['transfer.nickname']"
          autocomplete="off"
          autocapitalize="off"
          spellcheck="false"
          data-field="recipient"
        />
        <UiButton
          type="submit"
          variant="secondary"
          :loading="busy === 'transfer'"
          :disabled="!recipientNickname.trim()"
          data-testid="manage-offer-submit"
          >{{ t('topicManage.offer') }}</UiButton
        >
      </form>
      <p v-if="sectionError?.section === 'transfer'" class="manage__error" role="alert">
        {{ sectionError.message }}
      </p>
    </section>
  </div>
</template>

<style scoped>
.manage {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.manage__card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.manage__card--offer {
  border: var(--size-border) solid var(--color-border);
}

.manage__heading {
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
}

.manage__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.manage__inline {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: var(--space-3);
}

.manage__inline > :first-child {
  flex: 1 1 14rem;
  min-width: 0;
}

.manage__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.manage__list {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

.manage__member {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  min-width: 0;
}

.manage__name {
  overflow: hidden;
  color: var(--color-text);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.manage__text,
.manage__rules {
  margin: 0;
}

.manage__rules {
  white-space: pre-line;
  overflow-wrap: anywhere;
}

.manage__muted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.manage__error {
  margin: 0;
  color: var(--color-danger);
}

@media (max-width: 639px) {
  .manage__card {
    padding: var(--space-4);
    border-radius: 0;
  }
}
</style>
