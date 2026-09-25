# ⚖️ MuktoAin (মুক্ত আইন)

[![Live site](https://img.shields.io/badge/live%20site-muktoain--kr.azurewebsites.net-0078D4?logo=microsoftazure)](https://muktoain-kr.azurewebsites.net)
[![CI](https://github.com/hrittikaaa/MuktoAin-SD/actions/workflows/ci.yml/badge.svg)](https://github.com/hrittikaaa/MuktoAin-SD/actions/workflows/ci.yml)
[![Deploy](https://github.com/hrittikaaa/MuktoAin-SD/actions/workflows/deploy.yml/badge.svg)](https://github.com/hrittikaaa/MuktoAin-SD/actions/workflows/deploy.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![SQL Server 2022](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Data: CC BY-SA 4.0](https://img.shields.io/badge/data-CC%20BY--SA%204.0-lightgrey)](https://creativecommons.org/licenses/by-sa/4.0/)

**A legal-aid platform for Bangladesh.** Citizens describe a legal problem in
Bangla, English, or mixed Banglish. MuktoAin finds the relevant statutes,
explains their rights in plain language, and drafts structured legal documents
(GD applications, RTI requests, labour and consumer complaints). Every draft is
locked behind a **mandatory review by a verified lawyer** before a citizen can
use it.

**🌐 Live site: [muktoain-kr.azurewebsites.net](https://muktoain-kr.azurewebsites.net)**

[Deployment guide](docs/deployment-guide.md) ·
[Runbook](docs/runbook.md) ·
[Contributing](CONTRIBUTING.md) ·
[Report a bug](https://github.com/hrittikaaa/MuktoAin-SD/issues/new) ·
[Issues](https://github.com/hrittikaaa/MuktoAin-SD/issues)

---

## 📑 Table of Contents

1. [About the Project](#1-about-the-project)
2. [Features](#2-features)
3. [Technology Stack](#3-technology-stack)
4. [Architecture](#4-architecture)
5. [Getting Started](#5-getting-started)
6. [Running with Docker](#6-running-with-docker)
7. [Testing](#7-testing)
8. [Deployment](#8-deployment)
9. [Project Structure](#9-project-structure)
10. [Troubleshooting](#10-troubleshooting)
11. [Contributing](#11-contributing)
12. [Team](#12-team)
13. [Dataset Attribution](#13-dataset-attribution)
14. [Legal Disclaimer](#14-legal-disclaimer)

---

## 1. About the Project

Legal help in Bangladesh is expensive and hard to reach, and most statutes are
written in dense legal language. MuktoAin closes that gap in three steps:

1. **Understand**: a citizen describes their situation in any mix of Bangla
   and English. The platform retrieves the matching sections from 1,484
   Bangladesh Acts and explains them in plain language.
2. **Draft**: for common case types (Labour, General Diary, Right to
   Information, Consumer), it produces a structured draft document.
3. **Verify**: a verified lawyer reviews, edits and approves every draft
   before the citizen can download it. Nothing reaches a citizen unreviewed.

The whole interface is bilingual (বাংলা / English), and a legal disclaimer is
shown in the UI, attached to every generated answer, and stamped on every
finalized PDF.

## 2. Features

**Citizens**
- Conversational legal Q&A in Bangla, English or Banglish, with citations to
  the exact Act and section
- Keyword search across all Acts and sections
- Case creation, draft documents, submission to a lawyer, and live status
  tracking
- PDF download of lawyer-approved documents
- Chat credits and lawyer honorarium payments through bKash or card
  (SSLCommerz), with a built-in simulator for offline development
- Real-time notifications

**Lawyers**
- Verification by bar registration number before gaining access
- Review queue with a "my field" filter by specialization
- Claim, edit, approve or return drafts with comments
- Earnings, payout requests and withdrawal history

**Admins**
- Lawyer verification and user suspension
- Category and scenario-mapping management
- Payment, refund and payout approval
- Corpus and embedding progress, generation-quota monitoring, audit logs and
  analytics

**Platform**
- Field-level encryption of case data (ASP.NET Core Data Protection)
- Per-user rate limiting on chat and payment endpoints
- Security headers on every response
- Idempotent startup seeding, so a fresh database is usable on first run

## 3. Technology Stack

| Layer | Choice |
|---|---|
| Backend | [ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/mvc/overview) (.NET 8), C# |
| Data access | Parameterized SQL repositories + EF Core mapping onto a hand-written schema (no EF migrations) |
| Relational database | [Microsoft SQL Server 2022](https://www.microsoft.com/sql-server) with Full-Text Search |
| Vector database | [Qdrant](https://qdrant.tech/documentation/) via the .NET SDK |
| Embeddings and generation | [Google Gemini API](https://ai.google.dev/gemini-api/docs) (`gemini-embedding-001`, Gemini Flash), key rotation with [Polly](https://www.pollydocs.org/) |
| Frontend | Razor Views, [Bootstrap 5](https://getbootstrap.com/), vanilla JS, SignalR |
| Authentication | [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity) with Citizen, Lawyer and Admin roles |
| PDF | [QuestPDF](https://www.questpdf.com/) |
| Payments | [bKash](https://developer.bka.sh/) tokenized checkout, [SSLCommerz](https://developer.sslcommerz.com/) |
| Hosting | [Azure App Service](https://learn.microsoft.com/azure/app-service/), Docker |
| CI/CD | GitHub Actions |

## 4. Architecture

The solution follows Clean Architecture across four projects. Dependencies
point inward: `Web → Application → Domain`, with `Infrastructure` implementing
the interfaces that `Domain` and `Application` define.

```text
src/
 ├── MuktoAin.Domain/          # Entities, enums, interfaces, constants
 ├── MuktoAin.Application/     # DTOs, business services, retrieval and drafting orchestration
 ├── MuktoAin.Infrastructure/  # SQL repositories, Gemini and Qdrant clients, payments, PDF, encryption, seeding
 └── MuktoAin.Web/             # MVC controllers, views, view models, localization, SignalR hub
```

**Retrieval flow.** A question is embedded and matched against section chunks
in Qdrant (vector search is the primary path). SQL Server Full-Text Search is
used only as a fallback, when Qdrant is unreachable or for standalone keyword
search. The retrieved sections are assembled into a prompt, the answer is
generated, and the disclaimer is attached before it is shown.

## 5. Getting Started

### 5.1 Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0.400 or newer | pinned in [`global.json`](global.json) |
| [SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads) | 2022 (Express or Developer) | **must include Full-Text Search. LocalDB will not work.** |
| [SSMS](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms) | any recent | optional, for inspecting the database |
| [LibMan CLI](https://learn.microsoft.com/aspnet/core/client-side/libman/libman-cli) | latest | `dotnet tool install -g Microsoft.Web.LibraryManager.Cli` |
| [Qdrant](https://cloud.qdrant.io/) | Cloud free tier or local Docker | needed for semantic search |
| [Gemini API key](https://aistudio.google.com/apikey) | free tier | one or more keys |

Check that your SQL Server instance has Full-Text Search installed:

```sql
SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS IsFTSInstalled;
```

> [!IMPORTANT]
> This must return `1`. If it returns `0`, rerun the SQL Server installer, choose
> **Add features to an existing instance**, and tick **Full-Text and Semantic
> Extractions for Search**.

#### What each piece is for

The app starts with only SQL Server, but its search features depend on two
things that are easy to miss:

| Component | Powers | If it's missing |
|---|---|---|
| **SQL Server Full-Text Search** | The keyword search page, and the chat's fallback retrieval when Qdrant is unavailable | `03_fulltext.sql` fails, keyword search returns nothing, and a Qdrant outage leaves the chat with no statutes at all |
| **Embeddings in Qdrant** (Gemini + Qdrant + the Acts dataset) | The chat's primary retrieval: finding the statute sections that match a question by meaning, in Bangla, English or Banglish | The chat falls back to keyword matching, so answers get fewer and weaker citations |

Full-Text Search is a one-time install. Embeddings need a one-time indexing
run after setup, covered in [step 5.7](#57-index-the-acts-for-semantic-search).

### 5.2 Clone and restore

```bash
git clone https://github.com/hrittikaaa/MuktoAin-SD.git
cd MuktoAin-SD

dotnet restore src/MuktoAin.sln

cd src/MuktoAin.Web
libman restore
cd ../..
```

### 5.3 Configure

Copy the settings template. The copy is git-ignored so your secrets stay local.

```powershell
# Windows
copy src\MuktoAin.Web\appsettings.Development.json.template src\MuktoAin.Web\appsettings.Development.json
```

```bash
# macOS / Linux
cp src/MuktoAin.Web/appsettings.Development.json.template src/MuktoAin.Web/appsettings.Development.json
```

Then fill in:

| Setting | What to put there |
|---|---|
| `ConnectionStrings:DefaultConnection` | Works as-is for an instance named `SQLEXPRESS`; otherwise change `Server=` |
| `Gemini:ApiKeys` | Your Gemini API key(s) |
| `Qdrant:Endpoint`, `Qdrant:ApiKey` | Your Qdrant cluster URL and key |
| `Qdrant:Collection` | `act_section_chunks_<your-name>`, so developers don't overwrite each other |
| `SeedAdmin:Password` | A strong password for the first admin account |
| `Embedding:RunOnStartup` | Leave `false` for now; see [step 5.7](#57-index-the-acts-for-semantic-search) |
| `Payments:Mode` | `Simulator` (offline, default) or `Sandbox` (real bKash/SSLCommerz sandboxes) |

The full list of settings and their environment-variable names is in the
[deployment guide](docs/deployment-guide.md#3-configuration-reference).

### 5.4 Set up the database

The schema is managed with plain SQL scripts in [`scripts/`](scripts), not EF
Core migrations. Run them all in order:

```powershell
.\scripts\run-all.ps1
# custom instance:
.\scripts\run-all.ps1 -ServerInstance ".\YourInstanceName"
```

Every script is idempotent, so rerun `run-all.ps1` whenever you pull schema
changes.

### 5.5 Download the Acts dataset

The Bangladesh Acts dataset is too large for git. Follow
[`data/README.md`](data/README.md) to download it from Kaggle and verify its
SHA256. The app starts without it, but there will be no statutes to search or
cite, and nothing to index in step 5.7. You can skip it if you're only working
on UI, accounts or payments.

> [!NOTE]
> [`data/README.md`](data/README.md) also lists a **Legal QA dataset**. You do
> **not** need it to run the app. It is only used by the optional
> [answer-quality benchmark](#answer-quality-benchmark).

### 5.6 Run

```bash
dotnet run --project src/MuktoAin.Web
```

Open **http://localhost:5250** (check the console's `Now listening on:` line
for the actual URL).

On startup the app seeds, idempotently: districts, categories, scenario
mappings, the Acts and their section chunks (if the dataset is present), and
the super admin account. In the `Development` environment it also seeds demo
cases, documents and payments, plus the demo accounts below. The login page
has a **Quick Demo Fill** button for each one.

| Role | Email | Password | Can do |
|---|---|---|---|
| **Super Admin** | `admin@muktoain.bd` | the value of `SeedAdmin:Password` | everything an admin can, plus create, suspend and promote admins, refund payments, approve lawyer payouts |
| Admin | `demoadmin@muktoain.bd` | `DemoAdmin@123` | verify lawyers, manage users, categories and scenarios, view logs and analytics |
| Lawyer | `lawyer@muktoain.bd` | `Lawyer@123` | review queue, approve or return drafts, earnings and payouts (already verified) |
| Citizen | `citizen@muktoain.bd` | `Citizen@123` | chat, search, cases and documents, payments (has unpaid finalized cases ready to pay) |

> [!NOTE]
> The super admin's email and password come from the `SeedAdmin` settings.
> If `SeedAdmin:Password` is not set at all, it falls back to `Admin@123!`, and
> the app logs a warning. The super admin is created in every environment, so
> **always set a strong `SeedAdmin__Password` in production**. The other three
> accounts exist only in `Development`.

For sandbox payments (`Payments:Mode = Sandbox`), use the bKash test wallet
`01770618575`, OTP `123456`, PIN `12121`.

### 5.7 Index the Acts for semantic search

The chat needs the Act sections turned into embeddings and stored in Qdrant.
This is a one-time job per Qdrant collection.

**Before you start:** the Acts dataset is downloaded ([step 5.5](#55-download-the-acts-dataset)),
the app has run once so the Acts are imported and chunked, and
`Gemini:ApiKeys`, `Qdrant:Endpoint`, `Qdrant:ApiKey` and `Qdrant:Collection`
are set.

1. In `appsettings.Development.json`, set `Embedding:RunOnStartup` to `true`.
2. Run the app again: `dotnet run --project src/MuktoAin.Web`.
3. Follow progress on the admin dashboard (log in as the super admin), or in
   the console lines starting with `EmbeddingBatchJob:`.
4. When the dashboard shows **Completed (All chunks indexed)**, set
   `Embedding:RunOnStartup` back to `false`.

Things to know:

- **It takes a while on the free tier.** The job paces itself to Gemini's
  rate limits. If the daily quota runs out, it pauses and resumes by itself
  after the reset (midnight Pacific time), as long as the app keeps running.
- **It's resumable.** Stopping the app is safe. The next run with
  `RunOnStartup=true` skips chunks already indexed.
- **Use your own collection** (`act_section_chunks_<your-name>`). The shared
  `act_section_chunks` collection is for production only.
- Leaving `RunOnStartup` on is harmless but wasteful: every start rescans for
  unindexed chunks.

## 6. Running with Docker

[`docker-compose.yml`](docker-compose.yml) starts the app together with SQL
Server and Qdrant for a local end-to-end check:

```bash
# 1. Start the databases first
docker compose up -d sqlserver qdrant

# 2. Apply the schema to the containerized SQL Server (host port 1434)
pwsh ./scripts/run-all.ps1 -ServerInstance "localhost,1434" -User sa -Password 'YourStrong!Passw0rd'

# 3. Build and start the app, then check it responds
docker compose up -d --build
curl -f http://localhost:8080/Home
```

To run only the app image against your own database and services:

```bash
docker build -t muktoain-web .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="..." \
  -e Gemini__ApiKeys__0="..." \
  -e Qdrant__Endpoint="..." -e Qdrant__ApiKey="..." \
  muktoain-web
```

> [!WARNING]
> The compose file uses a throwaway `sa` password and is meant for local use
> only. Do not deploy it as-is.

## 7. Testing

```bash
dotnet test tests/MuktoAin.UnitTests          # fast, no database or network needed
dotnet test tests/MuktoAin.IntegrationTests   # needs SQL Server with the schema applied
```

| Suite | Covers | Needs |
|---|---|---|
| `MuktoAin.UnitTests` | services, controllers, view models, seeding, localization | nothing (in-memory) |
| `MuktoAin.IntegrationTests/Api`, `Repositories` | HTTP endpoints and SQL repositories | SQL Server |
| `MuktoAin.IntegrationTests/AiPipeline` | retrieval, plus the answer-quality benchmark (off unless enabled, see below) | SQL Server, Qdrant, Gemini keys |
| `MuktoAin.IntegrationTests/Browser` | end-to-end payment flows in a real browser | SQL Server, Playwright Chromium (skipped if missing) |

To enable the browser tests, build once and install Chromium:

```powershell
pwsh tests/MuktoAin.IntegrationTests/bin/Debug/net8.0/playwright.ps1 install chromium
```

In CI, unit tests run on every push and pull request. Integration tests are
opt-in (see the [deployment guide](docs/deployment-guide.md#6-continuous-integration)).

### Answer-quality benchmark

This measures how accurately the chat answers real legal questions. It sends
questions from the [Bangladesh Legal QA dataset](https://huggingface.co/datasets/momahadi/bangladesh-legal-qa-dataset)
through the full pipeline and scores the answers against the known correct
ones. It is only for evaluating answer quality; the app never uses this dataset.

1. Download the dataset (optional). Without it, the benchmark uses the
   5-question sample already in the repo
   (`data/benchmark/benchmark-sample.json`), which is enough to check that
   everything is wired up.
   ```bash
   curl -L -o data/bangladesh-legal-qa-dataset.json https://huggingface.co/datasets/momahadi/bangladesh-legal-qa-dataset/resolve/main/sft/finetune_dataset_2165.json
   ```
2. Make sure the app works locally, with the Acts imported and indexed
   (steps 5.4 to 5.7). The benchmark uses the same settings.
3. Run it:
   ```powershell
   $env:MUKTOAIN_RUN_QA_BENCHMARK = "1"
   $env:MUKTOAIN_BENCHMARK_MAX_QUESTIONS = "25"   # optional; leave unset for all 2,165
   dotnet test tests/MuktoAin.IntegrationTests --filter "FullyQualifiedName~QaBenchmark"
   ```

Reports are written to `data/benchmark/results/`: `zero-shot.json`,
`few-shot.json`, and a comparison of the two. A full run makes thousands of
Gemini calls, so start with a small `MAX_QUESTIONS` on the free tier.

## 8. Deployment

Every push to `main` runs [`deploy.yml`](.github/workflows/deploy.yml), which
runs the unit tests, publishes the app and deploys it to **Azure App Service**.
The workflow stays skipped until the `AZURE_WEBAPP_NAME` repository variable is
set.

- **[Deployment guide](docs/deployment-guide.md)**: Azure setup, secrets,
  configuration reference, and how to verify a deploy
- **[Runbook](docs/runbook.md)**: rollback, monitoring and troubleshooting in
  production

## 9. Project Structure

```text
MuktoAin-SD/
 ├── .github/workflows/   # ci.yml (build + test), deploy.yml (Azure)
 ├── data/                # seed JSON files; downloaded datasets go here (git-ignored)
 ├── docs/                # deployment guide, runbook
 ├── scripts/             # numbered SQL schema scripts + run-all.ps1
 ├── src/                 # the four application projects (see Architecture)
 ├── tests/
 │   ├── MuktoAin.UnitTests/
 │   └── MuktoAin.IntegrationTests/
 ├── Dockerfile           # multi-stage build, non-root runtime
 ├── docker-compose.yml   # local app + SQL Server + Qdrant stack
 ├── global.json          # pinned .NET SDK
 └── MuktoAin.slnx        # web-only solution (the full solution is src/MuktoAin.sln)
```

## 10. Troubleshooting

The most common problems, roughly in the order people hit them. Most are
visible in the console output of `dotnet run`, so read that first.

<details>
<summary><b>Can't connect to SQL Server (<code>A network-related or instance-specific error</code>)</b></summary>
<br>

1. Check the SQL Server service is running (Windows: <code>services.msc</code>
   → <em>SQL Server (SQLEXPRESS)</em>).
2. Check the instance name in <code>ConnectionStrings:DefaultConnection</code>
   matches yours. The default is <code>.\SQLEXPRESS</code>. Run
   <code>sqlcmd -L</code> to list local instances.
3. Check the database exists: run <code>.\scripts\run-all.ps1</code>.
</details>

<details>
<summary><b><code>Invalid object name</code> or <code>Invalid column name</code> errors</b></summary>
<br>
Your database schema is older than the code, usually right after pulling.
Rerun the scripts; they are idempotent and only add what is missing:

```powershell
.\scripts\run-all.ps1
```
</details>

<details>
<summary><b>The site loads without any styling</b></summary>
<br>
<code>wwwroot/lib/</code> is missing. Run:

```bash
cd src/MuktoAin.Web
libman restore
```
</details>

<details>
<summary><b>Keyword search returns nothing, or <code>03_fulltext.sql</code> fails</b></summary>
<br>

- If the Acts aren't loaded yet, the startup log shows
  <code>'bangladesh-acts-dataset.json' not found -- skipping</code>. Download the
  dataset following <a href="data/README.md">data/README.md</a> and restart.
- If the Acts are loaded, your SQL Server has no Full-Text Search. Run the check
  in <a href="#51-prerequisites">Prerequisites</a>. LocalDB never supports it.
</details>

<details>
<summary><b>Chat answers have no citations, or are poor</b></summary>
<br>
Semantic search isn't working, so the app is falling back to keyword search.

- **Startup log shows <code>Qdrant collection check failed</code>:** Qdrant is
  unreachable. Check <code>Qdrant:Endpoint</code> and <code>Qdrant:ApiKey</code>,
  and resume the cluster if Qdrant Cloud suspended it.
- **No warning, but still no citations:** your collection is empty or
  only partly indexed. Run the indexing job in
  <a href="#57-index-the-acts-for-semantic-search">step 5.7</a>.
- **Startup error <code>EmbeddingOutputDimensionality != Qdrant:VectorSize</code>:**
  make the two settings equal (3072 by default).
</details>

<details>
<summary><b>Chat fails, hangs, or logs <code>429</code> errors</b></summary>
<br>
Your Gemini keys are invalid or out of free-tier quota. The admin
dashboard shows the status of each key. Add more keys to
<code>Gemini:ApiKeys</code> or wait for the quota to reset. Keys from the same
Google Cloud project share one quota, so extra keys only help if they come from
different projects.
</details>

<details>
<summary><b>Build fails with MSB3027 / MSB3021 (file locked)</b></summary>
<br>
A previous <code>dotnet run</code> is still running. Stop it:

```powershell
Get-Process MuktoAin.Web -ErrorAction SilentlyContinue | Stop-Process -Force
```
</details>

Production issues (failed deploys, crashes, quota limits) are covered in the
[runbook](docs/runbook.md).

## 11. Contributing

`main` is protected, so all changes go through pull requests that pass CI. See
[CONTRIBUTING.md](CONTRIBUTING.md) for the branch workflow, coding conventions
and the pull request checklist.

## 12. Team

| Member | Role | Area |
|---|---|---|
| **Shads** | Project Lead | Identity, retrieval and drafting core, evaluation, delivery |
| **Hrittika** | Data Foundation | Schema, entities, repositories, search infrastructure, deployment |
| **Arpita** | Document Pipeline | Case and document services, lawyer review gate, admin |

## 13. Dataset Attribution

- **Bangladesh Legal Acts Dataset** by sakhadib, from
  [Kaggle](https://www.kaggle.com/datasets/sakhadib/bangladesh-legal-acts-dataset),
  licensed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
  1,484 Acts, used as the statute corpus.
- **Bangladesh Legal QA Dataset** by momahadi, from
  [Hugging Face](https://huggingface.co/datasets/momahadi/bangladesh-legal-qa-dataset),
  licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
  2,165 question-answer pairs, used for evaluation.

Download and verification steps are in [data/README.md](data/README.md).

## 14. Legal Disclaimer

> MuktoAin provides general legal information and document drafting assistance.
> This is **not formal legal advice**. Every document must be reviewed by a
> verified lawyer before use. For urgent legal matters, consult a qualified
> advocate.

> মুক্ত আইন সাধারণ আইনি তথ্য ও নথি প্রণয়নে সহায়তা প্রদান করে। এটি আনুষ্ঠানিক আইনি
> পরামর্শ নয়। প্রতিটি নথি ব্যবহারের পূর্বে একজন যাচাইকৃত আইনজীবী দ্বারা পর্যালোচনা
> করা আবশ্যক।
