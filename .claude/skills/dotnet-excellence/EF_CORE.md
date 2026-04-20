# Entity Framework Core Standards

## DbContext Design

### Single Bounded Context Per DbContext
```csharp
// One DbContext per bounded context — not one massive god-context
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs { get; set; }
    public DbSet<Organization> Organizations { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

// Configuration per entity (IEntityTypeConfiguration<T>) — never inline in OnModelCreating
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Title).HasMaxLength(200).IsRequired();
        builder.Property(j => j.Status)
            .HasConversion<string>()   // store enum as string — readable in DB
            .HasMaxLength(50);
        builder.HasIndex(j => j.OrganizationId);
        builder.HasQueryFilter(j => !j.IsDeleted);   // global soft-delete filter
    }
}
```

### Testability Interface
```csharp
// Expose minimal interface for Application layer to depend on
public interface IAppDbContext
{
    DbSet<Job> Jobs { get; }
    DbSet<Organization> Organizations { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// AppDbContext implements it — Application layer injects IAppDbContext, not the concrete type
```

## Query Patterns

### Read-Only Queries — Always AsNoTracking + Projection
```csharp
// Correct: projected DTO, no tracking, cancel on request abort
public async Task<IReadOnlyList<JobSummaryDto>> GetActiveJobsAsync(
    Guid orgId,
    int page,
    int pageSize,
    CancellationToken ct = default)
{
    return await _context.Jobs
        .AsNoTracking()
        .Where(j => j.OrganizationId == orgId)   // global filter for IsDeleted already applied
        .OrderByDescending(j => j.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(j => new JobSummaryDto(j.Id, j.Title, j.Status, j.CreatedAt))
        .ToListAsync(ct);
}

// Wrong: load entity then map — N+1 risk, unnecessary tracking
// ❌ var jobs = await _context.Jobs.ToListAsync(ct);
// ❌ return jobs.Select(j => mapper.Map<JobSummaryDto>(j)).ToList();
```

### Write Queries — Track Entities, Check Concurrency
```csharp
public async Task<Result<Guid>> CreateJobAsync(CreateJobCommand cmd, CancellationToken ct)
{
    var orgExists = await _context.Organizations
        .AsNoTracking()
        .AnyAsync(o => o.Id == cmd.OrganizationId, ct);

    if (!orgExists)
        return Result<Guid>.Failure("Organization not found");

    var job = Job.Create(cmd.Title, cmd.Description, cmd.OrganizationId);
    _context.Jobs.Add(job);
    await _context.SaveChangesAsync(ct);

    return Result<Guid>.Success(job.Id);
}
```

### Finding by ID — Handle Not Found
```csharp
// FindAsync is efficient for PK lookup (checks tracker first)
var job = await _context.Jobs.FindAsync([id], ct);
if (job is null)
    return Result<JobDto>.Failure($"Job {id} not found");

// For queries with filtering — use FirstOrDefaultAsync + null check
var job = await _context.Jobs
    .AsNoTracking()
    .Where(j => j.OrganizationId == orgId && j.Id == id)
    .Select(j => new JobDto(j.Id, j.Title, j.Status, j.CreatedAt))
    .FirstOrDefaultAsync(ct);
```

### Avoid N+1 Queries
```csharp
// Correct: single query with projection
var jobsWithOrg = await _context.Jobs
    .AsNoTracking()
    .Select(j => new JobWithOrgDto(
        j.Id,
        j.Title,
        j.Organization.Name))  // EF Core resolves this with a JOIN, not N+1
    .ToListAsync(ct);

// Wrong: N+1 pattern
// ❌ var jobs = await _context.Jobs.ToListAsync(ct);
// ❌ foreach (var j in jobs) { var org = await _context.Organizations.FindAsync(j.OrgId); }
```

### Bulk Operations
```csharp
// .NET 7+ EF Core bulk operations — avoid loading entities to delete/update
await _context.Jobs
    .Where(j => j.OrganizationId == orgId && j.ExpiresAt < DateTimeOffset.UtcNow)
    .ExecuteDeleteAsync(ct);

// Bulk update without loading
await _context.Jobs
    .Where(j => j.OrganizationId == orgId)
    .ExecuteUpdateAsync(
        s => s.SetProperty(j => j.Status, JobStatus.Archived),
        ct);
```

## Migrations

### Rules
1. Every migration must be reviewed before applying to staging/production
2. Breaking migrations (drop column, rename, NOT NULL on existing rows) require two-phase deployment
3. Never use `EnsureCreated()` in production — always `Migrate()`
4. `HasData()` for static seed data only — not for environment-specific data

### Two-Phase Migration Pattern (breaking changes)
```
Phase 1: Add new nullable column (backward compatible — deploy)
Phase 2: Backfill data (script or migration — deploy)
Phase 3: Add NOT NULL constraint (after backfill verified — deploy)
Phase 4: Remove old column (after all services updated — deploy)
```

### Commands
```bash
# Add migration (run from solution root or specify project)
dotnet ef migrations add <Name> --project YourProject.Infrastructure --startup-project YourProject.API

# Apply
dotnet ef database update --project YourProject.Infrastructure --startup-project YourProject.API

# Verify last migration
dotnet ef migrations list --project YourProject.Infrastructure

# Remove last migration (if not applied)
dotnet ef migrations remove --project YourProject.Infrastructure

# Generate SQL script for review
dotnet ef migrations script --project YourProject.Infrastructure --output migration.sql
```

## Performance Checklist

- [ ] `AsNoTracking()` on all reads
- [ ] Projected to DTO (not loading full entity)
- [ ] Pagination on all list queries (`Skip`/`Take`)
- [ ] Index on FK columns and frequently filtered columns (in `IEntityTypeConfiguration<T>`)
- [ ] `AnyAsync()` instead of `Count() > 0` for existence checks
- [ ] No `Include()` chains deeper than 2 levels — use projections instead
- [ ] `ExecuteDeleteAsync` / `ExecuteUpdateAsync` for bulk operations
- [ ] Connection resiliency configured for cloud databases:
  ```csharp
  options.UseNpgsql(conn, o => o.EnableRetryOnFailure(3));
  ```
