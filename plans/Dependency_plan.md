# MuktoAin — Teammate Dependency & Execution Flow

> **Progress Tracking:** To update progress, edit this file and change `[ ]` to `[x]` on completed tasks, and wrap the completed line in `~~strikethrough~~` (e.g. `- [x] ~~**[H-1.1]** ...~~`).
> **Initials & Step Prefix Key:**
> - **`[S-Setup.#]`**, **`[S-#.#]`** & **`[S-F.#]`** = **Shads** (Project Lead · `Shads_plan.md`) — the
>   `-F.#` suffix marks a frontend view task, wired directly to the real backend service since
>   Shads owns both ends of that feature.
> - **`[H-Setup.#]`**, **`[H-#.#]`** & **`[H-F.#]`** = **Hrittika** (Data Foundation & Search ·
>   `Hrittika_plan.md`) — the `-Setup.#` suffix marks the initial repo-administration steps (repo
>   creation, branch protection), hers rather than Shads's since the repo lives under her GitHub
>   account.
> - **`[A-#.#]`** & **`[A-F.#]`** = **Arpita** (Document Generation, Review Gate & Admin · `Arpita_plan.md`)
>
> Frontend view tasks (`*-F.#`) are each owned by whichever of the 3 teammates above owns that
> feature's backend, and each sits immediately after the backend task it wires to — not batched
> into a separate frontend phase at the end of a checkpoint.

**Frontend ownership map** (who builds + wires which view):

| Task | View | Wires to | Owner |
|---|---|---|---|
| S-F.1 | Master Layout | — (Day 0, foundational) | Shads |
| S-F.2 | Home page | S-F.1 | Shads |
| S-F.3 | Login/Register views | S-1.1 Identity | Shads |
| S-F.5 | Case Result / Rights Explanation view | S-2.3 RightsExplanationService | Shads |
| S-F.6 | `.resx` localization files | S-3.5 Localization Middleware (own task) | Shads |
| S-F.7 | Admin Users view | S-3.6 UserManagementService | Shads |
| H-F.1 | Disclaimer Banner + Language Toggle | — (Day 0, foundational) | Hrittika |
| H-F.2 | Acts Search view | H-2.4 SearchService | Hrittika |
| H-F.3 | Category views | H-2.5 CategoryService | Hrittika |
| H-F.4 | Admin Acts/Scenario Mapping views | H-3.1 / H-3.2 | Hrittika |
| A-F.1 | Static Assets (CSS/JS/fonts) | — (Day 0, foundational; also feeds A-2.5) | Arpita |
| A-F.2 | Case Submit view | A-2.1 CaseService | Arpita |
| A-F.3 | Case Track view | A-2.1 CaseService | Arpita |
| A-F.4 | Document Preview view | A-2.4 DocumentService | Arpita |
| A-F.5 | Lawyer Queue/Review views | A-2.7 LawyerReviewService | Arpita |
| A-F.6 | Admin Dashboard/Analytics view | A-3.2 AdminAnalyticsService | Arpita |

---

## 🏁 Checkpoint 1: Foundation, Schema & Data Ingestion (30%)

### 1. Day 0 Setup & Independent Start (No Cross-Teammate Blockers)
- [x] ~~**[H-Setup.1]** Create GitHub Repository — *Hrittika* `[Unblocks: H-1.1, H-Setup.2]`~~
- [x] ~~**[H-Setup.2]** Set Up Branch Protection on `main` — *Hrittika*~~
- [ ] **[S-Setup.3]** Prepare Seed Data Files (`districts.json`, `categories.json`, etc.) — *Shads* `[Unblocks: H-1.7]`
- [ ] **[S-Setup.4]** Set Up Multi-Key Gemini API Key Rotation Config — *Shads* `[Unblocks: S-1.3]`
- [ ] **[S-Setup.5]** Set Up Qdrant Cloud Cluster — *Shads* `[Unblocks: H-1.11]`
- [ ] **[S-1.3]** Implement `GeminiClient.cs` with Key Rotation — *Shads* `[Unblocks: S-1.4]`
- [ ] **[S-1.5]** Define Legal Prompt Templates & Disclaimers (`Disclaimers.cs`) — *Shads* `[Unblocks: S-1.6, S-2.1]`
- [ ] **[S-1.6]** Implement `DisclaimerInjector.cs` — *Shads* `[Blocked by: S-1.5] [Unblocks: S-2.2]`
- [ ] **[S-1.7]** Implement `EncryptionService.cs` (ASP.NET Data Protection API) — *Shads* `[Unblocks: S-2.5]`
- [ ] **[S-F.1]** Master Layout `_Layout.cshtml` (Bootstrap 5, Nav, Footer) — *Shads* `[Unblocks: S-F.2, everyone's views]`
- [ ] **[S-F.2]** Home Controller & Views (Landing page with mock data) — *Shads* `[Blocked by: S-F.1]`
- [x] ~~**[H-F.1]** `_DisclaimerBanner.cshtml` & `_LanguageToggle.cshtml` — *Hrittika* `[Unblocks: S-F.1]`~~
- [ ] **[A-F.1]** Static Assets (CSS, JS & Noto Sans Bengali Fonts in `wwwroot/`) — *Arpita* `[Unblocks: A-2.5]`

