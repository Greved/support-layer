import { useEffect, useRef } from 'react'
import type { Message as MessageType } from '../types'
import { Message } from './Message'

interface Props {
  messages: MessageType[]
  onFeedback: (
    messageId: string,
    rating: 'up' | 'down',
    comment?: string
  ) => Promise<boolean>
}

export function MessageList({ messages, onFeedback }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  return (
    <div
      style={{
        flex: 1,
        overflowY: 'auto',
        padding: '12px 16px',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {messages.length === 0 && (
        <p style={{ color: '#94a3b8', fontSize: '14px', textAlign: 'center', marginTop: '32px' }}>
          Ask a question to get started.
        </p>
      )}

      {messages.length > 0 && (
        <div
          style={{
            textAlign: 'center',
            margin: '4px 0 14px',
          }}
        >
          <span style={{ fontSize: '11px', color: '#94a3b8', fontWeight: 600, letterSpacing: '0.06em' }}>
            TODAY
          </span>
        </div>
      )}

      {messages.map((m) => (
        <Message key={m.id} message={m} onFeedback={onFeedback} />
      ))}
      <div ref={bottomRef} />
    </div>
  )
}
