<template>
  <div class="app-shell">
    <header class="topbar">
      <div class="brand">
        <span class="brand-badge">🛎️</span>
        <span>OnCall Helper</span>
      </div>

      <nav class="nav">
        <router-link to="/triage">Triage</router-link>
        <router-link to="/search">Search</router-link>
        <router-link to="/log">Log incident</router-link>
      </nav>

      <div class="topbar-right">
        <span class="account-chip" :title="connectionTitle">
          <span class="status-dot" :class="{ on: authState.isAuthenticated }"></span>
          {{ accountLabel }}
        </span>
        <button class="btn ghost small" @click="settingsOpen = true">Settings</button>
        <button v-if="!authState.isAuthenticated" class="btn ghost small" @click="signIn">Sign in</button>
        <button v-else class="btn ghost small" @click="signOut">Sign out</button>
      </div>
    </header>

    <main class="app-main">
      <router-view />
    </main>

    <!-- Settings drawer -->
    <div v-if="settingsOpen" class="drawer-backdrop" @click.self="settingsOpen = false">
      <div class="drawer">
        <div class="spread" style="margin-bottom: 1rem">
          <h2 style="font-size: 1.15rem">Settings</h2>
          <button class="btn ghost small" @click="settingsOpen = false">✕</button>
        </div>

        <label class="field">
          API base URL
          <input v-model="baseUrlDraft" placeholder="http://localhost:5172" />
        </label>
        <p class="dim" style="font-size: 0.8rem; margin-top: 0.4rem">
          Where the OnCall Helper API is running.
        </p>

        <div class="row" style="margin-top: 1.2rem">
          <button class="btn" @click="saveSettings">Save</button>
          <button class="btn ghost" @click="settingsOpen = false">Cancel</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue';
import { apiState, setBaseUrl } from './api/client';
import { authState, isConfigured, login, logout } from './auth';

const settingsOpen = ref(false);
const baseUrlDraft = ref(apiState.baseUrl);

watch(settingsOpen, (open) => {
  if (open) baseUrlDraft.value = apiState.baseUrl;
});

const accountLabel = computed(() => {
  if (authState.isAuthenticated) return authState.user?.email || authState.user?.name || 'Signed in';
  return isConfigured() ? 'Signed out' : 'Local / no auth';
});

const connectionTitle = computed(() =>
  authState.isAuthenticated
    ? `Signed in as ${authState.user?.email || authState.user?.name}`
    : 'Not signed in — works when API auth is disabled'
);

const signIn = () => login(window.location.pathname);
const signOut = () => logout();

const saveSettings = () => {
  setBaseUrl(baseUrlDraft.value.trim());
  settingsOpen.value = false;
};
</script>

<style>
.drawer-backdrop {
  position: fixed; inset: 0; z-index: 40;
  background: rgba(4, 8, 18, 0.6);
  display: flex; justify-content: flex-end;
}
.drawer {
  width: min(420px, 90vw); height: 100%;
  background: var(--surface); border-left: 1px solid var(--border);
  padding: 1.5rem 1.4rem; overflow-y: auto;
  box-shadow: var(--shadow);
}
</style>
