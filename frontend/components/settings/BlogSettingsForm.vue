<script setup lang="ts">
// The «Блог» section of the settings (E04-03, FR-PROFILE, docs/ui/settings.md): the avatar, the name and the
// description. The limits (25 and 160 characters as people see them) are checked here as the person types and again
// by the API, whose field errors land under the same fields. A new avatar is cropped in the browser
// (AvatarCropper.vue), uploaded through the media service on «Сохранить» and saved with the profile (ADR-0034).
// While saving, the button shows its indicator and the fields stay editable; a failure keeps what was typed.
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import AvatarCropper from './AvatarCropper.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import UiModal from '../ui/UiModal.vue'
import { useToast } from '../../composables/useToast'
import type { MediaUpload } from '../../api/media'
import {
  blogTitle,
  descriptionMaxLength,
  nameMaxLength,
  profileInputErrors,
  profileTextLength,
  type Profile,
  type ProfileInput,
  type ProfileSave,
} from '../../api/profiles'

const props = defineProps<{
  profile: Profile
  save: (input: ProfileInput) => Promise<ProfileSave>
  upload: (avatar: Blob, fileName: string) => Promise<MediaUpload>
}>()
const emit = defineEmits<{ saved: [profile: Profile]; unauthorized: [] }>()

/** What the media service accepts as an image (ADR-0030) and its size limit (Media:MaxBytes:Image). */
const imageTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']
const imageMaxBytes = 20 * 1024 * 1024

const toast = useToast()
const name = ref(props.profile.name ?? '')
const description = ref(props.profile.description ?? '')
const avatarId = ref<string | null>(props.profile.avatarId)
/** The cropped avatar not uploaded yet, and its picture for the preview. */
const pendingAvatar = ref<Blob | null>(null)
const preview = ref<string>()
const choosing = ref<File | null>(null)
const cropping = ref(false)
const saving = ref(false)
const serverErrors = ref<Record<string, string>>({})
const avatarError = ref<string>()
const fileInput = ref<HTMLInputElement>()
const form = ref<HTMLFormElement>()

const clientErrors = computed(() => profileInputErrors({ name: name.value, description: description.value }))
const errors = computed(() => ({ ...serverErrors.value, ...clientErrors.value }))
const title = computed(() => blogTitle({ name: name.value, nickname: props.profile.nickname }))
const hasAvatar = computed(() => pendingAvatar.value !== null || avatarId.value !== null)

// A field edited after a server error is checked again by the rules here, not by the old answer.
watch(name, () => delete serverErrors.value.name)
watch(description, () => delete serverErrors.value.description)

function setPreview(blob: Blob | null) {
  if (preview.value) URL.revokeObjectURL(preview.value)
  preview.value = blob ? URL.createObjectURL(blob) : undefined
}
onBeforeUnmount(() => setPreview(null))

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
    avatarError.value = 'Выберите картинку: JPEG, PNG, WebP или GIF.'
    return
  }
  if (file.size > imageMaxBytes) {
    avatarError.value = 'Картинка больше 20 МБ, выберите поменьше.'
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
  avatarError.value = 'Не удалось открыть картинку, выберите другую.'
}

function removeAvatar() {
  pendingAvatar.value = null
  avatarId.value = null
  avatarError.value = undefined
  delete serverErrors.value.avatarId
  setPreview(null)
}

async function focusFirstError() {
  await nextTick()
  const field = ['name', 'description'].find((key) => errors.value[key])
  form.value?.querySelector<HTMLElement>(`[data-field="${field}"]`)?.focus()
}

const uploadErrors: Record<Exclude<MediaUpload['status'], 'uploaded'>, string> = {
  unsupported: 'Этот формат картинки не подходит, выберите JPEG, PNG, WebP или GIF.',
  'too-large': 'Картинка больше 20 МБ, выберите поменьше.',
  unauthorized: '',
  error: 'Не удалось загрузить аватар. Попробуйте ещё раз.',
}

async function submit() {
  if (saving.value) return
  if (Object.keys(clientErrors.value).length > 0) return focusFirstError()
  saving.value = true
  try {
    let avatar = avatarId.value
    if (pendingAvatar.value) {
      const uploaded = await props.upload(pendingAvatar.value, 'avatar.jpg')
      if (uploaded.status === 'unauthorized') return emit('unauthorized')
      if (uploaded.status !== 'uploaded') {
        avatarError.value = uploadErrors[uploaded.status]
        return
      }
      avatar = uploaded.media.id
    }
    const result = await props.save({ name: name.value, description: description.value, avatarId: avatar })
    if (result.status === 'saved') {
      serverErrors.value = {}
      avatarError.value = undefined
      // The preview stays: the uploaded avatar is the same picture.
      pendingAvatar.value = null
      avatarId.value = result.profile.avatarId
      name.value = result.profile.name ?? ''
      description.value = result.profile.description ?? ''
      toast.show('Изменения сохранены', { tone: 'success' })
      emit('saved', result.profile)
    } else if (result.status === 'invalid') {
      serverErrors.value = result.errors
      if (result.errors.avatarId) avatarError.value = result.errors.avatarId
      await focusFirstError()
    } else if (result.status === 'unauthorized') {
      emit('unauthorized')
    } else {
      toast.show('Не удалось сохранить. Попробуйте ещё раз.', { tone: 'error' })
    }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <form ref="form" class="blog-settings" novalidate data-testid="blog-settings" @submit.prevent="submit">
    <div class="blog-settings__avatar">
      <UiAvatar :name="title" :src="preview" size="lg" data-testid="avatar-preview" />
      <div class="blog-settings__avatar-actions">
        <UiButton variant="secondary" size="sm" @click="chooseFile">
          {{ hasAvatar ? 'Заменить аватар' : 'Загрузить аватар' }}
        </UiButton>
        <UiButton v-if="hasAvatar" variant="ghost" size="sm" @click="removeAvatar">Убрать аватар</UiButton>
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
      </div>
      <p v-if="avatarError" class="blog-settings__error" role="alert" data-testid="avatar-error">
        {{ avatarError }}
      </p>
    </div>

    <UiField
      v-model="name"
      label="Название"
      :hint="`${profileTextLength(name)}/${nameMaxLength}`"
      :error="errors.name"
      autocomplete="off"
      data-field="name"
    />
    <UiField
      v-model="description"
      label="Описание"
      multiline
      :rows="3"
      :hint="`${profileTextLength(description)}/${descriptionMaxLength}`"
      :error="errors.description"
      data-field="description"
    />

    <div class="blog-settings__submit">
      <UiButton type="submit" :loading="saving">Сохранить</UiButton>
    </div>

    <UiModal v-model:open="cropping" title="Аватар">
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
.blog-settings {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.blog-settings__avatar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-4);
}

.blog-settings__avatar-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.blog-settings__error {
  flex-basis: 100%;
  margin: 0;
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.blog-settings__submit {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 639px) {
  .blog-settings__submit > :deep(.ui-button) {
    width: 100%;
  }
}
</style>
