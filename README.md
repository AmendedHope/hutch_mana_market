# HutchisMuch Mana Shop

A private card-shop tool: inventory, a searchable card-art gallery, and daily market prices from TCGCSV.
Built with ASP.NET Core 10 and PostgreSQL. Every page requires the owner sign-in.

- **Run it on your computer:** see below
- **Put it on a server:** see [DEPLOY.md](DEPLOY.md)

## First time: create your sign-in

```bash
dotnet run --project src/CardShop.Web -- set-password
```

This saves the owner account to your computer's user-secrets file. Both ways of running the app below use it.

## Option A: run with dotnet

Uses your Windows PostgreSQL (connection string in user secrets).

```bash
dotnet run --project src/CardShop.Web
```

Open http://localhost:5133

## Option B: run with Docker

Runs the same container image and PostgreSQL version as the server, with its own separate database.

### Install Docker Desktop (once)

In **PowerShell as Administrator** (right-click PowerShell → *Run as administrator*):

```powershell
wsl --install --no-distribution
```

**Restart the computer**, then in a normal PowerShell:

```powershell
winget install -e --id Docker.DockerDesktop
```

Start **Docker Desktop** from the Start menu, accept the terms, and wait until it says *Engine running*.
Check it works with `docker --version`.

### Start / stop

From the project folder:

```bash
docker compose -f docker-compose.local.yml up -d --build   # start (first build takes a few minutes)
docker compose -f docker-compose.local.yml logs -f app     # watch the logs (Ctrl+C stops watching)
docker compose -f docker-compose.local.yml down            # stop (your data is kept)
```

Open http://localhost:8080

| Thing | Where |
|---|---|
| The shop | http://localhost:8080 |
| Its database (for psql or pgAdmin) | `localhost:5433`, database `cardshop`, user `cardshop`, password `localdev` |

```bash
psql -h localhost -p 5433 -U cardshop -d cardshop   # password: localdev
```

### Good to know

- **Separate data:** the Docker database is separate from your Windows PostgreSQL, so import your inventory again on http://localhost:8080/Import.
- **Code changes:** after changing code, run the `up -d --build` command again to rebuild.
- **New password:** after running `set-password`, restart the app container with `docker compose -f docker-compose.local.yml restart app`.
- **Fresh start:** to wipe the Docker database, run `docker compose -f docker-compose.local.yml down -v`. This deletes all its data.
- **Memory:** Docker Desktop uses a couple of GB of RAM while it's running. Quit it from the system tray when you're not using it.
