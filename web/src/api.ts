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
export type Ingredient = { id: number; name: string; unit: string; stock: number }
export type Sales = { ordersPaid: number; total: number }
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
  createIngredient: (name: string, unit: string, stock: number) => post<Ingredient>('/ingredients', { name, unit, stock }),
  addStock: (id: number, quantity: number) => post<Ingredient>(`/ingredients/${id}/stock`, { quantity }),
  sales: () => request<Sales>('/sales'),
}
