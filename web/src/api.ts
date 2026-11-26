export type MenuItem = {
  id: number
  name: string
  description?: string | null
  price: number
  isAvailable: boolean
  category?: string | null
}

export type Table = {
  id: number
  number: number
  capacity: number
  isOccupied: boolean
}

export type Order = {
  id: number
  tableId: number | null
  status: string
  items: OrderItem[]
  total: number
  createdAt: string
  payment: { id: number; method: string; amount: number; paidAt: string } | null
  cancelledAt?: string | null
  cancelledBy?: string | null
  cancellationReason?: string | null
  cancellationStockDisposition?: StockDisposition | null
}

export type OrderItem = { menuItemId: number; name: string; quantity: number; unitPrice: number; notes: string | null }
export type OrderLine = { menuItemId: number; quantity: number; notes: string | null }
export type KitchenTicket = { id: number; orderId: number; tableId: number | null; status: string; createdAt: string; isReconstructed: boolean; items: OrderItem[] }
export type User = { username: string; role: string }
export type PaymentMethod = 'Cash' | 'Card'
export type StockDisposition = 'None' | 'Return' | 'Waste'
export type Ingredient = { id: number; name: string; unit: string; stock: number; lowStockThreshold: number; isLowStock: boolean }
export type Supplier = { id: number; name: string; contact: string | null }
export type SupplierChange = { id: number; supplierId: number; name: string; contact: string | null; actor: string; reason: string; occurredAt: string }
export type ThresholdChange = { id: number; ingredientId: number; previousThreshold: number | null; newThreshold: number; actor: string; reason: string; occurredAt: string }
export type StockMovement = { id: number; ingredientId: number; orderId: number | null; kind: string; quantityChange: number; balanceAfter: number; actor: string; occurredAt: string; reason: string | null; purchaseReceiptLineId: number | null }
export type PurchaseLine = { ingredientId: number; quantity: number; unit: string; unitCost: number }
export type PurchaseReceipt = { id: number; supplierId: number; supplierName: string; reference: string; reason: string; actor: string; receivedAt: string; total: number; lines: { id: number; ingredientId: number; ingredientName: string; unit: string; quantity: number; unitCost: number; lineTotal: number }[] }
export type Sales = { ordersPaid: number; total: number }
export type ReportWindow = { fromDate: string; toDate: string; timeZone: string; fromUtc: string; toUtcExclusive: string }
export type PaidSalesReport = ReportWindow & { ordersPaid: number; total: number }
export type BestSellingReport = ReportWindow & { items: { menuItemId: number; name: string; unitPrice: number; quantity: number; revenue: number }[] }
export type MenuInput = { name: string; description: string | null; category: string; price: number; isAvailable: boolean }
export type GuestStatus = 'Pending' | 'Accepted' | 'Rejected'
export type GuestQuoteLine = OrderLine & { quotedUnitPrice: number }
export type GuestRequest = {
  id: number; tableId: number | null; status: GuestStatus; createdAt: string
  decidedAt: string | null; decidedBy: string | null; rejectionReason: string | null; orderId: number | null
  items: { menuItemId: number; name: string; quantity: number; quotedUnitPrice: number; notes: string | null }[]
}
export type GuestSubmission = { id: number; status: GuestStatus; accessToken: string }
export type GuestRequestStatus = { id: number; status: GuestStatus; orderId: number | null }

export class ApiError extends Error {
  status: number
  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

let csrfToken: string | null = null

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  if (options.body) headers.set('Content-Type', 'application/json')
  if (options.method && !['GET', 'HEAD'].includes(options.method)) {
    csrfToken ??= (await request<{ requestToken: string }>('/auth/csrf')).requestToken
    headers.set('X-CSRF-TOKEN', csrfToken)
  }
  const response = await fetch(`/api${path}`, { ...options, headers, credentials: 'same-origin' })
  if (!response.ok) {
    let message = `Request failed (${response.status}).`
    const body: unknown = await response.json().catch(() => null)
    if (body && typeof body === 'object' && 'detail' in body && typeof body.detail === 'string') message = body.detail
    else if (body && typeof body === 'object' && 'title' in body && typeof body.title === 'string') message = body.title
    throw new ApiError(message, response.status)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: 'POST', ...(body === undefined ? {} : { body: JSON.stringify(body) }) })
const put = <T>(path: string, body: unknown) =>
  request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
const page = (beforeId?: number) => {
  const query = new URLSearchParams({ limit: '20' })
  if (beforeId !== undefined) query.set('beforeId', String(beforeId))
  return query.toString()
}

