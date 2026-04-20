---
name: dotnet-excellence
description: Produce production-grade C# + ASP.NET Core + Entity Framework Core code with strong typing, security, clean architecture, and maintainability.
trigger_phrases:
  - "csharp"
  - "c#"
  - "dotnet"
  - ".net"
  - "asp.net"
  - "entity framework"
  - "ef core"
  - "controller"
  - "endpoint"
  - "middleware"
  - "repository"
  - "handler"
  - "command"
  - "query"
  - "migration"
  - "refactor"
---

## Goal
Generate or modify .NET backend code that is correct, readable, type-safe, secure, and easy to maintain. Target stack: C# 12+, ASP.NET Core, Entity Framework Core, Clean Architecture or Vertical Slice.

## Use for
- New API endpoints, controllers, minimal API groups
- Application layer handlers (commands, queries)
- Domain entities, value objects
- EF Core repositories, DbContext, migrations
- Middleware, filters, background services
- Validation (FluentValidation), mapping (Mapster), authentication/authorization
- Refactors, bug fixes, performance improvements

## Version awareness
Before using any .NET feature or API:
1. Check the target framework in `*.csproj` (`<TargetFramework>net6.0</TargetFramework>` etc.)
2. Only use APIs available in that version
3. For .NET 6 vs .NET 8 vs .NET 10 differences, check official release notes

## Working contract
- If requirements are incomplete, make safe assumptions and state them in 2-4 lines
- Prefer minimal change for edits — keep existing patterns, naming, and folder structure
- Do not add new NuGet packages unless asked or already used in the project
- Always include proper async patterns (CancellationToken, ConfigureAwait(false) in libraries)
- Layer boundaries are mandatory — check ARCHITECTURE.md

## What to read
- `CSHARP.md` — language rules, patterns, and idioms
- `ASPNET.md` — ASP.NET Core endpoint, middleware, DI patterns
- `EF_CORE.md` — Entity Framework Core query, migration, and modeling patterns
- `ARCHITECTURE.md` — Clean Architecture and Vertical Slice structure and boundaries
- `SECURITY.md` — auth, input validation, secret management, OWASP standards
- `TESTING.md` — xUnit, Moq, FluentAssertions, WebApplicationFactory, TestContainers patterns

## Default assumptions (override if project shows otherwise)
- Nullable reference types enabled
- TreatWarningsAsErrors enabled
- xUnit + Moq + FluentAssertions for tests
- FluentValidation for request validation
- Mapster for DTO mapping
- Clean Architecture with Domain/Application/Infrastructure/API layers
- Repository pattern with interfaces in Application layer
