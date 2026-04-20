# AI Implementation Changelog

This file records implementation history produced by AI-assisted development sessions.
Each entry documents what was built, decisions made, and version adjustments relative to the plan.

---

## 2026-04-20 — Phase 0: Foundations scaffolded

**Phase goal:** Buildable empty monorepo with CI, Central Package Management, analyzers, and Shared Kernel skeleton.

### Files created

**Root configuration**
- `Directory.Build.props` — global compiler flags: Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel, EnforceCodeStyleInBuild, LangVersion=latest, ManagePackageVersionsCentrally, MinVer tag prefix.
- `Directory.Packages.props` — Central Package Management with all Phase 0–9 packages pinned (see version notes below).
- `global.json` — SDK pinned to 10.0.103 with rollForward=latestFeature.
- `.editorconfig` — file-scoped namespaces, 4-space indent, LF endings, naming conventions, var preferences aligned with backend.md.
- `nuget.config` — nuget.org source; internal feed entry commented pending CI secrets configuration.
- `stylecop.json` — StyleCop configuration: XML docs required on public members only, _camelCase fields allowed, no file headers.
- `Chassis.sln` — solution containing SharedKernel and smoke test projects.

**SharedKernel (`src/Chassis.SharedKernel/`)**
- `Chassis.SharedKernel.csproj` — multi-target netstandard2.0;net10.0; IsPackable=true; MinVer + analyzer references (PrivateAssets=All); FrameworkReference for Microsoft.AspNetCore.App on net10.0 only.
- `Abstractions/IModuleStartup.cs` — ConfigureServices (both targets) + Configure(IEndpointRouteBuilder) guarded with `#if NET10_0_OR_GREATER`.
- `Abstractions/IModuleDispatcher.cs` — SendAsync<TRequest,TResponse> + PublishAsync<TEvent>.
- `Abstractions/ICommand.cs` — non-generic + generic<TResponse> variants.
- `Abstractions/IQuery.cs` — generic<TResponse> variant.
- `Abstractions/IDomainEvent.cs` — marker interface.
- `Abstractions/IIntegrationEvent.cs` — marker interface.
- `Abstractions/Result.cs` — non-generic Result with Success/Failure factories; class-based (see design note).
- `Abstractions/ResultT.cs` — generic Result<T> with implicit operator; class-based.
- `Tenancy/ITenantContext.cs` — TenantId, UserId, CorrelationId, Roles.
- `Tenancy/ITenantContextAccessor.cs` — Current getter/setter.
- `Contracts/CorrelationHeaders.cs` — string constants: tenant-id, user-id, correlation-id, traceparent.
- `Contracts/CloudEventEnvelope.cs` — CloudEvents 1.0 envelope: Id, Source, Type, Time, SpecVersion, DataContentType, Subject, Data.

**CI workflows (`.github/workflows/`)**
- `build.yml` — push+PR trigger; ubuntu-latest + windows-latest matrix; dotnet restore + dotnet build -warnaserror.
- `test.yml` — push+PR trigger; dotnet test with XPlat coverage; continue-on-error=true until Phase 1 adds xUnit projects; uploads coverage artifacts.
- `pack-and-publish.yml` — main branch only; dotnet pack -c Release; dotnet nuget push with skip-duplicate; guarded by `if: vars.NUGET_FEED_URL != '' && secrets.NUGET_API_KEY != ''` so forks do not fail.

**Smoke tests (`tests/Chassis.SharedKernel.PackageTests/`)**
- `Net10/Chassis.SharedKernel.SmokeTest.Net10.csproj` — net10.0 executable; project-references SharedKernel.
- `Net10/SmokeTest.cs` — instantiates Result<T>, Result, CorrelationHeaders, CloudEventEnvelope.
- `Net48/Chassis.SharedKernel.SmokeTest.Net48.csproj` — net48 executable; project-references SharedKernel (resolves netstandard2.0 facet).
- `Net48/SmokeTest.cs` — same coverage as net10 smoke test.

### Design decisions made in this session

