import { useState, useEffect } from 'react'
import type { WidgetConfig } from './types'
import { ChatWindow } from './components/ChatWindow'
import { useChat } from './hooks/useChat'

interface Props {
  config: WidgetConfig
}

export function Widget({ config }: Props) {
  const [open, setOpen] = useState(false)
  const { messages, loading, sendMessage } = useChat(config.apiBase, config.apiKey)

  // Auto-open after delay
  useEffect(() => {
    if (config.autoOpenDelaySeconds == null) return
    const id = setTimeout(() => setOpen(true), config.autoOpenDelaySeconds * 1000)
    return () => clearTimeout(id)
  }, [config.autoOpenDelaySeconds])

  const positionStyles: Record<string, React.CSSProperties> = {
    'bottom-right': { position: 'fixed', bottom: '24px', right: '24px', zIndex: 9999 },
    'bottom-left': { position: 'fixed', bottom: '24px', left: '24px', zIndex: 9999 },
    inline: { position: 'relative', display: 'inline-block' },
  }

  const containerStyle = positionStyles[config.position] ?? positionStyles['bottom-right']

  return (
    <div style={containerStyle}>
      <style>{`
        @keyframes sl-blink {
          0%, 100% { opacity: 1; }
          50% { opacity: 0; }
        }
        :root { --sl-color: ${config.color}; }
      `}</style>

      {open ? (
        <ChatWindow
          title={config.title}
          color={config.color}
          messages={messages}
          loading={loading}
          onSend={sendMessage}
          onClose={() => setOpen(false)}
        />
      ) : (
        <button
          onClick={() => setOpen(true)}
          aria-label="Open chat"
          style={{
            width: '56px',
            height: '56px',
            borderRadius: '50%',
            background: config.color,
            border: 'none',
            cursor: 'pointer',
            boxShadow: '0 4px 16px rgba(0,0,0,0.2)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#fff',
            fontSize: '24px',
          }}
        >
          💬
        </button>
      )}
    </div>
  )
}
