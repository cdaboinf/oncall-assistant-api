<template>
  <span class="badge" :class="severityClass">{{ display }}</span>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  severity: { type: String, default: '' }
});

const normalized = computed(() => (props.severity || '').toLowerCase().replace(/[^a-z0-9]/g, ''));
const display = computed(() => props.severity || 'unknown');
const severityClass = computed(() => {
  if (normalized.value.includes('sev1') || normalized.value.includes('critical') || normalized.value.includes('p1')) return 'sev1';
  if (normalized.value.includes('sev2') || normalized.value.includes('high') || normalized.value.includes('p2')) return 'sev2';
  if (normalized.value.includes('sev3') || normalized.value.includes('medium') || normalized.value.includes('p3')) return 'sev3';
  return 'sev4';
});
</script>
