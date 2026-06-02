# Secrets & Credentials

The application reads its secrets (currently the database connection string) from configuration
**outside** source control. They are never committed to `appsettings.json`.

- **Local development:** `dotnet user-secrets` (enabled via `<UserSecretsId>` in the csproj).
- **Production (AWS, later):** environment variables sourced from AWS Secrets Manager / SSM.

Config keys:
- `ConnectionStrings:DBOpenFinanceConnection` — database connection string.
- `Jwt:Key` — JWT signing key (>= 32 chars). In Development the app falls back to a known dev-only
  key if this is unset; outside Development it refuses to start without one.

---

## First-time local setup

After cloning, set the connection string so the app and EF migrations can reach the database:

```powershell
cd "WebAPI.OpenFinance"
dotnet user-secrets set "ConnectionStrings:DBOpenFinanceConnection" "Host=<host>;Database=DBOpenFinance;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
```

Set a JWT signing key (any random string of 32+ characters):

```powershell
dotnet user-secrets set "Jwt:Key" "<a-long-random-development-signing-key>"
```

Verify:

```powershell
dotnet user-secrets list
```

> **Gotcha:** user-secrets are only loaded when the environment is `Development`. Visual Studio and
> the launch profiles set this automatically, but a **plain terminal defaults to Production**, where
> secrets are ignored and the connection string reads as empty (this commonly breaks `dotnet ef`).
> Before running `dotnet run` or any `dotnet ef` command from a fresh terminal:
>
> ```powershell
> $env:ASPNETCORE_ENVIRONMENT = "Development"
> ```

---

## Rotating the database password (Neon)

Only the **password** changes — host, username, and database stay the same.

1. Log in at **console.neon.tech** → open the **DBOpenFinance** project.
2. Go to **Roles** (or **Dashboard → Connection Details**), select role **`neondb_owner`**, and click
   **Reset password**. This immediately invalidates the old password.
3. Copy the new connection string from **Connection Details**. Use the **pooled** connection (host
   ending in `-pooler`). Neon may present it as a `postgresql://...` URI; you only need the new
   password — the rest of the string is unchanged.
4. Update the local secret (only `Password=` differs from before):

   ```powershell
   cd "WebAPI.OpenFinance"
   dotnet user-secrets set "ConnectionStrings:DBOpenFinanceConnection" "Host=<host>;Database=DBOpenFinance;Username=neondb_owner;Password=<NEW_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true"
   ```

   Keep `SSL Mode=Require;Trust Server Certificate=true` — Neon requires SSL.

5. Verify connectivity:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run   # Swagger at http://localhost:5280/swagger
   ```

---

## Production (AWS, later)

Do **not** use user-secrets in production. Provide the same config key as an environment variable
(note the double underscore, which maps to the `:` separator):

```
ConnectionStrings__DBOpenFinanceConnection = "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
```

Source this from AWS Secrets Manager or SSM Parameter Store. No application code change is required —
the config key is identical across environments.

---

## Notes

- A previously committed connection string remains in **git history** even after it is removed from
  the working tree. Rotating the password (above) is what actually neutralizes a leaked credential.
  Scrubbing history (`git filter-repo` / BFG) is optional and rewrites history — avoid on shared
  branches unless necessary.
- Never paste real secrets into commits, issues, PRs, or this file. The examples use placeholders.
