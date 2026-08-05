<template>
  <div>
    <div class="page-header">
      <h1>Triage an alert</h1>
      <p>Paste the alert, error, or symptom. You'll get likely cause, next steps, and similar past incidents.</p>
    </div>

    <div class="card">
      <label class="field">
        Alert / symptom
        <textarea
          v-model="description"
          class="triage-input"
          placeholder="e.g. NAG transfers posting twice for the same pending transfer id in production since 11:00 UTC…"
          @keydown.meta.enter="analyze"
          @keydown.ctrl.enter="analyze"
        ></textarea>
      </label>
      <div class="row" style="margin-top: 0.8rem">
        <button class="btn" :disabled="loading || !description.trim()" @click="analyze">
          <span v-if="loading" class="spinner"></span>
          {{ loading ? 'Analyzing…' : 'Analyze' }}
        </button>
        <span class="dim" style="font-size: 0.82rem">⌘/Ctrl + Enter</span>
      </div>

      <div v-if="error" class="alert error">
        {{ error }}
        <div v-if="errorDetail" class="dim" style="margin-top: 0.4rem; font-size: 0.82rem">{{ errorDetail }}</div>
      </div>
    </div>

    <div v-if="result" style="margin-top: 1.1rem">
      <TriageResultView :result="result" />
    </div>

    <div v-else-if="!loading && !error" class="empty">
      <div class="big">🛎️</div>
      <div>Describe what you're seeing to get triage guidance.</div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { api } from '../api/client';
import TriageResultView from '../components/TriageResultView.vue';

const description = ref('');
const result = ref(null);
const loading = ref(false);
const error = ref('');
const errorDetail = ref('');

const analyze = async () => {
  if (!description.value.trim() || loading.value) return;
  loading.value = true;
  error.value = '';
  errorDetail.value = '';
  result.value = null;
  try {
    result.value = await api.analyze(description.value.trim());
  } catch (e) {
    error.value = e.message || 'Analysis failed.';
    errorDetail.value = e.detail && e.detail !== e.message ? e.detail : '';
  } finally {
    loading.value = false;
  }
};
</script>
