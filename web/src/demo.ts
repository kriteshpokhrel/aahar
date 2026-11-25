import type { MenuItem, Order, Table } from './api'

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
  { id: 1, number: 1, capacity: 2 }, { id: 2, number: 2, capacity: 4 },
  { id: 3, number: 3, capacity: 4 }, { id: 4, number: 4, capacity: 6 },
  { id: 5, number: 5, capacity: 2 }, { id: 6, number: 6, capacity: 4 },
]

export const demoOrders: Order[] = [
  { id: 104, tableId: 2, status: 'Sent', createdAt: new Date().toISOString(), items: [{ menuItemId: 1, name: 'Chicken momo', quantity: 2, unitPrice: 320 }, { menuItemId: 7, name: 'Masala tea', quantity: 2, unitPrice: 120 }], total: 880 },
  { id: 103, tableId: 4, status: 'Sent', createdAt: new Date().toISOString(), items: [{ menuItemId: 5, name: 'Dal bhat set', quantity: 2, unitPrice: 420 }], total: 840 },
]
