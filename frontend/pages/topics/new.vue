<script setup lang="ts">
// A new topic (E11-11, FR-TOPIC, ADR-0084) at /topics/new: the form, sent through the site's server
// (server/routes/bff/topics.post.ts, ADR-0018). A guest signs in first and comes back here; a created topic opens at
// its own address /<topic>. The catalogue of topics at /topics is E11-02 (#78).
import TopicCreateForm from '~/components/topic/TopicCreateForm.vue'
import type { Topic, TopicCreate, TopicInput } from '~/api/topics'
import { t } from '../../i18n'

const signIn = '/auth/sign-in?returnTo=/topics/new'
useHead({ title: t('topicCreate.title') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo(signIn, { external: true })

const create = (input: TopicInput) =>
  $fetch<TopicCreate>('/bff/topics', { method: 'POST', body: input }).catch((): TopicCreate => ({
    status: 'error',
  }))

async function onCreated(topic: Topic) {
  // The editor's topic list asks again, so the new topic is there at once.
  clearNuxtData((key) => key.startsWith('topics'))
  await navigateTo(`/${topic.nickname}`)
}

function onUnauthorized() {
  window.location.assign(signIn)
}
</script>

<template>
  <section class="topic-new" aria-labelledby="topic-new-title">
    <h1 id="topic-new-title" class="topic-new__title">{{ t('topicCreate.title') }}</h1>
    <div class="topic-new__card">
      <TopicCreateForm :create="create" @created="onCreated" @unauthorized="onUnauthorized" />
    </div>
  </section>
</template>

<style scoped>
.topic-new {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.topic-new__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
}

.topic-new__card {
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

@media (max-width: 639px) {
  .topic-new__card {
    padding: var(--space-4);
  }
}
</style>
