import { useState, type KeyboardEvent } from 'react'

function ArrowRightIcon({ active }: { active: boolean }) {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke={active ? 'white' : '#94a3b8'}
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <line x1="5" y1="12" x2="19" y2="12" />
      <polyline points="12 5 19 12 12 19" />
    </svg>
  )
}

interface Props {
  onSend: (text: string) => void
  disabled: boolean
  color: string
}

export function InputBar({ onSend, disabled, color }: Props) {
  const [text, setText] = useState('')

  const handleSend = () => {
    const trimmed = text.trim()
    if (!trimmed || disabled) return
    onSend(trimmed)
    setText('')
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  const canSend = !disabled && text.trim().length > 0

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'flex-end',
        gap: '8px',
        padding: '12px 14px',
        borderTop: '1px solid #f1f5f9',
        background: '#fff',
      }}
    >
      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        placeholder="Type your message..."
        rows={1}
        style={{
          flex: 1,
          resize: 'none',
          border: '1px solid #e2e8f0',
          borderRadius: '10px',
          padding: '9px 12px',
          fontSize: '14px',
          outline: 'none',
          fontFamily: 'inherit',
          color: '#1e293b',
          background: '#fff',
          lineHeight: '1.4',
        }}
      />
      <button
        onClick={handleSend}
        disabled={!canSend}
        aria-label="Send message"
        style={{
          width: '38px',
          height: '38px',
          borderRadius: '10px',
          background: canSend ? color : '#fff',
          border: canSend ? 'none' : '1px solid #e2e8f0',
          cursor: canSend ? 'pointer' : 'default',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
          transition: 'background 0.15s ease, border-color 0.15s ease',
        }}
      >
        <ArrowRightIcon active={canSend} />
      </button>
    </div>
  )
}