**Result<T> as class, not record:** `init`-only setters on records require the `IsExternalInit` polyfill on netstandard2.0. Using a simple sealed class with a private constructor achieves identical immutability with zero polyfill complexity. See `Result.cs` for the comment.

**IModuleStartup.Configure guarded with `#if NET10_0_OR_GREATER`:** `IEndpointRouteBuilder` is an ASP.NET Core type. It is not available on netstandard2.0 (no ASP.NET Core on .NET Framework). The guard ensures the netstandard2.0 facet compiles cleanly while the net10.0 facet exposes the full interface.

**FrameworkReference for ASP.NET Core on net10.0:** `Microsoft.AspNetCore.Routing` has no standalone NuGet package beyond 2.3.9. On .NET 10, ASP.NET Core types are accessed via `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in the project file.

**StyleCop suppressions in shared kernel:** SA1633 (file headers), SA1101 (this-prefix), SA1309 (underscore prefix on fields), SA0001 (XML doc analysis globally). These suppressions are intentional and justified by the project's naming conventions and the preference for _camelCase private fields.

### Version adjustments relative to IMPLEMENTATION_PLAN.md §4

| Package | Plan target | Actual version | Reason |
|---|---|---|---|
| MassTransit | 8.x | 8.5.9 | Latest stable 8.x; MT 9.x is in develop-channel only as of 2026-04-20. Using 8.5.9 per plan §2.1. Revisit when MT 9.0.0 GA ships. |
| Marten | 7.x | 8.31.0 | Marten 7.x EOL; 8.x is the current stable series with .NET 10 support. |
| OpenIddict | 6.x | 7.4.0 | OpenIddict 6.x is incompatible with .NET 10; 7.x is the current release with .NET 10 support. |
| FluentValidation | 11.x | 12.1.1 | FV 12.x is the current stable release; 11.x is maintenance-only. |
| Mapster | 7.x | 10.0.7 | Mapster 7.x is not compatible with .NET 10; 10.x is the current stable release series. |
| Serilog.AspNetCore | 8.x | 10.0.0 | Serilog.AspNetCore 10.x tracks the .NET 10 release cadence; 8.x was the .NET 8 era release. |
| MinVer | 5.x | 7.0.0 | MinVer 7.x is the latest stable; 5.x was released alongside .NET 8. |
| FluentAssertions | 6.x | 8.9.0 | FA 8.x is the current stable series with .NET 10 support. |
| xUnit.runner.visualstudio | — | 3.1.0 | Required for VS/Rider test discovery with xUnit 2.9.x. |
| coverlet.collector | — | 10.0.0 | Latest stable; added for code coverage collection in CI. |

### Phase 0 acceptance criteria — status

- `dotnet restore` — PASS
- `dotnet build -warnaserror` — PASS (0 warnings, 0 errors)
- `dotnet pack src/Chassis.SharedKernel -c Release` — to be verified after this session
- nupkg contains lib/netstandard2.0/ and lib/net10.0/ — to be verified
- net10.0 smoke test builds — PASS
- net48 smoke test builds — PASS

### What is NOT done (Phase 0 scope gaps)

- No `README.md` — documentation files are out of scope for this AI session per project rules.
- No `load-test-gate.yml` — explicitly deferred to Phase 8 per instructions.
- No actual module projects (Chassis.Host, Identity, Ledger, etc.) — Phase 1+ scope.

### Follow-up items for Phase 1

1. Remove `continue-on-error: true` from `test.yml` once the first xUnit test project is added in Phase 1.
2. Configure `NUGET_FEED_URL` variable (not secret — it's a URL) and `NUGET_API_KEY` secret in GitHub repository settings before the first release publish.
3. Add `MinVer` version tag (`v0.1.0`) before first pack to establish baseline SemVer.
4. Verify `dotnet pack` nupkg content (lib/netstandard2.0/ + lib/net10.0/ facets) after tagging.
5. Review MassTransit 8.5.9 → 9.x migration when MT 9.0.0 GA ships (check license terms per plan §2.1 note on MT licensing churn risk).
