import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  ArrowLeft, ArrowRight, BookOpen, ChefHat, ChevronDown,
  CircleHelp, ClipboardList, Clock3, Grid2X2, LayoutDashboard, LogOut,
  Minus, Package, Plus, Search, ShoppingBag, Sparkles, Store, Users, UtensilsCrossed, X,
} from 'lucide-react'
import { api, ApiError } from './api'
import type { KitchenTicket, MenuItem, Order, OrderLine, PaymentMethod, StockDisposition, Table, User } from './api'
import { demoMenu, demoOrders, demoTables, demoTickets } from './demo'
import { MenuEditor, SalesReport, TableEditor } from './Management'
import { Inventory } from './Inventory'
import type { UncertainPurchase } from './Inventory'
import { OrderTicket } from './OrderTicket'
import { KitchenCard } from './KitchenCard'
import { GuestOrdering } from './GuestOrdering'
import { GuestRequests } from './GuestRequests'
import { RecipeEditor } from './RecipeEditor'
import { GuestQr } from './GuestQr'
import { guestMenuUrl } from './guestLink'
import { canAddToCart, unavailableCartIds } from './orderSafety'
import { ServiceOverview } from './ServiceOverview'
import { StaffAccounts } from './StaffAccounts'
import './App.css'

type Page = 'pos' | 'tables' | 'kitchen' | 'menu' | 'inventory' | 'reports' | 'guest' | 'guest-requests' | 'staff'
type Cart = Record<number, number>

