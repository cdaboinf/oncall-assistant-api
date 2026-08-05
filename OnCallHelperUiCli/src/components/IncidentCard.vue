<template>
  <article class="incident">
    <div class="incident-head">
      <div class="incident-title">{{ incident.title }}</div>
      <span v-if="scoreLabel" class="score-pill" :title="'Similarity score'">{{ scoreLabel }}</span>
    </div>

    <div class="incident-badges">
      <SeverityBadge v-if="incident.severity" :severity="incident.severity" />
      <span v-if="incident.serviceName" class="badge neutral">{{ incident.serviceName }}</span>
      <span v-if="incident.environment" class="badge env">{{ incident.environment }}</span>
      <span v-if="createdLabel" class="badge env">{{ createdLabel }}</span>
    </div>

    <p v-if="incident.description" class="incident-desc">{{ truncatedDescription }}</p>

    <button
      v-if="hasResolution || isDescriptionLong"
      class="btn ghost small"
      style="margin-top: 0.6rem"
      type="button"
      @click="expanded = !expanded"
    >
      {{ expanded ? 'Hide details' : 'Show details' }}
    </button>

    <dl v-if="expanded && hasResolution" class="resolution">
      <template v-if="incident.resolution.rootCause">
        <dt>Root cause</dt>
        <dd>{{ incident.resolution.rootCause }}</dd>
      </template>
      <template v-if="incident.resolution.summary">
        <dt>Resolution summary</dt>
        <dd>{{ incident.resolution.summary }}</dd>
      </template>
      <template v-if="steps.length">
        <dt>Steps taken</dt>
        <dd>
          <ul class="bullets">
            <li v-for="(step, i) in steps" :key="i">{{ step }}</li>
          </ul>
        </dd>
      </template>
      <template v-if="incident.resolution.resolvedBy">
        <dt>Resolved by</dt>
        <dd>{{ incident.resolution.resolvedBy }}</dd>
      </template>
    </dl>
  </article>
</template>

<script setup>
import { computed, ref } from 'vue';
import SeverityBadge from './SeverityBadge.vue';

const props = defineProps({
  incident: { type: Object, required: true }
});

const expanded = ref(false);

const scoreLabel = computed(() =>
  typeof props.incident.score === 'number' ? `${Math.round(props.incident.score * 100)}% match` : ''
);

const createdLabel = computed(() => {
  if (!props.incident.createdAt) return '';
  const d = new Date(props.incident.createdAt);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString();
});

const steps = computed(() => props.incident.resolution?.stepsTaken || []);
const hasResolution = computed(() => {
  const r = props.incident.resolution;
  return Boolean(r && (r.rootCause || r.summary || r.resolvedBy || steps.value.length));
});

const isDescriptionLong = computed(() => (props.incident.description || '').length > 220);
const truncatedDescription = computed(() => {
  const d = props.incident.description || '';
  if (expanded.value || d.length <= 220) return d;
  return `${d.slice(0, 220)}…`;
});
</script>
