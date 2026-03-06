# OnCallHelper UI (Vue CLI SPA)

This is the active UI app and all app source files used by `npm run serve` are inside this folder.

## App file locations

- `public/index.html` (HTML shell)
- `src/main.js` (Vue app bootstrap)
- `src/App.vue` (single-page UI component)
- `src/styles.css` (styles)

`npm run serve` executes `vue-cli-service serve` from `OnCallHelperUiCli`, which compiles and serves these files.

## Prerequisites

- Node.js 18+
- npm

## Install and run locally

```bash
cd OnCallHelperUiCli
npm install
npm run serve
```

Then open `http://localhost:8080`.

> Note: API endpoints are protected with JWT bearer auth. Paste a valid token in the **Bearer Token** field in the UI before calling the API.

## Build

```bash
npm run build
```

## API endpoints used

- `POST /api/incidents`
- `GET /api/incidents`
- `POST /api/incidents/similar`
- `POST /api/oncall/analyze`