### 2. Core Architecture Critical Path (Hrittika's First Wave)
- [x] ~~**[H-1.1]** Initialize .NET 8 Solution (`MuktoAin.sln` 4 Projects + References) — *Hrittika* `[Blocked by: H-Setup.1] [Unblocks: H-1.2]`~~
- [x] ~~**[H-1.2]** Implement All 9 Enums in `MuktoAin.Domain/Enums/` — *Hrittika* `[Blocked by: H-1.1] [Unblocks: H-1.3, A-1.1]`~~
- [x] ~~**[H-1.3]** Implement All 14 Domain Entities in `MuktoAin.Domain/Entities/` — *Hrittika* `[Blocked by: H-1.2] [Unblocks: H-1.4, A-1.1]`~~
- [x] ~~**[H-1.4]** Define Repository & Service Interfaces in Domain/Application — *Hrittika* `[Blocked by: H-1.3] [Unblocks: H-1.5, A-2.1]`~~
- [x] ~~**[H-1.5]** EF Core `AppDbContext.cs` in `MuktoAin.Infrastructure` — *Hrittika* `[Blocked by: H-1.3, H-1.4] [Unblocks: H-1.6, S-1.1]`~~
- [x] ~~**[H-1.6]** Manual MSSQL Schema Scripts in SSMS (`scripts/01-14_*.sql`) — *Hrittika* `[Blocked by: H-1.3, H-1.5] [Unblocks: S-1.1, H-1.7]`~~
- [ ] **[A-1.1]** All DTOs in `MuktoAin.Application/DTOs/` — *Arpita* `[Blocked by: H-1.2, H-1.3] [Unblocks: A-2.1]`
- [ ] **[A-1.2]** Checkpoint 1 DTOs Exit Gate — *Arpita* `[Blocked by: A-1.1]`

### 3. Identity, Data Ingestion & Vector Indexing
- [ ] **[S-1.1]** ASP.NET Core Identity Configuration in `MuktoAin.Infrastructure` — *Shads* `[Blocked by: H-1.5, H-1.6] [Unblocks: S-1.2, S-3.6]`
- [ ] **[S-1.2]** `SeedAdminUser.cs` Startup Seeding — *Shads* `[Blocked by: S-1.1]`
- [ ] **[S-F.3]** Identity Views (`Login.cshtml`, `Register.cshtml`), wired to real Identity — *Shads* `[Blocked by: S-F.1, S-1.1]`
- [ ] **[S-F.4]** Checkpoint 1 Frontend Exit Gate — *Shads* `[Blocked by: S-F.1, S-F.2, H-F.1, A-F.1, S-F.3]`
- [x] ~~**[H-1.7]** Seed Data Loaders (`SeedDistricts`, `SeedCategories`, `SeedScenarioMappings`) — *Hrittika* `[Blocked by: S-Setup.3, H-1.6] [Unblocks: H-1.8]`~~
- [x] ~~**[H-1.8]** 1,484 Bangladesh Acts Ingestion Pipeline (`ActImportService.cs`) — *Hrittika* `[Blocked by: H-1.6, H-1.7] [Unblocks: H-1.9]`~~
- [ ] **[H-1.9]** Legal Section Chunking Pipeline (`LegalChunkingService.cs`) — *Hrittika* `[Blocked by: H-1.8] [Unblocks: S-1.8]`
- [x] ~~**[H-1.10]** Manual SQL Server Full-Text Search (FTS) SSMS Script — *Hrittika* `[Blocked by: H-1.6] [Unblocks: H-2.2]`~~
- [ ] **[H-1.11]** `QdrantVectorStore.cs` (.NET SDK Vector Implementation) — *Hrittika* `[Blocked by: S-Setup.5, H-1.4] [Unblocks: S-1.8]`
- [x] ~~**[H-1.12]** MSSQL Repository Implementations (Parameterized SQL) — *Hrittika* `[Blocked by: H-1.4, H-1.6] [Unblocks: H-1.13, A-2.1]`~~
- [ ] **[H-1.13]** DI Registration in `Program.cs` — *Hrittika* `[Blocked by: H-1.11, H-1.12] [Unblocks: S-1.9]`
- [ ] **[H-1.14]** Data Layer Unit Tests (`MuktoAin.UnitTests`) — *Hrittika* `[Blocked by: H-1.12]`
- [ ] **[S-1.8]** `EmbeddingBatchJob.cs` (Embed & Index All Chunks into Qdrant) — *Shads* `[Blocked by: H-1.9, H-1.11, S-1.4] [Unblocks: S-1.9, H-2.1]`
- [ ] **[H-1.15]** Checkpoint 1 Data Foundation Exit Gate — *Hrittika* `[Blocked by: H-1.1 to H-1.14]`
- [ ] **[S-1.9]** Checkpoint 1 Overall RAG Ingestion Smoke Test Exit Gate — *Shads* `[Blocked by: S-1.8, H-1.13, H-2.1]`

