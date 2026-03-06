<template>
  <div class="container">
    <header>
      <h1>OnCall Helper Dashboard</h1>
      <p>Vue CLI SPA with Auth0 login flow for protected API access.</p>

      <div v-if="isLoading" class="meta">Loading auth...</div>

      <div v-else-if="isAuthenticated && user" class="auth-actions">
        <span class="success">Logged in as {{ user.email || user.name }}</span>
        <button @click="logout">Logout</button>
      </div>

      <div v-else class="auth-actions">
        <p v-if="error" class="error">Error: {{ error.message }}</p>
        <button @click="signup">Signup</button>
        <button @click="login">Login</button>
      </div>

      <label>
        API Base URL
        <input v-model="apiBaseUrl" placeholder="http://localhost:5000" :disabled="!isAuthenticated" />
      </label>
    </header>

    <main v-if="isAuthenticated" class="grid">
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

    <main v-else class="card">
      <p class="meta">Please login to use the protected API endpoints.</p>
    </main>
  </div>
</template>

<script setup>
import { reactive, ref, watch } from 'vue';
import { useAuth0 } from '@auth0/auth0-vue';

const {
  isLoading,
  isAuthenticated,
  error,
  loginWithRedirect,
  logout: auth0Logout,
  user,
  getAccessTokenSilently
} = useAuth0();

const signup = () => loginWithRedirect({ authorizationParams: { screen_hint: 'signup' } });
const login = () => loginWithRedirect();
const logout = () => auth0Logout({ logoutParams: { returnTo: window.location.origin } });

const apiBaseUrl = ref(localStorage.getItem('apiBaseUrl') || 'http://localhost:5000');
watch(apiBaseUrl, (value) => localStorage.setItem('apiBaseUrl', value));

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
const pretty = (value) => JSON.stringify(value, null, 2);
const clearError = (key) => { errors[key] = ''; };

const apiFetch = async (path, options = {}) => {
  if (!isAuthenticated.value) {
    throw new Error('Please login first.');
  }

  const token = await getAccessTokenSilently({
    authorizationParams: {
      audience: process.env.VUE_APP_AUTH0_AUDIENCE || 'http://localhost:5172'
    }
  });

  const headers = {
    Authorization: `Bearer ${token}`,
    ...(options.headers || {})
  };

  const response = await fetch(endpoint(path), {
    ...options,
    headers
  });

  if (!response.ok) throw new Error(await response.text());
  return response.json();
};

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

    const created = await apiFetch('/api/incidents', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    createMessage.value = `Incident created with id: ${created.id || created.Id}`;
    await loadIncidents();
  } catch (err) {
    errors.create = `Failed to create incident: ${err.message}`;
  } finally {
    loading.create = false;
  }
};

const analyzeIncident = async () => {
  clearError('analyze');
  loading.analyze = true;
  try {
    analysisResult.value = await apiFetch('/api/oncall/analyze', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ description: analysisDescription.value })
    });
  } catch (err) {
    errors.analyze = `Failed to analyze incident: ${err.message}`;
  } finally {
    loading.analyze = false;
  }
};

const findSimilar = async () => {
  clearError('similar');
  loading.similar = true;
  try {
    similarResults.value = await apiFetch('/api/incidents/similar', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(similarRequest)
    });
  } catch (err) {
    errors.similar = `Failed to find similar incidents: ${err.message}`;
  } finally {
    loading.similar = false;
  }
};

const loadIncidents = async () => {
  clearError('all');
  loading.all = true;
  try {
    incidents.value = await apiFetch('/api/incidents');
  } catch (err) {
    errors.all = `Failed to load incidents: ${err.message}`;
  } finally {
    loading.all = false;
  }
};
</script>
