# B2CSample — Local Setup

## Database password

The connection string in `appsettings.json` ships with an **empty** `Password=` placeholder.
Never commit a real password to this file.

Supply the password via **dotnet user-secrets** (recommended for local dev):

```bash
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=b2csample;Username=postgres;Password=<your-password>"
```

Or via an **environment variable** (works in any environment):

```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=b2csample;Username=postgres;Password=<your-password>"
```

The `__` double-underscore maps to `:` in .NET Configuration, so `ConnectionStrings__Default`
overrides `ConnectionStrings:Default` in `appsettings.json`.
