import { useCallback, useEffect, useState } from 'react'
import type { BankingInstitution, ClientConfiguration, CreateFundingTransfer, CreatePaymentRequest, ExternalBankAccount, FundingTransfer, Notification, OperationsStatus, Payment, PaymentRequest, Profile, TransferRequest, Wallet } from '@senzy/contracts'
import type { ProfileInput } from '@senzy/contracts/client'
import { ApiError } from '@senzy/contracts/client'
import { api } from '../api'
import { ActivityView } from '../features/activity'
import { AuthView } from '../features/auth'
import type { AuthSubmission } from '../features/auth'
import { ProfileView } from '../features/profile'
import { FundingView } from '../features/funding'
import { ErrorBoundary, FeedbackBanner, getErrorMessage } from '../shared'
import type { AppScreen } from './types'
import '../App.css'

function SenzyApp() {
  const [config, setConfig] = useState<ClientConfiguration | null>(null)
  const [profile, setProfile] = useState<Profile | null>(null)
  const [wallet, setWallet] = useState<Wallet | null>(null)
  const [payments, setPayments] = useState<Payment[] | null>(null)
  const [requests, setRequests] = useState<PaymentRequest[] | null>(null)
  const [institutions, setInstitutions] = useState<BankingInstitution[]>([])
  const [accounts, setAccounts] = useState<ExternalBankAccount[]>([])
  const [fundingTransfers, setFundingTransfers] = useState<FundingTransfer[]>([])
  const [notifications, setNotifications] = useState<Notification[]>([])
  const [operations, setOperations] = useState<OperationsStatus | null>(null)
  const [screen, setScreen] = useState<AppScreen>('activity')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [attempt, setAttempt] = useState(0)

  const handleFailure = useCallback((cause: unknown) => {
    setError(getErrorMessage(cause))
    if (cause instanceof ApiError && cause.status === 401) {
      setProfile(null)
      setWallet(null)
      setPayments(null)
      setRequests(null)
    } else if (!(cause instanceof ApiError) || cause.status >= 500) {
      api.report('client_error', screen)
    }
  }, [screen])

  useEffect(() => {
    let active = true

    async function restoreSession() {
      setLoading(true)
      setError('')
      try {
        const settings = await api.configuration()
        if (!active) return
        setConfig(settings)
        try {
          const user = await api.profile()
          if (active) setProfile(user)
        } catch (cause) {
          if (!(cause instanceof ApiError && cause.status === 401)) throw cause
        }
      } catch (cause) {
        if (active) setError(getErrorMessage(cause))
      } finally {
        if (active) setLoading(false)
      }
    }

    void restoreSession()
    return () => { active = false }
  }, [attempt])

  const userId = profile?.userId
  useEffect(() => {
    let active = true
    if (userId && screen === 'activity') {
      void Promise.all([api.wallet(), api.payments(), api.paymentRequests()])
        .then(([nextWallet, nextPayments, nextRequests]) => {
          if (active) {
            setWallet(nextWallet)
            setPayments(nextPayments.items)
            setRequests(nextRequests.items)
            api.report('activity_viewed', 'activity')
          }
        })
        .catch(cause => { if (active) handleFailure(cause) })
    }
    return () => { active = false }
  }, [userId, screen, attempt, handleFailure])

  useEffect(() => {
    let active = true
    if (userId && screen === 'funding' && config?.sandboxBankingEnabled) {
      void Promise.all([api.bankingInstitutions(), api.bankAccounts(), api.fundingTransfers(), api.notifications(), api.operationsStatus()])
        .then(([nextInstitutions, nextAccounts, nextTransfers, nextNotifications, nextOperations]) => {
          if (!active) return
          setInstitutions(nextInstitutions.institutions)
          setAccounts(nextAccounts.accounts)
          setFundingTransfers(nextTransfers.items)
          setNotifications(nextNotifications.items)
          setOperations(nextOperations)
        }).catch(cause => { if (active) handleFailure(cause) })
    }
    return () => { active = false }
  }, [userId, screen, attempt, config?.sandboxBankingEnabled, handleFailure])

  async function authenticate(submission: AuthSubmission) {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      if (submission.kind === 'register') await api.register(submission.credentials)
      await api.login({ email: submission.credentials.email, password: submission.credentials.password })
      setProfile(await api.profile())
      setScreen('activity')
      api.report(submission.kind === 'register' ? 'signup_completed' : 'login_succeeded', 'auth')
    } catch (cause) {
      handleFailure(cause)
    } finally {
      setBusy(false)
    }
  }

  async function saveProfile(input: ProfileInput) {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      setProfile(await api.updateProfile(input))
      setNotice('Profile saved.')
      api.report('profile_saved', 'profile')
    } catch (cause) {
      handleFailure(cause)
    } finally {
      setBusy(false)
    }
  }

  async function logout() {
    setBusy(true)
    setError('')
    try {
      await api.logout()
      setProfile(null)
      setWallet(null)
      setPayments(null)
      setRequests(null)
      setAccounts([])
      setFundingTransfers([])
      setNotifications([])
      setOperations(null)
      setNotice('You have signed out on all devices.')
    } catch (cause) {
      handleFailure(cause)
    } finally {
      setBusy(false)
    }
  }

  function navigate(nextScreen: AppScreen) {
    setScreen(nextScreen)
    setError('')
    setNotice('')
  }

  async function runPaymentAction(action: () => Promise<unknown>, success: string) {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await action()
      const [nextWallet, nextPayments, nextRequests] = await Promise.all([api.wallet(), api.payments(), api.paymentRequests()])
      setWallet(nextWallet)
      setPayments(nextPayments.items)
      setRequests(nextRequests.items)
      setNotice(success)
    } catch (cause) {
      handleFailure(cause)
    } finally {
      setBusy(false)
    }
  }

  async function linkSandboxAccount(institutionId: string, displayName: string, accountKind: 'checking' | 'savings') {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const session = await api.createBankLinkSession()
      await api.connectBankAccount({ linkToken: session.linkToken, institutionId, displayName, accountKind })
      setAccounts((await api.bankAccounts()).accounts)
      setNotice('Sandbox funding source linked. No real bank was contacted.')
    } catch (cause) { handleFailure(cause) } finally { setBusy(false) }
  }

  async function runFunding(input: CreateFundingTransfer) {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await api.createFundingTransfer(input)
      const [nextWallet, nextTransfers, nextOperations, nextNotifications] = await Promise.all([
        api.wallet(), api.fundingTransfers(), api.operationsStatus(), api.notifications(),
      ])
      setWallet(nextWallet)
      setFundingTransfers(nextTransfers.items)
      setOperations(nextOperations)
      setNotifications(nextNotifications.items)
      setNotice('Simulated funding transfer posted and queued for notification delivery.')
    } catch (cause) { handleFailure(cause) } finally { setBusy(false) }
  }

  async function setWalletFrozen(frozen: boolean) {
    setBusy(true)
    setError('')
    try {
      if (frozen) await api.freezeWallet()
      else await api.unfreezeWallet()
      setOperations(await api.operationsStatus())
      setNotice(frozen ? 'Wallet frozen. Outbound payments and cash-out are blocked.' : 'Wallet unfrozen.')
    } catch (cause) { handleFailure(cause) } finally { setBusy(false) }
  }

  return <div className="app-shell">
    <a className="skip-link" href="#main">Skip to content</a>
    <header className="app-header">
      <a className="wordmark" href="/">senzy<span>●</span></a>
      <span className="sandbox-label">Sandbox · no real money</span>
      {profile && <button className="quiet" onClick={() => void logout()} disabled={busy}>Sign out</button>}
    </header>
    {profile && <nav aria-label="Main navigation">
      <button aria-current={screen === 'activity' ? 'page' : undefined} onClick={() => navigate('activity')}>Activity</button>
      {config?.sandboxBankingEnabled && <button aria-current={screen === 'funding' ? 'page' : undefined} onClick={() => navigate('funding')}>Funding</button>}
      <button aria-current={screen === 'profile' ? 'page' : undefined} onClick={() => navigate('profile')}>Profile</button>
    </nav>}
    <main id="main">
      <FeedbackBanner error={error} notice={notice} onRetry={() => setAttempt(value => value + 1)} />
      {loading ? <p role="status">Opening Senzy…</p>
        : !config ? <section className="auth-card"><h1>Let’s reconnect</h1><p>We couldn’t reach Senzy. Use retry above to connect again.</p></section>
          : !profile ? <AuthView busy={busy} config={config} onSubmit={submission => void authenticate(submission)} />
            : screen === 'activity' ? <ActivityView busy={busy} payments={payments} profile={profile} requests={requests} wallet={wallet}
              onSend={(input: TransferRequest) => void runPaymentAction(() => api.sendPayment(input), 'Simulated payment posted.')}
              onRequest={(input: CreatePaymentRequest) => void runPaymentAction(() => api.createPaymentRequest(input), 'Payment request created.')}
              onAcceptRequest={id => void runPaymentAction(() => api.acceptPaymentRequest(id), 'Payment request paid.')}
              onReverse={id => void runPaymentAction(() => api.reversePayment(id), 'Payment reversed with a compensating ledger entry.')} />
              : screen === 'funding' ? <FundingView accounts={accounts} busy={busy} institutions={institutions}
                notifications={notifications} operations={operations} transfers={fundingTransfers}
                onFunding={input => void runFunding(input)} onLink={(institutionId, displayName, accountKind) => void linkSandboxAccount(institutionId, displayName, accountKind)}
                onSetFrozen={frozen => void setWalletFrozen(frozen)} />
                : <ProfileView busy={busy} profile={profile} onSave={input => void saveProfile(input)} />}
    </main>
    <footer>Made for the moments between people. <span>Senzy sandbox</span></footer>
  </div>
}

export function App() {
  return <ErrorBoundary><SenzyApp /></ErrorBoundary>
}
