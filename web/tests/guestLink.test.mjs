import assert from 'node:assert/strict'
import { test } from 'node:test'
import React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { QRCodeSVG } from 'qrcode.react'
import jsQR from 'jsqr'
import { guestMenuUrl } from '../src/guestLink.ts'

function decodeQr(value) {
  const svg = renderToStaticMarkup(React.createElement(QRCodeSVG, { value, size: 192, marginSize: 4 }))
  const size = Number(svg.match(/viewBox="0 0 (\d+) \d+"/)[1])
  const path = svg.match(/fill="#000000" d="([^"]+)"/)[1]
  const scale = 5, width = size * scale
  const pixels = new Uint8ClampedArray(width * width * 4).fill(255)
  for (const match of path.matchAll(/M(\d+)[, ](\d+) ?h(\d+)v1H\d+z/g)) {
    const [, x, y, length] = match.map(Number)
    for (let py = y * scale; py < (y + 1) * scale; py++) {
      for (let px = x * scale; px < (x + length) * scale; px++) {
        const index = (py * width + px) * 4
        pixels[index] = pixels[index + 1] = pixels[index + 2] = 0
      }
    }
  }
  return jsQR(pixels, width, width)?.data
}

test('guest QR locally encodes the exact link for takeaway, table and sample', () => {
  const current = 'https://localhost:5173/restaurant?old=ignored#section'
  const urls = [
    guestMenuUrl(current, false, ''),
    guestMenuUrl(current, false, '42'),
    guestMenuUrl(current, true, '42'),
  ]
  assert.deepEqual(urls, [
    'https://localhost:5173/restaurant?guest',
    'https://localhost:5173/restaurant?guest&tableId=42',
    'https://localhost:5173/restaurant?guest=sample',
  ])
  for (const url of urls) assert.equal(decodeQr(url), url)
})
