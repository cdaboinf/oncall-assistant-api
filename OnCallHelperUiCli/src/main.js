import { createApp } from 'vue';
import App from './App.vue';
import router from './router';
import { initAuth } from './auth';
import './styles.css';

// Complete any Auth0 redirect callback before the app mounts.
initAuth().finally(() => {
  createApp(App).use(router).mount('#app');
});
