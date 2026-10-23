# Senzy web client

The React client applies the same pragmatic layered modular-monolith style on the frontend: organize primarily by feature, with a small application layer for composition and shared infrastructure for API access.

```text
src/
  app/                  Application composition, state orchestration, and navigation
  api/                  Configured shared-contract client
  features/
    activity/           Simulated wallet, payment, request, and history presentation
    funding/            Sandbox account setup, funding, operations, and notifications
    auth/               Registration and sign-in presentation
    profile/            Profile presentation
  shared/
    components/         Reusable, feature-neutral UI
```

Each module exposes its supported public API through `index.ts`. Import from the module boundary—for example, `../features/auth`—instead of reaching into another module's implementation files. Keep barrels small and explicit: named exports only, no `export *`, and no application-wide barrel that hides feature ownership or encourages dependency cycles.

Prefer named React component and utility exports. Feature components own form and presentation concerns; `app/App.tsx` coordinates API calls and cross-feature session state. Promote code to `shared/` only after it is genuinely feature-neutral and reused. Network request/response types come from `@senzy/contracts`; do not redefine them locally.

The dependency direction is `main → app → features → api/shared`. A feature must not import from `app` or reach into another feature's implementation. Start with a component and local helpers; introduce feature hooks, services, or subfolders only when state or behavior becomes complex enough to justify them.

Run the web checks from the repository root:

```sh
pnpm --filter @senzy/web check
pnpm build:web
```
