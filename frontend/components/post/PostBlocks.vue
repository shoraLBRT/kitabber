<script setup lang="ts">
// The blocks of a post drawn for reading (E07-03, FR-POST, docs/ui/post.md, ADR-0045): one renderer for the post page,
// the feed and the preview, drawn on the server. Semantic markup: a paragraph is `p`, a heading `h2` or `h3` with its
// anchor (ADR-0031; the post's title is the page's `h1`), a list `ul` or `ol` with nested lists, a quote a `figure`
// with `blockquote` and `figcaption`, a delimiter `hr`. A block the renderer does not know is a placeholder.
// An image (E05-06, ADR-0049) is a `figure` with the picture (UiImage: its place held by the block's size, the
// sources from PostView.images, the blurred stand-in from PostView.imagePreviews, ADR-0067) and its caption.
import { computed } from 'vue'
import PostList from './PostList.vue'
import UiImage from '../ui/UiImage.vue'
import type { ImagePreview, ImageSource } from '../../types/ui'
import { viewBlocks } from '../../utils/post-blocks'
import { t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    document: unknown
    /** The sources of the post's pictures by file id (PostView.images). */
    images?: Readonly<Record<string, readonly ImageSource[]>>
    /** The blurred stand-ins of the pictures by file id (PostView.imagePreviews). */
    previews?: Readonly<Record<string, ImagePreview>>
  }>(),
  { images: () => ({}), previews: () => ({}) },
)

/** The text column is at most --size-measure (680 px) wide; on a phone the picture takes the screen's width. */
const imageSizes = '(max-width: 720px) 100vw, 680px'

const blocks = computed(() => viewBlocks(props.document))
</script>

<!-- eslint-disable vue/no-v-html -- every text is cleaned by cleanInlineHtml (ADR-0023, ADR-0045) -->
<template>
  <div class="post-blocks" data-testid="post-blocks">
    <p v-if="blocks.length === 0" class="post-blocks__empty" data-testid="post-blocks-empty">
      {{ t('post.empty') }}
    </p>
    <template v-for="block in blocks" :key="block.key">
      <p v-if="block.kind === 'paragraph'" class="post-blocks__text" v-html="block.html" />
      <h2
        v-else-if="block.kind === 'header' && block.level === 2"
        :id="block.anchor"
        class="post-blocks__heading post-blocks__heading--h2"
        v-html="block.html"
      />
      <h3
        v-else-if="block.kind === 'header'"
        :id="block.anchor"
        class="post-blocks__heading post-blocks__heading--h3"
        v-html="block.html"
      />
      <PostList v-else-if="block.kind === 'list'" :ordered="block.ordered" :items="block.items" />
      <figure v-else-if="block.kind === 'quote'" class="post-blocks__quote">
        <blockquote class="post-blocks__quote-text"><p v-html="block.html" /></blockquote>
        <figcaption v-if="block.caption" class="post-blocks__quote-caption" v-html="block.caption" />
      </figure>
      <hr v-else-if="block.kind === 'delimiter'" class="post-blocks__delimiter" />
      <figure v-else-if="block.kind === 'image'" class="post-blocks__image" data-testid="post-block-image">
        <UiImage
          class="post-blocks__picture"
          :sources="images[block.media] ?? []"
          :preview="previews[block.media]"
          :sizes="imageSizes"
          :width="block.width"
          :height="block.height"
          :alt="block.caption ? '' : t('post.photo')"
          fit="contain"
        />
        <figcaption v-if="block.caption" class="post-blocks__image-caption" v-html="block.caption" />
      </figure>
      <p
        v-else
        class="post-blocks__unknown"
        role="note"
        data-testid="post-block-unknown"
        :data-block-type="block.type || undefined"
      >
        {{ t('post.unknownBlock') }}
      </p>
    </template>
  </div>
</template>

<style scoped>
.post-blocks {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
  max-width: var(--size-measure);
  font-size: var(--font-size-reading);
  line-height: var(--line-height-reading);
}

.post-blocks__text,
.post-blocks__heading,
.post-blocks__empty,
.post-blocks__unknown {
  margin: 0;
  overflow-wrap: anywhere;
}

.post-blocks__heading {
  margin-block-start: var(--space-2);
  font-weight: var(--font-weight-bold);
  line-height: var(--line-height-tight);
}

/* docs/ui/post.md: subheadings H2 at --font-size-2xl, H3 at --font-size-xl. */
.post-blocks__heading--h2 {
  font-size: var(--font-size-2xl);
}

.post-blocks__heading--h3 {
  font-size: var(--font-size-xl);
}

/* The quote as in the editor (E05-05): a line on the start side, the caption under it in a smaller muted type. */
.post-blocks__quote {
  margin: 0;
  padding-block: var(--space-2);
  padding-inline-start: var(--space-4);
  border-inline-start: var(--space-1) solid var(--color-border);
}

.post-blocks__quote-text {
  margin: 0;
}

.post-blocks__quote-text p {
  margin: 0;
  overflow-wrap: anywhere;
}

.post-blocks__quote-caption {
  padding-block-start: var(--space-1);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  overflow-wrap: anywhere;
}

.post-blocks__image {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  margin: 0;
}

.post-blocks__picture {
  width: 100%;
  height: auto;
  border-radius: var(--radius-md);
}

.post-blocks__image-caption {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  text-align: center;
  overflow-wrap: anywhere;
}

/* The delimiter as in the editor (E05-05): three dots drawn by styles over an empty rule. */
.post-blocks__delimiter {
  margin: 0;
  padding-block: var(--space-4);
  border: 0;
  color: var(--color-muted);
  line-height: var(--line-height-tight);
  text-align: center;
}

.post-blocks__delimiter::before {
  content: '* * *';
  letter-spacing: var(--space-2);
}

.post-blocks__empty,
.post-blocks__unknown {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.post-blocks__unknown {
  padding: var(--space-3);
  border: var(--size-border) dashed var(--color-border-strong);
  border-radius: var(--radius-md);
}

.post-blocks :deep(a) {
  color: var(--color-accent);
}

.post-blocks :deep(a:hover) {
  color: var(--color-accent-hover);
}

.post-blocks :deep(mark) {
  background: color-mix(in srgb, var(--color-warning) 30%, transparent);
  color: inherit;
}
</style>