---

## 🚀 Checkpoint 2: Core Citizen Flow, RAG & Document Generation (45%)

### 1. Retrieval & RAG Context Assembly
- [ ] **[H-2.1]** `SimilaritySearchService.cs` (Qdrant Vector Retrieval) — *Hrittika* `[Blocked by: S-1.4, S-1.8, H-1.11] [Unblocks: H-2.3]`
- [ ] **[H-2.2]** `KeywordSearchService.cs` (SQL FTS Fallback) — *Hrittika* `[Blocked by: H-1.10] [Unblocks: H-2.3]`
- [ ] **[H-2.3]** `RagContextBuilder.cs` (Vector-Primary with FTS Fallback) — *Hrittika* `[Blocked by: H-2.1, H-2.2] [Unblocks: S-2.1]`
- [ ] **[H-2.4]** `SearchService.cs` (Standalone Keyword Search for FR-7) — *Hrittika* `[Blocked by: H-2.2]`
- [ ] **[H-F.2]** Acts Keyword Search View (`/Search`), wired to `SearchService` — *Hrittika* `[Blocked by: H-2.4, S-F.1]`
- [ ] **[H-2.5]** `CategoryService.cs` (Category Hierarchy for FR-6) — *Hrittika* `[Blocked by: H-1.12]`
- [ ] **[H-F.3]** Act Category Browsing & Detail Views (`/Category`), wired to `CategoryService` — *Hrittika* `[Blocked by: H-2.5, S-F.1]`
- [ ] **[H-2.6]** Checkpoint 2 Search Infrastructure Exit Gate — *Hrittika* `[Blocked by: H-2.1 to H-2.5, H-F.2, H-F.3]`

### 2. AI Orchestration, Prompt Assembly & Logging
- [ ] **[S-2.1]** `PromptAssembler.cs` (Context Assembly & Grounding) — *Shads* `[Blocked by: H-2.3, S-1.5] [Unblocks: S-2.2]`
- [ ] **[S-2.2]** `AiOrchestrationService.cs` (Gemini Flash Pipeline) — *Shads* `[Blocked by: S-2.1, S-1.3, S-1.6, S-2.6] [Unblocks: S-2.3, S-2.4, A-2.2]`
- [ ] **[S-2.3]** `RightsExplanationService.cs` (Explain My Rights) — *Shads* `[Blocked by: S-2.2] [Unblocks: A-2.2]`
- [ ] **[S-F.5]** Legal Analysis & Rights Explanation View (`/Case/Result`), wired to `RightsExplanationService` — *Shads* `[Blocked by: S-2.3, S-F.1, A-2.1]`
- [ ] **[S-2.4]** `AiLogService.cs` (Audit Logging & Token Tracking) — *Shads* `[Blocked by: H-1.4, S-2.2] [Unblocks: S-2.7]`
- [ ] **[S-2.6]** Polly Resilience Policies (Retry, Key Rotation & Fallback) — *Shads* `[Blocked by: S-1.3] [Unblocks: S-2.2]`
- [ ] **[S-2.7]** AI Logging PII Redaction & Audit Safety — *Shads* `[Blocked by: S-2.4]`
- [ ] **[S-F.6]** `.resx` Localization Resource Files — *Shads* `[Unblocks: S-3.5]`

