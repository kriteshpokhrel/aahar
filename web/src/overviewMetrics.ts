import type { KitchenTicket, Order, Table } from './api.ts'
import { billReadiness } from './bill.ts'

export function serviceOverview(tickets: KitchenTicket[], orders: Order[], tables: Table[]) {
  const count = (values: { status: string }[], status: string) => values.filter(item => item.status.toLowerCase() === status).length
  return {
    queued: count(tickets, 'queued'),
    preparing: count(tickets, 'preparing'),
    ready: count(tickets, 'ready'),
    drafts: count(orders, 'draft'),
    completed: orders.filter(order => billReadiness(order, tickets)).length,
    availableTables: tables.filter(table => !table.isOccupied).length,
    totalTables: tables.length,
  }
}

export function pendingRequestCount(count: number) {
  return count >= 20 ? '20+' : String(count)
}

export function statusClass(status: string) {
  switch (status.toLowerCase()) {
    case 'queued': case 'pending': case 'completed': return 'status-attention'
    case 'preparing': case 'sent': return 'status-progress'
    case 'ready': case 'paid': case 'accepted': return 'status-success'
    case 'cancelled': case 'rejected': return 'status-closed'
    default: return 'status-neutral'
  }
}
