## What does this change?

<!-- One or two sentences. Link the issue if there is one: Closes #123 -->

## How was it tested?

<!-- Commands run, screens checked, test cases added. -->

## Checklist

- [ ] `dotnet build` and `dotnet test tests/MuktoAin.UnitTests` pass locally
- [ ] New behavior has tests
- [ ] No secrets, keys or real user data in the diff
- [ ] User-facing text has both Bangla and English
- [ ] **Schema:** any new `scripts/NN_*.sql` is idempotent, and it will be applied to production before merge (or there are no schema changes)
- [ ] **Docs:** README, [deployment guide](https://github.com/hrittikaaa/MuktoAin-SD/blob/main/docs/deployment-guide.md) or [runbook](https://github.com/hrittikaaa/MuktoAin-SD/blob/main/docs/runbook.md) updated if setup, configuration, deployment or failure handling changed (or not needed)
