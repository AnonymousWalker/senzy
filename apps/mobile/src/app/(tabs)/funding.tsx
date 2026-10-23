import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { Text, View } from 'react-native';
import type { BankingInstitution, ExternalBankAccount, FundingTransfer, Notification, OperationsStatus } from '@senzy/contracts';
import { ApiError } from '@senzy/contracts/client';
import { api } from '../../api/client';
import { Action, Field, Notice, Screen, styles } from '../../components/ui';
import { useSession } from '../../session/Session';

function parseAmountMinor(value: string): number | null {
  const match = /^(\d{1,5})(?:\.(\d{1,2}))?$/.exec(value.trim());
  if (!match) return null;
  const amount = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'));
  return amount > 0 && amount <= 1_000_000 ? amount : null;
}

function dollars(amountMinor: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amountMinor / 100);
}

export default function Funding() {
  const { config, setProfile } = useSession();
  const [institutions, setInstitutions] = useState<BankingInstitution[]>([]);
  const [accounts, setAccounts] = useState<ExternalBankAccount[]>([]);
  const [transfers, setTransfers] = useState<FundingTransfer[]>([]);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [operations, setOperations] = useState<OperationsStatus | null>(null);
  const [amount, setAmount] = useState('');
  const [direction, setDirection] = useState<'cash_in' | 'cash_out'>('cash_in');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const reportError = useCallback((cause: unknown) => {
    setError(cause instanceof Error ? cause.message : 'Unable to complete that funding action.');
    if (cause instanceof ApiError && cause.status === 401) setProfile(null);
  }, [setProfile]);

  const load = useCallback(async () => {
    const [nextInstitutions, nextAccounts, nextTransfers, nextNotifications, nextOperations] = await Promise.all([
      api.bankingInstitutions(), api.bankAccounts(), api.fundingTransfers(), api.notifications(), api.operationsStatus(),
    ]);
    setInstitutions(nextInstitutions.institutions);
    setAccounts(nextAccounts.accounts);
    setTransfers(nextTransfers.items);
    setNotifications(nextNotifications.items);
    setOperations(nextOperations);
  }, []);

  useFocusEffect(useCallback(() => {
    if (!config?.sandboxBankingEnabled) return () => {};
    let active = true;
    setError('');
    void load().catch(cause => { if (active) reportError(cause); });
    return () => { active = false; };
  }, [config?.sandboxBankingEnabled, load, reportError]));

  async function run(action: () => Promise<void>, success: string) {
    setBusy(true); setError(''); setNotice('');
    try { await action(); await load(); setNotice(success); }
    catch (cause) { reportError(cause); }
    finally { setBusy(false); }
  }

  function link() {
    const institution = institutions[0];
    if (!institution) return;
    void run(async () => {
      const session = await api.createBankLinkSession();
      await api.connectBankAccount({ linkToken: session.linkToken, institutionId: institution.id, displayName: 'Mobile checking', accountKind: 'checking' });
    }, 'Sandbox account linked. No real bank was contacted.');
  }

  function fund() {
    const amountMinor = parseAmountMinor(amount);
    const account = accounts[0];
    if (amountMinor === null) { setError('Enter a USD amount from $0.01 to $10,000.00.'); return; }
    if (!account) { setError('Link a sandbox account first.'); return; }
    void run(async () => { await api.createFundingTransfer({ externalAccountId: account.id, direction, amountMinor, currency: 'USD' }); },
      'Simulated funding transfer posted.');
    setAmount('');
  }

  if (!config?.sandboxBankingEnabled) return <Screen><Text style={styles.title}>Funding unavailable</Text><Text style={styles.text}>Sandbox banking is disabled in this environment.</Text></Screen>;
  return <Screen>
    <Text style={styles.brand}>senzy</Text>
    <Text style={styles.badge}>Sandbox · no real money</Text>
    <Text style={styles.title}>Funding & controls</Text>
    <Text style={styles.text}>Generated accounts and simulated transfers only. No bank is contacted.</Text>

    <View style={styles.card}>
      <Text style={styles.subtitle}>Funding source</Text>
      {accounts.length > 0 ? <Text style={styles.text}>{accounts[0].displayName} · •••• {accounts[0].mask}</Text> : <Text style={styles.text}>No account linked.</Text>}
      <Action title={busy ? 'Working…' : 'Link generated sandbox account'} disabled={busy || institutions.length === 0} onPress={link} />
    </View>

    <View style={styles.card}>
      <Text style={styles.subtitle}>Move test funds</Text>
      <View style={styles.switcher}>
        <Action title="Cash in" disabled={busy || direction === 'cash_in'} onPress={() => setDirection('cash_in')} />
        <Action title="Cash out" disabled={busy || direction === 'cash_out'} onPress={() => setDirection('cash_out')} />
      </View>
      <Field label="Amount (USD)" value={amount} onChangeText={setAmount} keyboardType="decimal-pad" placeholder="25.00" />
      <Action title={busy ? 'Working…' : 'Post simulated transfer'} disabled={busy || accounts.length === 0} onPress={fund} />
    </View>

    <View style={styles.card}>
      <Text style={styles.subtitle}>Wallet controls</Text>
      <Text style={styles.text}>{operations ? `${dollars(operations.dailyOutboundUsedMinor)} of ${dollars(operations.dailyOutboundLimitMinor)} daily limit used.` : 'Loading…'}</Text>
      {operations?.latestReconciliation ? <Text style={styles.text}>Reconciliation: {operations.latestReconciliation.status.replace('_', ' ')}</Text> : null}
      <Action title={operations?.isFrozen ? 'Unfreeze wallet' : 'Freeze wallet'} disabled={busy || !operations}
        onPress={() => { void run(async () => { if (operations?.isFrozen) await api.unfreezeWallet(); else await api.freezeWallet(); }, operations?.isFrozen ? 'Wallet unfrozen.' : 'Wallet frozen.'); }} />
    </View>

    <Notice>{error}</Notice>
    {notice ? <Text accessibilityLiveRegion="polite" style={styles.success}>{notice}</Text> : null}
    <Text style={styles.subtitle}>Funding history</Text>
    {transfers.length === 0 ? <Text style={styles.text}>No funding transfers yet.</Text> : transfers.map(transfer => <View style={styles.card} key={transfer.id}>
      <Text style={styles.subtitle}>{transfer.direction === 'cash_in' ? 'Cash in' : 'Cash out'}</Text>
      <Text style={styles.paymentAmount}>{transfer.direction === 'cash_in' ? '+' : '−'}{dollars(transfer.amountMinor)}</Text>
      <Text style={styles.text}>{transfer.status} · {new Date(transfer.createdAt).toLocaleString()}</Text>
    </View>)}
    <Text style={styles.subtitle}>Notifications</Text>
    {notifications.length === 0 ? <Text style={styles.text}>New committed events appear here after outbox delivery.</Text> : notifications.map(item => <View style={styles.card} key={item.id}>
      <Text style={styles.subtitle}>{item.title}</Text><Text style={styles.text}>{item.body}</Text>
    </View>)}
  </Screen>;
}
