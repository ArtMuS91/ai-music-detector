FROM node:22-alpine AS build
WORKDIR /web

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# Empty, not unset: api.ts then calls the API same-origin, and nginx proxies /api to it (see nginx.conf).
ENV VITE_API_BASE_URL=
RUN npm run build

FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist /usr/share/nginx/html
EXPOSE 80
