<script setup lang="ts">
// Showcase of the base UI components (E02-04, ADR-0013): every component of components/ui/ with its states, in
// the theme picked at the top (it sets data-theme on <html>, so dialogs and toasts follow it too).
// tests/ui/showcase.spec.ts fails when a component of components/ui/ has no section here.
import { Bookmark, FileText, LogOut, Pencil, Settings, Trash2, User } from 'lucide-vue-next'
import { onBeforeUnmount, ref, watch } from 'vue'
import { faviconHref } from '../../brand/favicon'
import { useToast } from '../composables/useToast'
import type { ImagePreview, ImageSource, MenuItem, TabItem } from '../types/ui'
import UiAvatar from './ui/UiAvatar.vue'
import UiButton from './ui/UiButton.vue'
import UiField from './ui/UiField.vue'
import UiImage from './ui/UiImage.vue'
import UiMenu from './ui/UiMenu.vue'
import UiModal from './ui/UiModal.vue'
import UiSheet from './ui/UiSheet.vue'
import UiSkeleton from './ui/UiSkeleton.vue'
import UiState from './ui/UiState.vue'
import UiTabs from './ui/UiTabs.vue'

// One picture in three widths (server/routes/showcase), as the API gives the sources of an uploaded image (ADR-0040).
const landscape: ImageSource[] = [320, 640, 1280].map((width) => ({
  url: `/showcase/landscape-${width}.svg`,
  width,
}))

// What the API gives with such a picture (E06-03, ADR-0067): its average colour and a 16 by 10 PNG of it, both drawn
// from the same brand colours as the picture.
const landscapePreview: ImagePreview = {
  color: '#1a3936',
  image:
    'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAKCAIAAAAy3EnLAAAAOklEQVR42mOQVVAhCTFQX4PNyWUQRDMNBJzEnRdNggbuvGgIwmMEA6ZqZD2YRjBgVY0VITQQoxquBwBvLVXiEq0FXQAAAABJRU5ErkJggg==',
}

const themeTabs: TabItem[] = [
  { id: 'system', label: 'Системная' },
  { id: 'light', label: 'Светлая' },
  { id: 'dark', label: 'Тёмная' },
]
const theme = ref('system')
watch(theme, (value) => {
  if (value === 'system') delete document.documentElement.dataset.theme
  else document.documentElement.dataset.theme = value
})
onBeforeUnmount(() => delete document.documentElement.dataset.theme)

const feedTabs: TabItem[] = [
  { id: 'popular', label: 'Популярное' },
  { id: 'fresh', label: 'Свежее' },
  { id: 'mine', label: 'Моя лента' },
  { id: 'archive', label: 'Архив', disabled: true },
]
const feed = ref('popular')

const menuItems: MenuItem[] = [
  { id: 'profile', label: 'Профиль', icon: User },
  { id: 'drafts', label: 'Черновики', icon: FileText },
  { id: 'bookmarks', label: 'Закладки', icon: Bookmark },
  { id: 'settings', label: 'Настройки', icon: Settings, disabled: true },
  { id: 'logout', label: 'Выйти', icon: LogOut, danger: true },
]
const chosen = ref('')

const name = ref('')
const about = ref('')
const loading = ref(false)
const modalOpen = ref(false)
const sheetOpen = ref(false)
const { show: showToast } = useToast()

function save() {
  loading.value = true
  setTimeout(() => {
    loading.value = false
    showToast('Изменения сохранены', { tone: 'success' })
  }, 1500)
}
</script>

