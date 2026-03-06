import { createApp } from 'vue';
import { createAuth0 } from '@auth0/auth0-vue';
import App from './App.vue';
import './styles.css';

const app = createApp(App);

app.use(
  createAuth0({
    domain: process.env.VUE_APP_AUTH0_DOMAIN,
    clientId: process.env.VUE_APP_AUTH0_CLIENT_ID,
    authorizationParams: {
      redirect_uri: window.location.origin,
      audience: process.env.VUE_APP_AUTH0_AUDIENCE || 'http://localhost:5172'
    },
    cacheLocation: 'localstorage'
  })
);

app.mount('#app');
