import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import { api } from './api'
import type { RestaurantProfile } from './api'

const sampleProfile: RestaurantProfile = {
  name: 'Sample restaurant', tagline: '', address: '', phone: '', email: '', website: '',
  receiptFooter: 'Thank you for visiting.', version: null,
}

export function useRestaurantProfile(sample: boolean) {
  const [profile, setProfile] = useState<RestaurantProfile | null>(null)
  const [loading, setLoading] = useState(!sample)
  const [error, setError] = useState('')
  const sequence = useRef(0)
  const load = useCallback((signal?: AbortSignal) => {
    const request = ++sequence.current
    return api.restaurantProfile(signal).then(result => {
      if (!signal?.aborted && request === sequence.current) { setProfile(result); setError('') }
      if (!signal?.aborted && request === sequence.current) setLoading(false)
      return result
    }, cause => {
      if (!signal?.aborted && request === sequence.current) setError(`Restaurant details could not be loaded. ${cause instanceof Error ? cause.message : 'Please retry.'}`)
      if (!signal?.aborted && request === sequence.current) setLoading(false)
      return null
    })
  }, [])
  const refresh = useCallback(() => sample ? Promise.resolve(sampleProfile) : load(), [sample, load])
  const reload = useCallback(async () => {
    if (sample) return sampleProfile
    setLoading(true); setError('')
    return refresh()
  }, [sample, refresh])
  useEffect(() => {
    const controller = new AbortController()
    if (!sample) void load(controller.signal)
    return () => controller.abort()
  }, [sample, load])
  async function save(value: RestaurantProfile) {
    if (sample) throw new Error('Restaurant settings cannot be saved in sample mode.')
    const request = ++sequence.current
    try {
      const result = await api.saveRestaurantProfile(value)
      if (request === sequence.current) { setProfile(result); setError('') }
      return result
    } finally {
      if (request === sequence.current) setLoading(false)
    }
  }
  return { profile: sample ? sampleProfile : profile, loading: !sample && loading, error: sample ? '' : error, reload, refresh, save }
}

export const RestaurantContext = createContext<ReturnType<typeof useRestaurantProfile> | null>(null)

export function useRestaurant() {
  const context = useContext(RestaurantContext)
  if (!context) throw new Error('Restaurant components must be inside RestaurantContext.')
  return context
}

