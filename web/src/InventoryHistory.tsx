import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'

export function InventoryHistory<T extends { id: number }>({ title, load, render }: {
  title: string
  load: (beforeId?: number) => Promise<T[]>
  render: (entry: T) => ReactNode
}) {
  const [beforeId, setBeforeId] = useState<number | undefined>()
  const [entries, setEntries] = useState<T[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    let active = true
    void load(beforeId).then(result => { if (active) setEntries(result) })
      .catch(e => { if (active) { setEntries([]); setError(e instanceof Error ? e.message : 'Unable to load history.') } })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [load, beforeId, revision])
  return <section className="inventory-history"><div className="management-heading"><h3>{title}</h3><button type="button" className="button small secondary" disabled={loading} onClick={() => { setLoading(true); setError(''); setRevision(value => value + 1) }}>Refresh</button></div>
    {loading && <p role="status">Loading history…</p>}{error && <div className="alert" role="alert">{error}</div>}
    {!loading && !error && !entries.length && <p className="muted-copy">No entries found.</p>}
    <div>{entries.map(entry => <div className="history-entry" key={entry.id}>{render(entry)}</div>)}</div>
    {entries.length === 20 && <button type="button" className="button small secondary guest-pagination" disabled={loading} onClick={() => { setLoading(true); setError(''); setBeforeId(entries[entries.length - 1].id) }}>Older entries</button>}
    {beforeId !== undefined && <button type="button" className="button small secondary guest-pagination" disabled={loading} onClick={() => { setLoading(true); setError(''); setBeforeId(undefined) }}>Back to newest</button>}
  </section>
}
