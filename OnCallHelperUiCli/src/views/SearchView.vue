<template>
  <div>
    <div class="page-header">
      <h1>Search history</h1>
      <p>Find past incidents by keyword, service, severity, or environment.</p>
    </div>

    <div class="card">
      <div class="filters">
        <label class="field">
          Keyword
          <input v-model="filters.q" placeholder="title, description, service…" @keydown.enter="search" />
        </label>
        <label class="field">
          Service
          <input v-model="filters.serviceName" placeholder="e.g. NAG" @keydown.enter="search" />
        </label>
        <label class="field">
          Severity
          <select v-model="filters.severity">
            <option value="">Any</option>
            <option value="sev1">sev1</option>
            <option value="sev2">sev2</option>
            <option value="sev3">sev3</option>
            <option value="sev4">sev4</option>
          </select>
        </label>
        <label class="field">
          Environment
          <input v-model="filters.environment" placeholder="production…" @keydown.enter="search" />
        </label>
        <button class="btn" :disabled="loading" @click="search">
          <span v-if="loading" class="spinner"></span>
          {{ loading ? 'Searching…' : 'Search' }}
        </button>
      </div>
      <div class="row" style="margin-top: 0.7rem">
        <button class="btn ghost small" @click="reset">Clear filters</button>
        <span class="dim" style="font-size: 0.82rem" v-if="hasSearched">{{ incidents.length }} result{{ incidents.length === 1 ? '' : 's' }}</span>
      </div>
      <div v-if="error" class="alert error">{{ error }}</div>
    </div>

    <div style="margin-top: 1.1rem">
      <div v-if="incidents.length">
        <IncidentCard v-for="inc in incidents" :key="inc.id" :incident="inc" />
      </div>
      <div v-else-if="!loading" class="empty">
        <div class="big">{{ error ? '⚠️' : '🔍' }}</div>
        <div>{{ emptyMessage }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { api } from '../api/client';
import IncidentCard from '../components/IncidentCard.vue';

const filters = reactive({ q: '', serviceName: '', severity: '', environment: '', limit: 100 });
const incidents = ref([]);
const loading = ref(false);
const error = ref('');
const hasSearched = ref(false);

const emptyMessage = computed(() => {
  if (error.value) return "Couldn't load incidents.";
  if (hasSearched.value) return 'No incidents match those filters.';
  return 'Loading recent incidents…';
});

const search = async () => {
  loading.value = true;
  error.value = '';
  try {
    incidents.value = await api.searchIncidents(filters);
    hasSearched.value = true;
  } catch (e) {
    error.value = e.message || 'Search failed.';
  } finally {
    loading.value = false;
  }
};

const reset = () => {
  filters.q = '';
  filters.serviceName = '';
  filters.severity = '';
  filters.environment = '';
  search();
};

onMounted(search);
</script>
