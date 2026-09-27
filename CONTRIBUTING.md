# Contributing to MuktoAin

Thanks for helping out. This page explains how changes get into `main`.

## Getting set up

Follow [Getting Started](README.md#5-getting-started) in the README to get a
working local environment.

## Workflow

`main` is protected: nobody pushes to it directly, and every change goes
through a pull request.

1. Update your local `main`:
   ```bash
   git checkout main
   git pull
   ```
2. Create a branch with a descriptive name, prefixed by the kind of change:
   `feat/…`, `fix/…`, `docs/…`, `refactor/…`, `test/…`.
   ```bash
   git checkout -b feat/lawyer-queue-filter
   ```
3. Make your change, with tests (see below).
4. Before pushing, check locally:
   ```bash
   dotnet build src/MuktoAin.sln
   dotnet test tests/MuktoAin.UnitTests
   ```
5. Push and open a pull request against `main`. Fill in the template.
6. CI must pass and a teammate must approve before merging. Merging to `main`
   deploys to production automatically.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```text
feat: add specialization filter to lawyer queue
fix: keep chat credits when a payment is cancelled
docs: document DataProtection key path
```

## Code conventions

- **Architecture:** keep the Clean Architecture boundaries. `Domain` depends on
  nothing, `Application` depends only on `Domain`, and `Infrastructure` and
  `Web` implement and consume their interfaces. No business logic in
  controllers.
- **Database:** schema changes go in a **new** numbered script in
  [`scripts/`](scripts) (for example `21_add_x.sql`). Never edit a script that
  has already been merged. Scripts must be idempotent (`IF NOT EXISTS …`). There
  are no EF Core migrations.
- **SQL:** always parameterize queries; never build SQL from strings.
- **Secrets:** never commit keys or passwords. Local secrets belong in the
  git-ignored `appsettings.Development.json`.
- **UI text:** every user-facing string needs both Bangla and English.
- **Tests:** new behavior needs unit tests. Anything touching SQL or HTTP
  endpoints needs integration tests too.

## Documentation

Update the docs in the same pull request whenever you change:

| If you change… | Update |
|---|---|
| setup steps, prerequisites, tools | [README](README.md) |
| a setting, environment variable or secret | [deployment guide](docs/deployment-guide.md#3-configuration-reference) |
| `deploy.yml`, `ci.yml`, `Dockerfile` or hosting | [deployment guide](docs/deployment-guide.md) |
| a way production can fail, or how to recover | [runbook](docs/runbook.md) |
| the schema | add the new script; mention it in the PR so it is applied before merge |

## Reporting bugs

Open an [issue](https://github.com/hrittikaaa/MuktoAin-SD/issues/new) with the
steps to reproduce, what you expected, what happened, and any log output.
Never paste real user data, keys or passwords into an issue.