### 3. Case Lifecycle, Document Generation & Lawyer Review Gate
- [ ] **[A-2.1]** `CaseService.cs` (Intake, State Machine, Tracking Code) — *Arpita* `[Blocked by: H-1.4, H-1.12, A-1.1] [Unblocks: S-2.5, A-2.4]`
- [ ] **[A-F.2]** Citizen Case Intake View (`/Case/Submit`), wired to `CaseService` — *Arpita* `[Blocked by: A-2.1, S-F.1]`
- [ ] **[A-F.3]** Anonymous Case Tracking View (`/Case/Track`), wired to `CaseService` — *Arpita* `[Blocked by: A-2.1, S-F.1]`
- [ ] **[S-2.5]** Wire `EncryptionService` into `CaseService` for PII — *Shads* `[Blocked by: S-1.7, A-2.1]`
- [ ] **[A-2.2]** `DocumentGenerator.cs` (Core Document Generation Engine) — *Arpita* `[Blocked by: S-2.2, S-2.3] [Unblocks: A-2.3]`
- [ ] **[A-2.3]** `LabourComplaintTemplate.cs` (First Structured Template) — *Arpita* `[Blocked by: A-2.2] [Unblocks: A-2.4]`
- [ ] **[A-2.4]** `DocumentService.cs` (Document CRUD, Lifecycle & Lockout) — *Arpita* `[Blocked by: A-2.3, A-2.1] [Unblocks: A-2.5, A-2.7]`
- [ ] **[A-F.4]** Generated Document Preview View (`/Document/Preview`), wired to `DocumentService` — *Arpita* `[Blocked by: A-2.4, S-F.1]`
- [ ] **[A-2.5]** `PdfExportService.cs` with QuestPDF (Bengali Font) — *Arpita* `[Blocked by: A-F.1, A-2.4]`
- [ ] **[A-2.6]** `LawyerVerificationService.cs` (Bar Council Verification) — *Arpita* `[Blocked by: H-1.4, H-1.12] [Unblocks: A-2.7]`
- [ ] **[A-2.7]** `LawyerReviewService.cs` (Queue, Claim Race Guard, Decisions) — *Arpita* `[Blocked by: A-2.6, A-2.4]`
- [ ] **[A-F.5]** Lawyer Verification & Review Views (`/Lawyer/Queue`, `/Lawyer/Review`), wired to `LawyerVerificationService` + `LawyerReviewService` — *Arpita* `[Blocked by: A-2.6, A-2.7, S-F.1]`
- [ ] **[A-2.8]** Checkpoint 2 Document & Review Exit Gate — *Arpita* `[Blocked by: A-2.1 to A-2.7, A-F.2, A-F.3, A-F.4, A-F.5]`

### 4. Checkpoint 2 Close-Out
- [ ] **[S-2.8]** Checkpoint 2 Overall Citizen Flow & RAG Integration Exit Gate — *Shads* `[Blocked by: S-2.1 to S-2.7, H-2.6, A-2.8, S-F.5, S-F.6]`

---

## 🏛️ Checkpoint 3: Review Gate, Admin, Evaluation & Delivery (25%)

### 1. Admin Services + Views (each wired immediately, no separate frontend phase)
- [ ] **[A-3.1]** Complete All 4 Document Templates (GD, RTI, Consumer) — *Arpita* `[Blocked by: A-2.2]`
- [ ] **[A-3.2]** `AdminAnalyticsService.cs` (KPIs, Funnels, Workloads) — *Arpita* `[Blocked by: H-1.12]`
- [ ] **[A-F.6]** Admin Dashboard & Analytics Views (`/Admin/Analytics`), wired to `AdminAnalyticsService` — *Arpita* `[Blocked by: A-3.2, S-F.1]`
- [ ] **[H-3.1]** `ActsManagementService.cs` (Admin CRUD & SHA256 Re-indexing) — *Hrittika* `[Blocked by: H-1.8, S-1.8]`
- [ ] **[H-3.2]** `ScenarioMappingService.cs` (Admin Keyword Boosts for FR-18) — *Hrittika* `[Blocked by: H-1.12]`
- [ ] **[H-F.4]** Admin Acts Management & Scenario Views (`/Admin/Acts`), wired to `ActsManagementService` + `ScenarioMappingService` — *Hrittika* `[Blocked by: H-3.1, H-3.2, S-F.1]`
- [ ] **[S-3.6]** `UserManagementService.cs` (Admin Role Management) — *Shads* `[Blocked by: S-1.1]`
- [ ] **[S-F.7]** Admin User Management Views (`/Admin/Users`), wired to `UserManagementService` — *Shads* `[Blocked by: S-3.6, S-F.1]`

