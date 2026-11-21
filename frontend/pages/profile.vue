<script setup lang="ts">
// «Профиль» of the menu (utils/navigation.ts): the person's own blog. Only the server knows whose blog it is (the
// access token stays there, ADR-0018), so the page hands over to server/routes/bff/own-blog.get.ts.
await navigateTo('/bff/own-blog', { external: true, replace: true })
</script>

<template>
  <div />
</template>
