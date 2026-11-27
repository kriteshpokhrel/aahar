import { useEffect, useState } from 'react'
import { ArrowRight, ChefHat, ClipboardList, Package, RefreshCw, WalletCards } from 'lucide-react'
import { api } from './api'
import type { Ingredient, KitchenTicket, Order, Table } from './api'
import { pendingRequestCount, serviceOverview } from './overviewMetrics'

type Destination = 'kitchen' | 'billing' | 'guest-requests' | 'inventory' | 'tables'
type Result<T> = { value: T | null; error: string | null }

export function ServiceOverview({ orders, tickets, tables, demo, loading, incomplete, refreshWorkspace, navigate }: {
  orders: Order[]; tickets: KitchenTicket[]; tables: Table[]; demo: boolean
  loading: boolean; incomplete: boolean; refreshWorkspace: () => Promise<void>
  navigate: (page: Destination) => void
}) {
  const [inventory, setInventory] = useState<Result<Ingredient[]>>({ value: null, error: null })
  const [pending, setPending] = useState<Result<number>>({ value: null, error: null })
  const [checking, setChecking] = useState(!demo)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    if (demo) return
    let active = true
    void Promise.allSettled([api.lowStock(), api.guestRequests('Pending')]).then(([stock, requests]) => {
      if (!active) return
      setInventory(stock.status === 'fulfilled' ? { value: stock.value, error: null } : { value: null, error: stock.reason instanceof Error ? stock.reason.message : 'Unable to load low stock.' })
      setPending(requests.status === 'fulfilled' ? { value: requests.value.length, error: null } : { value: null, error: requests.reason instanceof Error ? requests.reason.message : 'Unable to load requests.' })
      setChecking(false)
    })
    return () => { active = false }
  }, [demo, revision])
  const summary = serviceOverview(tickets, orders, tables)
  const workspaceUnknown = loading || incomplete
  const showValue = (count: number) => workspaceUnknown ? '—' : String(count)
  function refresh() {
    if (!demo) { setChecking(true); setInventory({ value: null, error: null }); setPending({ value: null, error: null }); setRevision(value => value + 1) }
    void refreshWorkspace()
  }
  return <section className="service-overview" aria-labelledby="service-overview-title">
    <div className="overview-heading"><div><span className="eyebrow">SERVICE AT A GLANCE {demo && '· SAMPLE'}</span><h2 id="service-overview-title">What needs attention</h2><p>{demo ? 'Example service data, stored only in this workspace.' : 'Last loaded snapshot · refresh to check for changes from other staff.'}</p></div>
      {!demo && <button type="button" className="button small secondary" disabled={loading || checking} onClick={refresh}><RefreshCw size={14} aria-hidden="true" /> Refresh overview</button>}</div>
    {(checking || loading) && <p className="overview-feedback" role="status">Updating service overview…</p>}
    {incomplete && <p className="overview-feedback" role="status">Workspace data could not be refreshed. Service counts are unavailable until refresh succeeds.</p>}
    <div className="overview-grid">
      <button type="button" className={`overview-card ${!workspaceUnknown && summary.queued > 0 ? 'needs-attention' : ''}`} onClick={() => navigate('kitchen')}>
        <span className="overview-card-head"><span className="overview-icon"><ChefHat size={17} aria-hidden="true" /></span><span className="overview-card-label">Kitchen queue</span><ArrowRight size={15} aria-hidden="true" /></span>
        <strong>{showValue(summary.queued)}</strong><span>tickets waiting to start</span><small>{workspaceUnknown ? 'Check workspace connection' : `${summary.preparing} preparing · ${summary.ready} ready · ${summary.drafts} drafts`}</small>
      </button>
      <button type="button" className={`overview-card ${!workspaceUnknown && summary.completed > 0 ? 'needs-attention' : ''}`} onClick={() => navigate('billing')}>
        <span className="overview-card-head"><span className="overview-icon"><WalletCards size={17} aria-hidden="true" /></span><span className="overview-card-label">Payment</span><ArrowRight size={15} aria-hidden="true" /></span>
        <strong>{showValue(summary.completed)}</strong><span>bills ready for payment</span><small>Payments are simulated</small>
      </button>
      {!demo && <button type="button" className={`overview-card ${pending.value !== null && pending.value > 0 ? 'needs-attention' : ''}`} onClick={() => navigate('guest-requests')}>
        <span className="overview-card-head"><span className="overview-icon"><ClipboardList size={17} aria-hidden="true" /></span><span className="overview-card-label">Guest requests</span><ArrowRight size={15} aria-hidden="true" /></span>
        <strong>{pending.value === null ? '—' : pendingRequestCount(pending.value)}</strong><span>waiting for staff review</span><small>{checking ? 'Checking…' : pending.error ? 'Could not load · open to retry' : pending.value === 20 ? 'At least 20 · view requests' : 'Review before accepting'}</small>
      </button>}
      {!demo && <button type="button" className={`overview-card ${inventory.value && inventory.value.length > 0 ? 'needs-attention' : ''}`} onClick={() => navigate('inventory')}>
        <span className="overview-card-head"><span className="overview-icon"><Package size={17} aria-hidden="true" /></span><span className="overview-card-label">Low stock</span><ArrowRight size={15} aria-hidden="true" /></span>
        <strong>{inventory.value === null ? '—' : inventory.value.length}</strong><span>ingredients at or below threshold</span><small>{checking ? 'Checking…' : inventory.error ? 'Could not load · open to retry' : inventory.value?.length ? inventory.value.slice(0, 2).map(item => item.name).join(', ') : 'No low-stock ingredients'}</small>
      </button>}
    </div>
    {!workspaceUnknown && <button className="overview-floor" type="button" onClick={() => navigate('tables')}><span><strong>{summary.availableTables} of {summary.totalTables}</strong> tables available</span><span>View floor <ArrowRight size={14} aria-hidden="true" /></span></button>}
    {!demo && (pending.error || inventory.error) && <div className="overview-errors" role="alert">{pending.error && <p>Guest requests: {pending.error}</p>}{inventory.error && <p>Low stock: {inventory.error}</p>}</div>}
  </section>
}
