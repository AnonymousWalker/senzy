import { useState } from 'react'
import type { FormEvent } from 'react'
import type { CreatePaymentRequest, Payment, PaymentRequest, Profile, TransferRequest, Wallet } from '@senzy/contracts'

interface ActivityViewProps {
  busy: boolean
  payments: Payment[] | null
  profile: Profile
  requests: PaymentRequest[] | null
  wallet: Wallet | null
  onAcceptRequest: (id: string) => void
  onRequest: (input: CreatePaymentRequest) => void
  onReverse: (id: string) => void
  onSend: (input: TransferRequest) => void
}

function parseAmountMinor(value: string): number | null {
  const match = /^(\d{1,5})(?:\.(\d{1,2}))?$/.exec(value.trim())
  if (!match) return null
  const amount = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'))
  return amount > 0 && amount <= 1_000_000 ? amount : null
}

function dollars(amountMinor: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amountMinor / 100)
}

function initials(name: string) {
  return name.split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase()
}

export function ActivityView({ busy, payments, profile, requests, wallet, onAcceptRequest, onRequest, onReverse, onSend }: ActivityViewProps) {
  const [formError, setFormError] = useState('')

  function submit(event: FormEvent<HTMLFormElement>, kind: 'send' | 'request') {
    event.preventDefault()
    setFormError('')
    const form = new FormData(event.currentTarget)
    const amountMinor = parseAmountMinor(String(form.get('amount')))
    if (amountMinor === null) {
      setFormError('Enter a USD amount from $0.01 to $10,000.00 with at most two decimal places.')
      return
    }
    const note = String(form.get('note'))
    const handle = String(form.get('handle'))
    if (kind === 'send') onSend({ recipientHandle: handle, amountMinor, currency: 'USD', note })
    else onRequest({ payerHandle: handle, amountMinor, currency: 'USD', note })
    event.currentTarget.reset()
  }

  const incoming = requests?.filter(request => request.payerUserId === profile.userId && request.status === 'pending') ?? []

  return <section className="payment-page">
    <p className="eyebrow">YOUR MONEY, YOUR PEOPLE</p>
    <div className="profile-head"><span className="profile-avatar">{initials(profile.displayName)}</span><div><h1>Hello, {profile.displayName}.</h1><p className="handle">@{profile.handle}</p></div></div>
    <div className="balance payment-hero"><div className="balance-top"><span>Available balance</span><span className="sandbox-label">Simulated</span></div><strong>{wallet ? dollars(wallet.availableMinor) : '—'}</strong><small>Test funds only · no real money can move</small></div>

    {formError && <p className="alert" role="alert">{formError}</p>}
    <div className="payment-actions">
      <form className="action-card" onSubmit={event => submit(event, 'send')}>
        <div className="action-heading"><span className="action-mark">$</span><div><h2>Pay</h2><small>Send money to a friend</small></div></div>
        <label>Recipient’s Senzy Tag<input name="handle" required minLength={3} maxLength={20} placeholder="friend_tag" autoCapitalize="none" /></label>
        <label>Amount (USD)<input name="amount" required inputMode="decimal" placeholder="25.00" /></label>
        <label>Note<input name="note" maxLength={280} placeholder="What’s it for?" /></label>
        <button disabled={busy || !wallet}>Pay someone</button>
      </form>
      <form className="action-card" onSubmit={event => submit(event, 'request')}>
        <div className="action-heading"><span className="action-mark request-mark">↙</span><div><h2>Request</h2><small>Ask a friend for money</small></div></div>
        <label>Payer’s Senzy Tag<input name="handle" required minLength={3} maxLength={20} placeholder="friend_tag" autoCapitalize="none" /></label>
        <label>Amount (USD)<input name="amount" required inputMode="decimal" placeholder="25.00" /></label>
        <label>Note<input name="note" maxLength={280} placeholder="What’s it for?" /></label>
        <button disabled={busy}>Request money</button>
      </form>
    </div>

    {incoming.length > 0 && <><div className="section-title"><h2>Requests for you</h2><span className="sandbox-label">Action needed</span></div>
      <ul className="request-list">{incoming.map(request => <li key={request.id}>
        <div><strong>@{request.requesterHandle} requested {dollars(request.amountMinor)}</strong><p>{request.note || 'No note'}</p></div>
        <button disabled={busy} onClick={() => onAcceptRequest(request.id)}>Pay request</button>
      </li>)}</ul></>}

    <div className="section-title"><h2>Payment history</h2><span className="sandbox-label">Simulated ledger</span></div>
    {!payments ? <p role="status">Loading your wallet…</p>
      : payments.length === 0 ? <p>No payments yet.</p>
        : <ul className="activity-list">{payments.map(payment => {
          const received = payment.recipientUserId === profile.userId
          return <li key={payment.id}>
            <span className="activity-icon" aria-hidden="true">{initials(received ? payment.senderHandle : payment.recipientHandle)}</span>
            <div><h3>{received ? `From @${payment.senderHandle}` : `To @${payment.recipientHandle}`}</h3><p>{payment.note || 'No note'} · {payment.status}</p><time dateTime={payment.createdAt}>{new Date(payment.createdAt).toLocaleDateString()}</time></div>
            <strong className={received ? 'received' : ''}>{received ? '+' : '−'}{dollars(payment.amountMinor)}</strong>
            {!received && payment.status === 'posted' && <button className="quiet compact" disabled={busy} onClick={() => onReverse(payment.id)}>Reverse</button>}
          </li>
        })}</ul>}
  </section>
}