<template>
  <div class="showcase">
    <UiTabs v-model="theme" :tabs="themeTabs" label="Тема оформления витрины" />

    <section class="showcase__section" data-testid="showcase-UiButton">
      <h2>Кнопка — UiButton</h2>
      <div class="showcase__row">
        <UiButton>Опубликовать</UiButton>
        <UiButton variant="secondary">Отмена</UiButton>
        <UiButton variant="ghost">Подробнее</UiButton>
        <UiButton variant="danger"
          ><template #icon><Trash2 aria-hidden="true" /></template>Удалить</UiButton
        >
      </div>
      <div class="showcase__row">
        <UiButton size="sm">Маленькая</UiButton>
        <UiButton disabled>Недоступна</UiButton>
        <UiButton :loading="loading" @click="save">{{ loading ? 'Сохраняем…' : 'Сохранить' }}</UiButton>
        <UiButton variant="ghost" aria-label="Написать"
          ><template #icon><Pencil aria-hidden="true" /></template
        ></UiButton>
      </div>
    </section>

    <section class="showcase__section" data-testid="showcase-UiField">
      <h2>Поле — UiField</h2>
      <div class="showcase__grid">
        <UiField v-model="name" label="Имя" hint="Его увидят читатели" required autocomplete="nickname" />
        <UiField
          model-value="не почта"
          label="Почта"
          type="email"
          error="Введите адрес вида name@example.ru"
        />
        <UiField model-value="Только чтение" label="Недоступное поле" disabled />
        <UiField v-model="about" label="О себе" multiline :rows="3" placeholder="Пара слов о себе" />
      </div>
    </section>

    <section class="showcase__section" data-testid="showcase-UiMenu">
      <h2>Меню — UiMenu</h2>
      <div class="showcase__row">
        <UiMenu :items="menuItems" label="Меню профиля" @select="chosen = $event">
          <template #trigger><UiAvatar name="Анна Котова" size="sm" decorative /></template>
        </UiMenu>
        <UiMenu :items="menuItems" label="Действия" />
        <span class="showcase__note" aria-live="polite">{{ chosen ? `Выбрано: ${chosen}` : '' }}</span>
      </div>
    </section>

    <section class="showcase__section" data-testid="showcase-UiTabs">
      <h2>Вкладки — UiTabs</h2>
      <UiTabs v-model="feed" :tabs="feedTabs" label="Лента">
        <template #popular><p>Самое обсуждаемое за сутки.</p></template>
        <template #fresh><p>Новые публикации по порядку.</p></template>
        <template #mine><p>Подписки и темы.</p></template>
        <template #archive><p>Архив.</p></template>
      </UiTabs>
    </section>

    <section class="showcase__section" data-testid="showcase-UiModal">
      <h2>Модальное окно — UiModal</h2>
      <p class="showcase__note">На телефоне (уже 640 px) окно открывается шторкой.</p>
      <UiButton variant="secondary" @click="modalOpen = true">Открыть окно</UiButton>
      <UiModal v-model:open="modalOpen" title="Удалить черновик?">
        <p>Черновик удалится без возможности восстановления.</p>
        <template #footer>
          <UiButton variant="secondary" @click="modalOpen = false">Отмена</UiButton>
          <UiButton variant="danger" @click="modalOpen = false">Удалить</UiButton>
        </template>
      </UiModal>
    </section>

    <section class="showcase__section" data-testid="showcase-UiSheet">
      <h2>Шторка — UiSheet</h2>
      <UiButton variant="secondary" @click="sheetOpen = true">Открыть шторку</UiButton>
      <UiSheet v-model:open="sheetOpen" title="Поделиться">
        <UiField label="Ссылка" model-value="https://example.ru/p/1" readonly data-autofocus />
      </UiSheet>
    </section>

    <section class="showcase__section" data-testid="showcase-UiToast">
      <h2>Тосты — UiToast и UiToaster</h2>
      <div class="showcase__row" data-testid="showcase-UiToaster">
        <UiButton variant="secondary" @click="showToast('Ссылка скопирована')">Сведения</UiButton>
        <UiButton variant="secondary" @click="showToast('Пост опубликован', { tone: 'success' })"
          >Успех</UiButton
        >
        <UiButton variant="secondary" @click="showToast('Не удалось сохранить: нет сети', { tone: 'error' })">
          Ошибка
        </UiButton>
      </div>
    </section>

    <section class="showcase__section" data-testid="showcase-UiAvatar">
      <h2>Аватар — UiAvatar</h2>
      <div class="showcase__row">
        <UiAvatar name="Анна Котова" :src="faviconHref()" size="lg" />
        <UiAvatar name="Анна Котова" size="lg" />
        <UiAvatar name="Борис" />
        <UiAvatar name="Вера Лис" src="/missing-avatar.png" size="sm" />
      </div>
    </section>

    <section class="showcase__section" data-testid="showcase-UiImage">
      <h2>Изображение — UiImage</h2>
      <p>
        Место под картинку занято заранее, браузер берёт ширину под плотность экрана и загружает картинку,
        только когда до неё доходит прокрутка. Без ссылки и при ошибке остаётся заглушка того же размера.
      </p>
      <div class="showcase__images">
        <figure class="showcase__figure">
          <UiImage
            :sources="landscape"
            sizes="320px"
            :width="320"
            :height="200"
            alt="Горы у озера"
            data-testid="showcase-image"
          />
          <figcaption>Загружено</figcaption>
        </figure>
        <figure class="showcase__figure">
          <UiImage
            src="/showcase/landscape-640.svg?preview"
            :preview="landscapePreview"
            :width="320"
            :height="200"
            alt="Горы у озера"
            data-testid="showcase-image-preview"
          />
          <figcaption>Размытое превью до загрузки</figcaption>
        </figure>
        <figure class="showcase__figure">
          <UiImage :width="320" :height="200" alt="Нет изображения" data-testid="showcase-image-empty" />
          <figcaption>Пусто</figcaption>
        </figure>
        <figure class="showcase__figure">
          <UiImage
            src="/missing-image.svg"
            :width="320"
            :height="200"
            alt="Горы у озера"
            data-testid="showcase-image-error"
          />
          <figcaption>Ошибка</figcaption>
        </figure>
      </div>
    </section>

    <section class="showcase__section" data-testid="showcase-UiSkeleton">
      <h2>Скелетон — UiSkeleton</h2>
      <div class="showcase__skeleton">
        <div class="showcase__row">
          <UiSkeleton shape="avatar" size="sm" />
          <UiSkeleton size="sm" width="40%" />
        </div>
        <UiSkeleton size="xl" width="70%" />
        <UiSkeleton size="reading" />
        <UiSkeleton size="reading" width="80%" />
        <UiSkeleton shape="block" :ratio="16 / 9" />
      </div>
    </section>

    <section class="showcase__section" data-testid="showcase-UiState">
      <h2>Состояния — UiState</h2>
      <div class="showcase__states">
        <UiState>Здесь пока нет постов.</UiState>
        <UiState tone="error">
          Не удалось загрузить ленту. Проверьте соединение и попробуйте ещё раз.
          <template #actions><UiButton variant="secondary">Повторить</UiButton></template>
        </UiState>
        <UiState tone="not-found" title="Страница не найдена">
          Возможно, адрес набран с ошибкой или страница удалена.
          <template #actions><a href="/">На главную</a></template>
        </UiState>
      </div>
    </section>
  </div>
</template>

<style scoped>
.showcase {
  display: flex;
  flex-direction: column;
  gap: var(--space-8);
}

.showcase__section h2 {
  margin: 0 0 var(--space-3);
  font-size: var(--font-size-lg);
}

.showcase__row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}

.showcase__skeleton {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  width: calc(var(--size-feed) / 2);
  max-width: 100%;
}

.showcase__states {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.showcase__images {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4);
}

/* Half the feed, 320 px, as the sizes of the picture say; narrower on a phone. */
.showcase__figure {
  width: calc(var(--size-feed) / 2);
  max-width: 100%;
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.showcase__figure figcaption {
  margin-top: var(--space-2);
}

.showcase__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, var(--size-aside)), 1fr));
  gap: var(--space-4);
}

.showcase__note {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}
</style>
