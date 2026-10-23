import { useState } from 'react'
import type { FormEvent } from 'react'
import type { BankingInstitution, CreateFundingTransfer, ExternalBankAccount, FundingTransfer, Notification, OperationsStatus } from '@senzy/contracts'

interface FundingViewProps {
  accounts: ExternalBankAccount[]
  busy: boolean
  institutions: BankingInstitution[]
  notifications: Notification[]
  operations: OperationsStatus | null
  transfers: FundingTransfer[]
  onFunding: (input: CreateFundingTransfer) => void
  onLink: (institutionId: string, displayName: string, accountKind: 'checking' | 'savings') => void
  onSetFrozen: (frozen: boolean) => void
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

export function FundingView({ accounts, busy, institutions, notifications, operations, transfers, onFunding, onLink, onSetFrozen }: FundingViewProps) {
  const [error, setError] = useState('')

  function link(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    onLink(String(form.get('institutionId')), String(form.get('displayName')), String(form.get('accountKind')) as 'checking' | 'savings')
  }

  function fund(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    const form = new FormData(event.currentTarget)
    const amountMinor = parseAmountMinor(String(form.get('amount')))
    if (amountMinor === null) {
      setError('Enter a USD amount from $0.01 to $10,000.00.')
      return
    }
    onFunding({ externalAccountId: String(form.get('accountId')), direction: String(form.get('direction')) as 'cash_in' | 'cash_out', amountMinor, currency: 'USD' })
    event.currentTarget.reset()
  }

  return <section className="payment-page">
    <p className="eyebrow">SANDBOX MONEY TOOLS</p>
    <h1>Funding & controls</h1>
    <p className="muted">Every account and transfer here is simulated. No bank is contacted and no real money moves.</p>

    <div className="operations-grid">
      <div className="action-card">
        <div className="action-heading"><span className="action-mark">🏦</span><div><h2>Funding source</h2><small>Link generated sandbox data</small></div></div>
        <form onSubmit={link}>
          <label>Sandbox institution<select name="institutionId" required>{institutions.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label>Account name<input name="displayName" required maxLength={80} defaultValue="Everyday checking" /></label>
          <label>Account type<select name="accountKind"><option value="checking">Checking</option><option value="savings">Savings</option></select></label>
          <button disabled={busy || institutions.length === 0}>Link sandbox account</button>
        </form>
      </div>

      <div className="action-card">
        <div className="action-heading"><span className="action-mark">↕</span><div><h2>Move test funds</h2><small>Cash in or out through the ledger</small></div></div>
        <form onSubmit={fund}>
          <label>Account<select name="accountId" required>{accounts.map(account => <option key={account.id} value={account.id}>{account.displayName} · •••• {account.mask}</option>)}</select></label>
          <label>Direction<select name="direction"><option value="cash_in">Cash in</option><option value="cash_out">Cash out</option></select></label>
          <label>Amount (USD)<input name="amount" required inputMode="decimal" placeholder="25.00" /></label>
          <button disabled={busy || accounts.length === 0}>Post simulated transfer</button>
        </form>
      </div>
    </div>

    {error && <p className="alert" role="alert">{error}</p>}
    <div className="control-card">
      <div><h2>Wallet controls</h2><p>{operations ? `${dollars(operations.dailyOutboundUsedMinor)} of ${dollars(operations.dailyOutboundLimitMinor)} daily outbound limit used.` : 'Loading controls…'}</p>
        {operations?.latestReconciliation && <small>Last reconciliation: {operations.latestReconciliation.status.replace('_', ' ')} · variance {dollars(operations.latestReconciliation.unexplainedVarianceMinor)}</small>}</div>
      <button className={operations?.isFrozen ? '' : 'quiet'} disabled={busy || !operations} onClick={() => onSetFrozen(!operations?.isFrozen)}>{operations?.isFrozen ? 'Unfreeze wallet' : 'Freeze wallet'}</button>
    </div>

    <div className="section-title"><h2>Funding history</h2><span className="sandbox-label">Private</span></div>
    {transfers.length === 0 ? <p>No funding transfers yet.</p> : <ul className="activity-list">{transfers.map(transfer => <li key={transfer.id}>
      <span className="activity-icon">{transfer.direction === 'cash_in' ? 'IN' : 'OUT'}</span>
      <div><h3>{transfer.direction === 'cash_in' ? 'Cash in' : 'Cash out'}</h3><p>{transfer.provider} · {transfer.status}</p><time dateTime={transfer.createdAt}>{new Date(transfer.createdAt).toLocaleString()}</time></div>
      <strong className={transfer.direction === 'cash_in' ? 'received' : ''}>{transfer.direction === 'cash_in' ? '+' : '−'}{dollars(transfer.amountMinor)}</strong>
    </li>)}</ul>}

    <div className="section-title"><h2>Notifications</h2><span className="sandbox-label">{notifications.filter(item => !item.readAt).length} unread</span></div>
    {notifications.length === 0 ? <p>No notifications yet. New committed events appear here after outbox delivery.</p> : <ul className="activity-list">{notifications.map(item => <li key={item.id}>
      <span className="activity-icon">●</span><div><h3>{item.title}</h3><p>{item.body}</p><time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString()}</time></div>
    </li>)}</ul>}
  </section>
}
