<template>
  <button class="btn ghost small" type="button" @click="copy">
    {{ copied ? '✓ Copied' : label }}
  </button>
</template>

<script setup>
import { ref } from 'vue';

const props = defineProps({
  text: { type: String, default: '' },
  label: { type: String, default: 'Copy' }
});

const copied = ref(false);

const copy = async () => {
  try {
    await navigator.clipboard.writeText(props.text || '');
  } catch {
    // Fallback for non-secure contexts.
    const ta = document.createElement('textarea');
    ta.value = props.text || '';
    document.body.appendChild(ta);
    ta.select();
    document.execCommand('copy');
    document.body.removeChild(ta);
  }
  copied.value = true;
  setTimeout(() => (copied.value = false), 1500);
};
</script>
