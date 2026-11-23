<script setup lang="ts">
// Default layout: the shell (E02-05 desktop, E02-06 phone: bottom bar and floating "Написать" below 925 px).
// Signed in (E18-02), the shell shows the account menu; the name and avatar come with the profile (#33).
const route = useRoute()
const user = useShellUser()
// The floating «Написать» of a phone opens the quick post (E05-29, ADR-0055); its «Развернуть» leads on to /write.
const { sheetOpen } = useQuickPost()
// «Нет подключения» above the content while the browser is offline (E02-07, ADR-0057).
const online = useOnline()

// The account menu's "Выйти" (E03-03); its other items lead to pages of their own.
function onAccount(id: string) {
  if (id === 'logout') signOut()
}
</script>

<template>
  <ShellLayout
    :current-path="route.path"
    :user="user"
    :write-button="route.meta.writeButton !== false"
    quick-post
    :offline="!online"
    @account="onAccount"
    @write="sheetOpen = true"
  >
    <slot />
  </ShellLayout>
</template>
