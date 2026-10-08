import { useEffect, useState } from 'react'
import './App.css'
import { loadSandbox } from './api/sandbox'
import type { Institution } from './api/sandbox'

function App() {
  const [status, setStatus] = useState<'checking' | 'online' | 'offline'>('checking')
  const [institutions, setInstitutions] = useState<Institution[]>([])

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        setInstitutions(await loadSandbox(controller.signal))
        setStatus('online')
      } catch {
        if (!controller.signal.aborted) setStatus('offline')
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  return (
    <div className="shell">
      <header className="topbar"><a className="brand" href="/"><span>S</span> senzy</a><div className="badge">Sandbox preview</div></header>
      <main>
        <section className="hero">
          <div><p className="eyebrow">A simpler way to move together</p><h1>Payments that feel effortless.</h1><p className="intro">Senzy is taking shape across web and mobile. This preview uses simulated accounts only—no real funds or bank connections.</p><a className="action" href="#foundation">Explore the foundation <span aria-hidden="true">→</span></a></div>
          <div className="balance-card" aria-label="Illustrative balance card"><div className="card-top">senzy balance <span aria-hidden="true">✦</span></div><strong>$0.00</strong><small>Demo balance · no money moves yet</small><hr /><p>Payment service <b>Planned</b></p><p>Ledger <b>Planned</b></p></div>
        </section>
        <section className="foundation" id="foundation"><p className="eyebrow">Built carefully</p><h2>A foundation for trustworthy payments.</h2><div className="features"><article><span>01</span><h3>One API for every screen</h3><p>React web and Expo mobile share the ASP.NET Core API as the product grows.</p></article><article><span>02</span><h3>Ledger-first design</h3><p>Future payments will commit with balanced entries in PostgreSQL before success is reported.</p></article><article><span>03</span><h3>Safe to explore</h3><p>Bank setup is a stubbed sandbox. No actual bank credentials or transfers are supported.</p></article></div></section>
        <section className="status-panel"><div><p className="eyebrow">Developer preview</p><h2>Sandbox connection</h2><p>Live status from the local API and simulated institutions.</p></div><div className="status-card"><p className={`api-status ${status}`}><i /> API {status}</p><ul>{institutions.map(item => <li key={item.id}>{item.name}</li>)}</ul>{status === 'offline' && <small>Start the .NET API on port 4001 to see sandbox data.</small>}</div></section>
      </main><footer>Senzy sandbox · Original product concept · No real money movement</footer>
    </div>
  )
}

export default App
