import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  ArrowLeft, ArrowRight, BookOpen, ChefHat, ChevronDown,
  CircleHelp, ClipboardList, Clock3, Grid2X2, KeyRound, LayoutDashboard, LogOut,
  Minus, Package, Plus, Search, Settings, ShoppingBag, Store, Users, UtensilsCrossed, X,
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
import { canAddToCart, unavailableCartIds, uncertainMutationOutcome } from './orderSafety'
import { ServiceOverview } from './ServiceOverview'
import { StaffAccounts } from './StaffAccounts'
import { MenuImage } from './MenuImage'
import { ChangePassword } from './ChangePassword'
import { RunningBill } from './RunningBill'
import { Receipt } from './Receipt'
import { activeTableOrders, billReadiness } from './bill'
import { readPendingPayment, readPendingRound } from './billRecovery'
import type { PendingPayment, PendingRound } from './billRecovery'
import { defaultPreferences, parsePreferences, preferencesKey } from './preferences'
import type { WorkspacePreferences } from './preferences'
import { WorkspaceSettings } from './WorkspaceSettings'
import './App.css'

type Page = 'pos' | 'tables' | 'kitchen' | 'billing' | 'menu' | 'inventory' | 'reports' | 'guest' | 'guest-requests' | 'staff' | 'password' | 'settings'
type Cart = Record<number, number>

const money = (amount: number) => `Rs ${amount.toLocaleString('en-NP', { maximumFractionDigits: 2 })}`
const message = (error: unknown) => error instanceof ApiError && error.status === 409
  ? `${error.message} Refresh the workspace before retrying.`
  : error instanceof Error ? error.message : 'Something went wrong. Please try again.'
