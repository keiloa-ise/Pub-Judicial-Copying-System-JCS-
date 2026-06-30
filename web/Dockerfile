# ── JCS web (Vite SPA → Nginx) ───────────────────────────────────────────
# Build context = ./web
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
# Empty base => the SPA calls /api/... on its own origin (Nginx proxies it to the API).
ARG VITE_API_BASE=""
ENV VITE_API_BASE=$VITE_API_BASE
RUN npm run build

# ── Serve ────────────────────────────────────────────────────────────────
FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
