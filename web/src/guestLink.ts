export function guestMenuUrl(currentUrl: string, sample: boolean, tableId: string): string {
  const query = sample ? '?guest=sample' : tableId ? `?guest&tableId=${encodeURIComponent(tableId)}` : '?guest'
  return new URL(query, currentUrl).href
}