### 2. QA Benchmark, Testing & Hardening
- [ ] **[S-3.1]** QA Benchmark Dataset Loader (2,165 Questions) — *Shads* `[Blocked by: H-2.3, A-2.2] [Unblocks: S-3.2]`
- [ ] **[S-3.2]** Benchmark Runner (Zero-Shot Baseline Evaluation) — *Shads* `[Blocked by: S-3.1] [Unblocks: S-3.3]`
- [ ] **[S-3.3]** Few-Shot IRAC Prompt Assembly & Re-Evaluation — *Shads* `[Blocked by: S-3.2]`
- [ ] **[A-3.3]** Business Logic Unit Tests & Security Edge-Case Tests — *Arpita* `[Blocked by: A-2.1 to A-2.7]`
- [ ] **[A-3.5]** `ModerationService.cs` (Submission Blocklist Filter) — *Arpita* `[Blocked by: H-1.12]`
- [ ] **[A-3.6]** `docs/attribution-CC-BY-SA-4.0.md` (Acts + QA Dataset Licenses) — *Arpita* — *(no blockers)*
- [ ] **[H-3.3]** Repository & DB Integration Tests — *Hrittika* `[Blocked by: H-1.12]`
- [ ] **[H-3.5]** `docs/architecture.md` (Clean Arch, ERD, Retrieval Pipeline) — *Hrittika* — *(no blockers, update as architecture evolves)*
- [ ] **[A-3.4]** Checkpoint 3 Arpita Exit Gate — *Arpita* `[Blocked by: A-3.1 to A-3.3, A-3.5, A-3.6, A-F.6]`
- [ ] **[H-3.4]** Checkpoint 3 Hrittika Exit Gate — *Hrittika* `[Blocked by: H-3.1 to H-3.3, H-3.5, H-F.4]`

### 3. Packaging, Localization, Final Cross-Checks & Release
- [ ] **[S-3.4]** Multi-Stage Dockerfile & GitHub Actions CI/CD Pipeline — *Shads*
- [ ] **[S-3.5]** `RequestLocalizationMiddleware.cs` & Resource Files Wiring — *Shads* `[Blocked by: S-F.6]`
- [ ] **[S-3.8]** `docs/deployment-guide.md` (Prereqs, Local Setup, Docker, Azure, Secrets) — *Shads* `[Blocked by: S-3.4]`
- [ ] **[S-3.9]** Root `README.md` (Mission, Stack, Quick Start, Attribution, Disclaimer) — *Shads* `[Blocked by: S-3.4, S-3.8]`
- [ ] **[S-F.8]** Self-check: verify own-owned views against real (non-mock) data end-to-end — *Shads*
- [ ] **[H-F.5]** Self-check: verify own-owned views against real (non-mock) data end-to-end — *Hrittika*
- [ ] **[A-F.7]** Self-check: verify own-owned views against real (non-mock) data end-to-end — *Arpita*
- [ ] **[S-3.7]** Checkpoint 3 Final Release & Academic Delivery Gate (includes joint frontend sign-off) — *Shads* `[Blocked by: All Checkpoint 3 Tasks, S-F.8, H-F.5, A-F.7]`

---

## ⚡ Direct Handoff Summary Table

| Handoff # | Blocked Task | Assigned | Blocked By / Waiting On | Delivered By |
|---|---|---|---|---|
| **HO-1** | DTOs `[A-1.1]` | **Arpita** | Enums & Entities `[H-1.2, H-1.3]` | **Hrittika** |
| **HO-2** | Identity `[S-1.1]` | **Shads** | `User.cs`, `AppDbContext.cs`, SSMS Schema `[H-1.3, H-1.5, H-1.6]` | **Hrittika** |
| **HO-3** | Embedding Batch `[S-1.8]` | **Shads** | Chunk pipeline & `QdrantVectorStore` `[H-1.9, H-1.11]` | **Hrittika** |
| **HO-4** | `SimilaritySearchService` `[H-2.1]` | **Hrittika** | `GeminiEmbeddingService` & Qdrant chunks `[S-1.4, S-1.8]` | **Shads** |
| **HO-5** | `PromptAssembler` / AI `[S-2.1, S-2.2]` | **Shads** | `RagContextBuilder` `[H-2.3]` | **Hrittika** |
| **HO-6** | `DocumentGenerator` `[A-2.2]` | **Arpita** | `AiOrchestrationService` & Rights DTO `[S-2.2, S-2.3]` | **Shads** |
| **HO-7** | QA Benchmark Runner `[S-3.1]` | **Shads** | Full RAG pipeline + Document Generator `[H-2.3, A-2.2]` | **Hrittika & Arpita** |

> `H-1.1` (solution scaffold) no longer appears here — it's blocked by `H-Setup.1`, Hrittika's
> own task, not a cross-team handoff from Shads.
