import { useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError } from './api'
import type { RestaurantProfile } from './api'
import { useRestaurant } from './restaurantProfile'

export function RestaurantSettings() {
  const { profile, loading, error, reload, save } = useRestaurant()
  const [edits, setEdits] = useState<RestaurantProfile | null>(null)
  const draft = edits ?? profile
  const [busy, setBusy] = useState(false)
  const [saveError, setSaveError] = useState('')
  const [saved, setSaved] = useState(false)
  function change(field: keyof Omit<RestaurantProfile, 'version'>, value: string) {
    if (draft) setEdits({ ...draft, [field]: value })
    setSaved(false)
  }
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!draft || busy) return
    setBusy(true); setSaveError(''); setSaved(false)
    try { setEdits(await save(draft)); setSaved(true) }
    catch (cause) {
      setSaveError(cause instanceof ApiError && cause.status === 409
        ? 'Someone else changed restaurant settings. Reload the current details and reapply your edits.'
        : `Save was not confirmed. ${cause instanceof Error ? cause.message : ''} Reload the current details before retrying.`)
    } finally { setBusy(false) }
  }
  return <section className="surface restaurant-settings">
    <div className="section-heading"><div><span className="eyebrow">SHARED ACROSS THE RESTAURANT</span><h2>Business identity</h2></div></div>
    <p className="muted-copy">These details appear on the guest menu, receipts and staff workspace. Only administrators can change them. Enter public business information, not private account details.</p>
    {profile && !profile.version && <div className="notice" role="status">Restaurant details have not been configured yet.</div>}
    {saveError && <div className="alert" role="alert">{saveError}</div>}
    {saved && <div className="notice" role="status">Restaurant settings saved.</div>}
    {draft && <form onSubmit={submit}>
      <fieldset disabled={busy || loading || !!error}>
        <div className="restaurant-fields">
          <label className="field">Restaurant name<input required maxLength={100} value={draft.name} onChange={e => change('name', e.target.value)} /></label>
          <label className="field">Tagline<input maxLength={150} value={draft.tagline} onChange={e => change('tagline', e.target.value)} /></label>
          <label className="field restaurant-wide">Business address<textarea rows={3} maxLength={300} value={draft.address} onChange={e => change('address', e.target.value)} /></label>
          <label className="field">Business phone<input type="tel" maxLength={50} value={draft.phone} onChange={e => change('phone', e.target.value)} /></label>
          <label className="field">Business email<input type="email" maxLength={150} value={draft.email} onChange={e => change('email', e.target.value)} /></label>
          <label className="field restaurant-wide">Website<input type="url" placeholder="https://" maxLength={250} value={draft.website} onChange={e => change('website', e.target.value)} /></label>
          <label className="field restaurant-wide">Receipt footer<textarea rows={3} maxLength={300} value={draft.receiptFooter} onChange={e => change('receiptFooter', e.target.value)} /><small>A short thank-you or service note. Reprints use the current restaurant details.</small></label>
        </div>
        <button className="button primary" disabled={!draft.name.trim()}>{busy ? 'Saving...' : 'Save restaurant settings'}</button>
      </fieldset>
    </form>}
    <button className="button secondary small profile-reload" disabled={busy || loading} onClick={async () => { setSaved(false); setSaveError(''); if (await reload()) setEdits(null) }}>Reload current details</button>
    <p className="muted-copy">Reload replaces unsaved edits once the current details are retrieved. Workflow preferences are configured separately for this browser.</p>
  </section>
}
