<script setup lang="ts">
// The form of a new topic (E11-11, FR-TOPIC, ADR-0084) at /topics/new: the name, the address and the description. The
// lengths and the address form are checked here as the person types; whether the name and the address are free,
// reserved or on the stop list, the confirmed email and the limit of topics per account are the API's, and its answers
// land under the same fields or above the button. While creating, the button shows its indicator; a failure keeps
// what was typed.
import { computed, nextTick, ref, watch } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import { descriptionMaxLength, nameMaxLength, profileTextLength } from '../../api/profiles'
import {
  normalizeTopicAddress,
  topicInputErrors,
  type Topic,
  type TopicCreate,
  type TopicInput,
} from '../../api/topics'
import { t } from '../../i18n'

const props = defineProps<{ create: (input: TopicInput) => Promise<TopicCreate> }>()
const emit = defineEmits<{ created: [topic: Topic]; unauthorized: [] }>()

const name = ref('')
const nickname = ref('')
const description = ref('')
const creating = ref(false)
/** Whether the person has tried to send: until then an empty field is not an error yet. */
const tried = ref(false)
const serverErrors = ref<Record<string, string>>({})
/** What stops the whole form: the limit, an unconfirmed email or a failure. */
const formError = ref<{ kind: 'limit' | 'unconfirmed' | 'error'; message: string }>()
const form = ref<HTMLFormElement>()

const clientErrors = computed(() =>
  topicInputErrors({ name: name.value, nickname: nickname.value, description: description.value }),
)
const shownClientErrors = computed(() =>
  Object.fromEntries(
    Object.entries(clientErrors.value).filter(
      ([field]) =>
        tried.value ||
        (field === 'description' ? description.value : field === 'name' ? name.value : nickname.value),
    ),
  ),
)
const errors = computed<Record<string, string>>(() => ({ ...serverErrors.value, ...shownClientErrors.value }))
const address = computed(() => normalizeTopicAddress(nickname.value))

// A field edited after a server error is checked again by the rules here, not by the old answer.
watch(name, () => delete serverErrors.value.name)
watch(nickname, () => delete serverErrors.value.nickname)
watch(description, () => delete serverErrors.value.description)

async function focusFirstError() {
  await nextTick()
  const field = ['name', 'nickname', 'description'].find((key) => errors.value[key])
  form.value?.querySelector<HTMLElement>(`[data-field="${field}"]`)?.focus()
}

async function submit() {
  if (creating.value) return
  tried.value = true
  formError.value = undefined
  if (Object.keys(clientErrors.value).length > 0) return focusFirstError()
  creating.value = true
  try {
    const result = await props.create({
      name: name.value.trim(),
      nickname: address.value,
      description: description.value.trim() || null,
    })
    if (result.status === 'created') {
      emit('created', result.topic)
    } else if (result.status === 'invalid') {
      serverErrors.value = result.errors
      await focusFirstError()
    } else if (result.status === 'unauthorized') {
      emit('unauthorized')
    } else if (result.status === 'limit') {
      formError.value = { kind: 'limit', message: result.message }
    } else if (result.status === 'unconfirmed') {
      formError.value = { kind: 'unconfirmed', message: t('topicCreate.unconfirmed') }
    } else {
      formError.value = { kind: 'error', message: t('topicCreate.failed') }
    }
  } finally {
    creating.value = false
  }
}
</script>

<template>
  <form ref="form" class="topic-create" novalidate data-testid="topic-create" @submit.prevent="submit">
    <p class="topic-create__intro">{{ t('topicCreate.intro') }}</p>
    <UiField
      v-model="name"
      :label="t('topicCreate.name')"
      :hint="`${profileTextLength(name.trim())}/${nameMaxLength}`"
      :error="errors.name"
      autocomplete="off"
      data-field="name"
    />
    <UiField
      v-model="nickname"
      :label="t('topicCreate.address')"
      :hint="t('topicCreate.addressHint', { address: address || '…' })"
      :error="errors.nickname"
      autocomplete="off"
      autocapitalize="off"
      spellcheck="false"
      data-field="nickname"
    />
    <UiField
      v-model="description"
      :label="t('topicCreate.description')"
      multiline
      :rows="3"
      :hint="`${profileTextLength(description)}/${descriptionMaxLength}`"
      :error="errors.description"
      data-field="description"
    />

    <div v-if="formError" class="topic-create__alert" role="alert" data-testid="topic-create-error">
      <p class="topic-create__alert-text">{{ formError.message }}</p>
      <NuxtLink v-if="formError.kind === 'unconfirmed'" to="/settings/account" class="topic-create__link">{{
        t('topicCreate.confirmEmail')
      }}</NuxtLink>
    </div>

    <div class="topic-create__submit">
      <UiButton type="submit" :loading="creating" data-testid="topic-create-submit">{{
        t('topicCreate.create')
      }}</UiButton>
    </div>
  </form>
</template>

<style scoped>
.topic-create {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.topic-create__intro {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.topic-create__alert {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.topic-create__alert-text {
  margin: 0;
  color: var(--color-danger);
}

.topic-create__link {
  color: var(--color-text);
  text-decoration: underline;
}

.topic-create__submit {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 639px) {
  .topic-create__submit :deep(button) {
    width: 100%;
  }
}
</style>
