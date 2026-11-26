import { useState } from 'react'
import type { FormEvent } from 'react'
import type { MenuItem, Order, OrderLine } from './api'

export function DraftEditor({ order, menu, busy, save, close }: {
  order: Order
  menu: MenuItem[]
  busy: boolean
  save: (order: Order, items: OrderLine[]) => Promise<boolean>
  close: () => void
}) {
  const [lines, setLines] = useState<OrderLine[]>(() => order.items.map(({ menuItemId, quantity, notes }) => ({ menuItemId, quantity, notes })))
  const [addId, setAddId] = useState('')
  const available = menu.filter(item => item.isAvailable && !lines.some(line => line.menuItemId === item.id))
  function change(id: number, patch: Partial<OrderLine>) {
    setLines(current => current.map(line => line.menuItemId === id ? { ...line, ...patch } : line))
  }
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!lines.length) return
    if (await save(order, lines)) close()
  }
  return <form className="draft-editor" onSubmit={event => void submit(event)}>
    <h4>Edit draft items</h4>
    <p>Existing items keep their original price. Unavailable items can only be retained, reduced or removed.</p>
    {lines.map(line => {
      const current = menu.find(item => item.id === line.menuItemId)
      const original = order.items.find(item => item.menuItemId === line.menuItemId)
      const name = original?.name ?? current?.name ?? `Item #${line.menuItemId}`
      const max = current?.isAvailable ? 1000 : original?.quantity ?? 1
      return <div className="draft-line" key={line.menuItemId}>
        <div className="draft-line-head"><strong>{name}</strong><button type="button" className="button small secondary" onClick={() => setLines(lines.filter(item => item.menuItemId !== line.menuItemId))}>Remove</button></div>
        <label className="field">Quantity<input type="number" min="1" max={max} step="1" required value={line.quantity} onChange={event => change(line.menuItemId, { quantity: Number(event.target.value) })} /></label>
        <label className="field">Kitchen note<input maxLength={300} value={line.notes ?? ''} onChange={event => change(line.menuItemId, { notes: event.target.value || null })} /></label>
        {!current?.isAvailable && <small>Unavailable · maximum {max}</small>}
      </div>
    })}
    {available.length > 0 && <div className="draft-add"><label className="field">Add available item<select value={addId} onChange={event => setAddId(event.target.value)}><option value="">Select item</option>{available.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label><button type="button" className="button small secondary" disabled={!addId || lines.length >= 100} onClick={() => { setLines([...lines, { menuItemId: Number(addId), quantity: 1, notes: null }]); setAddId('') }}>Add</button></div>}
    {!lines.length && <p role="alert">Add at least one item, or cancel the order instead.</p>}
    <div className="payment-actions"><button type="button" className="button small secondary" onClick={close}>Discard edits</button><button className="button small primary" disabled={busy || !lines.length}>Save draft</button></div>
  </form>
}
