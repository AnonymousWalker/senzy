import { useCallback, useEffect, useState } from 'react'
import type { ClientConfiguration, CreatePaymentRequest, Payment, PaymentRequest, Profile, TransferRequest, Wallet } from '@senzy/contracts'
import type { ProfileInput } from '@senzy/contracts/client'
import { ApiError } from '@senzy/contracts/client'
import { api } from '../api'
import { ActivityView } from '../features/activity'
import { AuthView } from '../features/auth'
import type { AuthSubmission } from '../features/auth'
import { ProfileView } from '../features/profile'
import { ErrorBoundary, FeedbackBanner, getErrorMessage } from '../shared'
import type { AppScreen } from './types'
import '../App.css'

function SenzyApp() {
  const [config, setConfig] = useState<ClientConfiguration | null>(null)
  const [profile, setProfile] = useState<Profile | null>(null)
  const [wallet, setWallet] = useState<Wallet | null>(null)
  const [payments, setPayments] = useState<Payment[] | null>(null)
  const [requests, setRequests] = useState<PaymentRequest[] | null>(null)
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

  return <div className="app-shell">
    <a className="skip-link" href="#main">Skip to content</a>
    <header className="app-header">
      <a className="wordmark" href="/">senzy<span>●</span></a>
      <span className="sandbox-label">Sandbox · no real money</span>
      {profile && <button className="quiet" onClick={() => void logout()} disabled={busy}>Sign out</button>}
    </header>
    {profile && <nav aria-label="Main navigation">
      <button aria-current={screen === 'activity' ? 'page' : undefined} onClick={() => navigate('activity')}>Activity</button>
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
              : <ProfileView busy={busy} profile={profile} onSave={input => void saveProfile(input)} />}
    </main>
    <footer>Made for the moments between people. <span>Senzy sandbox</span></footer>
  </div>
}

export function App() {
  return <ErrorBoundary><SenzyApp /></ErrorBoundary>
}
