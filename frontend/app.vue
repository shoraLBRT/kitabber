<template>
  <div>
    <header class="app-header">
      <NuxtLink to="/" class="app-header__home"><BrandLogo /></NuxtLink>
    </header>
    <NuxtPage />
  </div>
</template>

<style scoped>
.app-header {
  padding: 0.75rem 1rem;
}

.app-header__home {
  text-decoration: none;
}
</style>
