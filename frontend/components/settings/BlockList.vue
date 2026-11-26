<script setup lang="ts">
// The black list in «Приватность и блокировки» (E11-06, FR-SUB, ADR-0091, docs/ui/settings.md): the accounts the
// person blocked, the latest first, each with the name and avatar of its blog and «Разблокировать». Until the list is
// read the section shows a skeleton, «Повторить» when it could not be, and a hint when it is empty. The calls come in as
// `api` (api/blocks.ts) and the names as `profiles`, so the list is tested without a server.
import { onMounted, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import type { BlockedAccount, BlockList, BlocksApi } from '../../api/blocks'
import type { Profile } from '../../api/profiles'
import { locale, t } from '../../i18n'

const props = defineProps<{
  api: BlocksApi
  profiles: (accountIds: readonly string[]) => Promise<Map<string, Profile | null>>
}>()

const state = ref<'loading' | 'error' | 'ready'>('loading')
const accounts = ref<BlockedAccount[]>([])
const names = ref(new Map<string, Profile | null>())
const removing = ref<string>()
const formError = ref<string>()
const notice = ref<string>()

const dateFormat = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' })

async function load() {
  state.value = 'loading'
  const result = await props.api.load()
  const list = result.body as BlockList | undefined
  if (result.status !== 200 || !Array.isArray(list?.accounts)) {
    state.value = 'error'
    return
  }
  accounts.value = list.accounts
  names.value = await props.profiles(list.accounts.map((a) => a.accountId))
  state.value = 'ready'
}

onMounted(load)

function nameOf(account: BlockedAccount): string {
  const profile = names.value.get(account.accountId)
  return profile?.name ?? (profile?.nickname ? `@${profile.nickname}` : t('settings.blockedUnknown'))
}

async function unblock(account: BlockedAccount) {
  formError.value = notice.value = undefined
  removing.value = account.accountId
  const result = await props.api.unblock(account.accountId)
  removing.value = undefined
  if (result.status === 204) {
    accounts.value = accounts.value.filter((a) => a.accountId !== account.accountId)
    notice.value = t('settings.unblocked', { name: nameOf(account) })
    return
  }
  formError.value = result.status === 401 ? t('settings.sessionEnded') : t('settings.unblockFailed')
}
</script>

<template>
  <section class="blocks" data-testid="block-list" :aria-busy="state === 'loading'">
    <h2 class="blocks__title">{{ t('settings.blackList') }}</h2>
    <p class="blocks__hint">{{ t('settings.blackListHint') }}</p>

    <template v-if="state === 'loading'">
      <span class="visually-hidden" role="status">{{ t('settings.blackListLoading') }}</span>
      <div class="blocks__skeleton" data-testid="block-list-loading" />
    </template>

    <template v-else-if="state === 'error'">
      <p class="blocks__error" role="alert">{{ t('settings.blackListLoadFailed') }}</p>
      <div>
        <UiButton variant="secondary" @click="load()">{{ t('common.retry') }}</UiButton>
      </div>
    </template>

    <template v-else>
      <p v-if="accounts.length === 0" class="blocks__hint" data-testid="block-list-empty">
        {{ t('settings.blackListEmpty') }}
      </p>
      <ul v-else class="blocks__list">
        <li
          v-for="account in accounts"
          :key="account.accountId"
          class="blocks__item"
          data-testid="blocked-account"
        >
          <div class="blocks__info">
            <NuxtLink
              v-if="names.get(account.accountId)?.nickname"
              class="blocks__name"
              :to="`/u/${names.get(account.accountId)?.nickname}`"
            >
              {{ nameOf(account) }}
            </NuxtLink>
            <p v-else class="blocks__name">{{ nameOf(account) }}</p>
            <p class="blocks__time">
              {{ t('settings.blockedSince', { date: dateFormat.format(new Date(account.blockedAt)) }) }}
            </p>
          </div>
          <UiButton
            variant="secondary"
            :loading="removing === account.accountId"
            :aria-label="t('settings.unblockLabel', { name: nameOf(account) })"
            @click="unblock(account)"
          >
            {{ t('settings.unblock') }}
          </UiButton>
        </li>
      </ul>

      <p v-if="formError" class="blocks__error" role="alert">{{ formError }}</p>
      <p v-if="notice" class="blocks__notice" role="status">{{ notice }}</p>
    </template>
  </section>
</template>

<style scoped>
.blocks {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.blocks__title {
  margin: 0;
  font-size: var(--font-size-lg);
}

.blocks__list {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.blocks__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: var(--size-border) solid var(--color-border);
}

.blocks__info {
  min-width: 0;
}

.blocks__name,
.blocks__time,
.blocks__hint {
  margin: 0;
  overflow-wrap: anywhere;
}

.blocks__name {
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
}

.blocks__time,
.blocks__hint {
  color: var(--color-muted);
}

.blocks__error {
  margin: 0;
  color: var(--color-danger);
}

.blocks__notice {
  margin: 0;
  color: var(--color-success);
}

.blocks__skeleton {
  height: var(--size-touch);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}
</style>
