import type { KitchenTicket, Order, Table } from './api.ts'

export function activeTableOrders(orders: Order[], tableId: number) {
  return orders.filter(order => order.tableId === tableId && ['draft', 'sent', 'completed'].includes(order.status.toLowerCase()))
}

export function billReadiness(order: Order, tickets: KitchenTicket[]) {
  const status = order.status.toLowerCase()
  if (status === 'completed') return true
  const rounds = tickets.filter(ticket => ticket.orderId === order.id)
  return status === 'sent' && typeof order.roundCount === 'number' && order.roundCount > 0 &&
    rounds.length === order.roundCount && rounds.every(ticket => ticket.status.toLowerCase() === 'ready')
}

export function tableLabel(order: Order, tables: Table[]) {
  if (order.tableId === null) return 'Takeaway'
  const table = tables.find(item => item.id === order.tableId)
  return table ? `Table ${table.number}` : `Table #${order.tableId}`
}
