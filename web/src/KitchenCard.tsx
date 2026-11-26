import type { KitchenTicket, Table } from './api'

export function KitchenCard({ ticket, tables, busy, canPrepare, advance }: {
  ticket: KitchenTicket
  tables: Table[]
  busy: boolean
  canPrepare: boolean
  advance: (ticket: KitchenTicket, action: 'prepare' | 'ready') => Promise<void>
}) {
  const table = tables.find(item => item.id === ticket.tableId)
  const status = ticket.status.toLowerCase()
  return <article className="ticket">
    <div className="ticket-top"><div><span className="eyebrow">KITCHEN TICKET #{ticket.id} · ORDER #{ticket.orderId}</span><h3>{ticket.tableId === null ? 'Takeaway' : table ? `Table ${table.number}` : `Table #${ticket.tableId}`}</h3></div><span className="status status-busy">{ticket.status}</span></div>
    {ticket.isReconstructed && <div className="alert reconstruction">Reconstructed ticket: verify actual kitchen status before preparation. Time shown is the original order time.</div>}
    <div className="ticket-items">{ticket.items.map(item => <div key={item.menuItemId}><span><b>{item.quantity}×</b> {item.name}{item.notes && <small className="ticket-note">Note: {item.notes}</small>}</span></div>)}</div>
    <div className="ticket-bottom"><span className="ticket-time">{new Date(ticket.createdAt).toLocaleString()}</span>{canPrepare && status === 'queued' && <button className="button small secondary" disabled={busy} onClick={() => void advance(ticket, 'prepare')}>Start preparing</button>}{canPrepare && status === 'preparing' && <button className="button small secondary" disabled={busy} onClick={() => void advance(ticket, 'ready')}>Mark ready</button>}</div>
  </article>
}