export const api = {
  me: () => request<User>('/auth/me'),
  login: async (username: string, password: string) => {
    await post('/auth/login', { username, password })
    csrfToken = null
    return request<User>('/auth/me')
  },
  logout: async () => {
    await post('/auth/logout')
    csrfToken = null
  },
  menu: () => request<MenuItem[]>('/menu'),
  guestMenu: () => request<MenuItem[]>('/guest/menu'),
  submitGuestRequest: (tableId: number | null, items: GuestQuoteLine[]) =>
    post<GuestSubmission>('/guest/requests', { tableId, items }),
  guestRequestStatus: (id: number, token: string) =>
    request<GuestRequestStatus>(`/guest/requests/${id}`, { headers: { 'X-Guest-Request-Token': token } }),
  guestRequests: (status: GuestStatus, beforeId?: number) => {
    const params = new URLSearchParams({ status, limit: '20' })
    if (beforeId !== undefined) params.set('beforeId', String(beforeId))
    return request<GuestRequest[]>(`/guest-requests?${params}`)
  },
  acceptGuestRequest: (id: number) => post<{ request: GuestRequest; order: Order }>(`/guest-requests/${id}/accept`),
  rejectGuestRequest: (id: number, reason: string) => post<GuestRequest>(`/guest-requests/${id}/reject`, { reason }),
  tables: () => request<Table[]>('/tables'),
  orders: () => request<Order[]>('/orders'),
  createOrder: (tableId: number | null, items: OrderLine[]) =>
    post<Order>('/orders', { tableId, items }),
  editOrder: (id: number, items: OrderLine[]) =>
    request<Order>(`/orders/${id}/items`, { method: 'PUT', body: JSON.stringify({ items }) }),
  sendOrder: (id: number) => post(`/orders/${id}/send`),
  completeOrder: (id: number) => post(`/orders/${id}/complete`),
  kitchenTickets: () => request<KitchenTicket[]>('/kitchen/tickets'),
  prepareTicket: (id: number) => post<KitchenTicket>(`/kitchen/tickets/${id}/prepare`),
  readyTicket: (id: number) => post<KitchenTicket>(`/kitchen/tickets/${id}/ready`),
  payOrder: (id: number, method: PaymentMethod) => post<Order>(`/orders/${id}/payments`, { method }),
  cancelOrder: (id: number, reason: string, stockDisposition: StockDisposition) =>
    post<Order>(`/orders/${id}/cancel`, { reason, stockDisposition }),
  createMenuItem: (item: MenuInput) => post<MenuItem>('/menu', item),
  updateMenuItem: (id: number, item: MenuInput) => request<MenuItem>(`/menu/${id}`, { method: 'PUT', body: JSON.stringify(item) }),
  createTable: (number: number, capacity: number) => post<Table>('/tables', { number, capacity }),
  ingredients: () => request<Ingredient[]>('/ingredients'),
  lowStock: () => request<Ingredient[]>('/ingredients/low-stock'),
  createIngredient: (name: string, unit: string, stock: number, lowStockThreshold = 0) => post<Ingredient>('/ingredients', { name, unit, stock, lowStockThreshold }),
  addStock: (id: number, quantity: number, unit: string, reason: string) => post<Ingredient>(`/ingredients/${id}/stock`, { quantity, unit, reason }),
  waste: (id: number, quantity: number, unit: string, reason: string) => post<Ingredient>(`/ingredients/${id}/waste`, { quantity, unit, reason }),
  setThreshold: (id: number, threshold: number, reason: string) => put<Ingredient>(`/ingredients/${id}/threshold`, { threshold, reason }),
  thresholdHistory: (id: number, beforeId?: number) => request<ThresholdChange[]>(`/ingredients/${id}/threshold-history?${page(beforeId)}`),
  movements: (id: number, beforeId?: number) => request<StockMovement[]>(`/ingredients/${id}/movements?${page(beforeId)}`),
  suppliers: () => request<Supplier[]>('/suppliers'),
  createSupplier: (name: string, contact: string | null, reason: string) => post<Supplier>('/suppliers', { name, contact, reason }),
  updateSupplier: (id: number, name: string, contact: string | null, reason: string) => put<Supplier>(`/suppliers/${id}`, { name, contact, reason }),
  supplierHistory: (id: number, beforeId?: number) => request<SupplierChange[]>(`/suppliers/${id}/history?${page(beforeId)}`),
  receivePurchase: (supplierId: number, reference: string, reason: string, lines: PurchaseLine[]) =>
    post<PurchaseReceipt>('/purchase-receipts', { supplierId, reference, reason, lines }),
  purchaseReceipts: (beforeId?: number) => request<PurchaseReceipt[]>(`/purchase-receipts?${page(beforeId)}`),
  purchaseReceipt: (id: number) => request<PurchaseReceipt>(`/purchase-receipts/${id}`),
  sales: () => request<Sales>('/sales'),
  salesReport: (fromDate: string, toDate: string) => {
    const query = new URLSearchParams({ fromDate, toDate })
    return request<PaidSalesReport>(`/reports/sales?${query}`)
  },
  bestSellingReport: (fromDate: string, toDate: string, limit = 20) => {
    const query = new URLSearchParams({ fromDate, toDate, limit: String(limit) })
    return request<BestSellingReport>(`/reports/best-selling?${query}`)
  },
}
