# Deploying HutchisMuch Mana Shop to DigitalOcean

The shop runs on one $6/month DigitalOcean droplet, as three Docker containers:

- **app**: the shop itself
- **db**: PostgreSQL, reachable only by the app and never from the internet
- **caddy**: gets and renews a free HTTPS certificate automatically

Allow about 30 minutes the first time.

---

## Step 1: Get a domain name

HTTPS needs a domain name that points at your server. Either:

- **Buy one** (about $10–15/year) from Namecheap, Cloudflare, Porkbun or similar, or
- **Use a free subdomain** from [DuckDNS](https://www.duckdns.org), e.g. `hutchmana.duckdns.org`.

You'll point it at the server in step 3.

## Step 2: Create the droplet

In the DigitalOcean control panel, choose **Create → Droplets**:

| Setting | Choose |
|---|---|
| Region | The one closest to you |
| Image | **Ubuntu 24.04 (LTS)** |
| Size | **Basic → Regular → $6/mo** (1 GB RAM / 1 CPU) |
| Authentication | **SSH key** is safest. A password also works: make it long. |
| Backups | Optional, about $1.20/mo extra for weekly snapshots. Recommended. |

Create it and copy its **IP address**.

## Step 3: Point your domain at the droplet

In your domain provider's DNS settings, add an **A record**:

- **Name/Host:** `@` for the bare domain, or a subdomain like `shop`
- **Value:** the droplet's IP address

On DuckDNS, paste the IP into the box next to your subdomain and click *update ip*.

DNS can take a few minutes to update. Check with `ping your-domain` from your computer: it should show the droplet's IP.

## Step 4: Make the owner password (on your computer)

In the project folder on **your computer**:

```bash
dotnet run --project src/CardShop.Web -- set-password --print
```

Choose a username and a strong password. Keep the two lines it prints (`Auth__Username=...` and `Auth__PasswordHash=...`) handy for step 6. Nothing is saved on your computer.

## Step 5: Connect to the droplet and download the code

```bash
ssh root@YOUR_DROPLET_IP
git clone https://github.com/AmendedHope/hutch_mana_market.git
cd hutch_mana_market
```

If the GitHub repo is **private**, `git clone` will ask for a username and password. Use your GitHub username, and for the password a **personal access token** (GitHub → Settings → Developer settings → Personal access tokens → *Fine-grained*, with read access to this repo's contents).

## Step 6: Run the setup script

```bash
sudo bash setup-server.sh
```

It will:

1. install updates, Docker and a firewall that allows only SSH, HTTP and HTTPS
2. add swap space so the small droplet can build the app
3. ask for your domain, the two lines from step 4, your time zone and an optional Discord webhook
4. ask which database to use: press **Enter** for the built-in one, or enter your **Managed Database** details (see below)
5. save everything to `.env` (readable only by root), then build and start the shop

The first build takes 5–10 minutes.

## Step 7: Sign in

Open `https://your-domain`. You should land on the sign-in page. Sign in, then:

1. **Import** your inventory file on `/Import`
2. Run **Sync now** on `/Sync` to load market prices

After that, prices sync automatically in the background.

### Using a DigitalOcean Managed Database (optional)

By default the shop runs its own PostgreSQL on the droplet for free. A Managed Database costs extra, but DigitalOcean handles backups, updates and failover for you.

When the setup script asks for the **Managed database host**, enter the details from the database's **Connection Details** panel in DigitalOcean:

| Prompt | Where to find it |
|---|---|
| Host | `host`. If the droplet is in the same region, choose **VPC network** in the panel and use that private host instead: it's faster and never crosses the internet. |
| Port | `port` (usually 25060) |
| Database | `database` (e.g. `defaultdb`) |
| Username | `username` (e.g. `doadmin`) |
| Password | `password`. It's hidden as you type or paste. |

The connection is always encrypted (SSL). The app creates its tables on first start.

**Lock the database down:** in DigitalOcean, open the database → **Settings** → **Trusted sources**, and add your droplet. After that, only your droplet can connect.

**Already ran the setup script with the built-in database?** Edit `.env` (`nano .env`): delete the `COMPOSE_PROFILES=localdb` line and replace the `DATABASE_CONNECTION=` line with:

```
DATABASE_CONNECTION='Host=YOUR_HOST;Port=25060;Database=defaultdb;Username=doadmin;Password=YOUR_PASSWORD;SSL Mode=Require'
```

Keep the single quotes. Then run `docker compose up -d --remove-orphans`.

---

## Everyday tasks

All commands run on the droplet, inside the `hutch_mana_market` folder.

**Deploy new changes** (after pushing them to GitHub from your computer):

```bash
git pull
docker compose up -d --build
```

**Change the owner password:** run `set-password --print` on your computer again. On the droplet, edit `.env` (`nano .env`), replace `AUTH_PASSWORD_HASH`, then run `docker compose up -d`. Everyone is signed out.

**Back up the database:**

```bash
docker compose exec -T db pg_dump -U cardshop cardshop > backup-$(date +%F).sql
```

Copy it to your computer with `scp root@YOUR_DROPLET_IP:~/hutch_mana_market/backup-*.sql .`

**Restore a backup:**

```bash
docker compose exec -T db psql -U cardshop cardshop < backup-2026-01-01.sql
```

**See what's happening:**

```bash
docker compose ps                # are all three running?
docker compose logs -f app       # shop logs (Ctrl+C to stop watching)
docker compose logs caddy        # HTTPS certificate problems show here
```

**Restart everything:** `docker compose restart`

---

## Troubleshooting

| Problem | Likely cause |
|---|---|
| Browser can't reach the site | DNS isn't pointing at the droplet yet (check with `ping your-domain`), or the containers aren't running (`docker compose ps`). |
| Certificate or "not secure" errors | Caddy couldn't get a certificate, usually because DNS wasn't pointing at the droplet when it started. Fix DNS, then `docker compose restart caddy`. |
| "No owner account yet" on the sign-in page | `AUTH_USERNAME` or `AUTH_PASSWORD_HASH` is missing from `.env`. Fix it, then `docker compose up -d`. |
| Sign-in loops back to the sign-in page | You're using `http://` or the IP address. Use `https://your-domain`. |
| Build fails with "killed" or out of memory | Swap is missing. Run `sudo bash setup-server.sh` again, which adds it. |
| Everyone is signed out after a redeploy | Expected only after a password change. Otherwise check that the `keys` volume still exists (`docker volume ls`). |

## Reference: settings

These live in `.env` on the droplet and are passed to the containers by `docker-compose.yml`.

| `.env` setting | Purpose |
|---|---|
| `DOMAIN` | Your domain, used by Caddy for HTTPS |
| `AUTH_USERNAME`, `AUTH_PASSWORD_HASH` | Owner sign-in (from `set-password --print`) |
| `DATABASE_CONNECTION` | How the app reaches its database: the built-in one or your Managed Database. Keep it in single quotes. |
| `COMPOSE_PROFILES` | `localdb` when using the built-in database. Leave it out for a Managed Database. |
| `POSTGRES_PASSWORD` | Built-in database only. Generated randomly by the setup script. Changing it after the first start won't update the database, so leave it alone. |
| `TZ` | Time zone for the home-page greeting, e.g. `America/Chicago` |
| `DISCORD_WEBHOOK_URL` | Optional price-alert notifications |
