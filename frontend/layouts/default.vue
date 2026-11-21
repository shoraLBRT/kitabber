<script setup lang="ts">
// Default layout: the shell (E02-05 desktop, E02-06 phone: bottom bar and floating "Написать" below 925 px).
// Signed in (E18-02), the shell shows the account menu; the name and avatar come with the profile (#33).
const route = useRoute()
const user = useShellUser()

// The account menu's "Выйти" (E03-03); its other items lead to pages of their own.
function onAccount(id: string) {
  if (id === 'logout') signOut()
}
</script>

<template>
  <ShellLayout :current-path="route.path" :user="user" @account="onAccount">
    <slot />
  </ShellLayout>
</template>