const navigation: { id: Page; label: string; icon: typeof Grid2X2 }[] = [
  { id: 'pos', label: 'Point of sale', icon: Grid2X2 },
  { id: 'tables', label: 'Tables', icon: LayoutDashboard },
  { id: 'kitchen', label: 'Kitchen', icon: ChefHat },
  { id: 'billing', label: 'Billing', icon: ClipboardList },
  { id: 'guest-requests', label: 'Guest requests', icon: ClipboardList },
  { id: 'menu', label: 'Menu', icon: BookOpen },
  { id: 'inventory', label: 'Inventory', icon: Package },
  { id: 'reports', label: 'Reports', icon: ClipboardList },
  { id: 'staff', label: 'Staff accounts', icon: Users },
  { id: 'password', label: 'Change password', icon: KeyRound },
  { id: 'settings', label: 'Workspace settings', icon: Settings },
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

function SignIn({ onLogin, onDemo, authNotice }: { onLogin: (user: User) => void; onDemo: () => void; authNotice: string }) {
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
    <section className="login-panel"><div className="login-inner"><span className="eyebrow">WELCOME TO AAHAR</span><h2>Welcome back.</h2><p>Sign in to pick up where you left off.</p>{authNotice && <div className="notice" role="status">{authNotice}</div>}<form onSubmit={submit}><label>Username<input autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} required placeholder="Enter your username" /></label><label>Password<input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required placeholder="Enter your password" /></label>{error && <div className="alert" role="alert">{error}</div>}<button className="button primary wide" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'} <ArrowRight size={17} /></button></form><div className="login-divider"><span>OR TAKE A LOOK AROUND</span></div><button className="button secondary wide" onClick={onDemo}>Explore sample workspace <ArrowRight size={17} /></button><p className="login-note">Sample mode uses temporary example data. No changes are sent to the server.</p></div><div className="login-footer">© Aahar <span>MADE FOR BETTER SERVICE</span></div></section>
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
  const [authNotice, setAuthNotice] = useState('')
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
  const [imageVersions, setImageVersions] = useState<Record<number, number>>({})
  const [activeOrderId, setActiveOrderId] = useState<number | null>(null)
  const [receiptOrder, setReceiptOrder] = useState<Order | null>(null)
  const [pendingRound, setPendingRound] = useState<PendingRound | null>(null)
  const [pendingPayment, setPendingPayment] = useState<PendingPayment | null>(null)
  const [recoveryReady, setRecoveryReady] = useState(false)
  const [recoveryError, setRecoveryError] = useState('')
  const [paymentVerified, setPaymentVerified] = useState(false)
  const [{ preferences, preferenceError }, setPreferenceState] = useState<{ preferences: WorkspacePreferences; preferenceError: string }>(() => {
    try { return { preferences: parsePreferences(localStorage.getItem(preferencesKey)), preferenceError: '' } }
    catch { return { preferences: defaultPreferences, preferenceError: 'Could not load browser preferences. Defaults are in use; save settings to try again.' } }
  })
  function savePreferences(value: WorkspacePreferences) {
    try { localStorage.setItem(preferencesKey, JSON.stringify(value)); setPreferenceState({ preferences: value, preferenceError: '' }) }
    catch { setPreferenceState(current => ({ ...current, preferenceError: 'Could not save preferences in this browser. Changes were not applied.' })) }
  }
  const storageKey = (kind: 'round' | 'payment') => `aahar-pending-${kind}:${user?.username.toLowerCase() ?? ''}`

  function loadRecovery(currentUser: User, sample: boolean) {
    if (sample) { setPendingRound(null); setPendingPayment(null); setRecoveryReady(true); setRecoveryError(''); return }
    setRecoveryReady(false); setRecoveryError('')
    try {
      setPendingRound(readPendingRound(sessionStorage.getItem(`aahar-pending-round:${currentUser.username.toLowerCase()}`)))
      setPendingPayment(readPendingPayment(sessionStorage.getItem(`aahar-pending-payment:${currentUser.username.toLowerCase()}`)))
      setRecoveryReady(true)
    } catch { setRecoveryError('Could not restore pending bill operations in this browser. Check the order and kitchen before continuing.'); }
  }
  useEffect(() => {
    if (!pendingRound && !pendingPayment) return
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = '' }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [pendingRound, pendingPayment])

  useEffect(() => {
    if (guestPath) return
    api.me().then(currentUser => { loadRecovery(currentUser, false); setLoading(true); setUser(currentUser) }).catch((e: unknown) => {
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
    const sampleUser = { username: 'Sample user', role: 'Demo' }
    loadRecovery(sampleUser, true); setAuthNotice(''); setDemo(true); setUser(sampleUser); setMenu(demoMenu); setTables(demoTables); setOrders(demoOrders); setTickets(demoTickets); setError(''); setWorkspaceIncomplete(false); setLoading(false)
  }
  function endSession(nextNotice = '') {
    setUser(null); setDemo(false); setMenu([]); setTables([]); setOrders([]); setTickets([]); setCart({}); setCartNotes({}); setTableId(null); setActiveOrderId(null); setReceiptOrder(null); setPendingRound(null); setPendingPayment(null); setRecoveryReady(false); setRecoveryError(''); setPaymentVerified(false); setOrderType('dine-in'); setPage('pos'); setLoading(false); setError(''); setWorkspaceIncomplete(false); setNotice(''); setAuthNotice(nextNotice)
  }
  async function signOut() {
    if (!demo) {
      try { await api.logout() } catch (e) { setError(message(e)); return }
    }
    endSession()
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
  const selectedOrder = orders.find(order => order.id === activeOrderId)
  const recoveryBlocked = !recoveryReady || !!recoveryError || !!pendingRound || !!pendingPayment
  const canAddItems = !recoveryBlocked && !workspaceIncomplete && !loading &&
    (!selectedOrder || (selectedOrder.roundCount !== undefined && ['sent', 'completed'].includes(selectedOrder.status.toLowerCase())))
  function selectBill(order: Order) {
    if (itemCount && activeOrderId !== order.id) { setError('Send or clear new items before opening another bill.'); return }
    if (pendingRound && pendingRound.orderId !== order.id) { setError('Resolve the pending kitchen round before opening another bill.'); return }
    setActiveOrderId(order.id); setTableId(order.tableId); setOrderType(order.tableId === null ? 'takeaway' : 'dine-in'); setPage('pos'); setNotice('')
  }
  function selectTable(id: number | null) {
    if (recoveryBlocked || (itemCount && (tableId !== null || activeOrderId !== null) && id !== tableId)) { setError('Send or clear new items and resolve pending operations before changing tables.'); return }
    setTableId(id); setActiveOrderId(null)
    if (id === null) return
    const found = activeTableOrders(orders, id)
    const table = tables.find(value => value.id === id)
    if (found.length === 1) setActiveOrderId(found[0].id)
    else if (found.length > 1 || (table && isOccupied(table))) setError('This table is occupied but its open bill cannot be determined. Refresh the workspace before continuing.')
  }
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
    if (recoveryBlocked || workspaceIncomplete || selectedOrder || !itemCount || unavailableIds.length || (orderType === 'dine-in' && (!selectedTable || isOccupied(selectedTable)))) return
    setBusy(true); setError(''); setNotice('')
    try {
      const items = cartItems.map(item => ({ menuItemId: item.id, quantity: cart[item.id], notes: cartNotes[item.id]?.trim() || null }))
      const destination = orderType === 'takeaway' ? null : tableId
      let order: Order
      if (demo) {
        order = { id: Math.max(100, ...orders.map(o => o.id)) + 1, tableId: destination, status: 'Draft', roundCount: 0, createdAt: new Date().toISOString(), items: cartItems.map(item => ({ menuItemId: item.id, name: item.name, quantity: cart[item.id], unitPrice: item.price, notes: cartNotes[item.id]?.trim() || null })), total, payment: null }
        setOrders(current => [order, ...current])
      } else {
        order = await api.createOrder(destination, items)
        setOrders(current => [order, ...current.filter(value => value.id !== order.id)])
      }
      setCart({}); setCartNotes({}); setActiveOrderId(order.id)
      let sendError: unknown = null
      if (preferences.autoSendToKitchen) {
        try {
          if (demo) {
            setOrders(current => current.map(value => value.id === order.id ? { ...value, status: 'Sent', roundCount: 1 } : value))
            setTickets(current => [{ id: Math.max(100, ...current.map(value => value.id)) + 1, orderId: order.id, tableId: order.tableId, status: 'Queued', createdAt: new Date().toISOString(), isReconstructed: false, items: order.items.map(value => ({ ...value })) }, ...current])
          } else await api.sendOrder(order.id)
        } catch (e) { sendError = e }
      }
      if (!demo) await refresh()
      if (sendError) setError(`Draft order #${order.id} was saved, but sending to the kitchen could not be confirmed: ${message(sendError)} Check the existing bill before retrying; do not create another order.`)
      else setNotice(preferences.autoSendToKitchen ? `Order #${order.id} saved and sent to the kitchen.` : 'Draft created. Review and send it to the kitchen.')
    } catch (e) {
      setError(uncertainMutationOutcome(e) ? 'Order creation could not be confirmed. Refresh Billing and check with staff before trying again.' : message(e))
    } finally { setBusy(false) }
  }
  async function sendRound(retry?: PendingRound) {
    if (busy || pendingPayment || recoveryError || !recoveryReady) return
    const order = orders.find(item => item.id === (retry?.orderId ?? selectedOrder?.id))
    if (!order || order.roundCount === undefined || (!retry && !['sent', 'completed'].includes(order.status.toLowerCase()))) {
      setError('The open bill is unavailable or does not support kitchen rounds. Refresh and check the order.'); return
    }
    if (!retry && (workspaceIncomplete || !itemCount || unavailableIds.length || pendingRound)) return
    let attempt = retry
    if (!attempt) {
      if (!globalThis.crypto?.randomUUID) { setError('Sending a kitchen round requires a secure browser connection.'); return }
      attempt = { orderId: order.id, requestId: globalThis.crypto.randomUUID(), items: cartItems.map(item => ({ menuItemId: item.id, quantity: cart[item.id], notes: cartNotes[item.id]?.trim() || null })) }
      if (!demo) {
        try { sessionStorage.setItem(storageKey('round'), JSON.stringify(attempt)) }
        catch { setError('Cannot safely retain this kitchen round in the browser. Enable session storage before sending.'); return }
        setPendingRound(attempt)
      }
    }
    setBusy(true); setError(''); setNotice('')
    try {
      if (demo) {
        const newItems = attempt.items.map(line => {
          const item = menu.find(value => value.id === line.menuItemId && value.isAvailable)
          if (!item) throw new Error('A selected item is unavailable.')
          return { ...line, name: item.name, unitPrice: item.price }
        })
        const ticket: KitchenTicket = { id: Math.max(100, ...tickets.map(item => item.id)) + 1, orderId: order.id, tableId: order.tableId, status: 'Queued', createdAt: new Date().toISOString(), isReconstructed: false, requestId: attempt.requestId, items: newItems }
        setOrders(current => current.map(item => item.id === order.id ? { ...item, status: 'Sent', roundCount: (item.roundCount ?? 0) + 1, items: [...item.items, ...newItems], total: item.total + newItems.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0) } : item))
        setTickets(current => [ticket, ...current])
      } else {
        const result = await api.addRound(attempt.orderId, attempt.requestId, attempt.items)
        sessionStorage.removeItem(storageKey('round'))
        setPendingRound(null)
        setOrders(current => current.map(item => item.id === result.order.id ? result.order : item))
        setTickets(current => [result.ticket, ...current.filter(item => item.id !== result.ticket.id)])
      }
      setCart({}); setCartNotes({}); setNotice('New items sent to the kitchen and added to the bill.')
      if (!demo) await refresh()
    } catch (e) {
      if (!demo && e instanceof ApiError && e.status === 400) {
        try { sessionStorage.removeItem(storageKey('round')); setPendingRound(null); setError(`${message(e)} The round was rejected; correct the items before sending again.`) }
        catch { setRecoveryError('Could not clear the rejected round from browser storage. Check the bill before continuing.') }
      } else {
        if (!demo) await refresh()
        setError(demo ? message(e) : `${message(e)} The outcome may be uncertain. Retry the same saved round; do not send a new one.`)
      }
    } finally { setBusy(false) }
  }
  async function settleBill(order: Order, method: PaymentMethod) {
    if (busy || recoveryBlocked || workspaceIncomplete || itemCount || !billReadiness(order, tickets)) return
    const attempt: PendingPayment = { orderId: order.id, method }
    if (!demo) {
      try { sessionStorage.setItem(storageKey('payment'), JSON.stringify(attempt)) }
      catch { setError('Cannot safely retain the payment attempt in this browser. Enable session storage before checkout.'); return }
      setPendingPayment(attempt)
    }
    setBusy(true); setError(''); setNotice(''); setPaymentVerified(false)
    try {
      const paid: Order = demo ? { ...order, status: 'Paid', payment: { id: order.id, method, amount: order.total, paidAt: new Date().toISOString() } } : await api.payOrder(order.id, method)
      if (!demo) { sessionStorage.removeItem(storageKey('payment')); setPendingPayment(null) }
      setOrders(current => current.map(item => item.id === order.id ? paid : item))
      if (preferences.autoOpenReceipt) setReceiptOrder(paid)
      setActiveOrderId(null); setTableId(null); setNotice('Full payment recorded as a simulation; no payment was processed. Find the receipt in Billing.')
      if (!demo) await refresh()
    } catch (e) {
      setError(demo ? message(e) : `${message(e)} Do not retry until you refresh and verify whether payment was recorded.`)
    } finally { setBusy(false) }
  }
  async function verifyPayment() {
    if (!pendingPayment || busy) return
    setBusy(true); setError(''); setPaymentVerified(false)
    try {
      const data = await fetchWorkspace(false)
      applyWorkspace(data)
      if (data.errors || !data.orders) throw new Error('Could not refresh the order. Check connectivity and try again.')
      const order = data.orders.find(value => value.id === pendingPayment.orderId)
      if (!order) throw new Error('The order was not found. Check with staff before proceeding.')
      if (order.status.toLowerCase() === 'paid' && order.payment) {
        sessionStorage.removeItem(storageKey('payment')); setPendingPayment(null); if (preferences.autoOpenReceipt) setReceiptOrder(order); setActiveOrderId(null); setTableId(null); setNotice('The simulated payment was recorded. Find the receipt in Billing.')
      } else if (['sent', 'completed'].includes(order.status.toLowerCase()) && !order.payment) {
        setPaymentVerified(true); setNotice('No payment is shown on the refreshed order. Confirm with staff before unlocking checkout.')
      } else throw new Error('The order is no longer payable. Check its status with staff before proceeding.')
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }
  function unlockPayment() {
    if (!pendingPayment || !paymentVerified) return
    try { sessionStorage.removeItem(storageKey('payment')); setPendingPayment(null); setPaymentVerified(false); setNotice('Checkout unlocked after verification.') }
    catch { setRecoveryError('Could not update payment recovery storage. Check the bill before continuing.') }
  }
  async function updateOrder(order: Order, action: 'send' | 'complete' | 'pay', method: PaymentMethod = 'Cash') {
    if (recoveryBlocked || workspaceIncomplete) { setError('Resolve pending operations and refresh before updating orders.'); return }
    setBusy(true); setError(''); setNotice('')
    try {
      if (demo) {
        setOrders(current => current.map(item => item.id === order.id ? { ...item, status: action === 'send' ? 'Sent' : action === 'complete' ? 'Completed' : 'Paid', roundCount: action === 'send' ? 1 : item.roundCount, payment: action === 'pay' ? { id: order.id, method, amount: order.total, paidAt: new Date().toISOString() } : item.payment } : item))
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
    if (recoveryBlocked || workspaceIncomplete) { setError('Resolve pending operations and refresh before cancelling an order.'); return false }
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
    if (recoveryBlocked || workspaceIncomplete) { setError('Resolve pending operations and refresh before editing the draft.'); return false }
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
  if (!user && !guestPath) return <><SignIn onLogin={currentUser => { loadRecovery(currentUser, false); setAuthNotice(''); setLoading(true); setUser(currentUser) }} onDemo={startDemo} authNotice={authNotice} />{error && <div className="connection-error" role="alert">Could not check your session: {error}</div>}</>
  if (guestPath) return <GuestMenu menu={menu} loading={loading} error={error} retry={refresh} sample={sampleGuest} />

  const title = navigation.find(item => item.id === page)?.label ?? 'Guest menu'
  const canManage = !demo && ['admin', 'manager'].includes(user?.role.toLowerCase() ?? '')
  const canAdmin = !demo && user?.role.toLowerCase() === 'admin'
  const canServe = demo || ['admin', 'manager', 'staff'].includes(user?.role.toLowerCase() ?? '')
  const canComplete = demo || ['admin', 'manager', 'kitchen'].includes(user?.role.toLowerCase() ?? '')
  const visibleNavigation = navigation.filter(item => (item.id !== 'guest-requests' || (canServe && !demo)) && (item.id !== 'staff' || canAdmin) && (item.id !== 'password' || !demo) && (!['inventory', 'reports'].includes(item.id) || canManage || demo))
  const serviceNavigation = visibleNavigation.filter(item => ['pos', 'tables', 'kitchen', 'billing', 'guest-requests'].includes(item.id))
  const managementNavigation = visibleNavigation.filter(item => ['menu', 'inventory', 'reports', 'staff'].includes(item.id))
  const accountNavigation = visibleNavigation.filter(item => item.id === 'password' || item.id === 'settings')
  return <div className={`app-shell density-${preferences.density}`}>
    <a className="skip-link" href="#main-content">Skip to content</a>
    <aside className="sidebar"><Brand /><nav aria-label="Main navigation">{[{ name: 'Service', items: serviceNavigation }, { name: 'Management', items: managementNavigation }].map(group => group.items.length > 0 && <div className="nav-group" key={group.name}><div className="workspace-label">{group.name}</div>{group.items.map(({ id, label, icon: Icon }) => <button key={id} className={`nav-item ${page === id ? 'active' : ''}`} onClick={() => { setInventoryLowOnly(false); setPage(id); setNotice('') }} aria-current={page === id ? 'page' : undefined}><Icon size={18} strokeWidth={1.7} />{label}{page === id && <span className="nav-indicator" />}</button>)}</div>)}</nav><div className="sidebar-bottom"><button className="nav-item guest-link" onClick={() => { setPage('guest'); setNotice('') }} aria-current={page === 'guest' ? 'page' : undefined}><Store size={18} /> Guest menu <ArrowRight size={15} /></button><div className="account-group"><div className="profile"><span className="avatar">{user?.username.slice(0, 1).toUpperCase()}</span><span><strong>{user?.username}</strong><small>{demo ? 'Sample workspace' : user?.role}</small></span></div>{accountNavigation.map(({ id, label, icon: Icon }) => <button key={id} className={`nav-item ${page === id ? 'active' : ''}`} aria-current={page === id ? 'page' : undefined} onClick={() => { setPage(id); setNotice('') }}><Icon size={18} />{label}</button>)}<button className="nav-item" onClick={signOut}><LogOut size={18} /> Sign out</button></div></div></aside>
    <main className="main" id="main-content" tabIndex={-1}><header className="topbar"><div className="breadcrumb">Workspace <span>/</span> <strong>{title}</strong></div><div className="top-actions">{demo && <span className="demo-badge">SAMPLE MODE</span>}<span className="today"><span className="online-dot" /> {demo ? 'Example data' : 'Restaurant workspace'}</span><button className="icon-button" aria-label="About this workspace" onClick={() => setNotice('Aahar is a restaurant workspace. Payments are simulated and receipts are not IRD compliant.')}><CircleHelp size={19} /></button></div></header>
      <div className="content">
        {(error || workspaceIncomplete) && <div className="alert global-alert" role="alert"><span>{error} Displayed workspace data may be out of date.</span><button onClick={() => void refresh()}>Refresh workspace</button>{!workspaceIncomplete && <button aria-label="Dismiss error" onClick={() => setError('')}><X size={16} /></button>}</div>}
        {recoveryError && <div className="alert global-alert" role="alert">{recoveryError}</div>}
        {pendingRound && <div className="alert global-alert" role="alert"><span>Kitchen round for order #{pendingRound.orderId} is pending confirmation. Keep this tab open; retrying sends the same saved items and request ID.</span><button disabled={busy || loading} onClick={() => void sendRound(pendingRound)}>Retry saved round</button><button disabled={busy || loading} onClick={() => void refresh()}>Refresh</button></div>}
        {pendingPayment && <div className="alert global-alert" role="alert"><span>Payment for order #{pendingPayment.orderId} needs verification. Do not retry until you confirm its status.</span><button disabled={busy} onClick={() => void verifyPayment()}>Refresh and check payment</button>{paymentVerified && <button disabled={busy} onClick={unlockPayment}>I verified no payment was recorded</button>}</div>}
        {notice && <div className="notice" role="status"><span>{notice}</span><button aria-label="Dismiss message" onClick={() => setNotice('')}><X size={16} /></button></div>}
        {loading && <div className="loading" role="status">Loading workspace…</div>}
        {page === 'pos' && <><div className="page-heading"><div><div className="eyebrow">YOUR SERVICE, IN ONE PLACE</div><h1>Point of sale<span className="heading-dot">.</span></h1><p>Let’s get something good to the table.</p></div><div className="shift-label"><span className="shift-icon"><Clock3 size={17} /></span><span><small>CURRENT WORKSPACE</small><strong>{demo ? 'Sample service' : 'Restaurant service'}</strong></span></div></div>
          {(canManage || demo) && <ServiceOverview orders={orders} tickets={tickets} tables={tables.map(table => ({ ...table, isOccupied: isOccupied(table) }))} demo={demo} loading={loading} incomplete={workspaceIncomplete} refreshWorkspace={refresh} navigate={destination => { setInventoryLowOnly(destination === 'inventory'); setPage(destination); setNotice('') }} />}
          <div className="pos-layout"><section className="menu-panel"><div className="section-heading"><div><span className="eyebrow">THE MENU</span><h2>What are we serving?</h2></div><span className="count-label">{visibleMenu.length} items</span></div><div className="menu-tools"><div className="categories" aria-label="Menu categories">{categories.map(c => <button key={c} className={category === c ? 'selected' : ''} aria-pressed={category === c} onClick={() => setCategory(c)}>{c}</button>)}</div><label className="search"><Search size={17} /><span className="sr-only">Search menu</span><input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search menu..." /></label></div>
            <div className="item-grid">{visibleMenu.map((item, index) => <button className="menu-card" key={item.id} aria-label={`Add ${item.name} to order, ${money(item.price)}`} disabled={!canServe || !canAddItems || busy || !canAddToCart(cart, item.id)} onClick={() => changeQuantity(item.id, 1)}><div className={`item-art art-${index % 4}`}><MenuImage key={`${item.id}-${imageVersions[item.id] ?? 0}`} id={item.id} name={item.name} version={imageVersions[item.id]} sample={demo} /></div><div className="item-details"><div className="item-category">{item.category || 'FROM THE MENU'}</div><strong>{item.name}</strong><p>{item.description || 'Made fresh to order'}</p><div className="item-bottom"><span>{money(item.price)}</span><span className="add-circle" aria-hidden="true"><Plus size={16} /></span></div></div></button>)}</div>{!loading && !workspaceIncomplete && !visibleMenu.length && <Empty title="Nothing on the menu yet" text={search || category !== 'All items' ? 'Try another search or category.' : 'Available menu items will appear here.'} />}</section>
            <aside className="order-panel"><div className="order-head"><div><span className="eyebrow">SERVICE WORKSPACE</span><h2>{selectedOrder ? 'Running bill' : 'New order'}</h2></div><span className="order-icon"><ShoppingBag size={18} /></span></div><div className="order-type"><label><span className="sr-only">Order type</span><select value={orderType} disabled={busy || recoveryBlocked || !!itemCount || loading} onChange={e => { setOrderType(e.target.value === 'takeaway' ? 'takeaway' : 'dine-in'); setTableId(null); setActiveOrderId(null) }}><option value="dine-in">Dine in</option><option value="takeaway">Takeaway</option></select><ChevronDown size={14} /></label>{orderType === 'dine-in' && <label><span className="sr-only">Select table</span><select value={tableId ?? ''} disabled={busy || recoveryBlocked || loading || workspaceIncomplete} onChange={e => selectTable(e.target.value ? Number(e.target.value) : null)}><option value="">Select table</option>{tables.map(table => <option key={table.id} value={table.id}>Table {table.number}{isOccupied(table) ? ' · in service' : ' · available'}</option>)}</select><ChevronDown size={14} /></label>}</div>
              {selectedOrder && <RunningBill key={selectedOrder.id} order={selectedOrder} tables={tables} tickets={tickets} busy={busy} stale={workspaceIncomplete || loading || !recoveryReady || !!recoveryError} hasNewItems={itemCount > 0} hasPendingRound={!!pendingRound || !!pendingPayment} canServe={canServe} defaultPayment={preferences.defaultPayment} onSendDraft={order => updateOrder(order, 'send')} onEditDraft={() => setPage('billing')} onPay={settleBill} />}
              {selectedOrder && selectedOrder.status.toLowerCase() !== 'draft' && selectedOrder.roundCount === undefined && <div className="schema-warning" role="status">Adding more items requires the kitchen-rounds server and database schema upgrade. The existing bill can still be completed and paid.</div>}
              <div className="new-items-heading"><strong>{selectedOrder ? 'New items · not yet sent' : 'Items for new order'}</strong>{itemCount > 0 && <button type="button" disabled={busy || !!pendingRound} onClick={() => { setCart({}); setCartNotes({}) }}>Clear new items</button>}</div>
              <div className="order-lines">{cartItems.length ? cartItems.map(item => <div className="order-line" key={item.id}><div><strong>{item.name}</strong><small>{money(item.price)} each{!item.isAvailable && ' · unavailable'}</small><label className="cart-note"><span>Kitchen note</span><input maxLength={300} disabled={busy} value={cartNotes[item.id] ?? ''} onChange={e => setCartNotes({ ...cartNotes, [item.id]: e.target.value })} placeholder="Optional" /></label></div><div className="line-right"><strong>{money(item.price * cart[item.id])}</strong><div className="stepper"><button disabled={busy} onClick={() => changeQuantity(item.id, -1)} aria-label={`Remove one ${item.name}`}><Minus size={13} /></button><span aria-live="polite" aria-atomic="true">{cart[item.id]}</span><button disabled={busy || !item.isAvailable || !canAddToCart(cart, item.id)} onClick={() => changeQuantity(item.id, 1)} aria-label={`Add one ${item.name}`}><Plus size={13} /></button></div></div></div>) : !itemCount && <div className="empty-order"><span><ShoppingBag size={22} strokeWidth={1.4} /></span><strong>A fresh start</strong><p>Choose an item from the menu<br />to begin an order.</p></div>}
                {unavailableIds.length > 0 && <div className="alert" role="alert"><div>Some selected items are unavailable or no longer on the menu: {unavailableIds.map(id => menu.find(item => item.id === id)?.name ?? `item #${id}`).join(', ')}. <button disabled={busy} onClick={() => { setCart(current => Object.fromEntries(Object.entries(current).filter(([id]) => !unavailableIds.includes(Number(id))))); setCartNotes(current => Object.fromEntries(Object.entries(current).filter(([id]) => !unavailableIds.includes(Number(id))))) }}>Remove unavailable items</button></div></div>}
              </div><div className="order-footer"><div className="summary-line"><span>New items</span><span aria-live="polite" aria-atomic="true">{itemCount}</span></div><div className="summary-line total-line"><strong>New items estimate</strong><strong>{money(total)}</strong></div><p className="tax-note">Final prices are determined by the server. Maximum 100 different items and 1,000 of each.</p>{selectedOrder ? <button className="button primary wide" disabled={!canAddItems || !itemCount || !!unavailableIds.length || busy || !canServe} onClick={() => void sendRound()}>{busy ? 'Sending…' : 'Send new items to kitchen'} <ArrowRight size={17} /></button> : <button className="button primary wide" disabled={!itemCount || unavailableIds.length > 0 || (orderType === 'dine-in' && (!selectedTable || isOccupied(selectedTable))) || busy || !canServe || recoveryBlocked || workspaceIncomplete || loading} onClick={() => void createOrder()}>{busy ? 'Creating order…' : 'Create draft order'} <ArrowRight size={17} /></button>}<p className="order-hint">{!canServe ? 'Your role cannot create orders.' : recoveryBlocked ? 'Resolve pending operations before continuing.' : unavailableIds.length ? 'Remove unavailable items before continuing.' : selectedOrder?.status.toLowerCase() === 'draft' ? 'Edit or send the existing draft before adding another round.' : selectedOrder && selectedOrder.roundCount === undefined ? 'Additional rounds require the server upgrade.' : !selectedOrder && orderType === 'dine-in' && !selectedTable ? 'Select a table to begin.' : 'Review new items before sending.'}</p></div></aside></div></>}
        {page === 'tables' && <><PageHeading eyebrow="THE FLOOR" title="Tables" text="Choose a table to open its service workspace." /><div className="surface"><div className="section-heading"><div><span className="eyebrow">FLOOR OVERVIEW</span><h2>Your tables</h2></div><span className="count-label">{tables.length} tables</span></div><div className="table-grid">{tables.map(table => { const active = isOccupied(table); const bills = activeTableOrders(orders, table.id); return <button key={table.id} className={`table-card ${active ? 'table-occupied' : 'table-available'} ${tableId === table.id ? 'table-selected' : ''}`} disabled={!canServe || busy || loading || workspaceIncomplete || recoveryBlocked} onClick={() => { if (itemCount && tableId !== table.id && (tableId !== null || activeOrderId !== null)) { setError('Send or clear new items before changing tables.'); return } setOrderType('dine-in'); selectTable(table.id); setPage('pos') }}><div className="table-card-top"><span className="table-symbol"><Grid2X2 size={20} /></span><span className={`status ${active ? 'status-busy' : ''}`}>{active ? '● Occupied' : '● Available'}</span></div><strong>Table {table.number}</strong><small>{table.capacity} seats{bills.length === 1 ? ` · Order #${bills[0].id}` : ''}</small><span className="table-action">{active ? 'Open running bill' : 'Start an order'} <ArrowRight size={14} /></span></button> })}</div>{!loading && !workspaceIncomplete && !tables.length && <Empty title="No tables found" text="Tables will appear here when available." />}{canManage && <TableEditor refresh={refresh} />}</div></>}
        {page === 'kitchen' && <><PageHeading eyebrow="KEEP THINGS MOVING" title="Kitchen" text="Each ticket is a separate kitchen round." /><div className="surface"><div className="section-heading"><div><span className="eyebrow">KITCHEN QUEUE</span><h2>On the pass</h2></div><button className="button small secondary" disabled={loading} onClick={() => void refresh()}>Refresh</button></div><div className="ticket-grid">{tickets.map(ticket => <KitchenCard key={ticket.id} ticket={ticket} tables={tables} busy={busy || workspaceIncomplete || loading} canPrepare={canComplete} advance={advanceTicket} />)}</div>{!loading && !workspaceIncomplete && !tickets.length && <Empty title="No kitchen tickets" text="Sent orders will appear here." />}</div></>}
        {page === 'billing' && (canServe || canComplete) && <><PageHeading eyebrow="CHECKOUT" title="Billing" text="Review open bills and access simulated receipts." /><div className="surface"><div className="section-heading"><div><span className="eyebrow">ORDER MANAGEMENT</span><h2>Orders</h2></div><button className="button small secondary" disabled={loading} onClick={() => void refresh()}>Refresh</button></div><div className="ticket-grid">{orders.map(order => <OrderTicket key={order.id} order={order} tickets={tickets} menu={menu} tables={tables} busy={busy || recoveryBlocked || workspaceIncomplete || loading} canServe={canServe} canComplete={canComplete} canCancelSubmitted={demo || canManage} update={updateOrder} cancel={cancelOrder} saveDraft={saveDraft} openBill={selectBill} />)}</div>{!loading && !workspaceIncomplete && !orders.length && <Empty title="No orders yet" text="Orders and their receipts will appear here." />}</div></>}
        {page === 'guest-requests' && canServe && !demo && <><PageHeading eyebrow="GUEST SERVICE" title="Guest requests" text="Review requests before they become orders." /><GuestRequests tables={tables} onChanged={refresh} /></>}
        {page === 'menu' && <><PageHeading eyebrow="FOOD & DRINK" title="Your menu" text="Everything you’re serving, in one place." /><div className="surface"><div className="section-heading"><div><span className="eyebrow">MENU CATALOGUE</span><h2>All menu items</h2></div><span className="count-label">{menu.length} items</span></div>{canManage && <><MenuEditor items={menu} refresh={refresh} imageVersions={imageVersions} imageChanged={id => setImageVersions(current => ({ ...current, [id]: (current[id] ?? 0) + 1 }))} /><RecipeEditor items={menu} /></>}<div className="list-table"><div className="list-header"><span>ITEM</span><span>AVAILABILITY</span><span>PRICE</span></div>{menu.map(item => <div className="list-row" key={item.id}><span><strong>{item.name}</strong><small>{item.description || item.category || 'Menu item'}</small></span><span><span className={`status ${item.isAvailable ? '' : 'status-busy'}`}>{item.isAvailable ? 'Available' : 'Unavailable'}</span></span><strong>{money(item.price)}</strong></div>)}</div>{!loading && !workspaceIncomplete && !menu.length && <Empty title="No menu items found" text="Your menu will appear here when available." />}</div></>}
        {(page === 'inventory' || page === 'reports') && !canManage && <><PageHeading eyebrow={page === 'inventory' ? 'STOCK & SUPPLIES' : 'THE BIG PICTURE'} title={page === 'inventory' ? 'Inventory' : 'Reports'} text={page === 'inventory' ? 'Stay on top of what your kitchen needs.' : 'Make sense of your service.'} /><div className="surface placeholder"><span className="placeholder-icon">{page === 'inventory' ? <Package size={25} /> : <ClipboardList size={25} />}</span><span className="eyebrow">ACCESS & AVAILABILITY</span><h2>{demo ? 'Not available in sample mode.' : 'Manager access required.'}</h2><p>{demo ? 'Sample mode does not connect to inventory or sales data.' : 'Inventory and sales data are available to managers and administrators.'}</p></div></>}
        {page === 'inventory' && canManage && <><PageHeading eyebrow="STOCK & SUPPLIES" title="Inventory" text="Stay on top of what your kitchen needs." /><Inventory uncertain={uncertainPurchase} setUncertain={setUncertainPurchase} initialLowOnly={inventoryLowOnly} /></>}
        {page === 'reports' && canManage && <><PageHeading eyebrow="THE BIG PICTURE" title="Reports" text="Make sense of your service." /><SalesReport /></>}
        {page === 'staff' && canAdmin && <><PageHeading eyebrow="YOUR TEAM" title="Staff accounts" text="Create access for your restaurant team." /><StaffAccounts /></>}
        {page === 'password' && !demo && <><PageHeading eyebrow="YOUR ACCOUNT" title="Account security" text="Update your password." /><ChangePassword onSignedOut={endSession} /></>}
        {page === 'settings' && <><PageHeading eyebrow="YOUR WORKSPACE" title="Settings" text="Configure this browser’s workflow." /><WorkspaceSettings preferences={preferences} error={preferenceError} save={savePreferences} /></>}
        {page === 'guest' && <><PageHeading eyebrow="SHARE WHAT’S GOOD" title="Guest menu" text={demo ? 'Preview the sample menu.' : 'Share the menu and let guests send requests for staff review.'} /><div className="surface guest-preview"><div><span className="eyebrow">GUEST EXPERIENCE</span><h2>Let the menu do the talking.</h2><p>Open the guest menu on this computer. A localhost link cannot be used by guests on other devices.</p>{!demo && <label className="field guest-table-select">Guest link destination<select value={guestTable} onChange={event => setGuestTable(event.target.value)}><option value="">Takeaway</option>{tables.map(table => <option value={table.id} key={table.id}>Table {table.number}</option>)}</select></label>}<a className="button primary" href={guestMenuUrl(window.location.href, demo, guestTable)} target="_blank" rel="noreferrer">Open guest menu <ArrowRight size={16} /></a></div><GuestQr url={guestMenuUrl(window.location.href, demo, guestTable)} /></div></>}
      </div>
    </main>
    {receiptOrder?.payment && <Receipt order={receiptOrder} table={tables.find(table => table.id === receiptOrder.tableId)} close={() => setReceiptOrder(null)} />}
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
  const tableParam = new URLSearchParams(window.location.search).get('tableId')
  const invalidTable = tableParam !== null && (!/^[1-9]\d*$/.test(tableParam) || Number(tableParam) > 2147483647)
  const tableId = tableParam === null || invalidTable ? null : Number(tableParam)
  return <main className="guest-page"><header><Brand /><a href={window.location.pathname}><ArrowLeft size={16} /> Staff sign in</a></header><div className="guest-content"><div className="section-heading"><div><span className="eyebrow">{sample ? 'SAMPLE MENU · EXAMPLE DATA' : 'GUEST MENU'}</span><h1>Explore the menu</h1></div><span className="count-label">{available.length} items</span></div>{error && <div className="alert" role="alert">{error} <button onClick={() => void retry()}>Retry</button></div>}{loading && <p role="status">Loading menu…</p>}<GuestOrdering menu={menu} tableId={tableId} invalidTable={invalidTable} menuError={error} menuLoading={loading} refreshMenu={retry} sample={sample} /></div><footer><Brand /><span>GOOD FOOD IS BETTER TOGETHER</span></footer></main>
}

export default App
