<script setup lang="ts">
// Default layout: the shell (E02-05 desktop, E02-06 phone: bottom bar and floating "Написать" below 925 px).
// Signed in (E18-02), the shell shows the account menu; the name and avatar come with the profile (#33).
const route = useRoute()
const user = useShellUser()
</script>

<template>
  <ShellLayout :current-path="route.path" :user="user">
    <slot />
  </ShellLayout>
</template>
