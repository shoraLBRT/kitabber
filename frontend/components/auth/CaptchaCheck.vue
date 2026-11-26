<script setup lang="ts">
// The captcha of the sign-in (E03-12, ADR-0038): shown after the API answered `captcha_required`. The button fetches
// a challenge and solves it (`solve`, from api/sign-in.ts); the solution goes to `v-model` and is spent by the next
// attempt, so the form clears it after each one. States: waiting for the click, checking, failed, passed.
import { ref, watch } from 'vue'
import UiButton from '../ui/UiButton.vue'
import { t } from '../../i18n'

const props = defineProps<{ solve: () => Promise<string | undefined> }>()
const solution = defineModel<string>({ default: '' })

const state = ref<'idle' | 'loading' | 'error' | 'done'>(solution.value ? 'done' : 'idle')

watch(solution, (value) => {
  if (!value && state.value === 'done') state.value = 'idle'
})

async function check() {
  state.value = 'loading'
  const result = await props.solve()
  if (result) {
    solution.value = result
    state.value = 'done'
  } else {
    state.value = 'error'
  }
}
</script>

<template>
  <div class="captcha" data-testid="captcha">
    <p class="captcha__lead">{{ t('auth.captchaLead') }}</p>
    <p v-if="state === 'done'" class="captcha__done" role="status">{{ t('auth.captchaDone') }}</p>
    <UiButton v-else variant="secondary" block :loading="state === 'loading'" @click="check">
      {{ state === 'loading' ? t('auth.captchaChecking') : t('auth.captchaButton') }}
    </UiButton>
    <p v-if="state === 'error'" class="captcha__error" role="alert">
      {{ t('auth.captchaFailed') }}
    </p>
  </div>
</template>

<style scoped>
.captcha {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-3);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-md);
}

.captcha__lead {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.captcha__done {
  margin: 0;
  color: var(--color-success);
}

.captcha__error {
  margin: 0;
  color: var(--color-danger);
}
</style>
