import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from './api'
import type { Ingredient, Supplier } from './api'
import { InventoryHistory } from './InventoryHistory'
import { InventorySuppliers } from './InventorySuppliers'
import { InventoryPurchases } from './InventoryPurchases'

export type UncertainPurchase = { supplier: string; reference: string; pending: boolean }
const loadInventory = () => Promise.all([api.ingredients(), api.suppliers()])

function IngredientHistory({ item, version }: { item: Ingredient; version: number }) {
  const movements = useCallback((before?: number) => api.movements(item.id, before), [item.id])
  const thresholds = useCallback((before?: number) => api.thresholdHistory(item.id, before), [item.id])
  return <div className="inventory-history-grid">
    <InventoryHistory key={`movement-${version}`} title="Stock movements" load={movements} render={entry => <><strong>{entry.kind}: {entry.quantityChange > 0 ? '+' : ''}{entry.quantityChange} {item.unit} · balance {entry.balanceAfter} {item.unit}</strong><span>{entry.actor} · {new Date(entry.occurredAt).toLocaleString()}{entry.orderId !== null ? ` · Order #${entry.orderId}` : ''}{entry.purchaseReceiptLineId !== null ? ` · Purchase line #${entry.purchaseReceiptLineId}` : ''}</span>{entry.reason && <span>Reason: {entry.reason}</span>}</>} />
    <InventoryHistory key={`threshold-${version}`} title="Threshold history" load={thresholds} render={entry => <><strong>{entry.previousThreshold === null ? 'Initial' : entry.previousThreshold} → {entry.newThreshold} {item.unit}</strong><span>{entry.actor} · {new Date(entry.occurredAt).toLocaleString()}</span><span>Reason: {entry.reason}</span></>} />
  </div>
}

function IngredientManager({ ingredients, refresh, initialLowOnly }: { ingredients: Ingredient[]; refresh: () => Promise<void>; initialLowOnly: boolean }) {
  const [name, setName] = useState('')
  const [unit, setUnit] = useState('')
  const [opening, setOpening] = useState('0')
  const [initialThreshold, setInitialThreshold] = useState('0')
  const [selectedId, setSelectedId] = useState('')
  const [action, setAction] = useState<'stock' | 'waste' | 'threshold'>('stock')
  const [quantity, setQuantity] = useState('')
  const [reason, setReason] = useState('')
  const [lowOnly, setLowOnly] = useState(initialLowOnly)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [version, setVersion] = useState(0)
  const item = ingredients.find(value => value.id === Number(selectedId))
  const visible = lowOnly ? ingredients.filter(value => value.isLowStock) : ingredients
  async function create(event: FormEvent) {
    event.preventDefault()
    if (!name.trim() || !unit.trim()) return
    setBusy(true); setError(''); setNotice('')
    try {
      await api.createIngredient(name.trim(), unit.trim(), Number(opening), Number(initialThreshold))
      setName(''); setUnit(''); setOpening('0'); setInitialThreshold('0'); setNotice('Ingredient created.')
      await refresh()
    } catch (e) { setError(e instanceof Error ? e.message : 'Unable to create ingredient.') } finally { setBusy(false) }
  }
  async function adjust(event: FormEvent) {
    event.preventDefault()
    if (!item || !reason.trim() || !quantity) return
    setBusy(true); setError(''); setNotice('')
    try {
      if (action === 'stock') await api.addStock(item.id, Number(quantity), item.unit, reason.trim())
      else if (action === 'waste') await api.waste(item.id, Number(quantity), item.unit, reason.trim())
      else await api.setThreshold(item.id, Number(quantity), reason.trim())
      setQuantity(''); setReason(''); setVersion(value => value + 1); setNotice('Inventory updated.')
      await refresh()
    } catch (e) { setError(e instanceof Error ? e.message : 'Unable to update inventory.') } finally { setBusy(false) }
  }
  return <section><div className="section-heading"><div><span className="eyebrow">STOCK CONTROL</span><h2>Ingredients</h2></div><span className="count-label">{ingredients.filter(value => value.isLowStock).length} low stock</span></div>
    <p className="muted-copy">Stock and thresholds are shown in each ingredient’s exact unit. Purchase receipts and adjustments are recorded in the ledger.</p>
    {error && <div className="alert" role="alert">{error}</div>}{notice && <div className="notice" role="status">{notice}</div>}
    <label className="checkbox"><input type="checkbox" checked={lowOnly} onChange={event => setLowOnly(event.target.checked)} /> Show low-stock only</label>
    <div className="stock-list">{visible.map(value => <div className="stock-row" key={value.id}><div><strong>{value.name} {value.isLowStock && <span className="inventory-low">LOW STOCK</span>}</strong><small>Threshold: {value.lowStockThreshold} {value.unit}</small></div><div className="payment-actions"><strong>{value.stock} {value.unit}</strong><button className="button small secondary" onClick={() => { setSelectedId(String(value.id)); setQuantity(''); setReason('') }}>Manage</button></div></div>)}{!visible.length && <p className="muted-copy">No ingredients to show.</p>}</div>
    {item && <><form className="management management-form" onSubmit={event => void adjust(event)}><h3>Manage {item.name}</h3>
      <label className="field">Action<select value={action} onChange={event => { setAction(event.target.value as 'stock' | 'waste' | 'threshold'); setQuantity('') }}><option value="stock">Add stock</option><option value="waste">Record waste</option><option value="threshold">Change low-stock threshold</option></select></label>
      <label className="field">{action === 'threshold' ? 'New threshold' : 'Quantity'} ({item.unit})<input type="number" required min={action === 'threshold' ? '0' : '0.001'} max="99999999999.999" step="0.001" value={quantity} onChange={event => setQuantity(event.target.value)} /></label>
      <label className="field">Reason<input required maxLength={300} value={reason} onChange={event => setReason(event.target.value)} /></label><button className="button primary" disabled={busy || !reason.trim()}>{busy ? 'Saving…' : 'Save adjustment'}</button>
    </form><IngredientHistory key={item.id} item={item} version={version} /></>}
    <form className="management management-form" onSubmit={event => void create(event)}><h3>Add ingredient</h3>
      <label className="field">Name<input required maxLength={100} value={name} onChange={event => setName(event.target.value)} /></label>
      <label className="field">Unit<input required maxLength={30} value={unit} onChange={event => setUnit(event.target.value)} placeholder="e.g. kg" /></label>
      <label className="field">Opening stock<input required type="number" min="0" max="99999999999.999" step="0.001" value={opening} onChange={event => setOpening(event.target.value)} /></label>
      <label className="field">Low-stock threshold<input required type="number" min="0" max="99999999999.999" step="0.001" value={initialThreshold} onChange={event => setInitialThreshold(event.target.value)} /></label>
      <button className="button primary" disabled={busy || !name.trim() || !unit.trim()}>{busy ? 'Saving…' : 'Add ingredient'}</button>
    </form>
  </section>
}

