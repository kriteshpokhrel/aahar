import { useState } from 'react'

export function MenuImage({ id, name, version = 0, sample = false }: { id: number; name: string; version?: number; sample?: boolean }) {
  const [unavailable, setUnavailable] = useState(false)
  return <div className="menu-image" aria-hidden="true">
    {!sample && !unavailable ? <img src={`/api/menu/${id}/image?v=${version}`} alt="" onError={() => setUnavailable(true)} /> : <span>{name.slice(0, 1)}</span>}
  </div>
}
