# Stage 1: Build the Vite app
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies from the lockfile for reproducible builds
COPY package.json package-lock.json ./
RUN npm ci

# Build the production bundle (tsc -b && vite build)
COPY . .
RUN npm run build

# Stage 2: Serve the static bundle with Nginx
FROM nginx:1.27-alpine AS runtime

# envsubst template — API_UPSTREAM is substituted at container start
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

# Default upstream is the compose service name; override for standalone runs
ENV API_UPSTREAM=api:8080

EXPOSE 80