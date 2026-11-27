import { useState } from 'react'
import type { KitchenTicket, Order, PaymentMethod, Table } from './api'
import { billReadiness, tableLabel } from './bill'
import { statusClass } from './overviewMetrics'

const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

export function RunningBill({ order, tables, tickets, busy, stale, hasNewItems, hasPendingRound, canServe, defaultPayment, onSendDraft, onEditDraft, onPay }: {
  order: Order; tables: Table[]; tickets: KitchenTicket[]; busy: boolean; stale: boolean; hasNewItems: boolean; hasPendingRound: boolean; canServe: boolean
  defaultPayment: PaymentMethod
  onSendDraft: (order: Order) => Promise<void>; onEditDraft: (order: Order) => void; onPay: (order: Order, method: PaymentMethod) => Promise<void>
}) {
  const [review, setReview] = useState(false)
  const [method, setMethod] = useState<PaymentMethod>(defaultPayment)
  const rounds = tickets.filter(ticket => ticket.orderId === order.id)
  const status = order.status.toLowerCase()
  const canPay = canServe && !busy && !stale && !hasNewItems && !hasPendingRound && billReadiness(order, tickets)
  return <section className="running-bill" aria-label={`Running bill for ${tableLabel(order, tables)}`}>
    <div className="running-bill-heading"><div><span className="eyebrow">OPEN BILL · ORDER #{order.id}</span><h3>{tableLabel(order, tables)}</h3></div><span className={`status ${statusClass(order.status)}`}>{order.status}</span></div>
    <p>{status === 'draft' ? 'Draft items have not been sent to the kitchen.' : 'Previously sent items are preserved on this bill.'}</p>
    <div className="bill-lines">{order.items.map((item, index) => <div key={`${item.menuItemId}-${index}`}><span>{item.quantity} × {item.name}{item.notes && <small>Note: {item.notes}</small>}</span><strong>{money(item.quantity * item.unitPrice)}</strong></div>)}</div>
    <div className="bill-total"><span>Bill total</span><strong>{money(order.total)}</strong></div>
    {status !== 'draft' && <p>{stale ? 'Refresh to check kitchen status.' : `${rounds.length} kitchen ${rounds.length === 1 ? 'round' : 'rounds'} · ${rounds.filter(ticket => ticket.status.toLowerCase() === 'ready').length} ready`}</p>}
    {status === 'draft' && canServe && <div className="bill-actions"><button className="button small secondary" disabled={busy || stale || hasNewItems || hasPendingRound} onClick={() => onEditDraft(order)}>Edit draft</button><button className="button small primary" disabled={busy || stale || hasNewItems || hasPendingRound} onClick={() => void onSendDraft(order)}>Send draft to kitchen</button></div>}
    {status !== 'draft' && canServe && <><button className="button small secondary" disabled={!canPay} aria-expanded={review} onClick={() => setReview(!review)}>{review ? 'Close checkout' : 'Review full bill'}</button>
      {!canPay && <p>{stale ? 'Refresh the workspace before checkout.' : hasPendingRound ? 'Resolve the pending round before checkout.' : hasNewItems ? 'Send or clear new items before checkout.' : status === 'sent' && order.roundCount === undefined ? 'This server requires a kitchen user or manager to mark the order complete before checkout.' : !billReadiness(order, tickets) ? 'All kitchen rounds must be ready before checkout.' : ''}</p>}
      {review && canPay && <div className="bill-checkout"><strong>Full balance · {money(order.total)}</strong><p>Simulated payment only. No payment is processed.</p><label className="field">Payment method<select value={method} onChange={event => setMethod(event.target.value === 'Card' ? 'Card' : 'Cash')}><option value="Cash">Cash (simulated)</option><option value="Card">Card (simulated)</option></select></label><div className="bill-actions"><button className="button small primary" onClick={() => void onPay(order, method)}>Simulate full payment</button></div></div>}</>}
  </section>
}
