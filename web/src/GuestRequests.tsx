import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api, ApiError } from './api'
import type { GuestRequest, GuestStatus, Table } from './api'

const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { maximumFractionDigits: 2 })}`
const errorText = (error: unknown) => error instanceof Error ? error.message : 'Something went wrong.'

export function GuestRequests({ tables, onChanged }: { tables: Table[]; onChanged: () => Promise<void> }) {
  const [status, setStatus] = useState<GuestStatus>('Pending')
  const [beforeId, setBeforeId] = useState<number | undefined>()
  const [requests, setRequests] = useState<GuestRequest[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [rejectId, setRejectId] = useState<number | null>(null)
  const [reason, setReason] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    let active = true
    void api.guestRequests(status, beforeId).then(result => { if (active) setRequests(result) })
      .catch(error => { if (active) { setRequests([]); setError(errorText(error)) } })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [status, beforeId, revision])
  function reload() { setLoading(true); setError(''); setRevision(value => value + 1) }
  function changeStatus(value: GuestStatus) {
    setLoading(true); setError(''); setNotice(''); setRejectId(null); setReason(''); setBeforeId(undefined); setStatus(value)
    if (value === status && beforeId === undefined) setRevision(current => current + 1)
  }
  async function accept(request: GuestRequest) {
    setBusy(true); setError(''); setNotice('')
    try {
      const result = await api.acceptGuestRequest(request.id)
      setNotice(`Request #${request.id} accepted as draft order #${result.order.id}.`)
      reload(); await onChanged()
    } catch (e) {
      setError(e instanceof ApiError && e.status === 409 ? `${errorText(e)} Refresh the request and verify its items and table before retrying.` : errorText(e))
    } finally { setBusy(false) }
  }
  async function reject(event: FormEvent, request: GuestRequest) {
    event.preventDefault()
    if (!reason.trim()) return
    setBusy(true); setError(''); setNotice('')
    try {
      await api.rejectGuestRequest(request.id, reason.trim())
      setRejectId(null); setReason(''); setNotice(`Request #${request.id} rejected.`); reload()
    } catch (e) {
      setError(e instanceof ApiError && e.status === 409 ? `${errorText(e)} Refresh before retrying.` : errorText(e))
    } finally { setBusy(false) }
  }
  return <section className="surface"><div className="section-heading"><div><span className="eyebrow">STAFF REVIEW</span><h2>Guest requests</h2></div><button className="button small secondary" disabled={busy || loading} onClick={reload}>Refresh</button></div>
    <p className="muted-copy">Accepting creates a draft order; it does not send food to the kitchen. Prices, availability and table occupancy are rechecked on acceptance.</p>
    <div className="guest-filters" aria-label="Request status">{(['Pending', 'Accepted', 'Rejected'] as const).map(value => <button key={value} className={`button small ${status === value ? 'primary' : 'secondary'}`} aria-pressed={status === value} onClick={() => changeStatus(value)}>{value}</button>)}</div>
    {error && <div className="alert" role="alert">{error}</div>}{notice && <div className="notice" role="status">{notice}</div>}{loading && <p role="status">Loading requests…</p>}
    {!loading && !requests.length && !error && <p className="muted-copy">No {status.toLowerCase()} requests found.</p>}
    <div className="ticket-grid">{requests.map(request => {
      const table = tables.find(item => item.id === request.tableId)
      return <article className="ticket" key={request.id}><div className="ticket-top"><div><span className="eyebrow">REQUEST #{request.id}</span><h3>{request.tableId === null ? 'Takeaway' : table ? `Table ${table.number}` : `Table #${request.tableId}`}</h3></div><span className="status status-busy">{request.status}</span></div><p className="muted-copy">{new Date(request.createdAt).toLocaleString()}</p><div className="ticket-items">{request.items.map(item => <div key={item.menuItemId}><span><b>{item.quantity}×</b> {item.name}{item.notes && <small className="ticket-note">Note: {item.notes}</small>}</span><span>{money(item.quantity * item.quotedUnitPrice)}</span></div>)}</div><div className="ticket-bottom"><strong>{money(request.items.reduce((sum, item) => sum + item.quantity * item.quotedUnitPrice, 0))}</strong>{request.status === 'Pending' && <div className="payment-actions"><button className="button small secondary" disabled={busy || loading} onClick={() => { setRejectId(request.id); setReason('') }}>Reject</button><button className="button small primary" disabled={busy || loading} onClick={() => void accept(request)}>Accept as draft</button></div>}</div>{request.orderId && <p className="muted-copy">Draft order #{request.orderId}</p>}{request.rejectionReason && <p className="muted-copy">Reason: {request.rejectionReason}</p>}
      {rejectId === request.id && request.status === 'Pending' && <form className="cancel-form" onSubmit={event => void reject(event, request)}><label className="field">Reason for rejection<textarea maxLength={300} required value={reason} onChange={event => setReason(event.target.value)} /></label><div className="guest-actions"><button type="button" className="button small secondary" disabled={busy} onClick={() => setRejectId(null)}>Keep pending</button><button className="button small primary" disabled={busy || !reason.trim()}>Confirm rejection</button></div></form>}</article>
    })}</div>
    {requests.length === 20 && <button className="button small secondary guest-pagination" disabled={loading || busy} onClick={() => { setLoading(true); setError(''); setBeforeId(requests[requests.length - 1].id) }}>Older requests</button>}{beforeId !== undefined && <button className="button small secondary guest-pagination" disabled={loading || busy} onClick={() => { setLoading(true); setError(''); setBeforeId(undefined) }}>Back to newest</button>}
  </section>
}
