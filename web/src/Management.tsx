import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from './api'
import type { Ingredient, MenuInput, MenuItem, Sales } from './api'

const errorText = (error: unknown) => error instanceof Error ? error.message : 'Something went wrong.'
const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { maximumFractionDigits: 2 })}`
const emptyMenu: MenuInput = { name: '', description: '', category: '', price: 0, isAvailable: true }

export function MenuEditor({ items, refresh }: { items: MenuItem[]; refresh: () => Promise<void> }) {
  const [form, setForm] = useState<MenuInput>(emptyMenu)
  const [editing, setEditing] = useState<number | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [open, setOpen] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      if (editing === null) await api.createMenuItem(form)
      else await api.updateMenuItem(editing, form)
      setForm(emptyMenu); setEditing(null); setOpen(false)
      await refresh()
    } catch (e) { setError(errorText(e)) } finally { setBusy(false) }
  }
  return <div className="management"><div className="management-heading"><h3>Manage menu</h3><button className="button small secondary" onClick={() => { setOpen(!open); setEditing(null); setForm(emptyMenu) }}>{open ? 'Close' : 'Add item'}</button></div>
    <label className="field">Edit an existing item<select value={editing ?? ''} onChange={event => {
      const item = items.find(value => value.id === Number(event.target.value))
      if (item) { setEditing(item.id); setForm({ name: item.name, description: item.description ?? '', category: item.category ?? '', price: item.price, isAvailable: item.isAvailable }); setOpen(true) }
    }}><option value="">Choose an item</option>{items.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
    {open && <form className="management-form" onSubmit={submit}><label className="field">Name<input required maxLength={100} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label><label className="field">Category<input required value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} /></label><label className="field">Description<input maxLength={300} value={form.description ?? ''} onChange={e => setForm({ ...form, description: e.target.value })} /></label><label className="field">Price (Rs)<input required type="number" min="0" step="0.01" value={form.price} onChange={e => setForm({ ...form, price: Number(e.target.value) })} /></label><label className="checkbox"><input type="checkbox" checked={form.isAvailable} onChange={e => setForm({ ...form, isAvailable: e.target.checked })} /> Available to order</label>{error && <div className="alert" role="alert">{error}</div>}<button className="button primary" disabled={busy}>{busy ? 'Saving…' : editing === null ? 'Add item' : 'Save changes'}</button></form>}
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

export function Inventory() {
  const [ingredients, setIngredients] = useState<Ingredient[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [name, setName] = useState('')
  const [unit, setUnit] = useState('')
  const [stock, setStock] = useState('')
  const [quantities, setQuantities] = useState<Record<number, string>>({})
  const [busy, setBusy] = useState(false)
  async function load() {
    setLoading(true); setError('')
    try { setIngredients(await api.ingredients()) } catch (e) { setError(errorText(e)) } finally { setLoading(false) }
  }
  useEffect(() => { void api.ingredients().then(setIngredients).catch(e => setError(errorText(e))).finally(() => setLoading(false)) }, [])
  async function addIngredient(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try { await api.createIngredient(name, unit, Number(stock)); setName(''); setUnit(''); setStock(''); await load() }
    catch (e) { setError(errorText(e)) } finally { setBusy(false) }
  }
  async function addStock(event: FormEvent, ingredient: Ingredient) {
    event.preventDefault(); setBusy(true); setError('')
    try { await api.addStock(ingredient.id, Number(quantities[ingredient.id])); setQuantities(current => ({ ...current, [ingredient.id]: '' })); await load() }
    catch (e) { setError(errorText(e)) } finally { setBusy(false) }
  }
  return <div className="surface"><div className="section-heading"><div><span className="eyebrow">STOCK ON HAND</span><h2>Ingredients</h2></div><span className="count-label">{ingredients.length} items</span></div>{error && <div className="alert" role="alert">{error} <button onClick={() => void load()}>Retry</button></div>}{loading && <p role="status">Loading ingredients…</p>}{!loading && !ingredients.length && !error && <p className="muted-copy">No ingredients yet. Add one below.</p>}<div className="stock-list">{ingredients.map(item => <div className="stock-row" key={item.id}><div><strong>{item.name}</strong><small>{item.stock} {item.unit} on hand</small></div><form onSubmit={e => void addStock(e, item)}><label className="sr-only" htmlFor={`stock-${item.id}`}>Add stock for {item.name}</label><input id={`stock-${item.id}`} type="number" min="0.01" step="any" required placeholder="Quantity" value={quantities[item.id] ?? ''} onChange={e => setQuantities({ ...quantities, [item.id]: e.target.value })} /><button className="button small secondary" disabled={busy}>Add stock</button></form></div>)}</div><form className="management management-form" onSubmit={addIngredient}><h3>Add an ingredient</h3><label className="field">Name<input required value={name} onChange={e => setName(e.target.value)} /></label><label className="field">Unit<input required placeholder="e.g. kg" value={unit} onChange={e => setUnit(e.target.value)} /></label><label className="field">Opening stock<input required type="number" min="0" step="any" value={stock} onChange={e => setStock(e.target.value)} /></label><button className="button primary" disabled={busy}>{busy ? 'Adding…' : 'Add ingredient'}</button></form></div>
}

export function SalesReport() {
  const [sales, setSales] = useState<Sales | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  function load() {
    setLoading(true); setError('')
    void api.sales().then(setSales).catch(e => setError(errorText(e))).finally(() => setLoading(false))
  }
  useEffect(() => { void api.sales().then(setSales).catch(e => setError(errorText(e))).finally(() => setLoading(false)) }, [])
  return <div className="surface"><div className="section-heading"><div><span className="eyebrow">PAID ORDERS</span><h2>Sales overview</h2></div></div>{loading && <p role="status">Loading sales…</p>}{error && <div className="alert" role="alert">{error} <button onClick={load}>Retry</button></div>}{sales && <div className="sales-grid"><div><span>PAID ORDERS</span><strong>{sales.ordersPaid}</strong></div><div><span>RECORDED TOTAL</span><strong>{money(sales.total)}</strong></div></div>}<p className="muted-copy">Payments are simulated. This is not a tax or IRD-compliant report.</p></div>
}
