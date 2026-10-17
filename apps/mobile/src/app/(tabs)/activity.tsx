import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { Text, View } from 'react-native';
import type { Payment, PaymentRequest, Wallet } from '@senzy/contracts';
import { ApiError } from '@senzy/contracts/client';
import { api } from '../../api/client';
import { useSession } from '../../session/Session';
import { Action, Field, Notice, Screen, styles } from '../../components/ui';

function parseAmountMinor(value: string): number | null {
  const match = /^(\d{1,5})(?:\.(\d{1,2}))?$/.exec(value.trim());
  if (!match) return null;
  const amount = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'));
  return amount > 0 && amount <= 1_000_000 ? amount : null;
}

function dollars(amountMinor: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amountMinor / 100);
}

function initials(name: string) {
  return name.split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase();
}

export default function Activity() {
  const { profile, setProfile } = useSession();
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [payments, setPayments] = useState<Payment[] | null>(null);
  const [requests, setRequests] = useState<PaymentRequest[] | null>(null);
  const [handle, setHandle] = useState('');
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [mode, setMode] = useState<'send' | 'request'>('send');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [attempt, setAttempt] = useState(0);

  const load = useCallback(async () => {
    const [nextWallet, nextPayments, nextRequests] = await Promise.all([
      api.wallet(),
      api.payments(),
      api.paymentRequests(),
    ]);
    setWallet(nextWallet);
    setPayments(nextPayments.items);
    setRequests(nextRequests.items);
  }, []);

  const reportError = useCallback((cause: unknown) => {
    setError(cause instanceof Error ? cause.message : 'Unable to complete that payment action.');
    if (cause instanceof ApiError && cause.status === 401) setProfile(null);
  }, [setProfile]);

  useFocusEffect(useCallback(() => {
    let active = true;
    setError('');
    void load()
      .then(() => { if (active) api.report('activity_viewed', 'activity'); })
      .catch(cause => { if (active) reportError(cause); });
    return () => { active = false; };
  }, [attempt, load, profile?.userId, reportError]));

  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await action();
      await load();
      setNotice(success);
    } catch (cause) {
      reportError(cause);
    } finally {
      setBusy(false);
    }
  }

  function submit() {
    const amountMinor = parseAmountMinor(amount);
    if (amountMinor === null) {
      setError('Enter a USD amount from $0.01 to $10,000.00 with at most two decimal places.');
      return;
    }
    const normalizedHandle = handle.trim();
    if (normalizedHandle.length < 3 || normalizedHandle.length > 20) {
      setError('Enter a handle between 3 and 20 characters.');
      return;
    }
    const action = mode === 'send'
      ? () => api.sendPayment({ recipientHandle: normalizedHandle, amountMinor, currency: 'USD', note })
      : () => api.createPaymentRequest({ payerHandle: normalizedHandle, amountMinor, currency: 'USD', note });
    void run(action, mode === 'send' ? 'Simulated payment sent.' : 'Payment request created.');
    setHandle('');
    setAmount('');
    setNote('');
  }

  const incoming = requests?.filter(request => request.payerUserId === profile?.userId && request.status === 'pending') ?? [];

  return <Screen>
    <Text style={styles.brand}>senzy</Text>
    <Text style={styles.badge}>Sandbox · no real money</Text>
    <View style={styles.profileHead}><Text style={styles.profileAvatar}>{initials(profile?.displayName ?? '')}</Text><View><Text style={styles.title}>Hello, {profile?.displayName}.</Text><Text style={styles.text}>@{profile?.handle}</Text></View></View>
    <View style={styles.balance}>
      <Text style={{ color: '#d4e5c8' }}>Simulated available balance</Text>
      <Text style={styles.amount}>{wallet ? dollars(wallet.availableMinor) : '—'}</Text>
      <Text style={{ color: '#d4e5c8' }}>Test funds only · no real money can move</Text>
    </View>

    <View style={styles.switcher}>
      <Action title="Send" disabled={busy || mode === 'send'} onPress={() => setMode('send')} />
      <Action title="Request" disabled={busy || mode === 'request'} onPress={() => setMode('request')} />
    </View>
    <View style={styles.card}>
      <Text style={styles.subtitle}>{mode === 'send' ? 'Send payment' : 'Request payment'}</Text>
      <Field label={mode === 'send' ? 'Recipient handle' : 'Payer handle'} value={handle} onChangeText={setHandle} autoCapitalize="none" autoCorrect={false} maxLength={20} placeholder="friend_handle" />
      <Field label="Amount (USD)" value={amount} onChangeText={setAmount} keyboardType="decimal-pad" placeholder="25.00" />
      <Field label="Note" value={note} onChangeText={setNote} maxLength={280} placeholder="What’s it for?" />
      <Action title={busy ? 'Working…' : mode === 'send' ? 'Send simulated payment' : 'Create request'} disabled={busy || (mode === 'send' && !wallet)} onPress={submit} />
    </View>

    <Notice>{error}</Notice>
    {notice ? <Text accessibilityLiveRegion="polite" style={styles.success}>{notice}</Text> : null}
    {error ? <Action title="Refresh" disabled={busy} onPress={() => setAttempt(value => value + 1)} /> : null}

    {incoming.length > 0 ? <>
      <Text style={styles.subtitle}>Requests for you</Text>
      {incoming.map(request => <View style={styles.card} key={request.id}>
        <Text style={styles.subtitle}>${request.requesterHandle} requested {dollars(request.amountMinor)}</Text>
        <Text style={styles.text}>{request.note || 'No note'}</Text>
        <Action title="Pay request" disabled={busy} onPress={() => { void run(() => api.acceptPaymentRequest(request.id), 'Request paid.'); }} />
      </View>)}
    </> : null}

    <Text style={styles.subtitle}>Payment history</Text>
    {!payments ? <Text style={styles.text}>Loading your wallet…</Text>
      : payments.length === 0 ? <Text style={styles.text}>No payments yet.</Text>
        : payments.map(payment => {
          const received = payment.recipientUserId === profile?.userId;
          return <View style={styles.card} key={payment.id}>
            <Text style={styles.subtitle}>{received ? `From $${payment.senderHandle}` : `To $${payment.recipientHandle}`}</Text>
            <Text style={styles.paymentAmount}>{received ? '+' : '−'}{dollars(payment.amountMinor)}</Text>
            <Text style={styles.text}>{payment.note || 'No note'} · {payment.status}</Text>
            <Text style={styles.text}>{new Date(payment.createdAt).toLocaleDateString()}</Text>
            {!received && payment.status === 'posted' ? <Action title="Reverse payment" disabled={busy} onPress={() => { void run(() => api.reversePayment(payment.id), 'Payment reversed.'); }} /> : null}
          </View>;
        })}
  </Screen>;
}
