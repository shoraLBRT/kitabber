<script setup lang="ts">
// Text field with a visible label (E02-04, ADR-0013). The hint and the error are tied to the control through
// aria-describedby, an error also sets aria-invalid. Other attributes (name, autocomplete, maxlength …) go to the
// control itself.
import { computed, useId } from 'vue'

defineOptions({ inheritAttrs: false })

const props = withDefaults(
  defineProps<{
    label: string
    type?: 'text' | 'email' | 'password' | 'search' | 'url' | 'tel'
    multiline?: boolean
    rows?: number
    hint?: string
    error?: string
    required?: boolean
    disabled?: boolean
    placeholder?: string
  }>(),
  { type: 'text', rows: 4, hint: undefined, error: undefined, placeholder: undefined },
)

const model = defineModel<string>({ default: '' })

const id = useId()
const hintId = `${id}-hint`
const errorId = `${id}-error`
const describedBy = computed(
  () => [props.hint ? hintId : '', props.error ? errorId : ''].filter(Boolean).join(' ') || undefined,
)

function onInput(event: Event) {
  model.value = (event.target as HTMLInputElement | HTMLTextAreaElement).value
}
</script>

<template>
  <div class="ui-field" :class="{ 'ui-field--invalid': error, 'ui-field--disabled': disabled }">
    <label :for="id" class="ui-field__label">
      {{ label }}<span v-if="required" class="ui-field__required" aria-hidden="true"> *</span>
    </label>
    <component
      :is="multiline ? 'textarea' : 'input'"
      :id="id"
      class="ui-field__control"
      :type="multiline ? undefined : type"
      :rows="multiline ? rows : undefined"
      :value="model"
      :placeholder="placeholder"
      :required="required"
      :disabled="disabled"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="describedBy"
      v-bind="$attrs"
      @input="onInput"
    />
    <p v-if="hint" :id="hintId" class="ui-field__hint">{{ hint }}</p>
    <p v-if="error" :id="errorId" class="ui-field__error">{{ error }}</p>
  </div>
</template>

<style scoped>
.ui-field {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.ui-field__label {
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

.ui-field__required {
  color: var(--color-danger);
}

.ui-field__control {
  min-height: var(--space-10);
  padding: var(--space-2) var(--space-3);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font-family: inherit;
  font-size: var(--font-size-md);
  line-height: var(--line-height-normal);
}

.ui-field__control::placeholder {
  color: var(--color-muted);
}

textarea.ui-field__control {
  resize: vertical;
}

.ui-field__control:focus-visible {
  outline: var(--size-focus-ring) solid var(--color-focus);
  outline-offset: 0;
  border-color: var(--color-focus);
}

.ui-field--invalid .ui-field__control {
  border-color: var(--color-danger);
}

.ui-field--disabled .ui-field__control {
  opacity: 0.5;
  cursor: not-allowed;
}

.ui-field__hint,
.ui-field__error {
  margin: 0;
  font-size: var(--font-size-sm);
}

.ui-field__hint {
  color: var(--color-muted);
}

.ui-field__error {
  color: var(--color-danger);
}
</style>
