import { reactive } from 'vue';
import { getAccessToken } from '../auth';

// Base URL is configurable (defaults from .env); the token comes from Auth0.
export const apiState = reactive({
  baseUrl: localStorage.getItem('apiBaseUrl') || process.env.VUE_APP_API_BASE_URL || 'http://localhost:5172'
});

export function setBaseUrl(value) {
  apiState.baseUrl = value;
  localStorage.setItem('apiBaseUrl', value);
}

const endpoint = (path) => `${apiState.baseUrl.replace(/\/$/, '')}${path}`;

async function request(path, { method = 'GET', body, useAuth = true } = {}) {
  const headers = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  if (useAuth) {
    const token = await getAccessToken();
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  let response;
  try {
    response = await fetch(endpoint(path), {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined
    });
  } catch (networkError) {
    const err = new Error(
      `Could not reach the API at ${apiState.baseUrl}. Is it running and is the base URL correct?`
    );
    err.cause = networkError;
    throw err;
  }

  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = text;
    }
  }

  if (!response.ok) {
    const message =
      (data && (data.error || data.detail)) ||
      (typeof data === 'string' && data) ||
      `Request failed (${response.status})`;
    const err = new Error(message);
    err.status = response.status;
    err.detail = data && data.detail;
    err.data = data;
    throw err;
  }

  return data;
}

export const api = {
  searchIncidents(filters = {}) {
    const params = new URLSearchParams();
    const map = {
      q: filters.q,
      serviceName: filters.serviceName,
      severity: filters.severity,
      environment: filters.environment,
      from: filters.from,
      to: filters.to,
      limit: filters.limit
    };
    Object.entries(map).forEach(([key, value]) => {
      if (value !== undefined && value !== null && `${value}`.trim() !== '') {
        params.append(key, value);
      }
    });
    const qs = params.toString();
    return request(`/api/incidents${qs ? `?${qs}` : ''}`);
  },

  createIncident(payload) {
    return request('/api/incidents', { method: 'POST', body: payload });
  },

  analyze(description) {
    return request('/api/oncall/analyze', { method: 'POST', body: { description } });
  },

  findSimilar(description, top = 5) {
    return request('/api/incidents/similar', { method: 'POST', body: { description, top } });
  }
};
