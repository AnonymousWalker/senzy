import type { ActivityFeed, ClientConfiguration, Profile } from '@senzy/contracts'

interface ActivityViewProps {
  config: ClientConfiguration
  feed: ActivityFeed | null
  profile: Profile
}

export function ActivityView({ config, feed, profile }: ActivityViewProps) {
  return <section>
    <p className="eyebrow">YOUR PRIVATE SPACE</p>
    <h1>Hello, {profile.displayName}.</h1>
    <p className="handle">${profile.handle}</p>
    <div className="balance"><span>Demo balance</span><strong>$0.00</strong><small>Payments are coming in the next phase.</small></div>
    <div className="section-title"><h2>Activity</h2><span className="sandbox-label">Sample data</span></div>
    <p className="muted">These examples are only visible in your session. They are not real transactions.</p>
    {!config.activityEnabled ? <p>Activity is currently unavailable.</p>
      : !feed ? <p role="status">Loading your activity…</p>
        : feed.items.length === 0 ? <p>No activity yet.</p>
          : <ul className="activity-list">{feed.items.map(item => <li key={item.id}>
            <span className="activity-icon" aria-hidden="true">{item.kind === 'received' ? '↙' : '↗'}</span>
            <div><h3>{item.title}</h3><p>{item.description}</p><time dateTime={item.occurredAt}>{new Date(item.occurredAt).toLocaleDateString()}</time></div>
            <strong className={item.kind}>{item.kind === 'received' ? '+' : '−'}{new Intl.NumberFormat('en-US', { style: 'currency', currency: item.currency }).format(item.amountMinor / 100)}</strong>
          </li>)}</ul>}
  </section>
}
