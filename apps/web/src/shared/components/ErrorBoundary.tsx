import { Component } from 'react'
import type { ReactNode } from 'react'
import { api } from '../../api'

export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  componentDidCatch() {
    api.report('client_error', 'app')
  }

  render() {
    return this.state.failed
      ? <main className="auth-card"><h1>Something went wrong</h1><p>Your account is safe. Reload to try again.</p><button onClick={() => window.location.reload()}>Reload</button></main>
      : this.props.children
  }
}