export function Inventory({ uncertain, setUncertain, initialLowOnly = false }: { uncertain: UncertainPurchase | null; setUncertain: (value: UncertainPurchase | null) => void; initialLowOnly?: boolean }) {
  const [ingredients, setIngredients] = useState<Ingredient[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [tab, setTab] = useState<'ingredients' | 'suppliers' | 'purchases'>('ingredients')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const refresh = useCallback(async () => {
    setError('')
    try {
      const [nextIngredients, nextSuppliers] = await loadInventory()
      setIngredients(nextIngredients); setSuppliers(nextSuppliers)
    } catch (e) { setError(e instanceof Error ? e.message : 'Unable to load inventory.') }
    finally { setLoading(false) }
  }, [])
  useEffect(() => {
    let active = true
    void loadInventory().then(([nextIngredients, nextSuppliers]) => {
      if (active) { setIngredients(nextIngredients); setSuppliers(nextSuppliers) }
    }).catch(e => { if (active) setError(e instanceof Error ? e.message : 'Unable to load inventory.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])
  return <div className="surface"><div className="inventory-tabs" aria-label="Inventory sections">{(['ingredients', 'suppliers', 'purchases'] as const).map(value => <button key={value} type="button" className={`button small ${tab === value ? 'primary' : 'secondary'}`} aria-pressed={tab === value} onClick={() => setTab(value)}>{value === 'purchases' ? 'Purchase receipts' : value[0].toUpperCase() + value.slice(1)}</button>)}<button type="button" className="button small secondary" onClick={() => void refresh()}>Refresh stock</button></div>
    {loading && <p role="status">Loading inventory…</p>}{error && <div className="alert" role="alert">{error}</div>}
    {!loading && !error && <><div hidden={tab !== 'ingredients'}><IngredientManager ingredients={ingredients} refresh={refresh} initialLowOnly={initialLowOnly} /></div><div hidden={tab !== 'suppliers'}><InventorySuppliers suppliers={suppliers} refresh={refresh} /></div><div hidden={tab !== 'purchases'}><InventoryPurchases ingredients={ingredients} suppliers={suppliers} refresh={refresh} uncertain={uncertain} setUncertain={setUncertain} /></div></>}
  </div>
}
