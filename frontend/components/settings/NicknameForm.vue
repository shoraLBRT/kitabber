<script setup lang="ts">
// The nickname of the person's own blog in the «Блог» section of the settings (E04-05, FR-PROFILE, ADR-0090). The
// shape is checked here as the person types; whether it is taken or reserved, and the 30 days, are the API's, whose
// answers land under the field. While the 30 days run, the field is locked and says from when it opens again.
import { computed, ref, watch } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import { useToast } from '../../composables/useToast'
import { nicknameInputError, type NicknameSave, type Profile } from '../../api/profiles'
import { locale, t } from '../../i18n'

const props = defineProps<{
  profile: Profile
  save: (nickname: string) => Promise<NicknameSave>
  /** The current moment; the tests pass a fixed one. */
  now?: () => Date
}>()
const emit = defineEmits<{ saved: [profile: Profile]; unauthorized: [] }>()

const toast = useToast()
const nickname = ref(props.profile.nickname ?? '')
const saving = ref(false)
const touched = ref(false)
const serverError = ref<string>()
const lockedMessage = ref<string>()

const changeableFrom = computed(() => {
  const from = props.profile.nicknameChangeableFrom
  return from && new Date(from) > (props.now?.() ?? new Date()) ? new Date(from) : null
})
const locked = computed(() => changeableFrom.value !== null || lockedMessage.value !== undefined)
const lockText = computed(
  () =>
    lockedMessage.value ??
    (changeableFrom.value
      ? t('settings.nicknameLockedUntil', {
          date: changeableFrom.value.toLocaleDateString(locale, {
            day: 'numeric',
            month: 'long',
            year: 'numeric',
          }),
        })
      : ''),
)
const normalized = computed(() => nickname.value.trim().replace(/^@/, '').toLowerCase())
const unchanged = computed(() => normalized.value === (props.profile.nickname ?? ''))
const error = computed(() =>
  touched.value ? (serverError.value ?? nicknameInputError(nickname.value) ?? undefined) : undefined,
)

watch(nickname, () => {
  serverError.value = undefined
  touched.value = true
})

async function submit() {
  touched.value = true
  if (saving.value || locked.value || unchanged.value || nicknameInputError(nickname.value)) return
  saving.value = true
  try {
    const result = await props.save(normalized.value)
    if (result.status === 'saved') {
      nickname.value = result.profile.nickname ?? ''
      touched.value = false
      toast.show(t('settings.nicknameSaved'), { tone: 'success' })
      emit('saved', result.profile)
    } else if (result.status === 'invalid') {
      serverError.value = result.errors.nickname ?? t('settings.saveFailed')
    } else if (result.status === 'too-soon') {
      lockedMessage.value = result.message
    } else if (result.status === 'unauthorized') {
      emit('unauthorized')
    } else {
      toast.show(t('settings.saveFailed'), { tone: 'error' })
    }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <form class="nickname-form" novalidate data-testid="nickname-form" @submit.prevent="submit">
    <UiField
      v-model="nickname"
      :label="t('settings.nickname')"
      :hint="t('settings.nicknameHint', { address: `/u/${normalized || '…'}` })"
      :error="error"
      :disabled="locked"
      autocomplete="off"
      data-field="nickname"
    />
    <p v-if="locked" class="nickname-form__lock" role="status" data-testid="nickname-locked">
      {{ lockText }}
    </p>
    <div class="nickname-form__submit">
      <UiButton type="submit" :loading="saving" :disabled="locked || unchanged">{{
        t('settings.nicknameSave')
      }}</UiButton>
    </div>
  </form>
</template>

<style scoped>
.nickname-form {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.nickname-form__lock {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.nickname-form__submit {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 639px) {
  .nickname-form__submit > :deep(.ui-button) {
    width: 100%;
  }
}
</style>
