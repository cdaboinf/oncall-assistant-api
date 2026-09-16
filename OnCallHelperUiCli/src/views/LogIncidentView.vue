<template>
  <div>
    <div class="page-header">
      <h1>Log an incident</h1>
      <p>Capture what happened and how it was resolved so future on-call shifts benefit.</p>
    </div>

    <div class="card" style="margin-bottom: 1.1rem">
      <div class="card-title">Import from Slack</div>
      <p class="dim" style="margin: 0 0 0.5rem; font-size: 0.88rem">
        Paste a Slack conversation and let AI fill in the fields below. Review before saving.
      </p>
      <textarea
        v-model="slackText"
        class="triage-input"
        placeholder="Paste the Slack thread here…"
      ></textarea>
      <div class="row" style="margin-top: 0.7rem">
        <button class="btn" :disabled="extracting || !slackText.trim()" @click="extractFromSlack">
          <span v-if="extracting" class="spinner"></span>
          {{ extracting ? 'Extracting…' : 'Extract with AI' }}
        </button>
        <button class="btn ghost" type="button" :disabled="extracting || !slackText" @click="slackText = ''">Clear</button>
      </div>
      <div v-if="extractMessage" class="alert success">{{ extractMessage }}</div>
      <div v-if="extractError" class="alert error">
        {{ extractError }}
        <div v-if="extractErrorDetail" class="dim" style="margin-top: 0.4rem; font-size: 0.82rem">{{ extractErrorDetail }}</div>
      </div>
    </div>

    <div class="grid-2">
      <div class="card">
        <div class="card-title">Incident</div>
        <label class="field">
          Title
          <input v-model="form.title" placeholder="Short, searchable title" />
        </label>
        <label class="field">
          Description
          <textarea v-model="form.description" placeholder="Symptoms, impact, when it started…"></textarea>
        </label>
        <div class="row" style="gap: 0.6rem; align-items: flex-start">
          <label class="field" style="flex: 1; min-width: 140px">
            Service
            <input v-model="form.serviceName" placeholder="e.g. NAG" />
          </label>
          <label class="field" style="flex: 1; min-width: 140px">
            Environment
            <input v-model="form.environment" placeholder="production" />
          </label>
          <label class="field" style="flex: 1; min-width: 120px">
            Severity
            <select v-model="form.severity">
              <option value="">—</option>
              <option value="sev1">sev1</option>
              <option value="sev2">sev2</option>
              <option value="sev3">sev3</option>
              <option value="sev4">sev4</option>
            </select>
          </label>
        </div>
      </div>

      <div class="card">
        <div class="card-title">Resolution</div>
        <label class="field">
          Root cause
          <input v-model="form.resolution.rootCause" placeholder="What actually caused it" />
        </label>
        <label class="field">
          Summary
          <textarea v-model="form.resolution.summary" placeholder="How it was resolved"></textarea>
        </label>
        <label class="field">Steps taken</label>
        <div class="stack-sm" style="margin-top: 0.35rem">
          <div v-for="(step, i) in form.resolution.stepsTaken" :key="i" class="row" style="gap: 0.4rem; flex-wrap: nowrap">
            <input v-model="form.resolution.stepsTaken[i]" :placeholder="`Step ${i + 1}`" />
            <button class="btn ghost small" type="button" @click="removeStep(i)" title="Remove step">✕</button>
          </div>
          <button class="btn ghost small" type="button" @click="addStep">+ Add step</button>
        </div>
        <label class="field">
          Resolved by
          <input v-model="form.resolution.resolvedBy" placeholder="Name" />
        </label>
      </div>
    </div>

    <div class="row" style="margin-top: 1.1rem">
      <button class="btn" :disabled="loading || !form.title.trim()" @click="submit">
        <span v-if="loading" class="spinner"></span>
        {{ loading ? 'Saving…' : 'Save incident' }}
      </button>
      <button class="btn ghost" type="button" :disabled="loading" @click="resetForm">Reset</button>
    </div>

    <div v-if="success" class="alert success">{{ success }}</div>
    <div v-if="error" class="alert error">
      {{ error }}
      <div v-if="errorDetail" class="dim" style="margin-top: 0.4rem; font-size: 0.82rem">{{ errorDetail }}</div>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue';
import { api } from '../api/client';

const blank = () => ({
  title: '',
  description: '',
  serviceName: '',
  environment: '',
  severity: '',
  resolution: { rootCause: '', summary: '', stepsTaken: [''], resolvedBy: '' }
});

const form = reactive(blank());
const loading = ref(false);
const success = ref('');
const error = ref('');
const errorDetail = ref('');

const slackText = ref('');
const extracting = ref(false);
const extractMessage = ref('');
const extractError = ref('');
const extractErrorDetail = ref('');

const extractFromSlack = async () => {
  if (!slackText.value.trim() || extracting.value) return;
  extracting.value = true;
  extractMessage.value = '';
  extractError.value = '';
  extractErrorDetail.value = '';
  try {
    const draft = await api.extractIncident(slackText.value.trim());
    // Populate the form from the draft; keep the steps editor non-empty.
    form.title = draft.title || '';
    form.description = draft.description || '';
    form.serviceName = draft.serviceName || '';
    form.environment = draft.environment || '';
    form.severity = draft.severity || '';
    const r = draft.resolution || {};
    form.resolution.rootCause = r.rootCause || '';
    form.resolution.summary = r.summary || '';
    form.resolution.resolvedBy = r.resolvedBy || '';
    const steps = Array.isArray(r.stepsTaken) ? r.stepsTaken.filter(Boolean) : [];
    form.resolution.stepsTaken = steps.length ? steps : [''];
    extractMessage.value = 'Fields filled from the conversation — review and edit before saving.';
  } catch (e) {
    extractError.value = e.message || 'Extraction failed.';
    extractErrorDetail.value = e.detail && e.detail !== e.message ? e.detail : '';
  } finally {
    extracting.value = false;
  }
};

const addStep = () => form.resolution.stepsTaken.push('');
const removeStep = (i) => {
  form.resolution.stepsTaken.splice(i, 1);
  if (!form.resolution.stepsTaken.length) form.resolution.stepsTaken.push('');
};

const resetForm = () => {
  Object.assign(form, blank());
  success.value = '';
  error.value = '';
  errorDetail.value = '';
  slackText.value = '';
  extractMessage.value = '';
  extractError.value = '';
  extractErrorDetail.value = '';
};

const submit = async () => {
  if (!form.title.trim() || loading.value) return;
  loading.value = true;
  success.value = '';
  error.value = '';
  errorDetail.value = '';
  try {
    const payload = {
      title: form.title.trim(),
      description: form.description.trim(),
      serviceName: form.serviceName.trim(),
      environment: form.environment.trim(),
      severity: form.severity,
      resolution: {
        rootCause: form.resolution.rootCause.trim(),
        summary: form.resolution.summary.trim(),
        resolvedBy: form.resolution.resolvedBy.trim(),
        stepsTaken: form.resolution.stepsTaken.map((s) => s.trim()).filter(Boolean)
      }
    };
    const created = await api.createIncident(payload);
    const id = created?.id || created?.Id || '';
    success.value = `Incident saved${id ? ` (id: ${id})` : ''}.`;
    Object.assign(form, blank());
  } catch (e) {
    error.value = e.message || 'Failed to save incident.';
    errorDetail.value = e.detail && e.detail !== e.message ? e.detail : '';
  } finally {
    loading.value = false;
  }
};
</script>
