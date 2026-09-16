<template>
  <div class="drawer-backdrop" @click.self="close">
    <div class="drawer">
      <div class="spread" style="margin-bottom: 1rem">
        <h2 style="font-size: 1.15rem">Edit incident</h2>
        <button class="btn ghost small" @click="close">✕</button>
      </div>

      <div class="card-title">Incident</div>
      <label class="field">
        Title
        <input v-model="form.title" placeholder="Short, searchable title" />
      </label>
      <label class="field">
        Description
        <textarea v-model="form.description" placeholder="Symptoms, impact, when it started…"></textarea>
      </label>
      <label class="field">
        Service
        <input v-model="form.serviceName" placeholder="e.g. NAG" />
      </label>
      <label class="field">
        Environment
        <input v-model="form.environment" placeholder="production" />
      </label>
      <label class="field">
        Severity
        <select v-model="form.severity">
          <option value="">—</option>
          <option value="sev1">sev1</option>
          <option value="sev2">sev2</option>
          <option value="sev3">sev3</option>
          <option value="sev4">sev4</option>
        </select>
      </label>

      <div class="card-title" style="margin-top: 1.2rem">Resolution</div>
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

      <div class="row" style="margin-top: 1.3rem">
        <button class="btn" :disabled="saving || !form.title.trim()" @click="save">
          <span v-if="saving" class="spinner"></span>
          {{ saving ? 'Saving…' : 'Save changes' }}
        </button>
        <button class="btn ghost" type="button" :disabled="saving" @click="close">Cancel</button>
      </div>

      <div v-if="error" class="alert error">
        {{ error }}
        <div v-if="errorDetail" class="dim" style="margin-top: 0.4rem; font-size: 0.82rem">{{ errorDetail }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue';
import { api } from '../api/client';

const props = defineProps({
  incident: { type: Object, required: true }
});
const emit = defineEmits(['saved', 'close']);

const r = props.incident.resolution || {};
const steps = Array.isArray(r.stepsTaken) ? r.stepsTaken.filter(Boolean) : [];

const form = reactive({
  title: props.incident.title || '',
  description: props.incident.description || '',
  serviceName: props.incident.serviceName || '',
  environment: props.incident.environment || '',
  severity: props.incident.severity || '',
  resolution: {
    rootCause: r.rootCause || '',
    summary: r.summary || '',
    resolvedBy: r.resolvedBy || '',
    stepsTaken: steps.length ? steps : ['']
  }
});

const saving = ref(false);
const error = ref('');
const errorDetail = ref('');

const addStep = () => form.resolution.stepsTaken.push('');
const removeStep = (i) => {
  form.resolution.stepsTaken.splice(i, 1);
  if (!form.resolution.stepsTaken.length) form.resolution.stepsTaken.push('');
};

const close = () => {
  if (!saving.value) emit('close');
};

const save = async () => {
  if (!form.title.trim() || saving.value) return;
  saving.value = true;
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
    const updated = await api.updateIncident(props.incident.id, payload);
    emit('saved', updated);
  } catch (e) {
    error.value = e.message || 'Failed to save changes.';
    errorDetail.value = e.detail && e.detail !== e.message ? e.detail : '';
  } finally {
    saving.value = false;
  }
};
</script>
