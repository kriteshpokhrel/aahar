import { useState } from 'react'
import type { FormEvent } from 'react'
import { api } from './api'
import type { StaffRole } from './api'
import { uncertainMutationOutcome } from './orderSafety'

export function StaffAccounts() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<StaffRole>('Staff')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!username.trim() || password.length < 12 || password.length > 256) return
    setBusy(true); setError(''); setNotice('')
    try {
      const created = await api.createUser(username.trim(), password, role)
      setUsername(''); setPassword(''); setRole('Staff')
      setNotice(`Account ${created.username} created with ${created.role} access.`)
    } catch (e) {
      setError(uncertainMutationOutcome(e)
        ? 'The response was lost or the server failed. The account may have been created; confirm before trying again.'
        : e instanceof Error ? e.message : 'Unable to create account.')
    } finally { setBusy(false) }
  }
  return <section className="surface staff-accounts">
    <div className="section-heading"><div><span className="eyebrow">ADMIN ACCESS</span><h2>Create a staff account</h2></div></div>
    <p className="muted-copy">Choose the least access needed for the person’s work. Usernames are stored in lowercase.</p>
    {error && <div className="alert" role="alert">{error}</div>}
    {notice && <div className="notice" role="status">{notice}</div>}
    <form className="management management-form" onSubmit={event => void submit(event)}>
      <label className="field">Username<input autoComplete="off" required maxLength={100} value={username} onChange={event => setUsername(event.target.value)} /></label>
      <label className="field">Role<select value={role} onChange={event => setRole(event.target.value as StaffRole)}><option value="Staff">Staff</option><option value="Kitchen">Kitchen</option><option value="Manager">Manager</option><option value="Admin">Admin</option></select></label>
      <label className="field">Password<input type="password" autoComplete="new-password" required minLength={12} maxLength={256} value={password} onChange={event => setPassword(event.target.value)} aria-describedby="staff-password-help" /><span id="staff-password-help">12–256 characters. Share it securely with the new user.</span></label>
      <button className="button primary" disabled={busy || !username.trim() || password.length < 12}>{busy ? 'Creating account…' : 'Create account'}</button>
    </form>
  </section>
}
