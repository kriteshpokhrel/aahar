import react from '@vitejs/plugin-react'
import { readFileSync } from 'node:fs'
import { defineConfig } from 'vite'

const cert = process.env.AAHAR_DEV_CERT
const key = process.env.AAHAR_DEV_KEY
if (!!cert !== !!key) throw new Error('Set both AAHAR_DEV_CERT and AAHAR_DEV_KEY for HTTPS.')

export default defineConfig({
  plugins: [react()],
  server: {
    https: cert && key ? { cert: readFileSync(cert), key: readFileSync(key) } : undefined,
    proxy: {
      '/api': {
        target: process.env.AAHAR_API_URL || 'https://localhost:7041',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
