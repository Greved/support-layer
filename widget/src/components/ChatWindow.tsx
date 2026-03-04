import type { Message } from '../types'
import { MessageList } from './MessageList'
import { InputBar } from './InputBar'

interface Props {
  title: string
  color: string
  messages: Message[]
  loading: boolean
  onSend: (text: string) => void
  onClose: () => void
}

export function ChatWindow({ title, color, messages, loading, onSend, onClose }: Props) {
  return (
    <div
      style={{
        width: '360px',
        height: '520px',
        borderRadius: '16px',
        boxShadow: '0 20px 60px rgba(0,0,0,0.18)',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        background: '#fff',
        fontFamily: 'system-ui, sans-serif',
      }}
    >
      {/* Header */}
      <div
        style={{
          background: color,
          color: '#fff',
          padding: '16px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexShrink: 0,
        }}
      >
        <span style={{ fontWeight: 700, fontSize: '16px' }}>{title}</span>
        <button
          onClick={onClose}
          style={{
            background: 'none',
            border: 'none',
            color: '#fff',
            cursor: 'pointer',
            fontSize: '20px',
            lineHeight: 1,
            padding: 0,
          }}
          aria-label="Close chat"
        >
          ×
        </button>
      </div>

      <MessageList messages={messages} />
      <InputBar onSend={onSend} disabled={loading} color={color} />
    </div>
  )
}
