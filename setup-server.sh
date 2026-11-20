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

    echo
    echo "==> Database"
    echo "Press Enter to use the built-in database on this droplet (free),"
    echo "or type the host of your DigitalOcean Managed Database to use that instead."
    read -rp "Managed database host [built-in]: " DB_HOST
    DB_HOST="$(echo "$DB_HOST" | tr -d '[:space:]')"

    umask 077
    if [ -z "$DB_HOST" ]; then
        PG_PASS="$(openssl rand -hex 24)"
        DB_BLOCK="COMPOSE_PROFILES=localdb
POSTGRES_PASSWORD=$PG_PASS
DATABASE_CONNECTION='Host=db;Database=cardshop;Username=cardshop;Password=$PG_PASS'"
    else
        read -rp "Port [25060]: " DB_PORT
        read -rp "Database [defaultdb]: " DB_NAME
        read -rp "Username [doadmin]: " DB_USER
        read -rsp "Password (hidden as you type): " DB_PASS; echo
        DB_PORT="$(echo "${DB_PORT:-25060}" | tr -d '[:space:]')"
        DB_NAME="$(echo "${DB_NAME:-defaultdb}" | tr -d '[:space:]')"
        DB_USER="$(echo "${DB_USER:-doadmin}" | tr -d '[:space:]')"
        DB_PASS="$(echo "$DB_PASS" | tr -d '[:space:]')"
        if [ -z "$DB_PASS" ]; then echo "The database password is required. Run this again."; exit 1; fi
        case "$DB_PASS" in *\'*|*\;*) echo "That password contains ' or ; which can't be used here. Reset it in DigitalOcean and try again."; exit 1;; esac
        DB_BLOCK="DATABASE_CONNECTION='Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASS;SSL Mode=Require'"
    fi

    cat > .env <<EOF
DOMAIN=$DOMAIN
AUTH_USERNAME=$AUTH_USERNAME
AUTH_PASSWORD_HASH=$AUTH_PASSWORD_HASH
TZ=${TZ_IN:-UTC}
DISCORD_WEBHOOK_URL=$DISCORD
$DB_BLOCK
EOF
    chmod 600 .env
    echo "Saved settings to .env (only root can read it)."
else
    echo "==> Keeping existing .env"
    # .env files from before the managed-database option: keep using the built-in database
    if ! grep -q '^DATABASE_CONNECTION=' .env; then
        PG_PASS="$(grep '^POSTGRES_PASSWORD=' .env | cut -d= -f2-)"
        if [ -n "$PG_PASS" ]; then
            printf "COMPOSE_PROFILES=localdb\nDATABASE_CONNECTION='Host=db;Database=cardshop;Username=cardshop;Password=%s'\n" "$PG_PASS" >> .env
            echo "    (updated .env for the new database setting; still using the built-in database)"
        fi
    fi
fi

echo
echo "==> Building and starting the shop (the first build takes 5-10 minutes on a small droplet)"
docker compose up -d --build

echo
docker compose ps
echo
echo "Done. Open https://$(grep '^DOMAIN=' .env | cut -d= -f2) in a few moments."
echo "If the page doesn't load, check:  docker compose logs caddy app"
