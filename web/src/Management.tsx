import { useState } from 'react'
import type { FormEvent } from 'react'
import { api } from './api'
import type { BestSellingReport, MenuInput, MenuItem, PaidSalesReport } from './api'
import { reportDateError } from './reportDates'
import { MenuImage } from './MenuImage'

const errorText = (error: unknown) => error instanceof Error ? error.message : 'Something went wrong.'
const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { maximumFractionDigits: 2 })}`
const emptyMenu: MenuInput = { name: '', description: '', category: '', price: 0, isAvailable: true }

export function MenuEditor({ items, refresh, imageVersions, imageChanged }: { items: MenuItem[]; refresh: () => Promise<void>; imageVersions: Record<number, number>; imageChanged: (id: number) => void }) {
  const [form, setForm] = useState<MenuInput>(emptyMenu)
  const [editing, setEditing] = useState<number | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [open, setOpen] = useState(false)
  const [imageFile, setImageFile] = useState<File | null>(null)
  const [imageInputVersion, setImageInputVersion] = useState(0)
  const [imageNotice, setImageNotice] = useState('')
  const selectedItem = items.find(item => item.id === editing)
  async function changeImage(remove: boolean) {
    if (!selectedItem || busy) return
    if (!remove && (!imageFile || !['image/png', 'image/jpeg'].includes(imageFile.type) || imageFile.size > 2 * 1024 * 1024)) {
      setError('Choose a PNG or JPEG image no larger than 2 MiB.'); return
    }
    setBusy(true); setError(''); setImageNotice('')
    try {
      if (remove) await api.deleteMenuImage(selectedItem.id)
      else if (imageFile) await api.uploadMenuImage(selectedItem.id, imageFile)
      imageChanged(selectedItem.id); setImageFile(null); setImageInputVersion(value => value + 1)
      setImageNotice(remove ? 'Image removed.' : 'Image saved.')
    } catch (e) { setError(errorText(e)) } finally { setBusy(false) }
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      if (editing === null) await api.createMenuItem(form)
      else await api.updateMenuItem(editing, form)
      setForm(emptyMenu); setEditing(null); setOpen(false)
      await refresh()
    } catch (e) { setError(errorText(e)) } finally { setBusy(false) }
  }
  return <div className="management"><div className="management-heading"><h3>Manage menu</h3><button className="button small secondary" disabled={busy} onClick={() => { setOpen(!open); setEditing(null); setForm(emptyMenu); setImageFile(null); setImageNotice(''); setError('') }}>{open ? 'Close' : 'Add item'}</button></div>
    <label className="field">Edit an existing item<select disabled={busy} value={editing ?? ''} onChange={event => {
      const item = items.find(value => value.id === Number(event.target.value))
      if (item) { setEditing(item.id); setForm({ name: item.name, description: item.description ?? '', category: item.category ?? '', price: item.price, isAvailable: item.isAvailable }); setImageFile(null); setImageInputVersion(value => value + 1); setImageNotice(''); setError(''); setOpen(true) }
    }}><option value="">Choose an item</option>{items.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
    {open && <form className="management-form" onSubmit={submit}><label className="field">Name<input required maxLength={100} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label><label className="field">Category<input required value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} /></label><label className="field">Description<input maxLength={300} value={form.description ?? ''} onChange={e => setForm({ ...form, description: e.target.value })} /></label><label className="field">Price (Rs)<input required type="number" min="0" step="0.01" value={form.price} onChange={e => setForm({ ...form, price: Number(e.target.value) })} /></label><label className="checkbox"><input type="checkbox" checked={form.isAvailable} onChange={e => setForm({ ...form, isAvailable: e.target.checked })} /> Available to order</label>{error && <div className="alert" role="alert">{error}</div>}<button className="button primary" disabled={busy}>{busy ? 'Saving…' : editing === null ? 'Add item' : 'Save changes'}</button></form>}
    {selectedItem && <div className="menu-image-editor"><h4>Item image</h4><p className="muted-copy">PNG or JPEG, up to 2 MiB. Save the item before adding an image.</p><MenuImage key={`${selectedItem.id}-${imageVersions[selectedItem.id] ?? 0}`} id={selectedItem.id} name={selectedItem.name} version={imageVersions[selectedItem.id]} /><label className="field">Choose image<input key={imageInputVersion} type="file" accept="image/png,image/jpeg" disabled={busy} onChange={event => { setImageFile(event.target.files?.[0] ?? null); setImageNotice(''); setError('') }} /></label><div className="guest-actions"><button type="button" className="button small secondary" disabled={busy || !imageFile} onClick={() => void changeImage(false)}>Upload image</button><button type="button" className="button small secondary" disabled={busy} onClick={() => void changeImage(true)}>Remove image</button></div>{imageNotice && <p role="status">{imageNotice}</p>}</div>}
  </div>
}

export function TableEditor({ refresh }: { refresh: () => Promise<void> }) {
  const [number, setNumber] = useState('')
  const [capacity, setCapacity] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try { await api.createTable(Number(number), Number(capacity)); setNumber(''); setCapacity(''); await refresh() }
    catch (e) { setError(errorText(e)) } finally { setBusy(false) }
  }
  return <form className="management management-form" onSubmit={submit}><h3>Add a table</h3><label className="field">Table number<input required type="number" min="1" step="1" value={number} onChange={e => setNumber(e.target.value)} /></label><label className="field">Seats<input required type="number" min="1" step="1" value={capacity} onChange={e => setCapacity(e.target.value)} /></label>{error && <div className="alert" role="alert">{error}</div>}<button className="button primary" disabled={busy}>{busy ? 'Adding…' : 'Add table'}</button></form>
}

export function SalesReport() {
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [report, setReport] = useState<{ sales: PaidSalesReport; bestSelling: BestSellingReport } | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  async function load(event: FormEvent) {
    event.preventDefault()
    const validation = reportDateError(fromDate, toDate)
    if (validation) { setError(validation); return }
    setLoading(true); setError(''); setReport(null)
    try {
      const [sales, bestSelling] = await Promise.all([api.salesReport(fromDate, toDate), api.bestSellingReport(fromDate, toDate)])
      setReport({ sales, bestSelling })
    } catch (e) { setError(errorText(e)) } finally { setLoading(false) }
  }
  return <div className="surface"><div className="section-heading"><div><span className="eyebrow">PAID ORDERS</span><h2>Sales overview</h2></div></div>
    <p className="muted-copy">Choose inclusive business dates, up to 366 days. Reports use the restaurant’s configured timezone and payment date.</p>
    <form className="report-form" onSubmit={event => void load(event)}><label className="field">From date<input type="date" min="1900-01-01" max="9998-12-30" required value={fromDate} onChange={event => setFromDate(event.target.value)} /></label><label className="field">To date<input type="date" min="1900-01-01" max="9998-12-30" required value={toDate} onChange={event => setToDate(event.target.value)} /></label><button className="button primary" disabled={loading}>{loading ? 'Loading…' : 'View report'}</button></form>
    {loading && <p role="status">Loading reports…</p>}{error && <div className="alert" role="alert">{error}</div>}
    {report && <><p className="report-period">{report.sales.fromDate} to {report.sales.toDate} · {report.sales.timeZone}</p><p className="muted-copy">Payment window: {report.sales.fromUtc} inclusive to {report.sales.toUtcExclusive} exclusive (UTC).</p><div className="sales-grid"><div><span>PAID ORDERS</span><strong>{report.sales.ordersPaid}</strong></div><div><span>RECORDED TOTAL</span><strong>{money(report.sales.total)}</strong></div></div><div className="report-table-heading"><h3>Best-selling items</h3><span className="count-label">Top {report.bestSelling.items.length} rows</span></div><p className="muted-copy">Grouped by the item name and price recorded at the time of sale. Renamed or repriced items may appear more than once.</p>{report.bestSelling.items.length ? <div className="report-table-wrap"><table className="report-table"><thead><tr><th>Item</th><th>Unit price</th><th>Quantity</th><th>Revenue</th></tr></thead><tbody>{report.bestSelling.items.map((item, index) => <tr key={`${item.menuItemId}-${index}`}><td>{item.name}</td><td>{money(item.unitPrice)}</td><td>{item.quantity}</td><td>{money(item.revenue)}</td></tr>)}</tbody></table></div> : <p className="muted-copy">No paid item sales in this period.</p>}</>}
    <p className="muted-copy">Payments are simulated. This is not a tax or IRD-compliant report.</p></div>
}
