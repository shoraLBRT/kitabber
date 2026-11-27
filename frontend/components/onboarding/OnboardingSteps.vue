<script setup lang="ts">
// The onboarding after sign-up (E03-10, FR-AUTH, ADR-0102): the name, the avatar and the
// topics, one step at a time. Every step has «Пропустить»: it drops that step's choice and goes on, so nothing here
// can keep the person from the site. The choices are sent once, on the last step: the avatar is uploaded through the
// media service first (ADR-0034), then the name, the avatar and the topics go to the API together. A field error of
// the API brings its step back; any other failure keeps the choices for another try.
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import AvatarCropper from '../settings/AvatarCropper.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import UiModal from '../ui/UiModal.vue'
import { useToast } from '../../composables/useToast'
import type { MediaUpload } from '../../api/media'
import { onboardingNameError, type OnboardingFinish, type OnboardingInput } from '../../api/onboarding'
import { nameMaxLength, profileTextLength, type Profile } from '../../api/profiles'
import type { TopicCard } from '../../api/topics'
import { t } from '../../i18n'

const props = defineProps<{
  profile: Profile
  /** The topics to choose from, the most followed first; null when they could not be loaded, undefined while loading. */
  topics?: TopicCard[] | null
  save: (input: OnboardingInput) => Promise<OnboardingFinish>
  upload: (avatar: Blob, fileName: string) => Promise<MediaUpload>
}>()
const emit = defineEmits<{ done: []; unauthorized: []; retryTopics: [] }>()

/** What the media service accepts as an image (ADR-0030) and its size limit (Media:MaxBytes:Image). */
const imageTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']
const imageMaxBytes = 20 * 1024 * 1024

type Step = 'name' | 'avatar' | 'topics'
const steps: Step[] = ['name', 'avatar', 'topics']

const toast = useToast()
const step = ref<Step>('name')
const name = ref(props.profile.name ?? '')
const nameTouched = ref(false)
const pendingAvatar = ref<Blob | null>(null)
const preview = ref<string>()
const choosing = ref<File | null>(null)
const cropping = ref(false)
const avatarError = ref<string>()
const chosen = ref<Set<string>>(new Set())
const serverErrors = ref<Record<string, string>>({})
const saving = ref(false)
const fileInput = ref<HTMLInputElement>()
const heading = ref<HTMLElement>()

const index = computed(() => steps.indexOf(step.value))
const last = computed(() => index.value === steps.length - 1)
const nameError = computed(
  () => serverErrors.value.name ?? (nameTouched.value ? onboardingNameError(name.value) : null),
)
const shownName = computed(() => name.value.trim() || props.profile.name || '')

watch(name, () => delete serverErrors.value.name)

function setPreview(blob: Blob | null) {
  if (preview.value) URL.revokeObjectURL(preview.value)
  preview.value = blob ? URL.createObjectURL(blob) : undefined
}
onBeforeUnmount(() => setPreview(null))

async function go(to: Step) {
  step.value = to
  await nextTick()
  heading.value?.focus()
}

function chooseFile() {
  fileInput.value?.click()
}

function onFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  avatarError.value = undefined
  if (!imageTypes.includes(file.type)) {
    avatarError.value = t('settings.avatarType')
    return
  }
  if (file.size > imageMaxBytes) {
    avatarError.value = t('settings.avatarTooLarge')
    return
  }
  choosing.value = file
  cropping.value = true
}

function onCropped(avatar: Blob) {
  pendingAvatar.value = avatar
  setPreview(avatar)
  delete serverErrors.value.avatarId
  cropping.value = false
}

function onCropFailed() {
  cropping.value = false
  avatarError.value = t('settings.avatarUnreadable')
}

