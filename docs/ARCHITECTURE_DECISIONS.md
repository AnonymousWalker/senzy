# Phase 0 architecture decisions

Status: accepted implementation direction for the simulated-money prototype. These decisions do not authorize real-money operation. See the [API architecture](../api-server/README.md) for code organization and the [product plan](PRODUCT_PLAN.md) for sequencing.

## ADR 001 — Pragmatic layered modular monolith

Use ASP.NET Core Minimal APIs, one deployable backend, one PostgreSQL database, and business-feature modules. Within a module, apply a pragmatic dependency direction: API/endpoint → application/use case → domain, with infrastructure implementing persistence and external-provider concerns. Modules are the primary ownership boundary; layers clarify responsibilities inside a module rather than creating application-wide folders that mix unrelated features.

Keep endpoints thin. They bind and validate HTTP input, authorize the actor, call a use case, and translate its result. Application handlers coordinate transactions, ownership, and workflows. Domain types enforce business invariants without depending on ASP.NET Core, EF Core, or provider SDKs. Infrastructure contains EF mappings, migrations, SQL, and external adapters. Simple CRUD-style features may combine an endpoint and its small application operation until real rules appear; do not create empty layers, pass-through services, or interfaces solely to satisfy a diagram.

Payment and Ledger are separate logical modules participating in the same database transaction. Cross-module calls use explicit application/domain contracts rather than reaching into another module's tables or endpoint DTOs. A shared `SenzyDbContext` is an implementation convenience, not shared ownership. Start with one application project and separate tests; extract assemblies only when compile-time enforcement, independent reuse, or deployment provides concrete value. Do not introduce a broker, distributed transactions, or independently deployed payment services in the prototype.

`Program.cs` is the composition root. New features go under `Features/`; cross-cutting implementations belong under `Infrastructure/` or narrowly scoped `Common/` code. Use ordinary dependency injection and direct method calls. Do not add MediatR, generic repositories, a unit-of-work wrapper around EF Core, or an interface for every class by default.

The React client follows the same pragmatic direction: `app` composes features, features own their screens and local presentation behavior, `api` owns remote access, and `shared` contains only feature-neutral code. Feature modules expose small named public APIs through explicit `index.ts` files and do not reach into another feature's private files.

## ADR 002 — Authentication and ownership

For Phase 1, use ASP.NET Core Identity with PostgreSQL persistence for user accounts, password hashing, lockout, confirmation, and recovery. Use its built-in API bearer-token flow for the first-party Expo client and secure HTTP-only cookies for the same-origin web client. The built-in bearer tokens are opaque first-party tokens, not an OAuth/OIDC authorization server or JWTs for third-party clients.

Web mutations require anti-forgery protection and origin checks. Production cookies require HTTPS, Secure, HttpOnly, and an explicit SameSite policy. Never put long-lived credentials in browser local storage. Mobile refresh credentials use Expo SecureStore on native platforms. Disable public registration until confirmation/recovery delivery is configured for a deployed pilot.

Derive the actor from the authenticated principal. Resolve ownership inside each use case; never trust `userId` in a request to grant access. Require authentication by default for user features, with explicit public exceptions. Rate-limit login and recovery and return non-enumerating errors. Require recent authentication for credential and funding changes; design MFA and session revocation before any external pilot. Verify refresh, logout, expiry, and lost-device behavior in Phase 1 tests.

This keeps identity in the existing stack without adding a paid vendor or operating an OIDC server now. Revisit with a standards-based external identity provider if federation or third-party clients become requirements. Phase 1 implements this decision.

Persist and protect the ASP.NET Data Protection key ring across restarts and replicas. Cookie logout does not immediately revoke an already-issued mobile bearer token: use short access-token lifetimes and implement an explicit session/revocation check for sensitive operations before pilot. Recovery and password changes must invalidate refresh capability; test these guarantees rather than assuming logout alone provides them.

## ADR 003 — OpenAPI is the HTTP contract

ASP.NET endpoint metadata and named request/response types are authoritative. Export OpenAPI from the real application in an in-memory development test host, normalize it, and generate TypeScript types with pinned `openapi-typescript` tooling. Commit both generated artifacts. CI regenerates them and fails on drift.

