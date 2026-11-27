import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from './api'
import type { Ingredient, PurchaseReceipt, Supplier } from './api'
import type { UncertainPurchase } from './Inventory'
import { uncertainMutationOutcome } from './orderSafety'

type DraftLine = { key: number; ingredientId: string; quantity: string; unitCost: string }
const blankLine = (key: number): DraftLine => ({ key, ingredientId: '', quantity: '', unitCost: '' })
const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

export function InventoryPurchases({ ingredients, suppliers, refresh, uncertain, setUncertain }: {
  ingredients: Ingredient[]; suppliers: Supplier[]; refresh: () => Promise<void>
  uncertain: UncertainPurchase | null; setUncertain: (value: UncertainPurchase | null) => void
}) {
  const [supplierId, setSupplierId] = useState('')
  const [reference, setReference] = useState('')
  const [reason, setReason] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([blankLine(1)])
  const nextKey = useRef(2)
  const submitting = useRef(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [beforeId, setBeforeId] = useState<number | undefined>()
  const [revision, setRevision] = useState(0)
  const [receipts, setReceipts] = useState<PurchaseReceipt[]>([])
  const [listError, setListError] = useState('')
  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<PurchaseReceipt | null>(null)
  const [detailError, setDetailError] = useState('')
  const [detailLoading, setDetailLoading] = useState(false)
  const detailRequest = useRef(0)
  useEffect(() => {
    let active = true
    void api.purchaseReceipts(beforeId).then(result => { if (active) setReceipts(result) })
      .catch(e => { if (active) { setReceipts([]); setListError(e instanceof Error ? e.message : 'Unable to load receipts.') } })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [beforeId, revision])
  function reload() { setLoading(true); setListError(''); setRevision(value => value + 1) }
  function newest() { setBeforeId(undefined); reload() }
  async function showDetail(id: number) {
    const request = ++detailRequest.current
    setDetailLoading(true); setDetailError(''); setDetail(null)
    try { const result = await api.purchaseReceipt(id); if (request === detailRequest.current) setDetail(result) }
    catch (e) { if (request === detailRequest.current) setDetailError(e instanceof Error ? e.message : 'Unable to load receipt.') }
    finally { if (request === detailRequest.current) setDetailLoading(false) }
  }
  function updateLine(key: number, patch: Partial<DraftLine>) { setLines(current => current.map(line => line.key === key ? { ...line, ...patch } : line)) }
  const duplicate = new Set(lines.filter(line => line.ingredientId).map(line => line.ingredientId)).size !== lines.filter(line => line.ingredientId).length
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (submitting.current || uncertain || duplicate || !reference.trim() || !reason.trim()) return
    const supplier = suppliers.find(value => value.id === Number(supplierId))
    if (!supplier) { setError('Select a supplier.'); return }
    const input = lines.map(line => {
      const ingredient = ingredients.find(value => value.id === Number(line.ingredientId))
      return ingredient ? { ingredientId: ingredient.id, quantity: Number(line.quantity), unit: ingredient.unit, unitCost: Number(line.unitCost) } : null
    })
    if (input.some(line => line === null)) { setError('Select an ingredient for every line.'); return }
    submitting.current = true; setBusy(true); setError(''); setNotice('')
    setUncertain({ supplier: supplier.name, reference: reference.trim(), pending: true })
    let received: PurchaseReceipt
    try {
      received = await api.receivePurchase(supplier.id, reference.trim(), reason.trim(), input.filter(line => line !== null))
    } catch (e) {
      const text = e instanceof Error ? e.message : 'Unable to record purchase.'
      if (uncertainMutationOutcome(e)) setUncertain({ supplier: supplier.name, reference: reference.trim(), pending: false })
      else setUncertain(null)
      setError(text); submitting.current = false; setBusy(false); return
    }
    setUncertain(null)
    setSupplierId(''); setReference(''); setReason(''); setLines([blankLine(nextKey.current++)])
    detailRequest.current++; setDetailLoading(false); setDetailError('')
    setNotice(`Purchase receipt #${received.id} recorded.`); setDetail(received); newest()
    await refresh()
    submitting.current = false; setBusy(false)
  }
  return <section><div className="section-heading"><div><span className="eyebrow">GOODS RECEIVED</span><h2>Purchase receipts</h2></div></div>
    <p className="muted-copy">Receipts record supplier and ingredient snapshots. Each ingredient can appear once per receipt; quantities use its exact unit.</p>
    {uncertain && <div className="alert" role="alert"><div>{uncertain.pending ? <><strong>Submission in progress.</strong> Do not submit another receipt until this finishes.</> : <><strong>Submission outcome unknown.</strong> Check the receipts below and with other staff for supplier “{uncertain.supplier}”, reference “{uncertain.reference}” before making another submission. The API does not support safe automatic retries.<div><button type="button" className="button small secondary" onClick={() => { setUncertain(null); setSupplierId(''); setReference(''); setReason(''); setLines([blankLine(nextKey.current++)]); setError('') }}>I have checked; clear this form</button></div></>}</div></div>}
    {error && <div className="alert" role="alert">{error}</div>}{notice && <div className="notice" role="status">{notice}</div>}
    <form className="management management-form" onSubmit={event => void submit(event)}><h3>Record purchase</h3>
      <label className="field">Supplier<select required value={supplierId} onChange={event => setSupplierId(event.target.value)}><option value="">Select supplier</option>{suppliers.map(supplier => <option value={supplier.id} key={supplier.id}>{supplier.name}</option>)}</select></label>
      <label className="field">Supplier reference<input required maxLength={100} value={reference} onChange={event => setReference(event.target.value)} /></label>
      <label className="field">Reason<input required maxLength={300} value={reason} onChange={event => setReason(event.target.value)} /></label>
      <div className="purchase-lines"><h4>Receipt lines</h4>{lines.map((line, index) => {
        const ingredient = ingredients.find(value => value.id === Number(line.ingredientId))
        return <div className="purchase-line" key={line.key}>
          <label className="field">Ingredient {index + 1}<select required value={line.ingredientId} onChange={event => updateLine(line.key, { ingredientId: event.target.value })}><option value="">Select ingredient</option>{ingredients.map(value => <option value={value.id} key={value.id}>{value.name} ({value.unit})</option>)}</select></label>
          <label className="field">Quantity {ingredient ? `(${ingredient.unit})` : ''}<input required type="number" min="0.001" max="99999999999.999" step="0.001" value={line.quantity} onChange={event => updateLine(line.key, { quantity: event.target.value })} /></label>
          <label className="field">Cost per unit (Rs)<input required type="number" min="0" max="9999999999.99" step="0.01" value={line.unitCost} onChange={event => updateLine(line.key, { unitCost: event.target.value })} /></label>
          <button type="button" className="button small secondary" aria-label={`Remove ingredient ${index + 1}`} disabled={lines.length === 1} onClick={() => setLines(current => current.filter(value => value.key !== line.key))}>Remove</button>
        </div>
      })}<button type="button" className="button small secondary" disabled={lines.length >= 100} onClick={() => setLines(current => [...current, blankLine(nextKey.current++)])}>Add line</button>
      {duplicate && <p className="inventory-low" role="alert">Each ingredient can appear only once.</p>}</div>
      <button className="button primary" disabled={busy || !!uncertain || duplicate || !suppliers.length || !ingredients.length || !reference.trim() || !reason.trim()}>{busy ? 'Recording…' : 'Record purchase'}</button>
    </form>
    <div className="management-heading"><h3>Receipt history</h3><button type="button" className="button small secondary" disabled={loading} onClick={newest}>Refresh newest</button></div>
    {loading && <p role="status">Loading receipts…</p>}{listError && <div className="alert" role="alert">{listError}</div>}
    {!loading && !listError && !receipts.length && <p className="muted-copy">No receipts found.</p>}
    <div className="stock-list">{receipts.map(receipt => <div className="stock-row" key={receipt.id}><div><strong>{receipt.supplierName} · {receipt.reference}</strong><small>#{receipt.id} · {new Date(receipt.receivedAt).toLocaleString()} · {money(receipt.total)}</small></div><button type="button" className="button small secondary" onClick={() => void showDetail(receipt.id)}>View details</button></div>)}</div>
    {receipts.length === 20 && <button type="button" className="button small secondary guest-pagination" disabled={loading} onClick={() => { setLoading(true); setListError(''); setBeforeId(receipts[receipts.length - 1].id) }}>Older receipts</button>}
    {beforeId !== undefined && <button type="button" className="button small secondary guest-pagination" disabled={loading} onClick={newest}>Back to newest</button>}
    {detailLoading && <p role="status">Loading receipt…</p>}{detailError && <div className="alert" role="alert">{detailError}</div>}
    {detail && <div className="inventory-receipt"><div className="management-heading"><h3>Receipt #{detail.id}</h3><button className="button small secondary" type="button" onClick={() => { detailRequest.current++; setDetail(null) }}>Close</button></div><p>{detail.supplierName} · {detail.reference}</p><p className="muted-copy">{detail.actor} · {new Date(detail.receivedAt).toLocaleString()} · Reason: {detail.reason}</p><div className="report-table-wrap"><table className="report-table"><thead><tr><th>Ingredient</th><th>Quantity</th><th>Unit cost</th><th>Line total</th></tr></thead><tbody>{detail.lines.map(line => <tr key={line.id}><td>{line.ingredientName}</td><td>{line.quantity} {line.unit}</td><td>{money(line.unitCost)}</td><td>{money(line.lineTotal)}</td></tr>)}</tbody></table></div><strong>Total: {money(detail.total)}</strong></div>}
  </section>
}
