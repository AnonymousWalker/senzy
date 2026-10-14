# Shared API contracts

ASP.NET OpenAPI is the source of truth for HTTP request and response types. `openapi.json` and `src/generated/api.ts` are generated and committed. Web and mobile depend on this package; import `operations`, `paths`, or `components` for generated types.

`src/client.ts` is the shared runtime client. Browser consumers use cookie mode with automatic CSRF handling; native consumers use bearer mode with access-token refresh and provide an OS-backed refresh-token store. The client exposes typed identity, profile, wallet, payment, request, activity, configuration, banking, and telemetry operations while `src/index.ts` validates untrusted responses with Zod. Payment commands create a fresh idempotency key by default and allow a caller to retain and reuse a key after an uncertain outcome.

From the repository root:

```sh
pnpm contracts:generate
pnpm contracts:check
```

The generator starts the actual API in an in-memory Development test host, with no listening port and no database requirement. It removes the host-specific server URL, sorts JSON object keys, and generates TypeScript using the pinned tool version. Check mode compares both artifacts without modifying them and fails on drift. CI runs check mode before frontend checks.

When changing an endpoint, give it a stable operation ID and explicit request/response metadata, regenerate, inspect the diff, update consumers and runtime validators, then commit both artifacts. Do not edit generated files directly.

Zod schemas in `src/index.ts` validate runtime data; TypeScript types do not. The contract command checks representative responses from the running test host against these schemas. This is a baseline check, not proof of complete schema equivalence; add cases for new endpoints and variants. The simulated payment and payment-request schemas correspond to the live authenticated Phase 2 endpoints.
