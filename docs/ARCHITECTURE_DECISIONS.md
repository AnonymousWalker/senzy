# Architecture decisions

Status: accepted direction for the simulated-money prototype and the modular-monolith refactor agreed on 2026-10-23. ADRs describe decisions, not proof of implementation. ADRs 007–010 are accepted refactor targets; the current implementation remains one application project with shared persistence. See the [API architecture](../api-server/README.md) for current behavior, the [refactor guide](MODULAR_MONOLITH_REFACTOR.md) for migration steps and acceptance criteria, and the [product plan](PRODUCT_PLAN.md) for product delivery gates. These decisions do not authorize real-money operation or public deployment.

## ADR 001 — Pragmatic layered modular monolith

Use ASP.NET Core Minimal APIs, one deployable backend, one PostgreSQL database, and business-feature modules. Within a module, apply a pragmatic dependency direction: API/endpoint → application/use case → domain, with infrastructure implementing persistence and external-provider concerns. Modules are the primary ownership boundary; layers clarify responsibilities inside a module rather than creating application-wide folders that mix unrelated features.

Keep endpoints thin. They bind and validate HTTP input, authorize the actor, call a use case, and translate its result. Application handlers coordinate transactions, ownership, and workflows. Domain types enforce business invariants without depending on ASP.NET Core, EF Core, or provider SDKs. Infrastructure contains EF mappings, migrations, SQL, and external adapters. Simple CRUD-style features may combine an endpoint and its small application operation until real rules appear; do not create empty layers, pass-through services, or interfaces solely to satisfy a diagram.

Payment and Ledger are separate logical modules participating in the same database transaction. Cross-module calls use explicit application/domain contracts rather than reaching into another module's tables or endpoint DTOs. A shared `SenzyDbContext` is an implementation convenience, not shared ownership. The current implementation uses one application project and separate tests. The accepted refactor incrementally extracts module assemblies to enforce financial ownership at compile time, while retaining one deployable application; see ADR 007. Do not introduce a broker, distributed transactions, or independently deployed payment services in the prototype.

`Program.cs` is the composition root. New features go under `Features/`; cross-cutting implementations belong under `Infrastructure/` or narrowly scoped `Common/` code. Use ordinary dependency injection and direct method calls. Do not add MediatR, generic repositories, a unit-of-work wrapper around EF Core, or an interface for every class by default.

The React client follows the same pragmatic direction: `app` composes features, features own their screens and local presentation behavior, `api` owns remote access, and `shared` contains only feature-neutral code. Feature modules expose small named public APIs through explicit `index.ts` files and do not reach into another feature's private files.

## ADR 002 — Authentication and ownership

For Phase 1, use ASP.NET Core Identity with PostgreSQL persistence for user accounts, password hashing, lockout, confirmation, and recovery. Use its built-in API bearer-token flow for the first-party Expo client and secure HTTP-only cookies for the same-origin web client. The built-in bearer tokens are opaque first-party tokens, not an OAuth/OIDC authorization server or JWTs for third-party clients.

Web mutations require anti-forgery protection and origin checks. Production cookies require HTTPS, Secure, HttpOnly, and an explicit SameSite policy. Never put long-lived credentials in browser local storage. Mobile refresh credentials use Expo SecureStore on native platforms. Disable public registration until confirmation/recovery delivery is configured for a deployed pilot.

Derive the actor from the authenticated principal. Resolve ownership inside each use case; never trust `userId` in a request to grant access. Require authentication by default for user features, with explicit public exceptions. Rate-limit login and recovery and return non-enumerating errors. Require recent authentication for credential and funding changes; design MFA and session revocation before any external pilot. Verify refresh, logout, expiry, and lost-device behavior in Phase 1 tests.

This keeps identity in the existing stack without adding a paid vendor or operating an OIDC server now. Revisit with a standards-based external identity provider if federation or third-party clients become requirements. Phase 1 implements this decision.

Persist and protect the ASP.NET Data Protection key ring across restarts and replicas. Cookie sign-out alone does not revoke an issued bearer token. The current implementation also rotates the security stamp on logout and validates it on authenticated requests; preserve this behavior across replicas and test it during the refactor. Recovery and password changes must invalidate refresh capability; test these guarantees rather than assuming logout alone provides them.

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

## ADR 007 — Enforceable module ownership within one application

Accepted refactor target, 2026-10-23. Retain one application artifact, one PostgreSQL system of record, and synchronous in-process module calls. Incrementally introduce module assemblies where they enforce ownership; this is not a service-extraction program. Small modules can remain compact. Do not add internal HTTP, a mediator, generic repositories, or an interface per class.