const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { maximumFractionDigits: 2 })}`
const message = (error: unknown) => error instanceof ApiError && error.status === 409
  ? `${error.message} Refresh the workspace before retrying.`
  : error instanceof Error ? error.message : 'Something went wrong. Please try again.'
const navigation: { id: Page; label: string; icon: typeof Grid2X2 }[] = [
  { id: 'pos', label: 'Point of sale', icon: Grid2X2 },
  { id: 'tables', label: 'Tables', icon: LayoutDashboard },
  { id: 'kitchen', label: 'Kitchen', icon: ChefHat },
  { id: 'guest-requests', label: 'Guest requests', icon: ClipboardList },
  { id: 'menu', label: 'Menu', icon: BookOpen },
  { id: 'inventory', label: 'Inventory', icon: Package },
  { id: 'reports', label: 'Reports', icon: ClipboardList },
  { id: 'staff', label: 'Staff accounts', icon: Users },
]

async function fetchWorkspace(guest: boolean) {
  if (guest) return { menu: await api.guestMenu(), tables: undefined, orders: undefined, tickets: undefined, errors: '' }
  const [menu, tables, orders, tickets] = await Promise.allSettled([api.menu(), api.tables(), api.orders(), api.kitchenTickets()])
  const errors = [
    menu.status === 'rejected' ? `Menu: ${message(menu.reason)}` : '',
    tables.status === 'rejected' ? `Tables: ${message(tables.reason)}` : '',
    orders.status === 'rejected' ? `Orders: ${message(orders.reason)}` : '',
    tickets.status === 'rejected' ? `Kitchen tickets: ${message(tickets.reason)}` : '',
  ].filter(Boolean).join(' ')
  return {
    menu: menu.status === 'fulfilled' ? menu.value : undefined,
    tables: tables.status === 'fulfilled' ? tables.value : undefined,
    orders: orders.status === 'fulfilled' ? orders.value : undefined,
    tickets: tickets.status === 'fulfilled' ? tickets.value : undefined,
    errors,
  }
}

function Brand({ light = false }: { light?: boolean }) {
  return <div className={`brand ${light ? 'brand-light' : ''}`}><span className="brand-mark"><UtensilsCrossed size={19} strokeWidth={1.8} /></span><span>aahar<span className="brand-dot">.</span><small>RESTAURANT WORKSPACE</small></span></div>
}

function SignIn({ onLogin, onDemo }: { onLogin: (user: User) => void; onDemo: () => void }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true); setError('')
    try { onLogin(await api.login(username, password)) } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }
  return <main className="login-layout">
    <section className="login-story"><Brand light /><div className="story-copy"><span className="eyebrow light-eyebrow">MADE FOR THE WAY YOU SERVE</span><h1>Good food deserves<br /><em>good flow.</em></h1><p>A calmer way to manage the moving parts of your restaurant, from the first order to the last table.</p><div className="story-rule" /><span className="story-foot"><span>01 / 03</span> &nbsp; A LITTLE MORE ROOM TO HOSPITABLY HOST</span></div><div className="story-illustration" aria-hidden="true"><div className="plate plate-one" /><div className="plate plate-two" /><div className="plate plate-three" /><span>THE ART OF<br />GOOD SERVICE</span></div><div className="story-bottom">A thoughtful workspace for restaurants <span>EST. FOR EVERY DAY</span></div></section>
    <section className="login-panel"><div className="login-inner"><span className="eyebrow">WELCOME TO AAHAR</span><h2>Welcome back.</h2><p>Sign in to pick up where you left off.</p><form onSubmit={submit}><label>Username<input autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} required placeholder="Enter your username" /></label><label>Password<input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required placeholder="Enter your password" /></label>{error && <div className="alert" role="alert">{error}</div>}<button className="button primary wide" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'} <ArrowRight size={17} /></button></form><div className="login-divider"><span>OR TAKE A LOOK AROUND</span></div><button className="button secondary wide" onClick={onDemo}>Explore sample workspace <ArrowRight size={17} /></button><p className="login-note">Sample mode uses temporary example data. No changes are sent to the server.</p></div><div className="login-footer">© Aahar <span>MADE FOR BETTER SERVICE</span></div></section>
  </main>
}

function App() {
  const guestParam = new URLSearchParams(window.location.search).get('guest')
  const guestPath = guestParam !== null
  const sampleGuest = guestParam === 'sample'
  const [user, setUser] = useState<User | null>(null)
  const [checking, setChecking] = useState(!guestPath)
  const [demo, setDemo] = useState(false)
  const [page, setPage] = useState<Page>(guestPath ? 'guest' : 'pos')
  const [menu, setMenu] = useState<MenuItem[]>(sampleGuest ? demoMenu : [])
  const [tables, setTables] = useState<Table[]>([])
  const [orders, setOrders] = useState<Order[]>([])
  const [tickets, setTickets] = useState<KitchenTicket[]>([])
  const [loading, setLoading] = useState(guestPath && !sampleGuest)
  const [error, setError] = useState('')
  const [workspaceIncomplete, setWorkspaceIncomplete] = useState(false)
  const [notice, setNotice] = useState('')
  const [cart, setCart] = useState<Cart>({})
  const [cartNotes, setCartNotes] = useState<Record<number, string>>({})
  const [tableId, setTableId] = useState<number | null>(null)
  const [orderType, setOrderType] = useState<'dine-in' | 'takeaway'>('dine-in')
  const [guestTable, setGuestTable] = useState('')
  const [category, setCategory] = useState('All items')
  const [search, setSearch] = useState('')
  const [busy, setBusy] = useState(false)
  const [uncertainPurchase, setUncertainPurchase] = useState<UncertainPurchase | null>(null)
  const [inventoryLowOnly, setInventoryLowOnly] = useState(false)

  useEffect(() => {
    if (guestPath) return
    api.me().then(currentUser => { setLoading(true); setUser(currentUser) }).catch((e: unknown) => {
      if (!(e instanceof ApiError && e.status === 401)) setError(message(e))
    }).finally(() => setChecking(false))
  }, [guestPath])

  const applyWorkspace = useCallback((data: Awaited<ReturnType<typeof fetchWorkspace>>) => {
    if (data.menu) setMenu(data.menu)
    if (data.tables) setTables(data.tables)
    if (data.orders) setOrders(data.orders)
    if (data.tickets) setTickets(data.tickets)
    setWorkspaceIncomplete(!!data.errors)
    setError(data.errors)
  }, [])
  const refresh = useCallback(async () => {
    if (demo || sampleGuest || (!user && !guestPath)) return
    setLoading(true); setError('')
    try { applyWorkspace(await fetchWorkspace(guestPath)) }
    catch (e) { setWorkspaceIncomplete(true); setError(message(e)) } finally { setLoading(false) }
  }, [applyWorkspace, demo, sampleGuest, user, guestPath])
  useEffect(() => {
    if (demo || sampleGuest || (!user && !guestPath)) return
    let active = true
    void fetchWorkspace(guestPath).then(data => { if (active) applyWorkspace(data) })
      .catch(e => { if (active) { setWorkspaceIncomplete(true); setError(message(e)) } })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [applyWorkspace, demo, sampleGuest, user, guestPath])
  useEffect(() => {
    if (!checking && user && !guestPath) document.getElementById('main-content')?.focus()
  }, [checking, user, guestPath, page])

  function startDemo() {
    setDemo(true); setUser({ username: 'Sample user', role: 'Demo' }); setMenu(demoMenu); setTables(demoTables); setOrders(demoOrders); setTickets(demoTickets); setError(''); setWorkspaceIncomplete(false); setLoading(false)
  }
  async function signOut() {
    if (!demo) {
      try { await api.logout() } catch (e) { setError(message(e)); return }
    }
    setUser(null); setDemo(false); setMenu([]); setTables([]); setOrders([]); setTickets([]); setCart({}); setCartNotes({}); setTableId(null); setOrderType('dine-in'); setPage('pos'); setLoading(false)
  }
  const categories = useMemo(() => ['All items', ...new Set(menu.map(item => item.category).filter((value): value is string => !!value))], [menu])
  const visibleMenu = menu.filter(item => item.isAvailable && (category === 'All items' || item.category === category) && `${item.name} ${item.description ?? ''}`.toLowerCase().includes(search.toLowerCase()))
  const cartItems = menu.filter(item => cart[item.id] > 0)
  const unavailableIds = unavailableCartIds(cart, menu)
  const total = cartItems.reduce((sum, item) => sum + item.price * cart[item.id], 0)
  const itemCount = Object.values(cart).reduce((sum, quantity) => sum + quantity, 0)
  const isOccupied = (table: Table) => demo
    ? orders.some(order => order.tableId === table.id && ['draft', 'sent', 'completed'].includes(order.status.toLowerCase()))
    : table.isOccupied
  const selectedTable = tables.find(table => table.id === tableId)
  function changeQuantity(id: number, amount: number) {
    if ((cart[id] ?? 0) + amount <= 0) setCartNotes(current => {
      const next = { ...current }
      delete next[id]
      return next
    })
    setCart(current => {
      if (amount > 0 && !canAddToCart(current, id)) return current
      const next = { ...current, [id]: Math.min(1000, Math.max(0, (current[id] ?? 0) + amount)) }
      if (next[id] === 0) delete next[id]
      return next
    })
  }
  async function createOrder() {
    if (!itemCount || unavailableIds.length || (orderType === 'dine-in' && (!selectedTable || isOccupied(selectedTable)))) return
    setBusy(true); setError(''); setNotice('')
    try {
      const items = cartItems.map(item => ({ menuItemId: item.id, quantity: cart[item.id], notes: cartNotes[item.id]?.trim() || null }))
      const destination = orderType === 'takeaway' ? null : tableId
      if (demo) {
        const order: Order = { id: Math.max(100, ...orders.map(o => o.id)) + 1, tableId: destination, status: 'Draft', createdAt: new Date().toISOString(), items: cartItems.map(item => ({ menuItemId: item.id, name: item.name, quantity: cart[item.id], unitPrice: item.price, notes: cartNotes[item.id]?.trim() || null })), total, payment: null }
        setOrders(current => [order, ...current])
      } else {
        await api.createOrder(destination, items)
      }
      setCart({}); setCartNotes({}); setTableId(null); setNotice('Order created. Open Kitchen to send it for preparation.')
      if (!demo) await refresh()
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }
  async function updateOrder(order: Order, action: 'send' | 'complete' | 'pay', method: PaymentMethod = 'Cash') {
    setBusy(true); setError(''); setNotice('')
    try {
      if (demo) {
        setOrders(current => current.map(item => item.id === order.id ? { ...item, status: action === 'send' ? 'Sent' : action === 'complete' ? 'Completed' : 'Paid', payment: action === 'pay' ? { id: order.id, method, amount: order.total, paidAt: new Date().toISOString() } : item.payment } : item))
        if (action === 'send') setTickets(current => [{ id: Math.max(100, ...current.map(item => item.id)) + 1, orderId: order.id, tableId: order.tableId, status: 'Queued', createdAt: new Date().toISOString(), isReconstructed: false, items: order.items.map(item => ({ ...item })) }, ...current])
      }
      else {
        if (action === 'send') await api.sendOrder(order.id)
        if (action === 'complete') await api.completeOrder(order.id)
        if (action === 'pay') await api.payOrder(order.id, method)
        await refresh()
      }
      setNotice(action === 'pay' ? 'Payment recorded as a simulation; Aahar did not process a payment.' : 'Order updated.')
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }
  async function cancelOrder(order: Order, reason: string, stockDisposition: StockDisposition): Promise<boolean> {
    setBusy(true); setError(''); setNotice('')
    try {
      if (demo) {
        setOrders(current => current.map(item => item.id === order.id ? {
          ...item, status: 'Cancelled', cancelledAt: new Date().toISOString(),
          cancelledBy: 'Sample user', cancellationReason: reason, cancellationStockDisposition: stockDisposition,
        } : item))
        setTickets(current => current.map(item => item.orderId === order.id ? { ...item, status: 'Cancelled' } : item))
      } else {
        await api.cancelOrder(order.id, reason, stockDisposition)
        await refresh()
      }
      setNotice(order.tableId === null ? 'Takeaway order cancelled.' : 'Order cancelled. The table is released.')
      return true
    } catch (e) { setError(message(e)); return false } finally { setBusy(false) }
  }

  async function saveDraft(order: Order, items: OrderLine[]): Promise<boolean> {
    setBusy(true); setError(''); setNotice('')
    try {
      if (demo) {
        const updated = items.map(line => {
          const previous = order.items.find(item => item.menuItemId === line.menuItemId)
          const current = menu.find(item => item.id === line.menuItemId)
          if (!current || (!current.isAvailable && (!previous || line.quantity > previous.quantity))) throw new Error('A menu item is unavailable.')
          return { ...line, name: previous?.name ?? current.name, unitPrice: previous?.unitPrice ?? current.price }
        })
        setOrders(current => current.map(item => item.id === order.id ? { ...item, items: updated, total: updated.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0) } : item))
      } else {
        await api.editOrder(order.id, items)
        await refresh()
      }
      setNotice('Draft updated.')
      return true
    } catch (e) { setError(message(e)); return false } finally { setBusy(false) }
  }
  async function advanceTicket(ticket: KitchenTicket, action: 'prepare' | 'ready') {
    setBusy(true); setError(''); setNotice('')
    try {
      if (demo) setTickets(current => current.map(item => item.id === ticket.id ? { ...item, status: action === 'prepare' ? 'Preparing' : 'Ready' } : item))
      else {
        if (action === 'prepare') await api.prepareTicket(ticket.id)
        else await api.readyTicket(ticket.id)
        await refresh()
      }
      setNotice('Kitchen ticket updated.')
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }

  if (checking) return <div className="full-message" role="status">Opening Aahar…</div>
  if (!user && !guestPath) return <><SignIn onLogin={currentUser => { setLoading(true); setUser(currentUser) }} onDemo={startDemo} />{error && <div className="connection-error" role="alert">Could not check your session: {error}</div>}</>
  if (guestPath) return <GuestMenu menu={menu} loading={loading} error={error} retry={refresh} sample={sampleGuest} />

  const title = navigation.find(item => item.id === page)?.label ?? 'Guest menu'
  const canManage = !demo && ['admin', 'manager'].includes(user?.role.toLowerCase() ?? '')
  const canAdmin = !demo && user?.role.toLowerCase() === 'admin'
  const canServe = demo || ['admin', 'manager', 'staff'].includes(user?.role.toLowerCase() ?? '')
  const canComplete = demo || ['admin', 'manager', 'kitchen'].includes(user?.role.toLowerCase() ?? '')
  const visibleNavigation = navigation.filter(item => (item.id !== 'guest-requests' || (canServe && !demo)) && (item.id !== 'staff' || canAdmin))
  return <div className="app-shell">
    <a className="skip-link" href="#main-content">Skip to content</a>
    <aside className="sidebar"><Brand /><div className="workspace-label">WORKSPACE</div><nav aria-label="Main navigation">{visibleNavigation.map(({ id, label, icon: Icon }) => <button key={id} className={`nav-item ${page === id ? 'active' : ''}`} onClick={() => { setInventoryLowOnly(false); setPage(id); setNotice('') }} aria-current={page === id ? 'page' : undefined}><Icon size={18} strokeWidth={1.7} />{label}{page === id && <span className="nav-indicator" />}</button>)}</nav><div className="sidebar-bottom"><div className="sidebar-note"><span className="note-icon"><Sparkles size={16} /></span><strong>Made for service.</strong><p>A little more ease in every shift.</p></div><button className="nav-item guest-link" onClick={() => { setPage('guest'); setNotice('') }} aria-current={page === 'guest' ? 'page' : undefined}><Store size={18} /> Guest menu <ArrowRight size={15} /></button><button className="profile" aria-label={`Sign out ${user?.username ?? ''}`} onClick={signOut}><span className="avatar">{user?.username.slice(0, 1).toUpperCase()}</span><span><strong>{user?.username}</strong><small>{demo ? 'Sample workspace' : user?.role}</small></span><LogOut size={17} /></button></div></aside>
    <main className="main" id="main-content" tabIndex={-1}><header className="topbar"><div className="breadcrumb">Workspace <span>/</span> <strong>{title}</strong></div><div className="top-actions">{demo && <span className="demo-badge">SAMPLE MODE</span>}<span className="today"><span className="online-dot" /> {demo ? 'Example data' : 'Restaurant workspace'}</span><button className="icon-button" aria-label="About this workspace" onClick={() => setNotice('Aahar is a restaurant workspace. Payments are simulated and receipts are not IRD compliant.')}><CircleHelp size={19} /></button></div></header>
      <div className="content">
        {(error || workspaceIncomplete) && <div className="alert global-alert" role="alert"><span>{error} Displayed workspace data may be out of date.</span><button onClick={() => void refresh()}>Refresh workspace</button>{!workspaceIncomplete && <button aria-label="Dismiss error" onClick={() => setError('')}><X size={16} /></button>}</div>}
        {notice && <div className="notice" role="status"><span>{notice}</span><button aria-label="Dismiss message" onClick={() => setNotice('')}><X size={16} /></button></div>}
        {loading && <div className="loading" role="status">Loading workspace…</div>}
        {page === 'pos' && <><div className="page-heading"><div><div className="eyebrow">YOUR SERVICE, IN ONE PLACE</div><h1>Point of sale<span className="heading-dot">.</span></h1><p>Let’s get something good to the table.</p></div><div className="shift-label"><span className="shift-icon"><Clock3 size={17} /></span><span><small>CURRENT WORKSPACE</small><strong>{demo ? 'Sample service' : 'Restaurant service'}</strong></span></div></div>
          {(canManage || demo) && <ServiceOverview orders={orders} tickets={tickets} tables={tables.map(table => ({ ...table, isOccupied: isOccupied(table) }))} demo={demo} loading={loading} incomplete={workspaceIncomplete} refreshWorkspace={refresh} navigate={destination => { setInventoryLowOnly(destination === 'inventory'); setPage(destination); setNotice('') }} />}
          <div className="pos-layout"><section className="menu-panel"><div className="section-heading"><div><span className="eyebrow">THE MENU</span><h2>What are we serving?</h2></div><span className="count-label">{visibleMenu.length} items</span></div><div className="menu-tools"><div className="categories" aria-label="Menu categories">{categories.map(c => <button key={c} className={category === c ? 'selected' : ''} aria-pressed={category === c} onClick={() => setCategory(c)}>{c}</button>)}</div><label className="search"><Search size={17} /><span className="sr-only">Search menu</span><input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search menu..." /></label></div>
            <div className="item-grid">{visibleMenu.map((item, index) => <button className="menu-card" key={item.id} aria-label={`Add ${item.name} to order, ${money(item.price)}`} disabled={!canServe || busy || !canAddToCart(cart, item.id)} onClick={() => changeQuantity(item.id, 1)}><div className={`item-art art-${index % 4}`} aria-hidden="true"><span>{item.name.slice(0, 1)}</span><i /><b /></div><div className="item-details"><div className="item-category">{item.category || 'FROM THE MENU'}</div><strong>{item.name}</strong><p>{item.description || 'Made fresh to order'}</p><div className="item-bottom"><span>{money(item.price)}</span><span className="add-circle" aria-hidden="true"><Plus size={16} /></span></div></div></button>)}</div>{!loading && !workspaceIncomplete && !visibleMenu.length && <Empty title="Nothing on the menu yet" text={search || category !== 'All items' ? 'Try another search or category.' : 'Available menu items will appear here.'} />}</section>
            <aside className="order-panel"><div className="order-head"><div><span className="eyebrow">CURRENT ORDER</span><h2>Order details</h2></div><span className="order-icon"><ShoppingBag size={18} /></span></div><div className="order-type"><label><span className="sr-only">Order type</span><select value={orderType} onChange={e => { setOrderType(e.target.value === 'takeaway' ? 'takeaway' : 'dine-in'); setTableId(null) }}><option value="dine-in">Dine in</option><option value="takeaway">Takeaway</option></select><ChevronDown size={14} /></label>{orderType === 'dine-in' && <label><span className="sr-only">Select table</span><select value={tableId ?? ''} onChange={e => setTableId(e.target.value ? Number(e.target.value) : null)}><option value="">Select table</option>{tables.map(table => <option key={table.id} value={table.id} disabled={isOccupied(table)}>Table {table.number}{isOccupied(table) ? ' · occupied' : ''}</option>)}</select><ChevronDown size={14} /></label>}</div>
              <div className="order-lines">{cartItems.length ? cartItems.map(item => <div className="order-line" key={item.id}><div><strong>{item.name}</strong><small>{money(item.price)} each{!item.isAvailable && ' · unavailable'}</small><label className="cart-note"><span>Kitchen note</span><input maxLength={300} disabled={busy} value={cartNotes[item.id] ?? ''} onChange={e => setCartNotes({ ...cartNotes, [item.id]: e.target.value })} placeholder="Optional" /></label></div><div className="line-right"><strong>{money(item.price * cart[item.id])}</strong><div className="stepper"><button disabled={busy} onClick={() => changeQuantity(item.id, -1)} aria-label={`Remove one ${item.name}`}><Minus size={13} /></button><span aria-live="polite" aria-atomic="true">{cart[item.id]}</span><button disabled={busy || !item.isAvailable || !canAddToCart(cart, item.id)} onClick={() => changeQuantity(item.id, 1)} aria-label={`Add one ${item.name}`}><Plus size={13} /></button></div></div></div>) : !itemCount && <div className="empty-order"><span><ShoppingBag size={22} strokeWidth={1.4} /></span><strong>A fresh start</strong><p>Choose an item from the menu<br />to begin an order.</p></div>}
                {unavailableIds.length > 0 && <div className="alert" role="alert"><div>Some selected items are unavailable or no longer on the menu: {unavailableIds.map(id => menu.find(item => item.id === id)?.name ?? `item #${id}`).join(', ')}. <button disabled={busy} onClick={() => { setCart(current => Object.fromEntries(Object.entries(current).filter(([id]) => !unavailableIds.includes(Number(id))))); setCartNotes(current => Object.fromEntries(Object.entries(current).filter(([id]) => !unavailableIds.includes(Number(id))))) }}>Remove unavailable items</button></div></div>}
              </div><div className="order-footer"><div className="summary-line"><span>Items</span><span aria-live="polite" aria-atomic="true">{itemCount}</span></div><div className="summary-line total-line"><strong>Total</strong><strong>{money(total)}</strong></div><p className="tax-note">Final totals are determined by the server. Maximum 100 different items and 1,000 of each.</p><button className="button primary wide" disabled={!itemCount || unavailableIds.length > 0 || (orderType === 'dine-in' && (!selectedTable || isOccupied(selectedTable))) || busy || !canServe} onClick={() => void createOrder()}>{busy ? 'Creating order…' : 'Create order'} <ArrowRight size={17} /></button><p className="order-hint">{!canServe ? 'Your role cannot create orders.' : unavailableIds.length ? 'Remove unavailable items before creating the order.' : orderType === 'takeaway' ? 'Review your takeaway order before sending it to the kitchen.' : !selectedTable ? 'Select a table to create an order.' : isOccupied(selectedTable) ? 'This table is occupied. Select another table.' : 'Review your order before sending it to the kitchen.'}</p></div></aside></div></>}
        {page === 'tables' && <><PageHeading eyebrow="THE FLOOR" title="Tables" text="A clear view of your dining room." /><div className="surface"><div className="section-heading"><div><span className="eyebrow">FLOOR OVERVIEW</span><h2>Your tables</h2></div><span className="count-label">{tables.length} tables</span></div>{canManage && <TableEditor refresh={refresh} />}<div className="table-grid">{tables.map(table => { const active = isOccupied(table); return <button key={table.id} className="table-card" disabled={active || !canServe} onClick={() => { setOrderType('dine-in'); setTableId(table.id); setPage('pos') }}><div className="table-card-top"><span className="table-symbol"><Grid2X2 size={20} /></span><span className={`status ${active ? 'status-busy' : ''}`}>{active ? 'Occupied' : 'Available'}</span></div><strong>Table {table.number}</strong><small>{table.capacity} seats</small><span className="table-action">{active ? 'In service' : canServe ? 'Start an order' : 'View only'} {!active && canServe && <ArrowRight size={14} />}</span></button> })}</div>{!loading && !workspaceIncomplete && !tables.length && <Empty title="No tables found" text="Tables will appear here when available." />}</div></>}
        {page === 'kitchen' && <><PageHeading eyebrow="KEEP THINGS MOVING" title="Kitchen" text="Tickets and orders, at a glance." /><div className="surface"><div className="section-heading"><div><span className="eyebrow">KITCHEN QUEUE</span><h2>On the pass</h2></div><button className="button small secondary" disabled={loading} onClick={() => void refresh()}>Refresh</button></div><div className="ticket-grid">{tickets.map(ticket => <KitchenCard key={ticket.id} ticket={ticket} tables={tables} busy={busy} canPrepare={canComplete} advance={advanceTicket} />)}</div>{!loading && !workspaceIncomplete && !tickets.length && <Empty title="No kitchen tickets" text="Sent orders will appear here." />}</div><div className="surface orders-surface"><div className="section-heading"><div><span className="eyebrow">ORDER MANAGEMENT</span><h2>Orders</h2></div><span className="count-label">{orders.length} orders</span></div><div className="ticket-grid">{orders.map(order => <OrderTicket key={order.id} order={order} ticket={tickets.find(ticket => ticket.orderId === order.id)} menu={menu} tables={tables} busy={busy} canServe={canServe} canComplete={canComplete} canCancelSubmitted={demo || canManage} update={updateOrder} cancel={cancelOrder} saveDraft={saveDraft} />)}</div>{!loading && !workspaceIncomplete && !orders.length && <Empty title="All caught up" text="New orders will show up here." />}</div></>}
        {page === 'guest-requests' && canServe && !demo && <><PageHeading eyebrow="GUEST SERVICE" title="Guest requests" text="Review requests before they become orders." /><GuestRequests tables={tables} onChanged={refresh} /></>}
        {page === 'menu' && <><PageHeading eyebrow="FOOD & DRINK" title="Your menu" text="Everything you’re serving, in one place." /><div className="surface"><div className="section-heading"><div><span className="eyebrow">MENU CATALOGUE</span><h2>All menu items</h2></div><span className="count-label">{menu.length} items</span></div>{canManage && <><MenuEditor items={menu} refresh={refresh} /><RecipeEditor items={menu} /></>}<div className="list-table"><div className="list-header"><span>ITEM</span><span>AVAILABILITY</span><span>PRICE</span></div>{menu.map(item => <div className="list-row" key={item.id}><span><strong>{item.name}</strong><small>{item.description || item.category || 'Menu item'}</small></span><span><span className={`status ${item.isAvailable ? '' : 'status-busy'}`}>{item.isAvailable ? 'Available' : 'Unavailable'}</span></span><strong>{money(item.price)}</strong></div>)}</div>{!loading && !workspaceIncomplete && !menu.length && <Empty title="No menu items found" text="Your menu will appear here when available." />}</div></>}
        {(page === 'inventory' || page === 'reports') && !canManage && <><PageHeading eyebrow={page === 'inventory' ? 'STOCK & SUPPLIES' : 'THE BIG PICTURE'} title={page === 'inventory' ? 'Inventory' : 'Reports'} text={page === 'inventory' ? 'Stay on top of what your kitchen needs.' : 'Make sense of your service.'} /><div className="surface placeholder"><span className="placeholder-icon">{page === 'inventory' ? <Package size={25} /> : <ClipboardList size={25} />}</span><span className="eyebrow">ACCESS & AVAILABILITY</span><h2>{demo ? 'Not available in sample mode.' : 'Manager access required.'}</h2><p>{demo ? 'Sample mode does not connect to inventory or sales data.' : 'Inventory and sales data are available to managers and administrators.'}</p></div></>}
        {page === 'inventory' && canManage && <><PageHeading eyebrow="STOCK & SUPPLIES" title="Inventory" text="Stay on top of what your kitchen needs." /><Inventory uncertain={uncertainPurchase} setUncertain={setUncertainPurchase} initialLowOnly={inventoryLowOnly} /></>}
        {page === 'reports' && canManage && <><PageHeading eyebrow="THE BIG PICTURE" title="Reports" text="Make sense of your service." /><SalesReport /></>}
        {page === 'staff' && canAdmin && <><PageHeading eyebrow="YOUR TEAM" title="Staff accounts" text="Create access for your restaurant team." /><StaffAccounts /></>}
        {page === 'guest' && <><PageHeading eyebrow="SHARE WHAT’S GOOD" title="Guest menu" text={demo ? 'Preview the sample menu.' : 'Share the menu and let guests send requests for staff review.'} /><div className="surface guest-preview"><div><span className="eyebrow">GUEST EXPERIENCE</span><h2>Let the menu do the talking.</h2><p>Open the guest menu on this computer. A localhost link cannot be used by guests on other devices.</p>{!demo && <label className="field guest-table-select">Guest link destination<select value={guestTable} onChange={event => setGuestTable(event.target.value)}><option value="">Takeaway</option>{tables.map(table => <option value={table.id} key={table.id}>Table {table.number}</option>)}</select></label>}<a className="button primary" href={guestMenuUrl(window.location.href, demo, guestTable)} target="_blank" rel="noreferrer">Open guest menu <ArrowRight size={16} /></a></div><GuestQr url={guestMenuUrl(window.location.href, demo, guestTable)} /></div></>}
      </div>
    </main>
    <nav className="mobile-nav" aria-label="Mobile navigation">{visibleNavigation.map(({ id, label, icon: Icon }) => <button key={id} className={page === id ? 'active' : ''} onClick={() => { setInventoryLowOnly(false); setPage(id); setNotice('') }} aria-label={label} aria-current={page === id ? 'page' : undefined}><Icon size={19} /><span>{label === 'Point of sale' ? 'POS' : label}</span></button>)}<button className={page === 'guest' ? 'active' : ''} onClick={() => { setPage('guest'); setNotice('') }} aria-label="Guest menu" aria-current={page === 'guest' ? 'page' : undefined}><Store size={19} /><span>Guest</span></button><button onClick={signOut} aria-label="Sign out"><LogOut size={19} /><span>Exit</span></button></nav>
  </div>
}

