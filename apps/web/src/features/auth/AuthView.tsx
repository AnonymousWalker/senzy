import { useState } from 'react'
import type { FormEvent } from 'react'
import type { ClientConfiguration } from '@senzy/contracts'
import type { LoginInput, RegisterInput } from '@senzy/contracts/client'

export type AuthSubmission =
  | { kind: 'login'; credentials: LoginInput }
  | { kind: 'register'; credentials: RegisterInput }

interface AuthViewProps {
  busy: boolean
  config: ClientConfiguration
  onSubmit: (submission: AuthSubmission) => void
}

export function AuthView({ busy, config, onSubmit }: AuthViewProps) {
  const [signUp, setSignUp] = useState(false)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const email = String(form.get('email'))
    const password = String(form.get('password'))
    onSubmit(signUp
      ? { kind: 'register', credentials: { email, password, displayName: String(form.get('displayName')), handle: String(form.get('handle')) } }
      : { kind: 'login', credentials: { email, password } })
  }

  return <div className="auth-layout">
    <section className="intro">
      <p className="eyebrow">MOVE TOGETHER</p>
      <h1>A little closer.<br />A lot simpler.</h1>
      <p>Your people, your profile, your own space. Explore what’s next for everyday payments.</p>
      <div className="sample-note">You’re exploring a prototype. No bank credentials or real money are used.</div>
    </section>
    <section className="auth-card">
      <h2>{signUp ? 'Make yourself at home' : 'Welcome back'}</h2>
      <p>{signUp ? 'Create your Senzy profile.' : 'Sign in to your private space.'}</p>
      <form onSubmit={submit} key={String(signUp)}>
        {signUp && <>
          <label>Display name<input name="displayName" autoComplete="name" required maxLength={80} /></label>
          <label>Payment handle<input name="handle" autoCapitalize="none" autoComplete="username" required pattern="[a-zA-Z][a-zA-Z0-9_]{2,19}" minLength={3} maxLength={20} aria-describedby="handle-help" /></label>
          <small id="handle-help">3–20 letters, numbers or underscores. Start with a letter.</small>
        </>}
        <label>Email<input name="email" type="email" autoComplete="email" required maxLength={254} /></label>
        <label>Password<input name="password" type="password" autoComplete={signUp ? 'new-password' : 'current-password'} required minLength={signUp ? 12 : undefined} maxLength={128} /></label>
        {signUp && <small>At least 12 characters.</small>}
        <button disabled={busy} type="submit">{busy ? 'Please wait…' : signUp ? 'Create account' : 'Sign in'}</button>
      </form>
      {config.registrationEnabled && <button className="quiet switch-auth" disabled={busy} onClick={() => setSignUp(value => !value)}>{signUp ? 'Already a member? Sign in' : 'New here? Create an account'}</button>}
    </section>
  </div>
}
