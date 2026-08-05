import { createRouter, createWebHistory } from 'vue-router';
import TriageView from '../views/TriageView.vue';
import SearchView from '../views/SearchView.vue';
import LogIncidentView from '../views/LogIncidentView.vue';
import LoginView from '../views/LoginView.vue';

const routes = [
  { path: '/', redirect: '/triage' },
  { path: '/triage', name: 'triage', component: TriageView, meta: { title: 'Triage' } },
  { path: '/search', name: 'search', component: SearchView, meta: { title: 'Search' } },
  { path: '/log', name: 'log', component: LogIncidentView, meta: { title: 'Log incident' } },
  { path: '/login', name: 'login', component: LoginView, meta: { title: 'Sign in' } },
  { path: '/:pathMatch(.*)*', redirect: '/triage' }
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

router.afterEach((to) => {
  document.title = to.meta?.title ? `${to.meta.title} · OnCall Helper` : 'OnCall Helper';
});

export default router;