Both frontend workspaces depend on `@senzy/contracts` and use its shared runtime client. Web uses cookie mode; mobile uses bearer mode with a platform token store. Runtime Zod schemas validate untrusted responses and are checked against representative live responses by the contract workflow; generated TypeScript alone does not validate network data. Phase 2 added live wallet, payment, payment-request, and private history contracts; Phase 3 adds funding, notifications, operations status, and combined transaction history consumed by both clients.

New versioned endpoints use stable operation IDs, explicit response metadata, and Problem Details for errors. Additive compatible changes remain in `/v1`; breaking changes require a new API version or a coordinated pre-release contract update before consumers ship. The shared client implements the Phase 2 payment and Phase 3 operations contracts.

## ADR 004 — Persistence and posting

Use PostgreSQL and EF Core with the Npgsql provider for Phase 1 schema migrations and normal entity persistence. Explicit SQL through Npgsql is acceptable for locking or posting operations where it makes correctness clearer. All operations within a posting must share the same connection and transaction; do not mix independently committing units of work. Migration tooling is Phase 1 work. Reviewed deployment migrations run once per release, not from every API replica on startup.

Represent money with integer minor units plus currency. Begin with USD simulation and no FX; launch market and real currency coverage remain product decisions before real-money planning. Use `long` in persistence and bound API amounts to JavaScript-safe integers. Use account/currency uniqueness, foreign keys, positive-entry checks, and database-enforced uniqueness for scoped idempotency keys. Phase 2 uses transaction-scoped advisory locks for command idempotency and deterministic `FOR UPDATE` wallet locks for balance enforcement; real PostgreSQL concurrency tests validate the protocol.

The payment use case atomically commits payment state, the idempotency result, balanced ledger entries, and an outbox event. Scope idempotency to actor and operation, persist a canonical request fingerprint, replay matching commands, and reject key reuse with changed inputs. Concurrent first attempts must resolve through database uniqueness/locking. Do not automatically expire financial idempotency records until a retention policy and replay window are defined.

## ADR 005 — Payment lifecycle

| From | To | Meaning |
| --- | --- | --- |
| New command | `posted` | Immediate internal simulated transfer committed with ledger entries. |
| New command | `pending` | Durable intent awaits an external workflow; no settled posting is implied. |
| `pending` | `posted` | Settlement and the required ledger posting commit successfully. |
| `pending` | `failed` | Workflow terminates without a settled posting; release any holds atomically. |
| `posted` | `reversed` | A linked, balanced compensating transaction commits; original entries remain immutable. |

`failed` and `reversed` are terminal. Do not turn a posted payment into failed, mutate its entries, or retry a failed payment by reusing its identity. Validation/auth failures do not create a payment. A database failure does not imply a durable failed record; a retry checks the committed idempotency outcome. Network timeouts can leave the client uncertain, so it retries with the same key or queries status.

Start with full reversals only. Partial reversals and available-balance holds require a separate design before implementation. Payment requests (invitations to pay) have their own lifecycle; accepting one invokes the payment command and does not post money twice.

## ADR 006 — Banking stub and events

Keep `IBankingProvider` as the external-provider boundary. The in-memory stub is available only in Development, accepts generated sandbox account choices, rejects raw bank account/routing fields, binds short-lived link tokens to a user, and permits only one successful token use. Accounts and tokens disappear on restart. It is not a verified funding source and must never be exposed through a public Development deployment.

External provider calls cannot commit atomically with PostgreSQL. Future integrations require pending states, durable retries, verified callbacks, and reconciliation. Phase 3 persists a `SandboxProviderTransfer` beside the funding command solely as a durable reconciliation fixture; this shortcut is not a design for real provider settlement. Outbox events commit with postings, then a leased PostgreSQL worker delivers them to database-idempotent notification handlers. Introduce Redis and Kafka only after measurement shows a need. Neither owns authoritative financial data.

## References

- [ASP.NET Core Identity API endpoints](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)
- [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [OpenAPI TypeScript generator](https://openapi-ts.dev/node)
