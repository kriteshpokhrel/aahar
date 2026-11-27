import { useState } from 'react'
import type { FormEvent } from 'react'
import { api, ApiError } from './api'
import { uncertainMutationOutcome } from './orderSafety'

export function ChangePassword({ onSignedOut }: { onSignedOut: (message: string) => void }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return
    const form = event.currentTarget
    const fields = new FormData(form)
    const currentPassword = String(fields.get('currentPassword') ?? '')
    const newPassword = String(fields.get('newPassword') ?? '')
    const confirmation = String(fields.get('confirmation') ?? '')
    form.reset()
    if (newPassword !== confirmation) { setError('The new passwords do not match. Please enter them again.'); return }
    if (!currentPassword || newPassword.length < 12 || newPassword.length > 256) { setError('Enter your current password and a new password of 12–256 characters.'); return }
    setBusy(true); setError('')
    try {
      await api.changePassword(currentPassword, newPassword)
      onSignedOut('Password changed. Sign in again with your new password.')
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onSignedOut('Your session has expired. Sign in again.')
      else if (e instanceof ApiError && e.status === 409) setError('Your password changed elsewhere. Sign out and sign in with the current password before trying again.')
      else if (e instanceof ApiError && e.status === 429) setError('Too many attempts. Please wait before trying again.')
      else if (uncertainMutationOutcome(e)) setError('The change could not be confirmed. Sign out and try signing in with your new password before retrying.')
      else setError(e instanceof Error ? e.message : 'Unable to change your password.')
    } finally { setBusy(false) }
  }

  return <section className="surface">
    <div className="section-heading"><div><span className="eyebrow">YOUR ACCOUNT</span><h2>Change password</h2></div></div>
    <p className="muted-copy">After a successful change, this browser signs out. Other signed-in sessions remain active until they expire.</p>
    {error && <div className="alert" role="alert">{error}</div>}
    <form className="management management-form" onSubmit={event => void submit(event)}>
      <label className="field">Current password<input name="currentPassword" type="password" autoComplete="current-password" required disabled={busy} /></label>
      <label className="field">New password<input name="newPassword" type="password" autoComplete="new-password" required minLength={12} maxLength={256} disabled={busy} aria-describedby="new-password-help" /><span id="new-password-help">12–256 characters.</span></label>
      <label className="field">Confirm new password<input name="confirmation" type="password" autoComplete="new-password" required minLength={12} maxLength={256} disabled={busy} /></label>
      <button className="button primary" disabled={busy}>{busy ? 'Changing password…' : 'Change password'}</button>
    </form>
  </section>
}
