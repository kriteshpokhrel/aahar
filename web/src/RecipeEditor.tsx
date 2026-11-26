import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { api, ApiError } from './api'
import type { Ingredient, MenuItem } from './api'

type Line = { key: number; ingredientId: string; quantity: string }

export function RecipeEditor({ items }: { items: MenuItem[] }) {
  const [selected, setSelected] = useState('')
  const [dirty, setDirty] = useState(false)
  const [saving, setSaving] = useState(false)
  return <section className="management"><div className="management-heading"><h3>Manage recipes</h3></div>
    <p className="muted-copy">Recipes determine ingredient deductions when an order is sent. No recipe means no stock is deducted for that item. Changes replace the entire recipe; refresh before saving if another manager may have edited it.</p>
    <label className="field">Menu item<select value={selected} disabled={saving} onChange={event => {
      if (dirty && !window.confirm('Discard unsaved recipe changes?')) return
      setDirty(false); setSelected(event.target.value)
    }}><option value="">Choose an item</option>{items.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
    {selected && <RecipeForm key={selected} id={Number(selected)} dirty={dirty} setDirty={setDirty} saving={saving} setSaving={setSaving} />}
  </section>
}

function RecipeForm({ id, dirty, setDirty, saving, setSaving }: {
  id: number; dirty: boolean; setDirty: (value: boolean) => void; saving: boolean; setSaving: (value: boolean) => void
}) {
  const [ingredients, setIngredients] = useState<Ingredient[]>([])
  const [lines, setLines] = useState<Line[]>([])
  const [loading, setLoading] = useState(true)
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [mustReload, setMustReload] = useState(false)
  const [revision, setRevision] = useState(0)
  const nextKey = useRef(1)
  useEffect(() => {
    let active = true
    void Promise.all([api.recipe(id), api.ingredients()]).then(([recipe, available]) => {
      if (!active) return
      setIngredients(available)
      setLines(recipe.ingredients.map(line => ({ key: nextKey.current++, ingredientId: String(line.ingredientId), quantity: String(line.quantity) })))
      setLoaded(true); setMustReload(false); setDirty(false)
    }).catch(e => { if (active) { setLoaded(false); setError(e instanceof Error ? e.message : 'Unable to load recipe.') } })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [id, revision, setDirty])
  function reload() {
    if (dirty && !window.confirm('Discard unsaved recipe changes and reload from the server?')) return
    setError(''); setNotice(''); setLoading(true); setRevision(value => value + 1)
  }
  function update(key: number, patch: Partial<Line>) {
    setLines(current => current.map(line => line.key === key ? { ...line, ...patch } : line))
    setDirty(true); setNotice('')
  }
  const selectedIds = lines.filter(line => line.ingredientId).map(line => line.ingredientId)
  const duplicate = new Set(selectedIds).size !== selectedIds.length
  const missing = lines.some(line => line.ingredientId && !ingredients.some(ingredient => String(ingredient.id) === line.ingredientId))
  async function save(event: FormEvent) {
    event.preventDefault()
    if (saving || loading || mustReload || duplicate || missing) return
    if (!lines.length && !window.confirm('Save an empty recipe? No stock will be deducted for this menu item.')) return
    setSaving(true); setError(''); setNotice('')
    try {
      await api.saveRecipe(id, lines.map(line => ({ ingredientId: Number(line.ingredientId), quantity: Number(line.quantity) })))
      setDirty(false); setNotice('Recipe saved.')
    } catch (e) {
      const uncertain = !(e instanceof ApiError) || ![400, 401, 403, 404, 429].includes(e.status)
      setMustReload(uncertain)
      setError(`${e instanceof Error ? e.message : 'Unable to save recipe.'}${uncertain ? ' Reload the recipe before saving again; the outcome or current recipe may have changed.' : ''}`)
    } finally { setSaving(false) }
  }
  return <div className="recipe-editor"><div className="management-heading"><strong>Recipe ingredients per item</strong><button className="button small secondary" type="button" disabled={loading || saving} onClick={reload}>Reload recipe</button></div>
    {loading && <p role="status">Loading recipe and ingredients…</p>}{error && <div className="alert" role="alert">{error}</div>}{notice && <div className="notice" role="status">{notice}</div>}
    {!loading && loaded && <form onSubmit={event => void save(event)}>
      {!lines.length && <p className="muted-copy">No ingredients configured. Sending this item will not deduct stock.</p>}
      {lines.map((line, index) => {
        const ingredient = ingredients.find(value => String(value.id) === line.ingredientId)
        return <div className="recipe-line" key={line.key}>
          <label className="field">Ingredient {index + 1}<select required value={line.ingredientId} disabled={saving || mustReload} onChange={event => update(line.key, { ingredientId: event.target.value })}><option value="">Choose ingredient</option>{line.ingredientId && !ingredient && <option value={line.ingredientId}>Unavailable ingredient #{line.ingredientId}</option>}{ingredients.map(value => <option value={value.id} key={value.id}>{value.name} ({value.unit})</option>)}</select></label>
          <label className="field">Quantity per item {ingredient ? `(${ingredient.unit})` : ''}<input required type="number" min="0.001" max="99999999999.999" step="0.001" value={line.quantity} disabled={saving || mustReload} onChange={event => update(line.key, { quantity: event.target.value })} /></label>
          <button type="button" className="button small secondary" disabled={saving || mustReload} aria-label={`Remove ingredient ${index + 1}`} onClick={() => { setLines(current => current.filter(value => value.key !== line.key)); setDirty(true); setNotice('') }}>Remove</button>
        </div>
      })}
      {duplicate && <p className="inventory-low" role="alert">Each ingredient can appear only once.</p>}{missing && <p className="inventory-low" role="alert">An ingredient is unavailable. Reload or replace it before saving.</p>}
      <div className="recipe-actions"><button type="button" className="button small secondary" disabled={saving || mustReload || lines.length >= 100 || !ingredients.length} onClick={() => { setLines(current => [...current, { key: nextKey.current++, ingredientId: '', quantity: '' }]); setDirty(true); setNotice('') }}>Add ingredient</button>
        <button className="button small primary" disabled={saving || mustReload || !dirty || duplicate || missing}>{saving ? 'Saving…' : lines.length ? 'Save recipe' : 'Save empty recipe'}</button></div>
    </form>}
  </div>
}
