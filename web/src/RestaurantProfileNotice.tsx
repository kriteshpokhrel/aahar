import { useRestaurant } from './restaurantProfile'

export function RestaurantProfileNotice() {
  const { loading, error, reload } = useRestaurant()
  if (error) return <div className="alert global-alert" role="alert"><span>{error}</span><button onClick={() => void reload()}>Reload restaurant details</button></div>
  if (loading) return <p className="muted-copy" role="status">Loading restaurant details...</p>
  return null
}