The target modules are Identity & Access, Profiles, Payments, Wallets, Ledger, Funding, Banking Integrations, Notifications, Reconciliation, and Activity/Reporting. The [refactor guide](MODULAR_MONOLITH_REFACTOR.md#target-module-ownership) defines ownership and the mapping from current code. Split current Operations responsibilities: Wallets owns freezes and spending controls, Reconciliation owns consistency checks and discrepancies, and Reporting owns combined history. Administrative routes invoke these owners rather than acquiring direct table-write access.

Payments owns payment lifecycle and payment idempotency. Funding owns funding lifecycle and funding idempotency. Both invoke Wallets for policy-controlled movements; Wallets invokes Ledger for balanced posting. Ledger owns accounting records and balance projections. Profiles exposes recipient resolution. Notifications owns its consumers; outbox infrastructure owns delivery mechanics. No module may change another module's records through its persistence entities.

Expose small public application contracts and module registration entry points; keep implementations and persistence records internal as assembly boundaries are introduced. Internal commands/results/events are separate from HTTP DTOs and provider SDK types. Architecture tests enforce allowed dependencies, prevent cycles and forbidden implementation references, and keep domain code independent of ASP.NET, EF Core, and providers. Read-only joins are explicit, authorized projection contracts; they do not grant write ownership. An assembly boundary inside this process is an engineering control, not a security isolation boundary.

## ADR 008 — Module boundaries preserve atomic financial posting

Accepted refactor target. The initiating Payments or Funding application workflow owns the transaction. Wallets checks policy under the agreed database locks; Ledger constructs postings and updates its balance projection. Payment/funding state, ledger entries, balances, transactional policy usage where persisted, idempotency outcome, and outbox records commit together. Participating module operations never independently commit. Return success only after commit; an uncertain commit remains unknown until the durable idempotency outcome is established.

Keep the shared `SenzyDbContext` during the first refactor stages, but put its usage behind module-owned operations. Moving to separate module contexts is optional and requires an explicit shared connection/transaction plus PostgreSQL rollback and concurrency tests before adoption. Do not introduce a generic unit-of-work wrapper. Coordinate EF mappings at the composition root without making persistence entities a shared public API.

Use module-owned schemas or table conventions where useful; moving existing tables is a reviewed migration, not a prerequisite to separating behavior. Preserve foreign keys, uniqueness, immutable posting protections, and idempotency history. Wallets owns policy and locks; Ledger remains the sole owner of accounting writes. All movement paths and freeze/limit mutations must follow one documented locking protocol so an acknowledged freeze cannot race with a later policy check. Holds and additional currencies remain separate future designs.

## ADR 009 — Durable events and provider workflows

Accepted refactor target building on the implemented PostgreSQL outbox. Use synchronous calls for ownership checks, financial policy, and ledger posting. Use post-commit events for notifications, activity projections, and integrations that can tolerate delay. Authoritative balance and spending decisions must never depend on an eventually consistent projection.

Give events stable IDs, explicit payload versions, correlation/causation metadata, and minimal necessary private data. Support at-least-once delivery with consumer-specific database deduplication, bounded retries, retained failed deliveries, authorized replay, and observability. Multiple consumers require independent delivery/checkpoint state; a single global delivered flag must not acknowledge unfinished consumers. Move notification handling out of the infrastructure dispatcher into the Notifications module as that boundary is introduced. A broker is optional and requires measured need.

Keep the existing Development-only banking stub and durable provider fixtures explicitly simulated. A future provider integration requires durable pending workflow state, stable provider idempotency identifiers, authenticated/deduplicated callbacks, durable retries, and reconciliation. Do not hold wallet locks while making provider network calls. Timeouts are uncertain outcomes to query/reconcile, not evidence of failure. External calls cannot join the PostgreSQL transaction. This design does not authorize implementing or enabling real banking.

## ADR 010 — Replicas, workers, and operational accountability

Accepted refactor target. Scale the monolith through API replicas before considering service extraction. The same application artifact may support configurable API and worker roles, released together. Separate worker roles are operational options, not independently owned business services.

Before replica validation, coordinate scheduled reconciliation through database leases and unique scheduled-run identities; preserve outbox database claims and consumer deduplication. Share protected Data Protection keys, bound total database connections across instances, establish distributed or edge rate limiting where global limits are required, and configure trusted proxy handling explicitly. The current in-memory sandbox must either remain a documented single-instance development mode or be replaced by a reviewed durable sandbox implementation that preserves ownership, expiry, and single-use tokens.

Measure financial outcomes and commit outcomes separately from HTTP success. Add traces spanning commands and background delivery, low-cardinality operational metrics, durable sensitive-action audit records, and actionable discrepancy handling. Reconciliation must use a defined cutoff/consistent view so concurrent postings do not produce misleading findings. Financial corrections use authorized compensating postings; reconciliation never silently rewrites entries.

Use explicit deployment migrations, compatibility-aware rolling changes, graceful shutdown, and tested backup/PITR restoration. See [operating targets](OPERATING_TARGETS.md) for unproven capacity/recovery objectives and [threat model](THREAT_MODEL.md) for planned security controls. Passing a structural refactor does not establish production readiness.

## References

- [ASP.NET Core Identity API endpoints](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)
- [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [OpenAPI TypeScript generator](https://openapi-ts.dev/node)
