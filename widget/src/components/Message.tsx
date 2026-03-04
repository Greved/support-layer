import type { Message as MessageType } from '../types'

function HomeIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="#94a3b8">
      <path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z" />
    </svg>
  )
}

function formatTime(ts: number): string {
  return new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

interface Props {
  message: MessageType
}

export function Message({ message }: Props) {
  const isUser = message.role === 'user'

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: isUser ? 'flex-end' : 'flex-start',
        marginBottom: '12px',
      }}
    >
      {/* Bubble */}
      <div
        style={{
          maxWidth: '78%',
          padding: '10px 14px',
          borderRadius: isUser ? '16px 16px 4px 16px' : '16px 16px 16px 4px',
          background: isUser ? '#111827' : '#f3f4f6',
          color: isUser ? '#fff' : '#1e293b',
          fontSize: '14px',
          lineHeight: '1.5',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-word',
        }}
      >
        {message.content}
        {message.streaming && (
          <span
            style={{
              display: 'inline-block',
              width: '2px',
              height: '14px',
              background: isUser ? '#fff' : '#64748b',
              marginLeft: '2px',
              verticalAlign: 'text-bottom',
              animation: 'sl-blink 0.8s step-end infinite',
            }}
          />
        )}
      </div>

      {/* Metadata row: [home icon] [timestamp] for bot, [timestamp] for user */}
      {message.createdAt != null && !message.streaming && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '4px',
            marginTop: '4px',
          }}
        >
          {!isUser && (
            <span
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: '20px',
                height: '20px',
                borderRadius: '50%',
                background: '#f1f5f9',
              }}
            >
              <HomeIcon />
            </span>
          )}
          <span style={{ fontSize: '11px', color: '#94a3b8' }}>
            {formatTime(message.createdAt)}
          </span>
        </div>
      )}
    </div>
  )
}
