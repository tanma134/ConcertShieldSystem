# AdminAPI.Tests

xUnit tests for the 13 functions of AdminAPI, written from the user's own matrix
(`Report5_Unit_Test_Case__1_.xlsx`, sheets Function1_FraudDashboard ... Function13_ExportAuditLog): 205 test cases.
One method per UTCID, named `UTCIDxx_<Name>` (see `TestMapping.csv`: UTCID, sheet, class, method, Type N/A/B).

## Layout
- `Support/TestSupport.cs`: `TestDb` (EF InMemory, IPAddress stored as text, SaveChanges can be forced to fail) and `DbTestBase` (seed helpers, mocked `IRiskConfigProvider`).
- `Services/FraudAlertServiceTests.cs`   : Function1-4  (dashboard, detail, block, restore)
- `Services/AppealServiceTests.cs`       : Function5-7  (decision detail, submit appeal, resolve appeal)
- `Services/FraudCaseServiceTests.cs`    : Function8-10 (case list, assign, resolve)
- `Services/RiskPolicyServiceTests.cs`   : Function11   (policy parameters)
- `Services/AuditLogServiceTests.cs`     : Function12-13 (activity log list, export)

## Run
Put this folder next to `AdminAPI/` (the csproj references `..\AdminAPI\AdminAPI.csproj`), then:

    dotnet test AdminAPI.Tests

## Status
Written WITHOUT compiling or running (no .NET SDK was available). Expect a few small fixes on the first build
(constructor / property names read from the source, exact message or mask formats). Role rules (Admin / Staff / Customer)
are enforced by `[Authorize]` policies, not by these service methods, so they are not covered here.
Two export tests (50,000 and 50,001 rows) are slower than the others.
