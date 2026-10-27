import type { FormEvent } from 'react'
import type { Profile } from '@senzy/contracts'
import type { ProfileInput } from '@senzy/contracts/client'

interface ProfileViewProps {
  busy: boolean
  profile: Profile
  onSave: (input: ProfileInput) => void
}

export function ProfileView({ busy, profile, onSave }: ProfileViewProps) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    onSave({ displayName: String(form.get('displayName')), handle: String(form.get('handle')) })
  }

  return <section className="profile-card">
    <p className="eyebrow">MAKE IT YOURS</p>
    <h1>Your profile</h1>
    <p>Your Senzy Tag is how people will find you on Senzy.</p>
    <form key={profile.handle + profile.displayName} onSubmit={submit}>
      <label>Display name<input name="displayName" defaultValue={profile.displayName} required maxLength={80} autoComplete="name" /></label>
      <label>Senzy Tag<input name="handle" defaultValue={profile.handle} required pattern="[a-zA-Z][a-zA-Z0-9_]{2,19}" minLength={3} maxLength={20} autoCapitalize="none" autoComplete="username" /></label>
      <small>3–20 letters, numbers or underscores. Start with a letter.</small>
      <button disabled={busy}>{busy ? 'Saving…' : 'Save profile'}</button>
    </form>
    <p className="muted">Your activity is private by default.</p>
  </section>
}
