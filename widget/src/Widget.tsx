import { useState, useEffect } from 'react'
import type { WidgetConfig } from './types'
import { ChatWindow } from './components/ChatWindow'
import { useChat } from './hooks/useChat'

interface Props {
  config: WidgetConfig
}

function ChatBubbleIcon() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="white">
      <path d="M20 2H4C2.9 2 2 2.9 2 4v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z" />
    </svg>
  )
}

function CloseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round">
      <line x1="18" y1="6" x2="6" y2="18" />
      <line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  )
}

export function Widget({ config }: Props) {
  const [open, setOpen] = useState(false)
  const { messages, loading, sendMessage } = useChat(config.apiBase, config.apiKey)

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
    <div
      style={{
        ...containerStyle,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-end',
        gap: '12px',
      }}
    >
      <style>{`
        @keyframes sl-blink {
          0%, 100% { opacity: 1; }
          50% { opacity: 0; }
        }
        @keyframes sl-fadein {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        :root { --sl-color: ${config.color}; }
      `}</style>

      {open && (
        <div style={{ animation: 'sl-fadein 0.18s ease-out' }}>
          <ChatWindow
            title={config.title}
            color={config.color}
            messages={messages}
            loading={loading}
            onSend={sendMessage}
            onClose={() => setOpen(false)}
          />
        </div>
      )}

      <button
        onClick={() => setOpen((o) => !o)}
        aria-label={open ? 'Close chat' : 'Open chat'}
        style={{
          width: '56px',
          height: '56px',
          borderRadius: '50%',
          background: config.color,
          border: 'none',
          cursor: 'pointer',
          boxShadow: '0 4px 20px rgba(0,0,0,0.22)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
          transition: 'transform 0.15s ease, box-shadow 0.15s ease',
        }}
        onMouseEnter={(e) => {
          ;(e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.07)'
        }}
        onMouseLeave={(e) => {
          ;(e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)'
        }}
      >
        {open ? <CloseIcon /> : <ChatBubbleIcon />}
      </button>
    </div>
  )
}
