# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

OpenFinance is a .NET 8 ASP.NET Core Web API (minimal APIs) that aggregates a client's financial holdings (cash, stocks, mutual funds) across bank connections and exposes portfolio totals plus signup/login. Data lives in a hosted PostgreSQL database accessed via EF Core 9 (Npgsql). The solution is `OpenFinance.sln` with a single project, `WebAPI.OpenFinance`.

## Commands

All commands run from the repo root.

- Build: `dotnet build`
- Run (serves on http://0.0.0.0:5280, Swagger UI at /swagger): `dotnet run --project WebAPI.OpenFinance`
- Add a migration: `dotnet ef migrations add <Name> --project WebAPI.OpenFinance`
- Apply migrations to the database: `dotnet ef database update --project WebAPI.OpenFinance`

`dotnet ef` requires the EF tools (`dotnet tool install --global dotnet-ef`). There is no test project in this repo.

## Architecture

The app follows a consistent three-layer convention. When adding a feature, mirror it.

- **Program.cs** — composition root. Registers `OpenFinanceContext` (connection string `DBOpenFinanceConnection`) and Swagger, then mounts each feature by calling its route extension method (e.g. `app.ClientRoutes()`). A new feature area means a new `Routes/*.cs` extension method registered here.

- **Routes/ (`Routes/*.cs`)** — one `static class` per feature with a single `public static void XxxRoutes(this WebApplication app)` extension method. Inside, `var route = app.MapGroup("groupName");` then map endpoints. Routes are thin: they receive `OpenFinanceContext` (and a request DTO) as endpoint parameters, run validation/existence checks, delegate work to a Helper, and return `Results.Ok(...)` / `Results.BadRequest(...)`. Responses are anonymous objects that include a `timestamp = DateTime.UtcNow`. Request DTOs (e.g. `Login`, `Signup`) are plain classes defined in the same route file.

- **Helpers/ (`Helpers/*.cs`)** — `static` classes holding all EF queries and business logic; routes never query EF directly. Each method takes `OpenFinanceContext context` as its first parameter. `ClientHelper` computes portfolio totals, `AuthenticationHelper` handles login/signup/blocking, `ValidationHelper` (internal) holds regex input validation.

- **Models/ (`Models/*.cs`)** — EF entities. Map to the DB with `[Table("snake_case")]` and `[Column("snake_case")]` while keeping **camelCase C# property names** (e.g. `clientID`, `lastDayPrice`). Primary keys use `{ get; init; }`; relationships use `[ForeignKey]` navigation properties. `OpenFinanceContext` (Data/) declares the `DbSet`s and sets DB defaults in `OnModelCreating` (e.g. `remainingLoginAttempts` = 3, `isBlocked` = false).

- **Migrations/** — EF Core migrations; regenerate the snapshot by adding a migration after any model change, never hand-edit `OpenFinanceContextModelSnapshot.cs`.

### Domain model

A `client` has `connections` (one per bank account). Each product has two tables: a holdings table per connection (`stock_info`, `cash_info`, `mutual_fund_info`) and a reference table with current price/NAV (`stock`, `mutual_fund`). Portfolio value = sum over a client's connections of `quantity × price`. See `ClientHelper.GetClienStockTotalAmount` and `GetClientMutualFundTotalAmount` for the join pattern to copy when adding a product type.

## Conventions

- Always use `DateTime.UtcNow` for timestamps (front and back end).
- Use `AnyAsync` for existence checks rather than fetching then null-checking.
- Validate inputs in the route before touching the DB; return `Results.BadRequest("message")` on failure.
- Auth flow: a wrong password decrements `remainingLoginAttempts`; at 0 the client is blocked for 5 minutes (`AuthenticationHelper.BlockClient`). A successful login resets attempts to 3.

## Caveats (current state, not yet hardened)

- The PostgreSQL connection string including credentials is committed in `appsettings.json`.
- Passwords are stored and compared as plaintext in `client_credentials` (no hashing).
- There is no authentication/authorization on the portfolio endpoints and no automated tests.