function PageHeading({ eyebrow, title, text }: { eyebrow: string; title: string; text: string }) {
  return <div className="page-heading"><div><div className="eyebrow">{eyebrow}</div><h1>{title}<span className="heading-dot">.</span></h1><p>{text}</p></div></div>
}
function Empty({ title, text }: { title: string; text: string }) {
  return <div className="empty-state"><strong>{title}</strong><p>{text}</p></div>
}
function GuestMenu({ menu, loading, error, retry, sample }: { menu: MenuItem[]; loading: boolean; error: string; retry: () => Promise<void>; sample: boolean }) {
  const available = menu.filter(item => item.isAvailable)
  const groups = [...new Set(available.map(item => item.category || 'Our menu'))]
  const tableParam = new URLSearchParams(window.location.search).get('tableId')
  const invalidTable = tableParam !== null && (!/^[1-9]\d*$/.test(tableParam) || Number(tableParam) > 2147483647)
  const tableId = tableParam === null || invalidTable ? null : Number(tableParam)
  return <main className="guest-page"><header><Brand /><a href={window.location.pathname}><ArrowLeft size={16} /> Staff sign in</a></header><div className="guest-hero"><span className="eyebrow">{sample ? 'SAMPLE MENU · EXAMPLE DATA' : 'PULL UP A CHAIR'}</span><h1>Good food,<br /><em>good company.</em></h1><p>Take your time. There’s something here for everyone.</p><span className="guest-ornament"><UtensilsCrossed size={35} strokeWidth={1.1} /></span></div><div className="guest-content"><div className="section-heading"><div><span className="eyebrow">FRESHLY PREPARED</span><h2>Explore the menu</h2></div><span className="count-label">{available.length} items</span></div>{error && <div className="alert" role="alert">{error} <button onClick={() => void retry()}>Retry</button></div>}{loading && <p role="status">Loading menu…</p>}{sample ? groups.map(group => <section key={group} className="guest-group"><h3>{group}</h3>{available.filter(item => (item.category || 'Our menu') === group).map(item => <div className="guest-row" key={item.id}><div><strong>{item.name}</strong><p>{item.description}</p></div><strong>{money(item.price)}</strong></div>)}</section>) : <GuestOrdering menu={menu} tableId={tableId} invalidTable={invalidTable} menuError={error} menuLoading={loading} refreshMenu={retry} />}{sample && <p className="muted-copy">Sample menu only; guest requests are not sent in sample mode.</p>}{sample && !loading && !available.length && !error && <Empty title="The menu is being prepared" text="Please check back soon." />}</div><footer><Brand /><span>GOOD FOOD IS BETTER TOGETHER</span></footer></main>
}

export default App
