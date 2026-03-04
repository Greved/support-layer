import { useState, type KeyboardEvent } from 'react'

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

  return (
    <div
      style={{
        display: 'flex',
        gap: '8px',
        padding: '12px',
        borderTop: '1px solid #e2e8f0',
        background: '#fff',
      }}
    >
      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        placeholder="Type a message…"
        rows={1}
        style={{
          flex: 1,
          resize: 'none',
          border: '1px solid #e2e8f0',
          borderRadius: '8px',
          padding: '8px 12px',
          fontSize: '14px',
          outline: 'none',
          fontFamily: 'inherit',
        }}
      />
      <button
        onClick={handleSend}
        disabled={disabled || !text.trim()}
        style={{
          background: color,
          color: '#fff',
          border: 'none',
          borderRadius: '8px',
          padding: '8px 16px',
          cursor: disabled ? 'not-allowed' : 'pointer',
          opacity: disabled || !text.trim() ? 0.6 : 1,
          fontSize: '14px',
          fontWeight: 600,
          flexShrink: 0,
        }}
      >
        Send
      </button>
    </div>
  )
}