function toggle(id: string) {
  const next = new Set(chosen.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  chosen.value = next
}

/** «Пропустить»: the step's choice is dropped, then the next step, or the end. */
function skip() {
  if (step.value === 'name') {
    name.value = ''
    nameTouched.value = false
  } else if (step.value === 'avatar') {
    pendingAvatar.value = null
    avatarError.value = undefined
    setPreview(null)
  } else {
    chosen.value = new Set()
  }
  return advance()
}

/** «Далее» / «Готово»: the step's choice is checked, then the next step, or the end. */
function next() {
  if (step.value === 'name') {
    nameTouched.value = true
    if (onboardingNameError(name.value)) return
  }
  return advance()
}

function advance() {
  if (last.value) return finish()
  return go(steps[index.value + 1]!)
}

const uploadErrors: Record<Exclude<MediaUpload['status'], 'uploaded'>, string> = {
  unsupported: t('settings.avatarUnsupported'),
  'too-large': t('settings.avatarTooLarge'),
  unauthorized: '',
  error: t('settings.avatarFailed'),
}

async function finish() {
  if (saving.value) return
  saving.value = true
  try {
    let avatarId: string | null = null
    if (pendingAvatar.value) {
      const uploaded = await props.upload(pendingAvatar.value, 'avatar.jpg')
      if (uploaded.status === 'unauthorized') return emit('unauthorized')
      if (uploaded.status !== 'uploaded') {
        avatarError.value = uploadErrors[uploaded.status]
        return go('avatar')
      }
      avatarId = uploaded.media.id
    }
    const result = await props.save({
      name: name.value.trim() || null,
      avatarId,
      topicIds: [...chosen.value],
    })
    if (result.status === 'saved' || result.status === 'done') return emit('done')
    if (result.status === 'unauthorized') return emit('unauthorized')
    if (result.status === 'invalid') {
      serverErrors.value = result.errors
      if (result.errors.avatarId) avatarError.value = result.errors.avatarId
      const back = result.errors.name ? 'name' : result.errors.avatarId ? 'avatar' : 'topics'
      return go(back)
    }
    toast.show(t('onboarding.saveFailed'), { tone: 'error' })
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <form class="onboarding" novalidate data-testid="onboarding" @submit.prevent="next">
    <p class="onboarding__progress" data-testid="onboarding-progress">
      {{ t('onboarding.stepOf', { step: index + 1, total: steps.length }) }}
    </p>

    <section v-if="step === 'name'" class="onboarding__step" aria-labelledby="onboarding-step">
      <h2 id="onboarding-step" ref="heading" class="onboarding__title" tabindex="-1">
        {{ t('onboarding.nameTitle') }}
      </h2>
      <p class="onboarding__text">{{ t('onboarding.nameText') }}</p>
      <UiField
        v-model="name"
        :label="t('onboarding.name')"
        :hint="`${profileTextLength(name)}/${nameMaxLength}`"
        :error="nameError ?? undefined"
        autocomplete="name"
        data-field="name"
      />
    </section>

    <section v-else-if="step === 'avatar'" class="onboarding__step" aria-labelledby="onboarding-step">
      <h2 id="onboarding-step" ref="heading" class="onboarding__title" tabindex="-1">
        {{ t('onboarding.avatarTitle') }}
      </h2>
      <p class="onboarding__text">{{ t('onboarding.avatarText') }}</p>
      <div class="onboarding__avatar">
        <UiAvatar :name="shownName" :src="preview" size="lg" data-testid="avatar-preview" />
        <UiButton variant="secondary" size="sm" @click="chooseFile">
          {{ pendingAvatar ? t('settings.replaceAvatar') : t('settings.uploadAvatar') }}
        </UiButton>
        <input
          ref="fileInput"
          class="visually-hidden"
          type="file"
          :accept="imageTypes.join(',')"
          tabindex="-1"
          aria-hidden="true"
          data-testid="avatar-file"
          @change="onFile"
        />
        <p v-if="avatarError" class="onboarding__error" role="alert" data-testid="avatar-error">
          {{ avatarError }}
        </p>
      </div>
    </section>

    <section v-else class="onboarding__step" aria-labelledby="onboarding-step">
      <h2 id="onboarding-step" ref="heading" class="onboarding__title" tabindex="-1">
        {{ t('onboarding.topicsTitle') }}
      </h2>
      <p class="onboarding__text">{{ t('onboarding.topicsText') }}</p>
      <p
        v-if="topics === undefined"
        class="onboarding__message"
        role="status"
        data-testid="onboarding-topics-loading"
      >
        {{ t('onboarding.loading') }}
      </p>
      <div v-else-if="topics === null" class="onboarding__message" data-testid="onboarding-topics-error">
        <p role="alert">{{ t('onboarding.topicsFailed') }}</p>
        <UiButton variant="secondary" size="sm" @click="emit('retryTopics')">{{
          t('common.retry')
        }}</UiButton>
      </div>
      <p v-else-if="topics.length === 0" class="onboarding__message" data-testid="onboarding-topics-empty">
        {{ t('onboarding.topicsEmpty') }}
      </p>
      <ul v-else class="onboarding__topics" data-testid="onboarding-topics">
        <li v-for="topic in topics" :key="topic.id">
          <button
            type="button"
            class="onboarding__topic"
            :aria-pressed="chosen.has(topic.id)"
            @click="toggle(topic.id)"
          >
            <span class="onboarding__topic-name">{{ topic.name }}</span>
            <span v-if="topic.description" class="onboarding__topic-text">{{ topic.description }}</span>
          </button>
        </li>
      </ul>
      <p v-if="serverErrors.topicIds" class="onboarding__error" role="alert">{{ serverErrors.topicIds }}</p>
      <p v-if="chosen.size > 0" class="onboarding__chosen" role="status">
        {{ t('onboarding.topicsChosen', { count: chosen.size }) }}
      </p>
    </section>

    <div class="onboarding__actions">
      <UiButton v-if="index > 0" variant="ghost" :disabled="saving" @click="go(steps[index - 1]!)">
        {{ t('onboarding.back') }}
      </UiButton>
      <span class="onboarding__spacer" />
      <UiButton variant="secondary" :disabled="saving" @click="skip">{{ t('onboarding.skip') }}</UiButton>
      <UiButton type="submit" :loading="saving">
        {{ last ? t('onboarding.finish') : t('onboarding.next') }}
      </UiButton>
    </div>

    <UiModal v-model:open="cropping" :title="t('settings.avatar')">
      <AvatarCropper
        v-if="choosing"
        :file="choosing"
        @done="onCropped"
        @cancel="cropping = false"
        @failed="onCropFailed"
      />
    </UiModal>
  </form>
</template>

<style scoped>
.onboarding {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.onboarding__progress {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.onboarding__step {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.onboarding__title {
  margin: 0;
  font-size: var(--font-size-xl);
  outline: none;
}

.onboarding__text,
.onboarding__message {
  margin: 0;
  color: var(--color-muted);
}

.onboarding__avatar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-4);
}

.onboarding__error {
  flex-basis: 100%;
  margin: 0;
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.onboarding__topics {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(calc(var(--space-16) * 3), 1fr));
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

.onboarding__topic {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  width: 100%;
  min-height: var(--size-touch);
  padding: var(--space-3);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: inherit;
  font: inherit;
  text-align: start;
  cursor: pointer;
}

.onboarding__topic[aria-pressed='true'] {
  border-color: var(--color-accent);
  box-shadow: inset 0 0 0 var(--size-border) var(--color-accent);
}

.onboarding__topic:focus-visible {
  outline: var(--size-focus-ring) solid var(--color-focus);
  outline-offset: var(--size-focus-ring);
}

.onboarding__topic-name {
  font-weight: var(--font-weight-bold);
}

.onboarding__topic-text {
  overflow: hidden;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.onboarding__chosen {
  margin: 0;
  font-size: var(--font-size-sm);
}

.onboarding__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.onboarding__spacer {
  flex: 1;
}

@media (max-width: 639px) {
  .onboarding__actions > :deep(.ui-button) {
    flex: 1;
  }

  .onboarding__spacer {
    display: none;
  }
}
</style>
