import { Component } from 'react'
import type { ReactNode } from 'react'
import { QRCodeSVG } from 'qrcode.react'

class QrErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }
  static getDerivedStateFromError() { return { failed: true } }
  render() {
    return this.state.failed ? <div className="alert" role="alert">Unable to generate the QR code. Use the guest menu link instead.</div> : this.props.children
  }
}

export function GuestQr({ url }: { url: string }) {
  return <div className="guest-qr"><QrErrorBoundary key={url}><QRCodeSVG value={url} size={192} marginSize={4} title="QR code for the selected guest menu link" /></QrErrorBoundary><strong>Guest menu QR</strong><span>Generated on this device. A localhost QR cannot be used from another device.</span></div>
}
