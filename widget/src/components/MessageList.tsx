import { useEffect, useRef } from 'react'
import type { Message as MessageType } from '../types'
import { Message } from './Message'

interface Props {
  messages: MessageType[]
}

export function MessageList({ messages }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  return (
    <div
      style={{
        flex: 1,
        overflowY: 'auto',
        padding: '12px',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {messages.length === 0 && (
        <p style={{ color: '#94a3b8', fontSize: '14px', textAlign: 'center', marginTop: '32px' }}>
          Ask a question to get started.
        </p>
      )}
      {messages.map((m) => (
        <Message key={m.id} message={m} />
      ))}
      <div ref={bottomRef} />
    </div>
  )
}
