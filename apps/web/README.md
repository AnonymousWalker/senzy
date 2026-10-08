# Senzy web

React + TypeScript preview of the Senzy payments product. It currently reads API health and sandbox institution data; it does not move money.

From the repository root, run `pnpm install`, `pnpm dev:api`, and `pnpm dev:web` in separate terminals. Open `http://localhost:5173`. The Vite development proxy forwards `/api` to `http://localhost:4001`.

Run `pnpm --filter @senzy/web check` for TypeScript and lint, and `pnpm --filter @senzy/web build` for a production bundle. The sandbox banking endpoint is development-only, so a deployed production build needs an authenticated production API and a different integration before showing live banking data.
