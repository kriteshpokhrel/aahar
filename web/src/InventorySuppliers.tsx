import { useCallback, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from './api'
import type { Supplier } from './api'
import { InventoryHistory } from './InventoryHistory'

function SupplierHistory({ supplier }: { supplier: Supplier }) {
  const load = useCallback((before?: number) => api.supplierHistory(supplier.id, before), [supplier.id])
  return <InventoryHistory title={`History · ${supplier.name}`} load={load} render={entry => <><strong>{entry.name}</strong><span>{entry.contact || 'No contact'} · {entry.actor} · {new Date(entry.occurredAt).toLocaleString()}</span><span>Reason: {entry.reason}</span></>} />
}

export function InventorySuppliers({ suppliers, refresh }: { suppliers: Supplier[]; refresh: () => Promise<void> }) {
  const [editing, setEditing] = useState<number | null>(null)
  const [history, setHistory] = useState<number | null>(null)
  const [name, setName] = useState('')
  const [contact, setContact] = useState('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  function reset() { setEditing(null); setName(''); setContact(''); setReason('') }
  async function save(event: FormEvent) {
    event.preventDefault()
    if (!name.trim() || !reason.trim()) return
    setBusy(true); setError(''); setNotice('')
    try {
      if (editing === null) await api.createSupplier(name.trim(), contact.trim() || null, reason.trim())
      else await api.updateSupplier(editing, name.trim(), contact.trim() || null, reason.trim())
      reset(); setHistory(null); setNotice('Supplier saved.'); await refresh()
    } catch (e) { setError(e instanceof Error ? e.message : 'Unable to save supplier.') } finally { setBusy(false) }
  }
  const selected = suppliers.find(item => item.id === history)
  return <section><div className="section-heading"><div><span className="eyebrow">SUPPLIER DIRECTORY</span><h2>Suppliers</h2></div><span className="count-label">{suppliers.length} suppliers</span></div>
    {error && <div className="alert" role="alert">{error}</div>}{notice && <div className="notice" role="status">{notice}</div>}
    <div className="stock-list">{suppliers.map(supplier => <div className="stock-row" key={supplier.id}><div><strong>{supplier.name}</strong><small>{supplier.contact || 'No contact provided'}</small></div><div className="payment-actions"><button className="button small secondary" onClick={() => { setEditing(supplier.id); setName(supplier.name); setContact(supplier.contact ?? ''); setReason(''); setError('') }}>Edit</button><button className="button small secondary" onClick={() => setHistory(history === supplier.id ? null : supplier.id)} aria-expanded={history === supplier.id}>History</button></div></div>)}</div>
    {selected && <SupplierHistory key={selected.id} supplier={selected} />}
    <form className="management management-form" onSubmit={event => void save(event)}><h3>{editing === null ? 'Add supplier' : 'Edit supplier'}</h3><label className="field">Name<input required maxLength={100} value={name} onChange={event => setName(event.target.value)} /></label><label className="field">Contact (optional)<input maxLength={200} value={contact} onChange={event => setContact(event.target.value)} /></label><label className="field">Reason<input required maxLength={300} value={reason} onChange={event => setReason(event.target.value)} /></label><div className="payment-actions">{editing !== null && <button type="button" className="button secondary" onClick={reset}>Cancel edit</button>}<button className="button primary" disabled={busy || !name.trim() || !reason.trim()}>{busy ? 'Saving…' : 'Save supplier'}</button></div></form>
  </section>
}
