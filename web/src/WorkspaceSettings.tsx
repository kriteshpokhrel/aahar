import type { WorkspacePreferences } from './preferences'

export function WorkspaceSettings({ preferences, error, save }: { preferences: WorkspacePreferences; error: string; save: (value: WorkspacePreferences) => void }) {
  return <section className="surface"><div className="section-heading"><div><span className="eyebrow">THIS BROWSER</span><h2>Workspace preferences</h2></div></div>
    <p className="muted-copy">These preferences apply only to this browser, not to other devices or the whole restaurant. Kitchen preparation remains manual.</p>
    {error && <div className="alert" role="alert">{error}</div>}
    <div className="settings-list">
      <label className="settings-row"><span><strong>Send new drafts to kitchen automatically</strong><small>After a new order is saved, send it to the kitchen. Additional rounds are sent immediately regardless of this setting.</small></span><input type="checkbox" checked={preferences.autoSendToKitchen} onChange={event => save({ ...preferences, autoSendToKitchen: event.target.checked })} /></label>
      <label className="settings-row"><span><strong>Workspace density</strong><small>Adjust desktop spacing; touch controls remain usable on mobile.</small></span><select value={preferences.density} onChange={event => save({ ...preferences, density: event.target.value === 'comfortable' ? 'comfortable' : 'compact' })}><option value="compact">Compact</option><option value="comfortable">Comfortable</option></select></label>
      <label className="settings-row"><span><strong>Default payment method</strong><small>Choose the initial method for checkout.</small></span><select value={preferences.defaultPayment} onChange={event => save({ ...preferences, defaultPayment: event.target.value === 'Card' ? 'Card' : 'Cash' })}><option value="Cash">Cash</option><option value="Card">Card</option></select></label>
      <label className="settings-row"><span><strong>Open receipt after payment</strong><small>Shows the receipt preview; it does not print automatically.</small></span><input type="checkbox" checked={preferences.autoOpenReceipt} onChange={event => save({ ...preferences, autoOpenReceipt: event.target.checked })} /></label>
    </div>
  </section>
}
