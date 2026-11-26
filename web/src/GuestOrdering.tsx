import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from './api'
import type { GuestRequestStatus, GuestSubmission, MenuItem } from './api'

const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { maximumFractionDigits: 2 })}`
const text = (error: unknown) => error instanceof Error ? error.message : 'Something went wrong.'

export function GuestOrdering({ menu, tableId, invalidTable, menuError, menuLoading, refreshMenu }: {
  menu: MenuItem[]
  tableId: number | null
  invalidTable: boolean
  menuError: string
  menuLoading: boolean
  refreshMenu: () => Promise<void>
}) {
  const [cart, setCart] = useState<Record<number, number>>({})
  const [notes, setNotes] = useState<Record<number, string>>({})
  const [submission, setSubmission] = useState<GuestSubmission | null>(null)
  const [status, setStatus] = useState<GuestRequestStatus | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [uncertain, setUncertain] = useState(false)
  const [conflictMenu, setConflictMenu] = useState<MenuItem[] | null>(null)
  const inFlight = useRef(false)
  const available = menu.filter(item => item.isAvailable)
  const groups = [...new Set(available.map(item => item.category || 'Our menu'))]
  const lines = available.filter(item => (cart[item.id] ?? 0) > 0)
  const missingItems = Object.keys(cart).some(id => !available.some(item => item.id === Number(id)))
  const quoteStale = conflictMenu === menu
  const total = lines.reduce((sum, item) => sum + item.price * cart[item.id], 0)

  useEffect(() => {
    if (!submission && !uncertain && !busy) return
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = '' }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [submission, uncertain, busy])

  function change(id: number, amount: number) {
    setCart(current => {
      const next = { ...current, [id]: Math.min(1000, Math.max(0, (current[id] ?? 0) + amount)) }
      if (!next[id]) delete next[id]
      return next
    })
  }
  async function submit() {
    if (inFlight.current || invalidTable || !lines.length || missingItems || quoteStale || submission || uncertain) return
    inFlight.current = true; setBusy(true); setError('')
    try {
      const items = lines.map(item => ({ menuItemId: item.id, quantity: cart[item.id], quotedUnitPrice: item.price, notes: notes[item.id]?.trim() || null }))
      const response = await api.submitGuestRequest(tableId, items)
      setSubmission(response); setStatus({ id: response.id, status: response.status, orderId: null })
    } catch (e) {
      if (!(e instanceof ApiError) || e.status >= 500) {
        setUncertain(true)
        setError('We could not confirm whether your request was received. Ask staff before submitting again; retrying may create a duplicate.')
      } else if (e.status === 409) {
        setConflictMenu(menu)
        setError(`${text(e)} Refresh the menu and review the new prices before trying again.`)
      } else setError(text(e))
    } finally { inFlight.current = false; setBusy(false) }
  }
  async function checkStatus() {
    if (!submission || busy) return
    setBusy(true); setError('')
    try { setStatus(await api.guestRequestStatus(submission.id, submission.accessToken)) }
    catch (e) { setError(text(e)) } finally { setBusy(false) }
  }
  async function reloadMenu() {
    await refreshMenu()
    setError('')
  }
  function newRequest() {
    setSubmission(null); setStatus(null); setCart({}); setNotes({}); setError('')
  }
  return <>
    {invalidTable && <div className="alert" role="alert">This table link is invalid. Ask staff for a new link.</div>}
    {error && <div className="alert" role="alert">{error}</div>}
    {submission ? <section className="surface guest-request-status"><span className="eyebrow">REQUEST #{submission.id}</span><h2>{status?.status ?? submission.status}</h2><p>{status?.status === 'Accepted' ? `Staff accepted your request${status.orderId ? ` as order #${status.orderId}` : ''}.` : status?.status === 'Rejected' ? 'Staff declined this request. Please speak with them for details.' : 'Your request is waiting for staff review. It has not been sent to the kitchen yet.'}</p><p>Keep this page open to check its status. Reloading or closing it loses access to this lookup.</p><div className="guest-actions"><button className="button secondary" disabled={busy} onClick={() => void checkStatus()}>{busy ? 'Checking…' : 'Check status'}</button>{status && status.status !== 'Pending' && <button className="button secondary" onClick={newRequest}>Start another request</button>}</div></section> : <>
      <p className="guest-order-context">{tableId === null ? 'Takeaway request' : `Dine-in · table reference #${tableId} (confirm with staff)`}. Staff must accept your request before it becomes an order.</p>
      {groups.map(group => <section key={group} className="guest-group"><h3>{group}</h3>{available.filter(item => (item.category || 'Our menu') === group).map(item => <div className="guest-row" key={item.id}><div><strong>{item.name}</strong><p>{item.description}</p></div><div className="guest-item-action"><strong>{money(item.price)}</strong><button className="button small secondary" disabled={busy || uncertain || (cart[item.id] ?? 0) >= 1000 || (!cart[item.id] && Object.keys(cart).length >= 100)} onClick={() => change(item.id, 1)}>Add {item.name}</button></div></div>)}</section>)}
      {!available.length && !menuError && !menuLoading && <p className="muted-copy">The menu is being prepared. Please check back soon.</p>}
      <section className="surface guest-cart"><span className="eyebrow">YOUR REQUEST</span><h2>Review your selection</h2>{lines.map(item => <div className="guest-cart-line" key={item.id}><div><strong>{item.name}</strong><small>{money(item.price)} each</small><label className="field">Kitchen note<input maxLength={300} value={notes[item.id] ?? ''} onChange={event => setNotes({ ...notes, [item.id]: event.target.value })} /></label></div><div><div className="stepper"><button aria-label={`Remove one ${item.name}`} onClick={() => change(item.id, -1)}><span aria-hidden="true">−</span></button><span>{cart[item.id]}</span><button aria-label={`Add one ${item.name}`} disabled={cart[item.id] >= 1000} onClick={() => change(item.id, 1)}>+</button></div><strong>{money(item.price * cart[item.id])}</strong></div></div>)}
        {missingItems && <div className="alert" role="alert">Some selected items are no longer available. <button onClick={() => setCart(current => Object.fromEntries(Object.entries(current).filter(([id]) => available.some(item => item.id === Number(id)))))}>Remove unavailable items</button></div>}
        {!lines.length && <p className="muted-copy">Choose items from the menu to begin.</p>}
        <div className="summary-line total-line"><strong>Quoted total</strong><strong>{money(total)}</strong></div><p className="muted-copy">Prices and availability are checked again by staff. This is a request, not a confirmed order.</p><div className="guest-actions"><button className="button secondary" disabled={busy || uncertain || menuLoading} onClick={() => void reloadMenu()}>Refresh menu</button><button className="button primary" disabled={busy || uncertain || menuLoading || invalidTable || missingItems || quoteStale || !lines.length} onClick={() => void submit()}>{busy ? 'Sending…' : 'Send request to staff'}</button></div>
      </section>
    </>}
  </>
}
