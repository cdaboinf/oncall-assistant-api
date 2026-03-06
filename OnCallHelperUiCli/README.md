# OnCallHelper UI (Vue CLI SPA)

This is the active UI app and all app source files used by `npm run serve` are inside this folder.

## App file locations

- `public/index.html` (HTML shell)
- `src/main.js` (Vue app bootstrap + Auth0 plugin)
- `src/App.vue` (single-page UI component)
- `src/styles.css` (styles)

## Prerequisites

- Node.js 18+
- npm

## Auth0 SPA setup

Create `OnCallHelperUiCli/.env.local` with:

```bash
VUE_APP_AUTH0_DOMAIN=dev-k0sl1xaa1o87ofbn.us.auth0.com
VUE_APP_AUTH0_CLIENT_ID=FDImJlb9f0gJPBc6RF8gUsyBtizMfApv
VUE_APP_AUTH0_AUDIENCE=http://localhost:5172
```

> For Vue CLI, env vars must be prefixed with `VUE_APP_` (not `VITE_`).

## Install and run locally

```bash
cd OnCallHelperUiCli
npm install
npm run serve
```

Then open `http://localhost:5172`.

Use **Login** / **Signup** in the app header. After login, API requests automatically attach `Authorization: Bearer <token>` from Auth0.

## Build

```bash
npm run build
```

## API endpoints used

- `POST /api/incidents`
- `GET /api/incidents`
- `POST /api/incidents/similar`
- `POST /api/oncall/analyze`
