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

## Run in Docker (portable for Windows/macOS/Linux)

Build the image from the repo root:

Inside directory: /OnCallHelperUiCli
```bash
docker build -t oncall-helper-ui:latest .

docker buildx build \
  --platform linux/amd64 \
  --provenance=false \
  -t cedaboin/oncall-helper-ui:v1.0.1 \
  -f Dockerfile \
  . \
  --push
```

Run the container and expose it on port `8080`:

```bash
docker run --rm -p 8080:80 --name oncall-helper-ui oncall-helper-ui:latest
```

Then open `http://localhost:8080` on any machine running Docker Desktop (including Windows PCs).

### Publish and share image (optional): docker account: cedaboin
```bash
docker push cedaboin/oncall-helper-ui:v1
```

If you want teammates to run the same UI image without building locally, push it to a registry (Docker Hub, ECR, GHCR) and they can run:

```bash
docker pull <your-registry>/oncall-helper-ui:latest
docker run --rm -p 8080:80 <your-registry>/oncall-helper-ui:latest
```
