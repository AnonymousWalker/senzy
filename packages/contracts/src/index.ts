import { z } from 'zod';
export type { paths, components, operations } from './generated/api.js';

export const healthResponseSchema = z.object({
  status: z.literal('ok'),
  service: z.literal('senzy-api'),
  timestamp: z.iso.datetime({ offset: true }),
});

export const transferRequestSchema = z.object({
  recipientHandle: z.string().trim().min(3).max(20),
  amountMinor: z.number().int().positive().max(1_000_000),
  currency: z.literal('USD'),
  note: z.string().trim().max(280).default(''),
});

export const createPaymentRequestSchema = z.object({
  payerHandle: z.string().trim().min(3).max(20),
  amountMinor: z.number().int().positive().max(1_000_000),
  currency: z.literal('USD'),
  note: z.string().trim().max(280).default(''),
});

export const walletSchema = z.object({
  availableMinor: z.number().int().safe(), settledMinor: z.number().int().safe(),
  currency: z.literal('USD'), isSimulated: z.literal(true),
});

export const paymentSchema = z.object({
  id: z.uuid(), kind: z.enum(['send', 'request']), status: z.enum(['pending', 'posted', 'failed', 'reversed']),
  senderUserId: z.uuid(), senderHandle: z.string(), recipientUserId: z.uuid(), recipientHandle: z.string(),
  amountMinor: z.number().int().positive().safe(), currency: z.literal('USD'), note: z.string(),
  createdAt: z.iso.datetime({ offset: true }), reversedAt: z.iso.datetime({ offset: true }).nullable(),
});

export const paymentListSchema = z.object({ items: z.array(paymentSchema) });

export const paymentRequestSchema = z.object({
  id: z.uuid(), status: z.enum(['pending', 'paid', 'declined', 'cancelled']),
  requesterUserId: z.uuid(), requesterHandle: z.string(), payerUserId: z.uuid(), payerHandle: z.string(),
  amountMinor: z.number().int().positive().safe(), currency: z.literal('USD'), note: z.string(),
  paymentId: z.uuid().nullable(), createdAt: z.iso.datetime({ offset: true }), resolvedAt: z.iso.datetime({ offset: true }).nullable(),
});

export const paymentRequestListSchema = z.object({ items: z.array(paymentRequestSchema) });

export const bankingInstitutionSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1),
  logoUrl: z.url().nullable(),
});

export const institutionsResponseSchema = z.object({
  provider: z.literal('stub-banking'),
  sandbox: z.literal(true),
  institutions: z.array(bankingInstitutionSchema),
});

export const createBankLinkSessionRequestSchema = z
  .object({
  })
  .strict();

export const bankLinkSessionSchema = z.object({
  provider: z.literal('stub-banking'),
  sandbox: z.literal(true),
  linkToken: z.string().startsWith('stub_link_'),
  expiresAt: z.iso.datetime({ offset: true }),
});

export const connectBankAccountRequestSchema = z
  .object({
    linkToken: z.string().startsWith('stub_link_'),
    institutionId: z.string().min(1),
    accountKind: z.enum(['checking', 'savings']),
    displayName: z.string().trim().min(1).max(80),
  })
  .strict();

export const externalBankAccountSchema = z.object({
  id: z.uuid(),
  provider: z.literal('stub-banking'),
  sandbox: z.literal(true),
  institutionId: z.string(),
  institutionName: z.string(),
  displayName: z.string(),
  mask: z.string().regex(/^\d{4}$/),
  accountKind: z.enum(['checking', 'savings']),
  status: z.literal('linked'),
  createdAt: z.iso.datetime({ offset: true }),
});

export type HealthResponse = z.infer<typeof healthResponseSchema>;
export type TransferRequest = z.infer<typeof transferRequestSchema>;
export type CreatePaymentRequest = z.infer<typeof createPaymentRequestSchema>;
export type Wallet = z.infer<typeof walletSchema>;
export type Payment = z.infer<typeof paymentSchema>;
export type PaymentList = z.infer<typeof paymentListSchema>;
export type PaymentRequest = z.infer<typeof paymentRequestSchema>;
export type PaymentRequestList = z.infer<typeof paymentRequestListSchema>;
export type BankingInstitution = z.infer<typeof bankingInstitutionSchema>;
export type CreateBankLinkSessionRequest = z.infer<typeof createBankLinkSessionRequestSchema>;
export type BankLinkSession = z.infer<typeof bankLinkSessionSchema>;
export type ConnectBankAccountRequest = z.infer<typeof connectBankAccountRequestSchema>;
export type ExternalBankAccount = z.infer<typeof externalBankAccountSchema>;

export const profileSchema = z.object({
  userId: z.uuid(), displayName: z.string(), handle: z.string(), createdAt: z.iso.datetime({ offset: true }),
});
export const configurationSchema = z.object({
  registrationEnabled: z.boolean(), activityEnabled: z.boolean(), telemetryEnabled: z.boolean(), sandboxBankingEnabled: z.boolean(),
});
export const activitySchema = z.object({
  userId: z.uuid(), isFixture: z.literal(true), items: z.array(z.object({
    id: z.string(), title: z.string(), description: z.string(), kind: z.enum(['sent', 'received']),
    amountMinor: z.number().int().nonnegative().safe(), currency: z.literal('USD'), occurredAt: z.iso.datetime({ offset: true }),
  })),
});
export const tokenSchema = z.object({ tokenType: z.string(), accessToken: z.string(), expiresIn: z.number(), refreshToken: z.string() });
export const csrfSchema = z.object({ token: z.string() });
export const problemSchema = z.object({ detail: z.string().optional(), title: z.string().optional(), code: z.string().optional(), traceId: z.string().optional() });
export type Profile = z.infer<typeof profileSchema>;
export type ClientConfiguration = z.infer<typeof configurationSchema>;
export type ActivityFeed = z.infer<typeof activitySchema>;
