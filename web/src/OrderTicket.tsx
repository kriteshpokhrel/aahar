import { useState } from 'react'
import type { FormEvent } from 'react'
import type { Order, PaymentMethod, StockDisposition, Table } from './api'

const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { maximumFractionDigits: 2 })}`

export function OrderTicket({ order, tables, busy, canServe, canComplete, canCancelSubmitted, update, cancel }: {
  order: Order
  tables: Table[]
  busy: boolean
  canServe: boolean
  canComplete: boolean
  canCancelSubmitted: boolean
  update: (order: Order, action: 'send' | 'complete' | 'pay', method?: PaymentMethod) => Promise<void>
  cancel: (order: Order, reason: string, stockDisposition: StockDisposition) => Promise<boolean>
}) {
  const [receiptOpen, setReceiptOpen] = useState(false)
  const [cancelOpen, setCancelOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [disposition, setDisposition] = useState<'' | 'Return' | 'Waste'>('')
  const status = order.status.toLowerCase()
  const table = tables.find(item => item.id === order.tableId)
  const canCancel = (status === 'draft' && canServe) || (['sent', 'completed'].includes(status) && canCancelSubmitted)
  async function submitCancellation(event: FormEvent) {
    event.preventDefault()
    const trimmed = reason.trim()
    const stockDisposition = status === 'draft' ? 'None' : disposition
    if (!trimmed || !stockDisposition) return
    const succeeded = await cancel(order, trimmed, stockDisposition)
    if (succeeded) { setCancelOpen(false); setReason(''); setDisposition('') }
  }
  return <article className="ticket">
    <div className="ticket-top"><div><span className="eyebrow">ORDER #{order.id}</span><h3>{table ? `Table ${table.number}` : `Table #${order.tableId}`}</h3></div><span className="status status-busy">{order.status}</span></div>
    <div className="ticket-items">{order.items.map((item, index) => <div key={`${item.menuItemId}-${index}`}><span><b>{item.quantity}×</b> {item.name}</span><span>{money(item.quantity * item.unitPrice)}</span></div>)}</div>
    <div className="ticket-bottom"><span>{money(order.total)}</span><div className="ticket-actions">
      {canServe && status === 'draft' && <button className="button small secondary" disabled={busy} onClick={() => void update(order, 'send')}>Send to kitchen</button>}
      {canComplete && status === 'sent' && <button className="button small secondary" disabled={busy} onClick={() => void update(order, 'complete')}>Mark complete</button>}
      {canServe && status === 'completed' && <div className="payment-actions"><button className="button small secondary" disabled={busy} onClick={() => void update(order, 'pay', 'Cash')}>Simulate cash</button><button className="button small secondary" disabled={busy} onClick={() => void update(order, 'pay', 'Card')}>Simulate card</button></div>}
      {status === 'paid' && order.payment && <button className="button small secondary" aria-expanded={receiptOpen} onClick={() => setReceiptOpen(!receiptOpen)}>{receiptOpen ? 'Hide receipt' : 'View receipt'}</button>}
      {canCancel && <button className="button small secondary cancel-toggle" disabled={busy} aria-expanded={cancelOpen} onClick={() => setCancelOpen(!cancelOpen)}>{cancelOpen ? 'Keep order' : 'Cancel order'}</button>}
    </div></div>
    {cancelOpen && canCancel && <form className="cancel-form" onSubmit={event => void submitCancellation(event)}>
      <label className="field">Reason for cancellation<textarea required maxLength={300} value={reason} onChange={event => setReason(event.target.value)} /></label>
      {status === 'draft' ? <p>Draft orders have not deducted stock.</p> : <fieldset><legend>What should happen to deducted stock?</legend><label><input type="radio" name={`disposition-${order.id}`} value="Return" checked={disposition === 'Return'} onChange={() => setDisposition('Return')} required /> Return reusable stock</label><label><input type="radio" name={`disposition-${order.id}`} value="Waste" checked={disposition === 'Waste'} onChange={() => setDisposition('Waste')} required /> Mark as waste</label><p>Older submitted orders without a stock ledger may be rejected.</p></fieldset>}
      <button className="button small primary" disabled={busy || !reason.trim() || (status !== 'draft' && !disposition)}>Confirm cancellation</button>
    </form>}
    {status === 'cancelled' && <div className="cancellation-details"><strong>Cancellation</strong><p>{order.cancellationReason || 'Reason unavailable'}</p><small>{order.cancelledBy ? `By ${order.cancelledBy}` : ''}{order.cancellationStockDisposition ? ` · Stock: ${order.cancellationStockDisposition}` : ''}</small></div>}
    {receiptOpen && order.payment && <section className="receipt" aria-label={`Simulated receipt for order ${order.id}`}><span className="eyebrow">SIMULATED RECEIPT · NOT IRD COMPLIANT</span><h4>Payment summary</h4><div><span>Order</span><strong>#{order.id}</strong></div>{order.items.map((item, index) => <div key={`${item.menuItemId}-${index}`}><span>{item.quantity} × {item.name}</span><strong>{money(item.quantity * item.unitPrice)}</strong></div>)}<div><span>Method</span><strong>{order.payment.method} (simulated)</strong></div><div><span>Recorded</span><strong>{new Date(order.payment.paidAt).toLocaleString()}</strong></div><div><span>Amount</span><strong>{money(order.payment.amount)}</strong></div><p>No money was collected. This is not a tax invoice.</p></section>}
  </article>
}
