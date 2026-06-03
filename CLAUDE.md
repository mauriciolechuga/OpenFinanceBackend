# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

OpenFinance is a .NET 8 ASP.NET Core Web API (minimal APIs) evolving from a manual portfolio-tracker
demo into a **read-only financial aggregation** product (banking + brokerage) for the Canadian market.
It exposes JWT-authenticated signup/login, a normalized aggregation model (accounts, securities,
holdings, transactions, net-worth snapshots) populated from pluggable data providers, plus the legacy
manual portfolio totals. Data lives in a hosted PostgreSQL database (Neon) accessed via EF Core 9
(Npgsql). The solution is `OpenFinance.sln` with the API project `WebAPI.OpenFinance` and a test
project `WebAPI.OpenFinance.Tests`.

Strategy and roadmap live in `docs/ARCHITECTURE.md`; secrets handling in `docs/SECRETS.md`.

## Commands

All commands run from the repo root.

- Build: `dotnet build`
- Test: `dotnet test`
- Run (serves on http://0.0.0.0:5280 — browse at http://localhost:5280/swagger): `dotnet run --project WebAPI.OpenFinance`
- Add a migration: `dotnet ef migrations add <Name> --project WebAPI.OpenFinance`
- Apply migrations: `dotnet ef database update --project WebAPI.OpenFinance`

`dotnet ef` requires the EF tools (`dotnet tool install --global dotnet-ef`, version 9.x to match the runtime).

> **Critical gotcha:** secrets (DB connection string, `Jwt:Key`) come from `dotnet user-secrets`, which
> only load when the environment is `Development`. A plain terminal defaults to **Production**, where the
> connection string reads empty and DB/EF commands fail. Before `dotnet run` or any `dotnet ef` command
> in a fresh terminal: `$env:ASPNETCORE_ENVIRONMENT = "Development"`. See `docs/SECRETS.md` for setup.

## Architecture

Requests flow **Routes → Services → (Helpers / EF) → Models**. When adding a feature, mirror this.

- **Program.cs** — composition root. Configures EF (`OpenFinanceContext`, connection string
  `DBOpenFinanceConnection`), JWT bearer auth, FluentValidation, ProblemDetails, Data Protection,
  Swagger (with bearer), registers the scoped services and the aggregation provider/background sync,
  then mounts each feature's route extension method (e.g. `app.AuthenticationRoutes()`). A new feature
  area means a new `Routes/*.cs` extension method registered here.

- **Routes/ (`Routes/*.cs`)** — one `static class` per feature with a `public static void XxxRoutes(this WebApplication app)`
  extension method. Inside: `var route = app.MapGroup("groupName").RequireAuthorization();` then map
  endpoints. Routes are thin: they take injected services/validators (and a request DTO) as endpoint
  parameters, validate, delegate to a **Service**, and translate the result to `Results.Ok` /
  `ValidationProblem` / `Problem`. Groups: `authentication` (AllowAnonymous), `clients`, `bankslist`,
  `connections`, `portfolio`. `authentication` and the aggregator webhook are anonymous; everything else
  requires a JWT.

- **Services/ (`Services/*.cs`)** — the business-logic layer, injected via interfaces
  (`IAuthService`, `IPortfolioService`, `IAggregationService`). This is where new logic goes. Services
  depend on `OpenFinanceContext` and other services through DI (constructor injection), not static calls.

- **Helpers/ (`Helpers/*.cs`)** — older `static` classes still used for some EF/auth primitives
  (`AuthenticationHelper` for hashing/blocking, `ClientHelper` for legacy totals, `ValidationHelper`).
  Each method takes `OpenFinanceContext` as its first parameter. Prefer adding new logic to a Service;
  treat helpers as lower-level utilities the services call.

- **Auth/ (`Auth/*.cs`)** — self-hosted JWT: `JwtTokenService` (HS256, claims `sub`=clientId/`name`/`jti`),
  `JwtSettings`, and `ClaimsPrincipalExtensions.GetClientId()`. Per-user authorization compares the route's
  client id to the token's `sub`.

- **Aggregation/ (`Aggregation/*.cs`)** — the vendor-agnostic seam. `IAggregationProvider`
  (`CreateLinkSession`/`ExchangeToken`/`FetchSnapshot`) with `MockAggregationProvider` (deterministic
  $0 sandbox data) registered today; real providers (Flinks/Plaid, SnapTrade) drop in behind it.
  `ITokenProtector` encrypts provider access tokens via ASP.NET Core Data Protection.

- **BackgroundServices/** — `AggregationSyncHostedService` periodically re-syncs active connections
  (in-process stand-in for a future SQS + worker).

- **Models/ (`Models/*.cs`)** — EF entities. Map to the DB with `[Table("snake_case")]` /
  `[Column("snake_case")]` while keeping **camelCase / PascalCase C# property names**. PKs use
  `{ get; init; }`; relationships use `[ForeignKey]` navigation props. Enums are stored as strings.
  `OpenFinanceContext` (Data/) declares the `DbSet`s, sets defaults, enum-to-string conversions, and
  unique reconciliation indexes in `OnModelCreating`.

- **Dtos/ (`Dtos/*.cs`)** — request/response DTOs, kept separate from EF entities. Validation lives in
  `Validation/*.cs` (FluentValidation).

- **Migrations/** — EF Core migrations; regenerate the snapshot by adding a migration after any model
  change, never hand-edit `OpenFinanceContextModelSnapshot.cs`.

### Domain model

Two coexisting models:

- **Legacy manual model:** a `client` has `connections`; each product has a holdings table per connection
  (`stock_info`, `cash_info`, `mutual_fund_info`) plus a reference table with price/NAV (`stock`,
  `mutual_fund`). Totals = sum over connections of `quantity × price` (`ClientHelper`).
- **Normalized aggregation model (current direction):** `accounts`, `securities`, `holdings`,
  `transactions`, `balance_snapshots`, with provider fields (`provider`, `provider_item_id`, encrypted
  token, `status`, `last_synced_at`) on `connections`. Populated by `AggregationService` syncing an
  `IAggregationProvider` snapshot; reconciliation is idempotent via unique indexes on provider IDs, so
  re-syncing updates rows instead of duplicating. Read endpoints: net worth, accounts, holdings,
  transactions (`PortfolioService`).

## Conventions

- Always use `DateTime.UtcNow` for timestamps (front and back end).
- Use `AnyAsync` for existence checks rather than fetching then null-checking.
- New business logic goes in a **Service** (DI'd interface), not a static helper or the route.
- Validate inputs with a FluentValidation validator; return `ValidationProblem` / ProblemDetails, not
  ad-hoc strings.
- Every endpoint except `authentication` and the webhook requires a JWT; per-user routes enforce that the
  route's client id equals the token `sub`.
- Auth flow: blocked-status is checked **before** the password; a wrong password decrements
  `remainingLoginAttempts`; at 0 the client is blocked for 5 minutes; a successful login resets to 3.
- Passwords are hashed with ASP.NET Core `PasswordHasher` (PBKDF2) — never stored or compared in plaintext.

## Secrets & current state

- The DB connection string and `Jwt:Key` are **not** committed — they come from user-secrets (dev) or
  environment/Secrets Manager (prod). `appsettings.json` ships empty placeholders. See `docs/SECRETS.md`.
- `Jwt:Key` must be **≥ 32 bytes (256 bits / 32+ ASCII chars)**; a shorter key makes login/signup fail
  with HTTP 500 (`IDX10720`). Development falls back to a dev-only key if unset; prod refuses to start.
- The originally committed Neon credential is dead (that project was abandoned and replaced with a fresh
  Neon project — see `docs/SECRETS.md`).
- Not yet hardened: no rate limiting; HTTPS redirect is on but TLS termination/hosting is deferred;
  data residency (Canadian-resident DB) is a pre-production item in `docs/ARCHITECTURE.md` §7.
