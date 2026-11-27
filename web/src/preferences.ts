import type { PaymentMethod } from './api.ts'

export type WorkspacePreferences = { autoSendToKitchen: boolean; density: 'compact' | 'comfortable'; defaultPayment: PaymentMethod; autoOpenReceipt: boolean }
export const defaultPreferences: WorkspacePreferences = { autoSendToKitchen: false, density: 'compact', defaultPayment: 'Cash', autoOpenReceipt: true }
export const preferencesKey = 'aahar-workspace-preferences'

export function parsePreferences(raw: string | null): WorkspacePreferences {
  if (raw === null) return { ...defaultPreferences }
  const value: unknown = JSON.parse(raw)
  if (!value || typeof value !== 'object' ||
      !('autoSendToKitchen' in value) || typeof value.autoSendToKitchen !== 'boolean' ||
      !('density' in value) || !['compact', 'comfortable'].includes(String(value.density)) ||
      !('defaultPayment' in value) || (value.defaultPayment !== 'Cash' && value.defaultPayment !== 'Card') ||
      !('autoOpenReceipt' in value) || typeof value.autoOpenReceipt !== 'boolean') throw new Error('Invalid workspace preferences.')
  return value as WorkspacePreferences
}
