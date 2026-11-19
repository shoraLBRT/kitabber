import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import prettier from 'eslint-config-prettier'
import pluginVue from 'eslint-plugin-vue'

export default defineConfigWithVueTs(
  { name: 'admin/files', files: ['**/*.{ts,mts,tsx,vue}'] },
  { name: 'admin/ignores', ignores: ['dist/**', 'coverage/**'] },
  pluginVue.configs['flat/recommended'],
  vueTsConfigs.recommended,
  prettier,
)
