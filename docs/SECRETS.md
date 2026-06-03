# Secrets & Credentials

The application reads its secrets (currently the database connection string) from configuration
**outside** source control. They are never committed to `appsettings.json`.

- **Local development:** `dotnet user-secrets` (enabled via `<UserSecretsId>` in the csproj).
- **Production (AWS, later):** environment variables sourced from AWS Secrets Manager / SSM.

Config keys:
- `ConnectionStrings:DBOpenFinanceConnection` — database connection string.
- `Jwt:Key` — JWT signing key. **Must be at least 32 bytes (256 bits).** For an ASCII/UTF-8 string
  that means **32+ characters**; a shorter key throws `IDX10720: ... key size must be greater than
  256 bits` at token-issuing time (login/signup return HTTP 500). In Development the app falls back to
  a known dev-only key if this is unset; outside Development it refuses to start without one.

---

## First-time local setup

After cloning, set the connection string so the app and EF migrations can reach the database:

```powershell
cd "WebAPI.OpenFinance"
dotnet user-secrets set "ConnectionStrings:DBOpenFinanceConnection" "Host=<host>;Database=DBOpenFinance;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
```

Set a JWT signing key. It **must be at least 32 characters** (256 bits) — anything shorter makes
login/signup fail with HTTP 500 (`IDX10720`). Generate a long random one and store it:

```powershell
# PowerShell — 48 random bytes, base64-encoded (~64 chars)
$key = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))
dotnet user-secrets set "Jwt:Key" $key
```

(Or just pass any string of 32+ characters explicitly.)

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

## Creating a fresh Neon project (new database)

Use this when you don't have access to the existing Neon project (or want a clean database). Unlike a
rotation, the **host, username, and database name all change**, and the new database starts empty, so
you must apply migrations. Abandoning the old project also makes any previously leaked credential moot.

1. At **console.neon.tech**, click **New Project**. Pick a name, Postgres 16, and the closest region
   (the free tier has no Canadian region — US East is fine for dev; Canadian residency is a
   pre-production concern, see `docs/ARCHITECTURE.md` §7).
2. Open **Connection Details / Connect** and copy the **pooled** connection string (host ends in
   `-pooler`). Neon may show a `.NET` tab — note the `Host`, `Username` (usually `neondb_owner`),
   `Password`, and `Database` (usually `neondb`).
3. Store it as the local secret in the key/value Npgsql format (Neon's `.NET` string already uses this;
   if you only have the `postgresql://...` URI, map its parts into the form below):

   ```powershell
   cd "WebAPI.OpenFinance"
   dotnet user-secrets set "ConnectionStrings:DBOpenFinanceConnection" "Host=<host>;Database=<dbname>;Username=<user>;Password=<password>;SSL Mode=VerifyFull;Channel Binding=Require;"
   ```

   Neon's `.NET` string ships `SSL Mode=VerifyFull;Channel Binding=Require` — keep it. (`SSL Mode=Require;
   Trust Server Certificate=true` also works.)
4. Create the schema in the empty database:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet ef database update --project WebAPI.OpenFinance
   ```
5. Run and verify; the DB has no users yet, so sign up a fresh one via Swagger
   (old plaintext credentials won't verify after the password-hashing change):

   ```powershell
   dotnet run --project WebAPI.OpenFinance   # http://localhost:5280/swagger
   ```

---

## Rotating the database password (Neon)

Use this when you **do** have the original project and only need to invalidate a leaked password —
the **password** changes while host, username, and database stay the same.

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
