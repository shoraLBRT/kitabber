import { createApp } from 'vue'
import brand from '../../brand/brand.json'
import { faviconHref } from '../../brand/favicon'
import App from './App.vue'

// The product name and the favicon come from brand/ (ADR-0011), not from index.html.
document.title = `Админка — ${brand.name}`
const icon = Object.assign(document.createElement('link'), { rel: 'icon', type: 'image/svg+xml' })
icon.href = faviconHref()
document.head.append(icon)

createApp(App).mount('#app')
