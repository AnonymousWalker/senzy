# Phase 3 verification

Status: implemented and locally verified on 2026-10-21. This record covers simulated funding and operations only; it does not authorize real banking or real-money movement.

## Delivered

- Development-only sandbox account selection and idempotent cash-in/cash-out commands.
- Balanced `FundingIn` and `FundingOut` ledger postings with a durable funding record, scoped command result, sandbox-provider counterpart, and outbox event in one PostgreSQL transaction.
- Private combined transaction history, funding history, notifications, wallet freeze controls, and a configurable daily outbound limit.
- A durable outbox dispatcher using PostgreSQL leases and `FOR UPDATE SKIP LOCKED`, exponential retry scheduling, delivery audit fields, and idempotent notification creation.
- Daily reconciliation plus an authenticated Development-only manual trigger. Reconciliation compares debit/credit totals, maintained account balances, payment postings/reversals, funding postings, and sandbox-provider records.
- Shared generated OpenAPI types, Zod response validation, and web/mobile Funding experiences.

## Verification run

The following passed locally against Node 24.15.0, .NET 10, and PostgreSQL 17:

```sh
SENZY_TEST_POSTGRES='Host=127.0.0.1;Port=5433;Database=senzy;Username=senzy;Password=senzy' dotnet test tests/Senzy.Api.Tests/Senzy.Api.Tests.csproj --no-restore
# 36 passed
```

The suite creates an isolated PostgreSQL schema per test host and applies every EF migration. Phase 3 tests verify matching idempotent retries, changed-input conflicts, actor-private funding/history, balanced funding entries, durable provider counterparts, zero-variance reconciliation, freeze/limit enforcement, idempotent outbox dispatch, and cross-user notification denial.

The final verification run also includes contract drift checks, repository TypeScript/.NET checks, web production build, mobile lint/export, and a pending-model check; their exact results are recorded in the completion handoff.

## Operational boundary

The banking provider and funding endpoints remain Development-only. Linked accounts are generated display data and disappear when the API restarts. Durable provider-transfer rows exist only to exercise reconciliation. The notification handler is an in-process outbox consumer; Kafka remains unnecessary until Phase 4 measurements justify it.
