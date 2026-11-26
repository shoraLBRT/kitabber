<script setup lang="ts">
// The devices of the account in the settings (E03-09, FR-AUTH, ADR-0078, docs/ui/settings.md): every device signed in,
// with its browser and system and the time it was last active; this one is marked «Это устройство». Any other one can
// be signed out with «Выйти», and all of them at once with «Выйти со всех устройств» after a confirmation in a dialog.
// A signed-out device stops working within a minute. Until the list is read the section shows a skeleton, and
// «Повторить» when it could not be. The calls come in as `api` (api/sessions.ts), so the list is tested without a server.
import { computed, onMounted, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiModal from '../ui/UiModal.vue'
import type { AccountSession, SessionsApi, SessionsOutcome } from '../../api/sessions'
import { locale, t } from '../../i18n'

const props = defineProps<{ api: SessionsApi }>()

const state = ref<'loading' | 'error' | 'ready'>('loading')
const sessions = ref<AccountSession[]>([])
const ending = ref<string>()
const endingOthers = ref(false)
const confirmOpen = ref(false)
const formError = ref<string>()
const notice = ref<string>()

const others = computed(() => sessions.value.filter((s) => !s.current))

const timeFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

async function load() {
  state.value = 'loading'
  const result = await props.api.load()
  if (result.status !== 200 || !Array.isArray(result.body)) {
    state.value = 'error'
    return
  }
  sessions.value = result.body as AccountSession[]
  state.value = 'ready'
}

onMounted(load)

function failure(result: SessionsOutcome, failed: string): string {
  return result.status === 401 ? t('settings.sessionEnded') : failed
}

function deviceName(session: AccountSession): string {
  return session.device ?? t('settings.deviceUnknown')
}

async function end(session: AccountSession) {
  formError.value = notice.value = undefined
  ending.value = session.id
  const result = await props.api.end(session.id)
  ending.value = undefined
  // 404: the device was signed out already, from somewhere else.
  if (result.status === 204 || result.status === 404) {
    sessions.value = sessions.value.filter((s) => s.id !== session.id)
    notice.value = t('settings.deviceSignedOut')
    return
  }
  formError.value = failure(result, t('settings.deviceSignOutFailed'))
}

async function endOthers() {
  formError.value = notice.value = undefined
  endingOthers.value = true
  const result = await props.api.endOthers()
  endingOthers.value = false
  confirmOpen.value = false
  if (result.status === 200) {
    sessions.value = sessions.value.filter((s) => s.current)
    notice.value = t('settings.signOutOthersDone')
    return
  }
  formError.value = failure(result, t('settings.signOutOthersFailed'))
}
</script>

<template>
  <section class="sessions" data-testid="sessions" :aria-busy="state === 'loading'">
    <h2 class="sessions__title">{{ t('settings.devices') }}</h2>

    <template v-if="state === 'loading'">
      <span class="visually-hidden" role="status">{{ t('settings.devicesLoading') }}</span>
      <div class="sessions__skeleton" data-testid="sessions-loading" />
    </template>

    <template v-else-if="state === 'error'">
      <p class="sessions__error" role="alert">{{ t('settings.devicesLoadFailed') }}</p>
      <div>
        <UiButton variant="secondary" @click="load()">{{ t('common.retry') }}</UiButton>
      </div>
    </template>

    <template v-else>
      <ul class="sessions__list">
        <li
          v-for="session in sessions"
          :key="session.id"
          class="sessions__item"
          data-testid="session"
          :data-current="session.current || undefined"
        >
          <div class="sessions__info">
            <p class="sessions__device">
              {{ deviceName(session) }}
              <span v-if="session.current" class="sessions__badge">{{ t('settings.deviceCurrent') }}</span>
            </p>
            <p class="sessions__time">
              {{
                t('settings.deviceLastActive', { time: timeFormat.format(new Date(session.lastActiveAt)) })
              }}
            </p>
          </div>
          <UiButton
            v-if="!session.current"
            variant="secondary"
            :loading="ending === session.id"
            :aria-label="t('settings.deviceSignOutLabel', { device: deviceName(session) })"
            @click="end(session)"
          >
            {{ t('settings.deviceSignOut') }}
          </UiButton>
        </li>
      </ul>

      <p v-if="others.length === 0" class="sessions__hint" data-testid="sessions-only-this">
        {{ t('settings.devicesOnlyThis') }}
      </p>
      <div v-else class="sessions__actions">
        <UiButton variant="danger" @click="confirmOpen = true">{{ t('settings.signOutOthers') }}</UiButton>
      </div>

      <p v-if="formError" class="sessions__error" role="alert">{{ formError }}</p>
      <p v-if="notice" class="sessions__notice" role="status">{{ notice }}</p>
    </template>

    <UiModal v-model:open="confirmOpen" :title="t('settings.signOutOthersConfirm')">
      <p class="sessions__text">{{ t('settings.signOutOthersText') }}</p>
      <template #footer>
        <UiButton variant="secondary" data-autofocus @click="confirmOpen = false">{{
          t('actions.cancel')
        }}</UiButton>
        <UiButton
          variant="danger"
          :loading="endingOthers"
          data-testid="sign-out-others-confirm"
          @click="endOthers"
        >
          {{ t('settings.signOutOthers') }}
        </UiButton>
      </template>
    </UiModal>
  </section>
</template>

<style scoped>
.sessions {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  min-width: 0;
}

.sessions__title {
  margin: 0;
  font-size: var(--font-size-lg);
}

.sessions__list {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.sessions__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: var(--size-border) solid var(--color-border);
}

.sessions__info {
  min-width: 0;
}

.sessions__device,
.sessions__time,
.sessions__hint,
.sessions__text {
  margin: 0;
  overflow-wrap: anywhere;
}

.sessions__time,
.sessions__hint {
  color: var(--color-muted);
}

.sessions__badge {
  margin-inline-start: var(--space-2);
  color: var(--color-success);
}

.sessions__error {
  margin: 0;
  color: var(--color-danger);
}

.sessions__notice {
  margin: 0;
  color: var(--color-success);
}

.sessions__actions {
  display: flex;
  justify-content: flex-end;
}

.sessions__skeleton {
  height: var(--size-touch);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}
</style>
