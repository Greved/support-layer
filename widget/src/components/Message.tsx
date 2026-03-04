import type { Message as MessageType } from '../types'

interface Props {
  message: MessageType
}

export function Message({ message }: Props) {
  const isUser = message.role === 'user'
  return (
    <div
      style={{
        display: 'flex',
        justifyContent: isUser ? 'flex-end' : 'flex-start',
        marginBottom: '8px',
      }}
    >
      <div
        style={{
          maxWidth: '80%',
          padding: '8px 12px',
          borderRadius: isUser ? '12px 12px 2px 12px' : '12px 12px 12px 2px',
          background: isUser ? 'var(--sl-color)' : '#f1f5f9',
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
              width: '8px',
              height: '14px',
              background: isUser ? '#fff' : '#94a3b8',
              marginLeft: '2px',
              verticalAlign: 'text-bottom',
              animation: 'sl-blink 1s step-end infinite',
            }}
          />
        )}
      </div>
    </div>
  )
}
