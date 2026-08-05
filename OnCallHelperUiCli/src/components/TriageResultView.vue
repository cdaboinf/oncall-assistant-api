<template>
  <div class="grid-2">
    <!-- Guidance column -->
    <div>
      <div class="card">
        <div class="card-title">Summary</div>
        <p style="margin: 0; line-height: 1.55">{{ analysis.summary || '—' }}</p>
      </div>

      <div class="card">
        <div class="card-title">Likely root cause</div>
        <p style="margin: 0; line-height: 1.55">{{ analysis.likelyRootCause || '—' }}</p>
      </div>

      <div class="card">
        <div class="card-title">Immediate actions</div>
        <ol v-if="immediateActions.length" class="checklist">
          <li v-for="(action, i) in immediateActions" :key="i">{{ action }}</li>
        </ol>
        <p v-else class="dim" style="margin: 0">No immediate actions suggested.</p>
      </div>

      <div class="card" v-if="longTermFixes.length">
        <div class="card-title">Long-term fixes</div>
        <ul class="bullets">
          <li v-for="(fix, i) in longTermFixes" :key="i">{{ fix }}</li>
        </ul>
      </div>
    </div>

    <!-- Meta / actions column -->
    <div>
      <div class="card">
        <div class="card-title">Confidence</div>
        <div class="spread">
          <strong style="font-size: 1.3rem">{{ confidencePct }}%</strong>
          <span class="dim">{{ confidenceLabel }}</span>
        </div>
        <div class="meter"><span :style="{ width: confidencePct + '%' }"></span></div>
      </div>

      <div class="card">
        <div class="card-title">Escalation</div>
        <p style="margin: 0; line-height: 1.5">{{ analysis.escalationRecommendation || '—' }}</p>
      </div>

      <div class="card" v-if="analysis.slackMessageDraft">
        <div class="draft-head">
          <div class="card-title" style="margin: 0">Slack update</div>
          <CopyButton :text="analysis.slackMessageDraft" />
        </div>
        <div class="draft">{{ analysis.slackMessageDraft }}</div>
      </div>

      <div class="card" v-if="analysis.statusPageDraft">
        <div class="draft-head">
          <div class="card-title" style="margin: 0">Status page</div>
          <CopyButton :text="analysis.statusPageDraft" />
        </div>
        <div class="draft">{{ analysis.statusPageDraft }}</div>
      </div>
    </div>
  </div>

  <!-- Evidence -->
  <div class="card" style="margin-top: 1.1rem" v-if="similarIncidents.length">
    <div class="card-title">Based on {{ similarIncidents.length }} past incident{{ similarIncidents.length === 1 ? '' : 's' }}</div>
    <IncidentCard v-for="inc in similarIncidents" :key="inc.id" :incident="inc" />
  </div>
</template>

<script setup>
import { computed } from 'vue';
import CopyButton from './CopyButton.vue';
import IncidentCard from './IncidentCard.vue';

const props = defineProps({
  result: { type: Object, required: true }
});

const analysis = computed(() => props.result.analysis || {});
const similarIncidents = computed(() => props.result.similarIncidents || []);
const immediateActions = computed(() => analysis.value.immediateActions || []);
const longTermFixes = computed(() => analysis.value.longTermFixes || []);

const confidencePct = computed(() => {
  const c = analysis.value.confidenceScore;
  if (typeof c !== 'number') return 0;
  return Math.round((c > 1 ? c / 100 : c) * 100);
});
const confidenceLabel = computed(() => {
  const p = confidencePct.value;
  if (p >= 75) return 'High';
  if (p >= 45) return 'Moderate';
  return 'Low — verify carefully';
});
</script>
