import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import type { Order, Table } from './api'
import { useRestaurant } from './restaurantProfile'
import { RestaurantProfileNotice } from './RestaurantProfileNotice'

const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

export function Receipt({ order, table, close }: { order: Order; table: Table | undefined; close: () => void }) {
  const { profile, loading, error, refresh } = useRestaurant()
  const [checkingProfile, setCheckingProfile] = useState(true)
  const closeButton = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    let active = true
    void refresh().then(() => { if (active) setCheckingProfile(false) })
    return () => { active = false }
  }, [refresh])
  useEffect(() => {
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const root = document.getElementById('root')
    const previousInert = root?.inert ?? false
    const previousOverflow = document.body.style.overflow
    if (root) root.inert = true
    document.body.style.overflow = 'hidden'
    closeButton.current?.focus()
    return () => {
      if (root) root.inert = previousInert
      document.body.style.overflow = previousOverflow
      previous?.focus()
    }
  }, [])
  if (!order.payment) return null
  const payment = order.payment
  return createPortal(<div className="receipt-overlay" onKeyDown={event => {
    if (event.key === 'Escape') close()
    if (event.key === 'Tab') {
      const buttons = event.currentTarget.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')
      if (buttons.length && ((event.shiftKey && document.activeElement === buttons[0]) || (!event.shiftKey && document.activeElement === buttons[buttons.length - 1]))) {
        event.preventDefault()
        ;(event.shiftKey ? buttons[buttons.length - 1] : buttons[0]).focus()
      }
    }
  }}>
    <section className="receipt-dialog" role="dialog" aria-modal="true" aria-labelledby="receipt-title">
      <div className="receipt-dialog-head"><div><span className="eyebrow">PAYMENT RECORD</span><h2 id="receipt-title">Receipt</h2></div><button ref={closeButton} type="button" className="button secondary small" onClick={close}>Close</button></div>
      <div className="receipt-profile-status">{checkingProfile && <p className="muted-copy" role="status">Refreshing restaurant details...</p>}<RestaurantProfileNotice /></div>
      <div className="print-receipt" tabIndex={0} aria-label="Receipt details">
        <header><strong>{profile?.name || 'aahar.'}</strong>{profile?.tagline && <p>{profile.tagline}</p>}<div className="receipt-business">{profile?.address && <p>{profile.address}</p>}{profile?.phone && <p>{profile.phone}</p>}{profile?.email && <p>{profile.email}</p>}{profile?.website && <p>{profile.website}</p>}</div><p>SIMULATED · NOT A TAX INVOICE</p></header>
        <div className="receipt-meta"><span>Order #{order.id}</span><span>Payment #{payment.id}</span><span>{order.tableId === null ? 'Takeaway' : table ? `Table ${table.number}` : `Table #${order.tableId}`}</span><span>{new Date(payment.paidAt).toLocaleString()}</span></div>
        <table><thead><tr><th>Item / quantity</th><th>Unit</th><th>Amount</th></tr></thead><tbody>{order.items.map((item, index) => <tr key={`${item.menuItemId}-${index}`}><td>{item.name}<br /><span>Qty {item.quantity}</span></td><td>{money(item.unitPrice)}</td><td>{money(item.quantity * item.unitPrice)}</td></tr>)}</tbody></table>
        <div className="receipt-total"><span>Total</span><strong>{money(payment.amount)}</strong></div>
        <div className="receipt-meta"><span>Status: Paid</span><span>Method: {payment.method}</span></div>
        {profile?.receiptFooter && <p className="receipt-business-footer">{profile.receiptFooter}</p>}
        {(checkingProfile || error || !profile) && <p className="receipt-disclaimer">Restaurant details unavailable. Reload before printing.</p>}
      </div>
      <div className="receipt-dialog-actions"><button type="button" className="button primary" disabled={checkingProfile || loading || !!error || !profile} onClick={() => window.print()}>Print / Save as PDF</button></div>
    </section>
  </div>, document.body)
}
