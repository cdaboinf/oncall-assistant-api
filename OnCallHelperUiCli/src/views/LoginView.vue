<template>
  <div class="center-narrow">
    <div class="page-header" style="text-align: center">
      <h1>Sign in</h1>
      <p>You'll be redirected to Auth0 to sign in securely, then brought back here.</p>
    </div>

    <div class="card" style="text-align: center">
      <template v-if="authState.isAuthenticated">
        <div class="alert success">Signed in as {{ authState.user?.email || authState.user?.name }}.</div>
        <router-link class="btn block" style="margin-top: 1rem" to="/triage">Go to Triage</router-link>
      </template>

      <template v-else-if="isConfigured()">
        <button class="btn block" @click="signIn">Sign in with Auth0</button>
        <div v-if="authState.error" class="alert error">{{ authState.error }}</div>
      </template>

      <template v-else>
        <div class="alert info">
          Auth0 isn't configured in this build. The app works without signing in while the API has auth disabled.
        </div>
      </template>
    </div>

    <p class="dim" style="text-align: center; margin-top: 1rem; font-size: 0.85rem">
      Running locally with API auth disabled? You can use the app without signing in.
    </p>
  </div>
</template>

<script setup>
import { useRoute } from 'vue-router';
import { authState, isConfigured, login } from '../auth';

const route = useRoute();

const signIn = () => {
  const target = typeof route.query.redirect === 'string' ? route.query.redirect : '/triage';
  login(target);
};
</script>
