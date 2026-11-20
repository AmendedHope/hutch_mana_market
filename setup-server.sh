#!/usr/bin/env bash
# One-time setup for a fresh Ubuntu 24.04 DigitalOcean droplet.
# Run from inside the cloned project folder:   sudo bash setup-server.sh
# Safe to run again: it skips anything already done and keeps your existing .env.
set -euo pipefail
cd "$(dirname "$0")"

if [ "$(id -u)" -ne 0 ]; then echo "Please run with sudo:  sudo bash setup-server.sh"; exit 1; fi

echo "==> Installing updates, Docker and the firewall (takes a few minutes)"
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get upgrade -y
apt-get install -y docker.io docker-compose-v2 ufw unattended-upgrades openssl
systemctl enable --now docker

# A 1 GB droplet needs swap space to build the app without running out of memory.
if ! swapon --show | grep -q .; then
    echo "==> Adding 2 GB of swap"
    fallocate -l 2G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile >/dev/null
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "==> Firewall: allowing only SSH, HTTP and HTTPS"
ufw allow OpenSSH >/dev/null
ufw allow 80/tcp >/dev/null
ufw allow 443/tcp >/dev/null
ufw allow 443/udp >/dev/null
ufw --force enable >/dev/null

if [ ! -f .env ]; then
    echo
    echo "==> Shop settings"
    read -rp "Your domain for the shop (e.g. shop.example.com): " DOMAIN
    read -rp "Owner username (the Auth__Username line from set-password --print): " AUTH_USERNAME
    read -rp "Owner password hash (the Auth__PasswordHash value, everything after the =): " AUTH_PASSWORD_HASH
    read -rp "Your time zone, e.g. America/Chicago [UTC]: " TZ_IN
    read -rp "Discord webhook URL for price alerts (optional, press Enter to skip): " DISCORD

    DOMAIN="$(echo "$DOMAIN" | tr -d '[:space:]')"
    AUTH_USERNAME="$(echo "$AUTH_USERNAME" | sed -E 's/^Auth__Username=//' | tr -d '[:space:]')"
    AUTH_PASSWORD_HASH="$(echo "$AUTH_PASSWORD_HASH" | sed -E 's/^Auth__PasswordHash=//' | tr -d '[:space:]')"
    if [ -z "$DOMAIN" ] || [ -z "$AUTH_USERNAME" ] || [ -z "$AUTH_PASSWORD_HASH" ]; then
        echo "Domain, username and password hash are all required. Run this again."; exit 1
    fi

    umask 077
    cat > .env <<EOF
DOMAIN=$DOMAIN
AUTH_USERNAME=$AUTH_USERNAME
AUTH_PASSWORD_HASH=$AUTH_PASSWORD_HASH
POSTGRES_PASSWORD=$(openssl rand -hex 24)
TZ=${TZ_IN:-UTC}
DISCORD_WEBHOOK_URL=$DISCORD
EOF
    chmod 600 .env
    echo "Saved settings to .env (only root can read it)."
else
    echo "==> Keeping existing .env"
fi

echo
echo "==> Building and starting the shop (the first build takes 5-10 minutes on a small droplet)"
docker compose up -d --build

echo
docker compose ps
echo
echo "Done. Open https://$(grep '^DOMAIN=' .env | cut -d= -f2) in a few moments."
echo "If the page doesn't load, check:  docker compose logs caddy app"
