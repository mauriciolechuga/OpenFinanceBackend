# OpenFinance — Architecture & Plan

> Status: living document. Captures the target architecture and the phased plan to evolve
> the current demo backend into a scalable, stable, read-only financial aggregation product
> for the **Canadian** market, built and operated at **zero cost** until real scale justifies spend.

## 1. Product goal

A mobile-first app (Flutter) that lets a Canadian user securely connect their **banking and
brokerage accounts** and see everything in one place: balances, holdings, transactions, and net
worth over time. **Read-only** aggregation only — no payments, no trading (for now).

## 2. Guiding constraints

| Constraint | Decision |
|---|---|
| Market | Canada. Consumer-driven banking (open banking) is legislated but **not yet live**, so we bridge with aggregators now and adopt the official APIs later behind an abstraction. |
| Scope | Read-only aggregation. Lowest regulatory burden; fastest path to a stable, demoable product. |
| Stage | Fundable MVP — front-load security and clean architecture, defer operational spend. |
| Budget | **$0 right now.** Prefer self-hosted/OSS over paid services. Paid/free-tier AWS services are *chosen and documented* but **deferred** until deployment. |
| Cloud | AWS (when we deploy). |
| Stack | Keep **.NET 8** (backend) + **Flutter** (frontend). Evolve patterns, do not rewrite. |

## 3. Target architecture

```
Flutter app
   │  link account via provider SDK/redirect; data via our API (JWT-authenticated)
   ▼
.NET 8 API ───────────────► Auth (self-hosted JWT now → AWS Cognito free tier later)
   │  per-user authorization, DTOs, validation
   ├── Background sync (IHostedService now → SQS + worker later)
   │        │ pulls via IAggregationProvider, normalizes, persists
   │        ▼
   ├── PostgreSQL (Neon free tier now → RDS/Aurora later)
   ├── Encrypted token store (app-level encryption now → AWS KMS later)
   └── Webhook endpoint ◄──── aggregator push notifications
```

### The key abstraction: `IAggregationProvider`

