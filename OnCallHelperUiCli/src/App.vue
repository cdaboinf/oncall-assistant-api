<template>
  <div class="container">
    <header>
      <h1>OnCall Helper Dashboard</h1>
      <p>Vue CLI single-page app for existing API controllers.</p>
      <label>
        API Base URL
        <input v-model="apiBaseUrl" placeholder="http://localhost:5000" />
      </label>
      <label>
        Bearer Token
        <input
          v-model="bearerToken"
          type="password"
          autocomplete="off"
          placeholder="Paste JWT token for secured endpoints"
        />
      </label>
    </header>

    <main class="grid">
      <section class="card">
        <h2>Create Incident</h2>
        <label>Title <input v-model="newIncident.title" /></label>
        <label>Description <textarea v-model="newIncident.description"></textarea></label>
        <label>Service Name <input v-model="newIncident.serviceName" /></label>
        <label>Environment <input v-model="newIncident.environment" /></label>
        <label>Severity <input v-model="newIncident.severity" /></label>
        <label>Resolution Summary <textarea v-model="newIncident.resolution.summary"></textarea></label>
        <label>Root Cause <input v-model="newIncident.resolution.rootCause" /></label>
        <label>Steps Taken (comma separated) <input v-model="resolutionSteps" /></label>
        <label>Resolved By <input v-model="newIncident.resolution.resolvedBy" /></label>
        <button @click="createIncident" :disabled="loading.create">{{ loading.create ? 'Submitting...' : 'Create Incident' }}</button>
        <p v-if="createMessage" class="success">{{ createMessage }}</p>
        <p v-if="errors.create" class="error">{{ errors.create }}</p>
      </section>

      <section class="card">
        <h2>Analyze Incident</h2>
        <label>Description <textarea v-model="analysisDescription"></textarea></label>
        <button @click="analyzeIncident" :disabled="loading.analyze">{{ loading.analyze ? 'Analyzing...' : 'Analyze' }}</button>
        <p v-if="errors.analyze" class="error">{{ errors.analyze }}</p>
        <pre v-if="analysisResult">{{ pretty(analysisResult) }}</pre>
      </section>

      <section class="card wide">
        <h2>Find Similar Incidents</h2>
        <label>Description <textarea v-model="similarRequest.description"></textarea></label>
        <label>Top <input type="number" min="1" max="20" v-model.number="similarRequest.top" /></label>
        <button @click="findSimilar" :disabled="loading.similar">{{ loading.similar ? 'Searching...' : 'Find Similar' }}</button>
        <p v-if="errors.similar" class="error">{{ errors.similar }}</p>
        <pre v-if="similarResults">{{ pretty(similarResults) }}</pre>
      </section>

      <section class="card wide">
        <h2>All Incidents</h2>
        <button @click="loadIncidents" :disabled="loading.all">{{ loading.all ? 'Loading...' : 'Refresh Incidents' }}</button>
        <p v-if="errors.all" class="error">{{ errors.all }}</p>
        <div v-if="incidents.length">
          <article v-for="incident in incidents" :key="incident.id" class="incident-item">
            <strong>{{ incident.title }}</strong>
            <div class="meta">{{ incident.serviceName }} • {{ incident.environment }} • {{ incident.severity }}</div>
            <p>{{ incident.description }}</p>
          </article>
        </div>
        <p v-else class="meta">No incidents loaded.</p>
      </section>
    </main>
  </div>
</template>

<script setup>
import { reactive, ref, watch } from 'vue';

const apiBaseUrl = ref(localStorage.getItem('apiBaseUrl') || 'http://localhost:5000');
const bearerToken = ref(localStorage.getItem('bearerToken') || '');

watch(apiBaseUrl, (value) => {
  localStorage.setItem('apiBaseUrl', value);
});

watch(bearerToken, (value) => {
  localStorage.setItem('bearerToken', value);
});

const newIncident = reactive({
  title: '',
  description: '',
  serviceName: '',
  environment: '',
  severity: '',
  resolution: { rootCause: '', summary: '', stepsTaken: [], resolvedBy: '' }
});

const resolutionSteps = ref('');
const analysisDescription = ref('');
const analysisResult = ref(null);
const similarRequest = reactive({ description: '', top: 5 });
const similarResults = ref(null);
const incidents = ref([]);
const createMessage = ref('');
const errors = reactive({ create: '', analyze: '', similar: '', all: '' });
const loading = reactive({ create: false, analyze: false, similar: false, all: false });

const endpoint = (path) => `${apiBaseUrl.value.replace(/\/$/, '')}${path}`;
const authHeaders = () => {
  const headers = { 'Content-Type': 'application/json' };
  if (bearerToken.value.trim()) {
    headers.Authorization = `Bearer ${bearerToken.value.trim()}`;
  }
  return headers;
};

const authOnlyHeaders = () => {
  if (bearerToken.value.trim()) {
    return { Authorization: `Bearer ${bearerToken.value.trim()}` };
  }
  return {};
};

const pretty = (value) => JSON.stringify(value, null, 2);
const clearError = (key) => { errors[key] = ''; };

const createIncident = async () => {
  clearError('create');
  createMessage.value = '';
  loading.create = true;
  try {
    const payload = {
      ...newIncident,
      resolution: {
        ...newIncident.resolution,
        stepsTaken: resolutionSteps.value.split(',').map((s) => s.trim()).filter(Boolean)
      }
    };

    const response = await fetch(endpoint('/api/incidents'), {
      method: 'POST',
      headers: authHeaders(),
      body: JSON.stringify(payload)
    });

    if (!response.ok) throw new Error(await response.text());
    const created = await response.json();
    createMessage.value = `Incident created with id: ${created.id || created.Id}`;
    await loadIncidents();
  } catch (error) {
    errors.create = `Failed to create incident: ${error.message}`;
  } finally {
    loading.create = false;
  }
};

const analyzeIncident = async () => {
  clearError('analyze');
  loading.analyze = true;
  try {
    const response = await fetch(endpoint('/api/oncall/analyze'), {
      method: 'POST',
      headers: authHeaders(),
      body: JSON.stringify({ description: analysisDescription.value })
    });

    if (!response.ok) throw new Error(await response.text());
    analysisResult.value = await response.json();
  } catch (error) {
    errors.analyze = `Failed to analyze incident: ${error.message}`;
  } finally {
    loading.analyze = false;
  }
};

const findSimilar = async () => {
  clearError('similar');
  loading.similar = true;
  try {
    const response = await fetch(endpoint('/api/incidents/similar'), {
      method: 'POST',
      headers: authHeaders(),
      body: JSON.stringify(similarRequest)
    });

    if (!response.ok) throw new Error(await response.text());
    similarResults.value = await response.json();
  } catch (error) {
    errors.similar = `Failed to find similar incidents: ${error.message}`;
  } finally {
    loading.similar = false;
  }
};

const loadIncidents = async () => {
  clearError('all');
  loading.all = true;
  try {
    const response = await fetch(endpoint('/api/incidents'), {
      headers: authOnlyHeaders()
    });
    if (!response.ok) throw new Error(await response.text());
    incidents.value = await response.json();
  } catch (error) {
    errors.all = `Failed to load incidents: ${error.message}`;
  } finally {
    loading.all = false;
  }
};
</script>
