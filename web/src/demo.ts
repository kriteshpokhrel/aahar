import type { KitchenTicket, MenuItem, Order, Table } from './api'

export const demoMenu: MenuItem[] = [
  { id: 1, name: 'Chicken momo', description: 'Steamed dumplings, tomato achar', price: 320, category: 'Momo', isAvailable: true },
  { id: 2, name: 'Jhol momo', description: 'Dumplings in a warm sesame broth', price: 360, category: 'Momo', isAvailable: true },
  { id: 3, name: 'Veg momo', description: 'Seasonal vegetables, tomato achar', price: 280, category: 'Momo', isAvailable: true },
  { id: 4, name: 'Chicken chowmein', description: 'Wok-tossed noodles and vegetables', price: 340, category: 'Mains', isAvailable: true },
  { id: 5, name: 'Dal bhat set', description: 'Lentils, rice and seasonal sides', price: 420, category: 'Mains', isAvailable: true },
  { id: 6, name: 'Crispy potatoes', description: 'Chilli, herbs and house spices', price: 240, category: 'Small plates', isAvailable: true },
  { id: 7, name: 'Masala tea', description: 'Milk tea with warming spices', price: 120, category: 'Drinks', isAvailable: true },
  { id: 8, name: 'Fresh lime soda', description: 'Sweet, salty or mixed', price: 160, category: 'Drinks', isAvailable: true },
]

export const demoTables: Table[] = [
  { id: 1, number: 1, capacity: 2, isOccupied: false }, { id: 2, number: 2, capacity: 4, isOccupied: true },
  { id: 3, number: 3, capacity: 4, isOccupied: false }, { id: 4, number: 4, capacity: 6, isOccupied: true },
  { id: 5, number: 5, capacity: 2, isOccupied: false }, { id: 6, number: 6, capacity: 4, isOccupied: false },
]

export const demoOrders: Order[] = [
  { id: 104, tableId: 2, status: 'Sent', roundCount: 1, createdAt: new Date().toISOString(), items: [{ menuItemId: 1, name: 'Chicken momo', quantity: 2, unitPrice: 320, notes: null }, { menuItemId: 7, name: 'Masala tea', quantity: 2, unitPrice: 120, notes: null }], total: 880, payment: null },
  { id: 103, tableId: 4, status: 'Sent', roundCount: 1, createdAt: new Date().toISOString(), items: [{ menuItemId: 5, name: 'Dal bhat set', quantity: 2, unitPrice: 420, notes: null }], total: 840, payment: null },
]
export const demoTickets: KitchenTicket[] = demoOrders.map(order => ({
  id: order.id, orderId: order.id, tableId: order.tableId, status: 'Queued',
  createdAt: order.createdAt, isReconstructed: false, items: order.items.map(item => ({ ...item })),
}))
