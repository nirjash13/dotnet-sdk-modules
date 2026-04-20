# constitution.md — Project Immutable Rules (.NET)

<!--
PURPOSE: Define rules that NEVER change without explicit human approval.
SCOPE: All agents (Architect, Builder, Critic) must obey these rules.
OVERRIDE: Only human can modify this file.
-->

# Constitution: [Project Name]

> These rules are IMMUTABLE. Agents must follow them regardless of task instructions.
> Violation of these rules requires human review before proceeding.

---

## 1. Absolute Prohibitions

### Never Do These (No Exceptions)

```
❌ NEVER commit secrets, API keys, or credentials to the repository
❌ NEVER store secrets in appsettings.json — use environment variables or secret manager
❌ NEVER use .Result or .Wait() on Tasks in ASP.NET Core context (deadlock risk)
❌ NEVER delete or modify production data without explicit approval
❌ NEVER bypass authentication or authorization checks
❌ NEVER disable security middleware, even for testing
❌ NEVER concatenate user input into raw SQL strings
❌ NEVER log sensitive user data (passwords, tokens, PII, credit card numbers)
❌ NEVER modify these files without human review:
   - constitution.md
   - .github/workflows/*
   - scripts/deploy-*
   - Migrations/* (existing applied migrations)
   - appsettings.Production.json
```

---

## 2. Architectural Invariants

### These Patterns Must Be Maintained

```
✅ All endpoints require authentication (AllowAnonymous is explicit opt-out)
✅ All request DTOs must be validated with FluentValidation
✅ All database access must go through repository interfaces in Application layer
✅ All async methods must accept and forward CancellationToken
✅ All errors must be logged with correlation IDs
✅ All async I/O operations must have timeout handling
✅ Domain layer has zero external package dependencies
✅ EF Core entities must never be exposed in API response DTOs
```

### These Patterns Are Forbidden

```
❌ No direct DbContext injection in API controllers
❌ No business logic in controllers (HTTP plumbing only)
❌ No synchronous I/O in request handlers (.Result, .Wait(), Thread.Sleep)
❌ No global mutable static state
❌ No circular dependencies between layers
❌ No cross-feature imports (features share only via interfaces/contracts)
```

---

## 3. Code Quality Gates

### All Code Must Pass

```
✅ dotnet build -warnaserror (zero warnings)
✅ dotnet format --verify-no-changes
✅ dotnet test (all tests pass)
✅ Nullable reference type warnings resolved
```

### Review Required For

```
⚠️ Changes to public API contracts (breaking changes require versioning)
⚠️ Database schema modifications (new migrations)
⚠️ New NuGet dependencies (verify: maintained? license? .NET version compatible?)
⚠️ Security-sensitive code (auth, crypto, PII handling)
⚠️ Performance-critical paths (EF Core queries on large tables)
```

---

## 4. Dependency Rules

### Requires Approval

```
⚠️ Any new runtime NuGet dependency
⚠️ Dependencies with LGPL/GPL/AGPL licenses (may affect commercial use)
⚠️ Dependencies not updated in >18 months
```

### Forbidden

```
❌ Dependencies with known unpatched CVEs
❌ Abandoned packages (archived repo, no updates in 2+ years)
❌ Packages that require unsafe code blocks (unless security-reviewed)
```

---

## 5. Data Handling Rules

```
✅ Sensitive data encrypted at rest (database-level or field-level)
✅ All API communication over HTTPS (HSTS in production)
✅ PII access logged with user ID and timestamp
✅ Passwords hashed with BCrypt/Argon2/PBKDF2 — never MD5/SHA1
❌ Never log: passwords, tokens, full credit card numbers, SSNs
❌ Never store plaintext passwords anywhere
❌ Never expose internal IDs that could be guessed/enumerated (use UUIDs)
```

---

## 6. Database / Migration Rules

```
✅ All migrations reviewed before applying to staging or production
✅ Breaking changes (NOT NULL on existing rows, drops) use two-phase deployment
✅ Migrations tested with dotnet ef database update on clean database
✅ Rollback script prepared for every production migration
❌ Never use EnsureCreated() in production
❌ Never delete applied migration files from source control
❌ Never modify applied migrations — create a new corrective migration instead
```

---

## 7. Error Handling Rules

```
✅ All exceptions caught at global middleware boundary
✅ All errors logged with stack trace (internal) and correlation ID
✅ ProblemDetails (RFC 7807) returned for all API error responses
✅ User-facing messages must be safe — never expose stack traces, paths, or internals
❌ No swallowed exceptions (empty catch blocks)
❌ No infinite retry loops without exponential backoff and max attempts
❌ No re-throwing exceptions without preserving original stack trace (use throw, not throw ex)
```

---

## 8. Testing Rules

```
✅ Behavior changes require tests before merge
✅ Bug fixes require a regression test (hard ceiling: exactly one per bug)
✅ New API endpoints require integration tests with WebApplicationFactory
✅ Tests use deterministic data — no DateTime.Now, no Guid.NewGuid() in assertions
❌ Never use real production data in tests
❌ Never hardcode credentials in test code
❌ Never skip tests with .Skip() permanently — fix or delete them
```

---

## 9. Agent-Specific Rules

### Architect (Opus)
```
✅ Must check constitution.md before proposing architectural changes
✅ Must flag assumptions explicitly with ASSUMPTION: tag
✅ Must consider migration complexity for schema changes
❌ Must not write implementation code
```

### Builder (Sonnet)
```
✅ Must follow TDD (failing test first)
✅ Must follow the plan from architect-opus
✅ Must flag deviations from plan with DEVIATION:
❌ Must not modify constitution.md or migrations for existing applied data
❌ Must not skip tests for "simple" changes
```

### Critic (Opus)
```
✅ Must check against constitution.md
✅ Must verify all new endpoints have [Authorize] or [AllowAnonymous]
✅ Must flag security issues as CRITICAL (blocking)
❌ Must not approve code with missing [Authorize] on mutation endpoints
```

---

## 10. Escalation Rules

### Escalate to Human When

```
⚠️ Constitution violation detected
⚠️ Security vulnerability found
⚠️ Ambiguous business rule interpretation
⚠️ Multiple valid architectural approaches (no clear winner)
⚠️ Breaking change to public API contract
⚠️ Database migration affecting production data
⚠️ New external service dependency proposed
```

### Do Not Proceed Without Human Approval

```
🛑 Deleting user data
🛑 Modifying authentication or authorization flow
🛑 Changing encryption implementation
🛑 Modifying payment processing code
🛑 Changing data retention or privacy policies
🛑 Multi-phase production database migration
```

---

## 11. Version Control Rules

```
✅ Conventional commit format: type(scope): message
✅ Atomic commits — one logical change per commit
✅ Feature branches from main/master
✅ PRs must pass CI (build + tests) before merge
❌ Never commit directly to main/master
❌ Never force push to shared branches
❌ Never commit .env, appsettings.Production.json, or *.pfx files
```

---

## Changelog

| Date | Change | Author |
|---|---|---|
| YYYY-MM-DD | Initial version | [Name] |

> **Remember**: These rules exist to protect the project, the team, and the users.
> When in doubt, ask a human.