Every external data source sits behind one interface so the domain and app never depend on a
specific vendor. This is what makes the product both **swappable** (avoid lock-in / cost) and
**futureproof** (drop in Canada's official open-banking APIs when they go live).

```
IAggregationProvider
 ├─ MockProvider                  (sandbox/demo data — zero cost, used for the MVP build)
 ├─ FlinksProvider / PlaidProvider (banking — added when budget allows a sandbox key)
 ├─ SnapTradeProvider             (brokerage — added likewise)
 └─ CanadianOpenBankingProvider   (official APIs — when the ecosystem is live)
```

Methods: `CreateLinkSession`, `ExchangeToken`, `FetchAccounts`, `FetchHoldings`,
`FetchTransactions`, `HandleWebhook`.

## 4. Zero-budget service choices (AWS)

Everything needed for the MVP runs free locally and on free tiers. Paid items are explicitly deferred.

| Concern | Now ($0) | Later (AWS, when justified) |
|---|---|---|
| Identity / auth | **Self-hosted JWT** in ASP.NET Core + built-in `PasswordHasher` (no external service) | AWS Cognito (free tier: 50k MAU) |
| Database | **Neon** Postgres free tier (already in use) | RDS / Aurora Serverless |
| Secrets | `dotnet user-secrets` (local) + env vars | AWS Secrets Manager / SSM Parameter Store |
| Token encryption | App-level envelope encryption with a key from config | AWS KMS |
| Background sync | `IHostedService` in-process | SQS + a worker service (ECS Fargate) |
| Hosting | Local / free container hosts | ECS Fargate or App Runner |
| CI/CD | **GitHub Actions** (free for the repo) | same |
| Observability | Serilog to console/file (OSS) | CloudWatch / OpenTelemetry → AWS |
| Account data | **MockProvider** (sandbox data) | Flinks/Plaid + SnapTrade sandbox → production keys |

**Principle:** nothing in the MVP requires a credit card. Aggregator integration is deferred until
there's a reason to pay for it; until then the `MockProvider` exercises the full pipeline end-to-end.

## 5. Domain model (target)

Generalizes today's `clients / connections / banks / cash|stock|mutual_fund (+ *_info)` into:

- **User** (← `clients`) — linked to the auth identity; **no plaintext password**.
- **Institution** (← `banks`) — + provider identifiers.
- **Connection** (← `connections`) — a linked login: `provider`, `provider_item_id`,
  **encrypted token reference**, `status`, `last_synced_at`.
- **Account** — one account in a connection: type/subtype (chequing, TFSA, RRSP, margin, credit),
  currency, balances. Generalizes the per-product tables.
- **Security** + **Holding** — instrument reference + position (qty, cost basis, market value).
- **Transaction** — normalized across all accounts.
- **BalanceSnapshot** — daily net-worth history (powers the analysis/charts screens).

Keep current good conventions: UTC everywhere, `AnyAsync` existence checks. **Change:** pre-compute
aggregates into snapshots instead of recomputing totals on every request.

## 6. Phased roadmap

### Phase 0 — Foundation & security hardening ($0)
Make the existing backend safe and maintainable before adding features.

- [x] Architecture & plan documented (this file)
- [x] Translate/clean all source comments; remove dead commented-out code
- [x] **Password hashing** — replace plaintext storage/compare with built-in `PasswordHasher`
- [x] **Secret hygiene** — move DB connection string out of `appsettings.json` to user-secrets/env
- [x] **Neon DB credential neutralized** — the original project was abandoned and replaced with a fresh Neon project (see `docs/SECRETS.md`), so the credential exposed in git history is now dead
- [x] DI/service-layer refactor — injected `IAuthService` / `IPortfolioService` / `IAggregationService`
- [x] DTOs separate from EF entities; FluentValidation + ProblemDetails
- [x] JWT bearer auth + per-user authorization on every endpoint; enable HTTPS
- [x] xUnit test project (in-memory provider; Testcontainers deferred — no Docker locally)
- [x] GitHub Actions CI (build + test)

### Phase 1 — Aggregation MVP ($0 via MockProvider)
- [x] `IAggregationProvider` + `MockProvider` (deterministic sandbox data)
- [x] Target domain model (Account/Security/Holding/Transaction/BalanceSnapshot) + EF migration
- [x] Webhook endpoint + `IHostedService` periodic sync; idempotent reconciliation
- [x] Backend read models: net worth + accounts + holdings + transactions
- [x] **Flutter link flow + JWT wiring** — done in the frontend repo (`mauriciolechuga/OpenFinance`,
  branch `phase1/flutter-jwt-and-link-flow`): centralized `ApiClient` attaches `Authorization: Bearer`
  and handles 401, login/signup store the JWT, link→exchange flow over `/connections/*`, and Dashboard/
  Holdings/Activity screens read `/portfolio/*`. (JWT is in-memory only for now — see note below.)
  **Verified end-to-end on an Android emulator (2026-06-03):** login → net worth/accounts/holdings/
  transactions → bank link → idempotent re-sync → logout, all against the live API + MockProvider data.
- [ ] Swap a real provider (Flinks/Plaid + SnapTrade) behind `IAggregationProvider` when a key exists
  *(deferred: needs a paid sandbox key, against the $0 constraint)*

### Phase 2 — Scale & polish (mostly $0)
- [ ] BalanceSnapshot history + analysis/charts screens
- [ ] Transaction categorization; aggregate caching
- [ ] Observability; load testing
- [ ] First real aggregator behind the interface (when budget allows a sandbox key)

### Phase 3 — Native Canadian open banking (when live)
- [ ] `CanadianOpenBankingProvider`; migrate connections off aggregators where cheaper
- [ ] Re-evaluate scope expansion (payments) only if market + licensing justify it

## 7. Owner action items

1. **Neon database — done.** The original project (whose credential was committed in `appsettings.json`
   and remains in git history) was abandoned and replaced with a **fresh Neon project**; the new
   connection string lives in user-secrets and the leaked credential is now dead. See `docs/SECRETS.md`
   ("Creating a fresh Neon project"). *Remaining:* the new DB is US-hosted — move to a Canadian-resident
   DB before real user data (item 2).
2. **Decide AWS region / data residency** (e.g. `ca-central-1`) before any deployment or real user data.
   *(Pending.)*
3. **`Jwt:Key` — done in dev.** Set via user-secrets (must be **≥ 32 bytes / 32+ chars**, or login/signup
   fail with HTTP 500 `IDX10720`). *Remaining:* set a production key via environment/Secrets Manager —
   the app refuses to start without one outside Development.
4. **Breaking API change (for the Flutter app, separate repo `mauriciolechuga/OpenFinance`):** all
   endpoints except `login`/`signup` and the webhook require a JWT, and the legacy `clients/{id}/...`
   routes enforce that `{id}` matches the token `sub`. The app must store the token from login/signup and
   send `Authorization: Bearer <token>`. *(Done — the Flutter app now stores the JWT and sends the
   bearer header via a central `ApiClient`; branch `phase1/flutter-jwt-and-link-flow`.)*

## 8. Notes / known issues to revisit

- ~~Login flow checks the password before the blocked-status check~~ — **fixed** in `AuthService`:
  blocked-status is now checked before the password so a blocked client can't keep burning attempts.
- Existing credential rows stored before password hashing will no longer verify — recreate test
  users (or reset the dev database) after this change. *(N/A on the fresh Neon DB, which starts empty.)*
- **Frontend JWT is in-memory only** (lost on app restart → re-login). Persisting it needs a native
  storage plugin (e.g. `flutter_secure_storage`), which on Windows requires enabling Developer Mode to
  build. The session API is shaped so a persistent store can drop in without touching callers.
- **Seeding dev data:** the aggregation DB is empty until a connection is linked (the MockProvider is
  the data source). `scripts/seed-dev-data.ps1` logs in as a test account, ensures banks exist, and
  calls `/connections/exchange` to populate accounts/holdings/transactions for local testing.
