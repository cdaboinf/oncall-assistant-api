<template>
  <div>
    <div class="page-header">
      <h1>Log an incident</h1>
      <p>Capture what happened and how it was resolved so future on-call shifts benefit.</p>
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
