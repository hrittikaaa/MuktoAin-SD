# MuktoAin Deployment Guide

This guide covers deploying MuktoAin to production on Azure App Service,
configuring it, and checking that a deploy worked. Production runs at
<https://muktoain-kr.azurewebsites.net>. For local development see
the [README](../README.md#5-getting-started). For incidents and rollback see
the [runbook](runbook.md).

## Contents

1. [Deployment topology](#1-deployment-topology)
2. [Prerequisites](#2-prerequisites)
3. [Configuration reference](#3-configuration-reference)
4. [First-time Azure setup](#4-first-time-azure-setup)
5. [Deploying](#5-deploying)
6. [Continuous integration](#6-continuous-integration)
7. [Verifying a deploy](#7-verifying-a-deploy)
8. [Running in Docker instead](#8-running-in-docker-instead)

---

## 1. Deployment topology

```text
GitHub (push to main)
   │
   ▼
GitHub Actions: deploy.yml ── unit tests ── dotnet publish ── bundle data/*.json
   │
   ▼
Azure App Service (Linux, .NET 8) ──► SQL Server 2022 (with Full-Text Search)
                                  ──► Qdrant Cloud (vector search)
                                  ──► Gemini API (embeddings + generation)
                                  ──► bKash / SSLCommerz (payments)
```

The app is a single ASP.NET Core process. It does not run database migrations;
the schema comes from the SQL scripts in [`scripts/`](../scripts), applied
before the first deploy and again whenever they change.

## 2. Prerequisites

- An Azure subscription (the free F1 App Service plan is enough for a demo).
- A SQL Server 2022 instance reachable from Azure, **with Full-Text Search**,
  hosting a database named `MuktoAin`.
- A [Qdrant Cloud](https://cloud.qdrant.io/) cluster and API key.
- One or more [Gemini API keys](https://aistudio.google.com/apikey).
- Admin access to the GitHub repository (to add secrets and variables).
- `sqlcmd` and PowerShell 7 (`pwsh`) on the machine that applies the schema.

> [!NOTE]
> The schema scripts use `USE MuktoAin;`, which works on SQL Server but not on
> Azure SQL Database. If you host the database on Azure SQL Database, connect
> directly to the database and run the scripts without the `USE` lines.

## 3. Configuration reference

ASP.NET Core reads settings from `appsettings.json`, then environment
variables. In an environment variable, `:` becomes `__` and array indexes
become `__0`, `__1`, and so on. On Azure, set these under **App Service →
Settings → Environment variables**.

| Setting (env var) | Required | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | yes | `Production`. Anything else seeds demo accounts. |
| `ConnectionStrings__DefaultConnection` | yes | SQL Server connection string |
| `SeedAdmin__Email` | yes | Email of the first admin account (created on first start) |
| `SeedAdmin__Password` | yes | Password for that account |
| `Gemini__ApiKeys__0` … `__N` | yes | Gemini API keys, rotated automatically |
| `Gemini__GenerationModel` | no | Generation model name (see the settings template for the current default) |
| `Gemini__EmbeddingModel` | no | Defaults to `gemini-embedding-001` |
| `Qdrant__Endpoint` | yes | Qdrant cluster URL |
| `Qdrant__ApiKey` | yes | Qdrant API key |
| `Qdrant__Collection` | yes | Use the canonical `act_section_chunks` collection in production |
| `Qdrant__VectorSize` | no | Defaults to `3072`; must match the embedding dimension |
| `DataProtection__KeysPath` | **yes on Azure** | `/home/data/keys`, see the warning below |
| `Embedding__RunOnStartup` | no | Leave `false` in production |
| `Payments__Mode` | no | `Simulator` (default) or `Sandbox` |
| `Bkash__Username`, `__Password`, `__AppKey`, `__AppSecret` | if `Sandbox` | bKash tokenized checkout credentials |
| `SslCommerz__StoreId`, `__StorePassword` | if `Sandbox` | SSLCommerz store credentials |

> [!WARNING]
> **Always set `DataProtection__KeysPath=/home/data/keys` on Azure.** Case
> titles and descriptions are encrypted with the Data Protection key ring. By
> default the keys live inside the app folder, which Azure replaces on every
> deploy. Without this setting, every deploy creates new keys and all existing
> encrypted case data becomes unreadable. `/home` is persistent storage on App
> Service, so keys kept there survive deploys.

Never commit real secrets. Locally they belong in the git-ignored
`appsettings.Development.json`; in production they belong in App Service
environment variables or GitHub secrets.

## 4. First-time Azure setup

### 4.1 Create the App Service

1. In the [Azure portal](https://portal.azure.com), go to **Create a resource →
   Web App**.
2. Set **Publish** to *Code*, **Runtime stack** to *.NET 8 (LTS)*, **Operating
   System** to *Linux*.
3. Pick a region close to your users and a pricing plan (*Free F1* works for a
   demo).
4. Create it and note the app name. The site will be at
   `https://<app-name>.azurewebsites.net`.

### 4.2 Configure the app

1. Open the app, go to **Settings → Environment variables**, and add every
   required setting from [section 3](#3-configuration-reference).
2. Under **Monitoring → App Service logs**, turn on **Application logging
   (Filesystem)** so startup output shows up in the Log stream.

### 4.3 Prepare the database

1. Apply the schema:
   ```powershell
   pwsh ./scripts/run-all.ps1 -ServerInstance "<server>,1433" -User <login> -Password '<password>'
   ```
2. Load the statute corpus. The large Acts dataset is not shipped with the app,
   so the production app cannot import it by itself. Run the app **once from
   your machine** with `ConnectionStrings:DefaultConnection` pointing at the
   production database, the dataset in `data/` (see
   [data/README.md](../data/README.md)), and `Embedding:RunOnStartup=true`.
   This imports the Acts, chunks the sections and indexes them into Qdrant.
   Set `Embedding:RunOnStartup` back to `false` when it finishes.

### 4.4 Connect GitHub Actions

1. In the portal, open the app's **Overview** and click **Download publish
   profile**. If the button is greyed out, first turn on **Settings →
   Configuration → General settings → SCM Basic Auth Publishing
   Credentials**.
2. In GitHub, go to **Settings → Environments** and create an environment
   named `production`.
3. In that environment (or at repository level), add:
   - **Secret** `AZURE_WEBAPP_PUBLISH_PROFILE`: the full contents of the
     downloaded publish profile file
   - **Variable** `AZURE_WEBAPP_NAME`: the app name from step 4.1

Until `AZURE_WEBAPP_NAME` is set, the deploy workflow is skipped.

## 5. Deploying

Deploys happen automatically on every push to `main`, which in practice means
every merged pull request. [`deploy.yml`](../.github/workflows/deploy.yml)
then:

1. restores LibMan front-end libraries,
2. runs the unit tests (a failure stops the deploy),
3. publishes the web project in Release mode,
4. copies the small seed files from `data/*.json` into the package,
5. deploys the package to App Service.

To redeploy without a new commit, open **Actions → Deploy → Run workflow**.
Only one deploy runs at a time; a newer push cancels one in progress.

**When a pull request changes the schema**, apply the new `scripts/*.sql` files
to the production database *before* merging, so the new code never runs
against the old schema. The scripts are idempotent and safe to rerun.

## 6. Continuous integration

[`ci.yml`](../.github/workflows/ci.yml) runs on every push and pull request to
`main`:

| Job | Runs | Needs |
|---|---|---|
| `build` | always | nothing |
| `unit-tests` | always | nothing |
| `docker-build` | always | builds the image and checks the published output |
| `integration-tests` | opt-in | see below |

To turn on integration tests, set the repository variable
`ENABLE_INTEGRATION_TESTS=true` and add these secrets: `QDRANT_ENDPOINT`,
`QDRANT_API_KEY`, `GEMINI_API_KEY_1`. The job starts its own SQL Server
container. That image has no Full-Text Search, so tests tagged
`RequiresFts` are skipped there.

## 7. Verifying a deploy

After each deploy:

1. **GitHub:** the Deploy run under the **Actions** tab is green.
2. **Azure:** **Deployment Center → Logs** shows the new deployment as
   successful.
3. **Startup:** **Monitoring → Log stream** shows the seeding messages and
   `Application started`, with no exceptions. A `Qdrant collection check
   failed` warning means vector search is down; see the
   [runbook](runbook.md#qdrant-unreachable).
4. **Smoke test:** open `https://<app-name>.azurewebsites.net` and check that:
   - the home page loads, with styling
   - a keyword search returns Acts
   - you can log in as the seed admin, and the admin dashboard loads
   - one question in the chat returns an answer with citations

If any check fails, follow the [runbook](runbook.md).

## 8. Running in Docker instead

The repository ships a multi-stage [`Dockerfile`](../Dockerfile) that runs as a
non-root user on port 8080. Any container host works (Azure Container Apps, a
VM, and so on):

```bash
docker build -t muktoain-web .
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="..." \
  -e SeedAdmin__Email="..." -e SeedAdmin__Password="..." \
  -e Gemini__ApiKeys__0="..." \
  -e Qdrant__Endpoint="..." -e Qdrant__ApiKey="..." -e Qdrant__Collection="act_section_chunks" \
  -e DataProtection__KeysPath=/keys \
  -v muktoain-keys:/keys \
  muktoain-web
```

Mount a volume for the key ring, as shown, for the same reason as the Azure
warning in [section 3](#3-configuration-reference). The container runs as
uid `1654`, so the volume must be writable by that user.
