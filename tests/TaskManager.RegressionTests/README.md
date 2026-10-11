# Regression checks

Run from the repository root with the .NET 10 SDK:

```powershell
dotnet run --project tests/TaskManager.RegressionTests/TaskManager.RegressionTests.csproj
```

The console runner exits with 0 when all groups pass and 1 on failure. It prints each group and a final assertion count. No production configuration, credentials or data are required.

Coverage includes controller ownership boundaries, authorization metadata, task editing and dates, assignment validation, template limits/content, due-soon quotas, threaded comment deletion, custom-field duplicates, subscription retries, browser dropdown refresh/reopen/failure/race cases and mobile editor field preservation/races. Billing checks cover checkout signatures, workspace isolation, trial activation, entitlement cache invalidation, missing callback recovery, stale reconciliation rejection, billing retry states and pricing callback submission.

Controller checks use EF Core InMemory. PostgreSQL queries are translated without opening a database connection; the relational billing atomic update path is not executed by this runner. Razorpay checks use mocked HTTP responses and test-only secrets, with no provider account access or charges. Mobile checks compile the actual viewmodel and MVVM generators with minimal navigation/attribute shims; they do not replace an Android build or UI test. Browser component checks invoke its event/load methods without rendering.
