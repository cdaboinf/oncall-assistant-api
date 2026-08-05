import { createAuth0Client } from '@auth0/auth0-spa-js';
import { reactive } from 'vue';

export const authState = reactive({
  loading: true,
  isAuthenticated: false,
  user: null,
  error: ''
});

const config = {
  domain: process.env.VUE_APP_AUTH0_DOMAIN,
  clientId: process.env.VUE_APP_AUTH0_CLIENT_ID,
  audience: process.env.VUE_APP_AUTH0_AUDIENCE
};

let client = null;

export const isConfigured = () => Boolean(config.domain && config.clientId);

async function getClient() {
  if (client) return client;
  client = await createAuth0Client({
    domain: config.domain,
    clientId: config.clientId,
    authorizationParams: {
      redirect_uri: window.location.origin,
      ...(config.audience ? { audience: config.audience } : {})
    },
    cacheLocation: 'localstorage',
    useRefreshTokens: true
  });
  return client;
}

// Called once at startup: completes a redirect callback if present, then
// loads the current session state.
export async function initAuth() {
  if (!isConfigured()) {
    authState.loading = false;
    return;
  }
  try {
    const c = await getClient();
    const params = new URLSearchParams(window.location.search);
    if (params.has('code') && params.has('state')) {
      await c.handleRedirectCallback();
      // Strip the ?code&state from the URL without a reload.
      window.history.replaceState({}, document.title, window.location.pathname);
    }
    authState.isAuthenticated = await c.isAuthenticated();
    authState.user = authState.isAuthenticated ? await c.getUser() : null;
  } catch (e) {
    authState.error = e.message || 'Authentication error';
  } finally {
    authState.loading = false;
  }
}

export async function login(targetPath = '/triage') {
  const c = await getClient();
  await c.loginWithRedirect({
    appState: { target: targetPath }
  });
}

export async function logout() {
  const c = await getClient();
  await c.logout({ logoutParams: { returnTo: window.location.origin } });
}

// Returns an access token for the API audience, or null if not signed in.
export async function getAccessToken() {
  if (!isConfigured()) return null;
  try {
    const c = await getClient();
    if (!(await c.isAuthenticated())) return null;
    return await c.getTokenSilently();
  } catch {
    return null;
  }
}
