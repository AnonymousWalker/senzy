import { execFileSync } from 'node:child_process';
import { mkdtemp, readFile, writeFile, mkdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import openapiTS, { astToString } from 'openapi-typescript';
import { healthResponseSchema, institutionsResponseSchema, bankLinkSessionSchema,
  externalBankAccountSchema } from '../packages/contracts/src/index.ts';

const root = fileURLToPath(new URL('../', import.meta.url));
const temporary = await mkdtemp(join(tmpdir(), 'senzy-contract-'));
const check = process.argv.includes('--check');
try {
  const exported = join(temporary, 'openapi.json');
  const samplesPath = join(temporary, 'samples.json');
  execFileSync('dotnet', ['run', '--project', 'tools/Senzy.ContractExport', '--', exported, samplesPath], {
    cwd: root, stdio: 'inherit',
  });
  const samples = JSON.parse(await readFile(samplesPath, 'utf8'));
  healthResponseSchema.parse(samples.health);
  institutionsResponseSchema.parse(samples.institutions);
  bankLinkSessionSchema.parse(samples.session);
  externalBankAccountSchema.parse(samples.account);
  const document = JSON.parse(await readFile(exported, 'utf8'));
  // TestServer supplies its own host. Do not commit environment-specific servers.
  delete document.servers;
  const sorted = (value) => Array.isArray(value) ? value.map(sorted)
    : value && typeof value === 'object'
      ? Object.fromEntries(Object.keys(value).sort().map(key => [key, sorted(value[key])])) : value;
  const normalized = sorted(document);
  const artifacts = new Map([
    ['packages/contracts/openapi.json', JSON.stringify(normalized, null, 2) + '\n'],
    ['packages/contracts/src/generated/api.ts',
      '// Generated from ASP.NET OpenAPI. Run pnpm contracts:generate; do not edit.\n' +
      astToString(await openapiTS(normalized))],
  ]);
  for (const [relative, content] of artifacts) {
    const destination = join(root, relative);
    if (check) {
      const current = await readFile(destination, 'utf8').catch(() => '');
      if (current !== content) throw new Error(`${relative} is stale. Run pnpm contracts:generate.`);
    } else {
      await mkdir(join(destination, '..'), { recursive: true });
      await writeFile(destination, content);
    }
  }
  console.log(check ? 'OpenAPI and TypeScript contracts are current.' : 'Generated OpenAPI and TypeScript contracts.');
} finally {
  await rm(temporary, { recursive: true, force: true });
}
