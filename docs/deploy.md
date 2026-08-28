# VPS Deployment Guide

Deploy the Mini E-Commerce Platform to a VPS with Docker, automatic HTTPS via Caddy, and persistent storage.

---

## Prerequisites

| Requirement | Minimum |
|---|---|
| OS | Ubuntu 22.04 LTS (or any Docker-capable Linux) |
| RAM | 1 GB (2 GB recommended) |
| Disk | 20 GB free |
| Domain | A DNS A record pointing to the VPS IP |
| SSH | Key-based access to the VPS |

---

## 1. Provision the VPS

Spin up a VPS from any provider (Hetzner, DigitalOcean, Vultr, AWS Lightsail, etc.) and point your domain's A record at its public IP.

```bash
ssh root@your-domain.com
```

---

## 2. Install Docker & Docker Compose

```bash
# Install Docker Engine
curl -fsSL https://get.docker.com | sh

# Verify
docker --version          # Docker version 27+
docker compose version    # Docker Compose v2
```

---

## 3. Clone the repository

```bash
cd /opt
git clone https://github.com/your-org/Mini-E-Commerce-Platform.git
cd Mini-E-Commerce-Platform
```

---

## 4. Create the `.env` file

Copy the example and fill in real secrets:

```bash
cp .env.example .env
```

Edit `.env` and set **every required value**:

```dotenv
# PostgreSQL
POSTGRES_DB=mini_ecommerce
POSTGRES_USER=postgres
POSTGRES_PASSWORD=<generate-a-strong-password>

# JWT — MUST be ≥ 32 bytes (256 bits). Generate with:
#   openssl rand -base64 48
JWT_KEY=<random-base64-string>
JWT_ISSUER=MiniEcommerce
JWT_AUDIENCE=MiniEcommerce

# Public origin (what the browser sees)
CORS_ORIGIN=https://your-domain.com
```

> **Never commit `.env` to version control.** It is already in `.gitignore`.

---

## 5. Start the production stack

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

This starts three containers:

| Container | Port | Purpose |
|---|---|---|
| `db` | internal | PostgreSQL 16 (data in the `pgdata` volume) |
| `api` | 5000 → 8080 | ASP.NET Core API |
| `web` | 80 → 80 | Nginx SPA + reverse proxy to `/api` |

Migrations run automatically on API startup.

### Verify

```bash
# Health check
curl -s http://localhost:5000/health | jq .

# Full stack
curl -sI http://localhost     # should return 200 from Nginx
```

---

## 6. Set up Caddy for automatic HTTPS

Caddy handles TLS termination, automatic Let's Encrypt certificates, and HTTP → HTTPS redirect.

### Option A: Caddy on the same VPS (recommended)

```bash
# Install Caddy
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install caddy
```

Create the Caddyfile:

```bash
sudo tee /etc/caddy/Caddyfile <<'EOF'
your-domain.com {
    reverse_proxy localhost:80
}
EOF
```

```bash
sudo systemctl reload caddy
```

Caddy will automatically obtain and renew a Let's Encrypt certificate. Visit `https://your-domain.com` to verify.

### Option B: Caddy in Docker

Add a Caddy container to `docker-compose.prod.yml` (alternative to the system Caddy above):

```yaml
  caddy:
    image: caddy:2-alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    depends_on:
      - web
```

With a `Caddyfile` in the project root:

```
your-domain.com {
    reverse_proxy web:80
}
```

In this case, remove the `ports: "80:80"` mapping from the `web` service in `docker-compose.prod.yml` so Caddy owns port 80.

---

## 7. Firewall / Security group

Open only the necessary ports:

```bash
# If using UFW
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP (Caddy redirect)
sudo ufw allow 443/tcp   # HTTPS
sudo ufw enable
```

> **Do not expose port 5000 (the API) directly.** Caddy on port 80/443 proxies to the Nginx container, which proxies `/api` to the API. The API is only reachable through this chain.

---

## 8. Backups

### PostgreSQL

The database lives in the `pgdata` Docker volume. Back it up with `pg_dump`:

```bash
docker compose -f docker-compose.prod.yml exec db \
  pg_dump -U postgres mini_ecommerce > backup_$(date +%Y%m%d).sql
```

Restore with:

```bash
cat backup_20260824.sql | docker compose -f docker-compose.prod.yml exec -T db \
  psql -U postgres mini_ecommerce
```

### Uploaded images

Product images are stored in the `images` Docker volume mounted at `/app/wwwroot/images`. Back it up with:

```bash
docker run --rm -v mini-ecommerce-platform_images:/data -v $(pwd):/backup \
  alpine tar czf /backup/images_$(date +%Y%m%d).tar.gz -C /data .
```

### Automated backups (optional)

Add a cron job for daily backups:

```bash
sudo crontab -e
```

```cron
# Daily at 03:00 UTC — database dump
0 3 * * * cd /opt/Mini-E-Commerce-Platform && docker compose -f docker-compose.prod.yml exec -T db pg_dump -U postgres mini_ecommerce | gzip > /var/backups/mini-ecommerce/db_$(date +\%Y\%m\%d).sql.gz

# Weekly on Sunday at 04:00 UTC — images tarball
0 4 * * 0 docker run --rm -v mini-ecommerce-platform_images:/data -v /var/backups/mini-ecommerce:/backup alpine tar czf /backup/images_$(date +\%Y\%m\%d).tar.gz -C /data .
```

Create the backup directory:

```bash
sudo mkdir -p /var/backups/mini-ecommerce
```

---

## 9. Updates

Pull the latest code and rebuild:

```bash
cd /opt/Mini-E-Commerce-Platform
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

This rebuilds only the images that changed, re-runs any pending migrations on API startup, and keeps the database volume intact.

### Zero-downtime note

The rebuild causes a brief restart (~2–5 seconds). For true zero-downtime deploys you would need a reverse proxy with blue/green or rolling strategy — out of scope for v1.

---

## 10. Logs & Troubleshooting

```bash
# Tail all services
docker compose -f docker-compose.prod.yml logs -f

# Tail one service
docker compose -f docker-compose.prod.yml logs -f api

# Check container status
docker compose -f docker-compose.prod.yml ps
```

### Common issues

| Symptom | Fix |
|---|---|
| `JWT_KEY` error on startup | Ensure `JWT_KEY` in `.env` is ≥ 32 bytes. Generate with `openssl rand -base64 48`. |
| API returns 401 for all requests | Check `CORS_ORIGIN` matches `https://your-domain.com` exactly (include scheme). |
| Migrations fail | Ensure the `db` container is healthy before the `api` starts (the `depends_on: condition: service_healthy` handles this). |
| Images not loading | Verify the `images` volume is mounted: `docker compose -f docker-compose.prod.yml exec api ls /app/wwwroot/images`. |
| Port 80 already in use | Stop any existing web server: `sudo systemctl stop nginx apache2`. |

---

## Summary

```
VPS
├── Caddy (80/443) ──→ Nginx (web container, port 80)
│                           ├── /        → SPA (static files)
│                           └── /api/    → API (api container, port 8080)
│                                            └── PostgreSQL (db container, internal)
└── Volumes
    ├── pgdata          (database)
    └── images          (uploaded product images)
```
