import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Widget } from './Widget'
import type { WidgetConfig } from './types'

function getConfig(script: HTMLOrSVGScriptElement | null): WidgetConfig {
  const el = script as HTMLScriptElement | null
  return {
    apiKey: el?.dataset.apiKey ?? '',
    apiBase: el?.dataset.apiBase ?? 'http://localhost:5000',
    title: el?.dataset.title ?? 'Support Bot',
    color: el?.dataset.color ?? '#2563eb',
    position: (el?.dataset.position ?? 'bottom-right') as WidgetConfig['position'],
    autoOpenDelaySeconds: el?.dataset.autoOpenDelaySeconds
      ? parseInt(el.dataset.autoOpenDelaySeconds, 10)
      : undefined,
  }
}

// Locate the current script tag to read data-* attributes
const currentScript = (() => {
  const candidate = document.currentScript as HTMLScriptElement | null
  if (candidate?.dataset?.apiKey) {
    return candidate
  }
  return document.querySelector('script[data-api-key]') as HTMLOrSVGScriptElement | null
})()

const config = getConfig(currentScript)

// Inject a mount point and render
const container = document.createElement('div')
container.id = 'sl-widget-root'
document.body.appendChild(container)

createRoot(container).render(
  <StrictMode>
    <Widget config={config} />
  </StrictMode>
)
