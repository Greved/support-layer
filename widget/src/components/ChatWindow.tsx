import { useState } from 'react'
import type { Message, Source } from '../types'
import { MessageList } from './MessageList'
import { InputBar } from './InputBar'

function LightningIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="white">
      <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
    </svg>
  )
}

function DocumentIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#64748b" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
      <polyline points="14 2 14 8 20 8" />
    </svg>
  )
}

function PdfBadge() {
  return (
    <div
      style={{
        width: '32px',
        height: '38px',
        background: '#fee2e2',
        borderRadius: '4px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
      }}
    >
      <span style={{ fontSize: '8px', fontWeight: 700, color: '#ef4444', letterSpacing: '0.5px' }}>
        PDF
      </span>
    </div>
  )
}

function SourceOverlay({ sources, onDismiss }: { sources: Source[]; onDismiss: () => void }) {
  const source = sources[0]
  return (
    <div
      style={{
        position: 'absolute',
        bottom: 0,
        left: 0,
        right: 0,
        background: '#fff',
        borderTop: '1px solid #e2e8f0',
        boxShadow: '0 -4px 16px rgba(0,0,0,0.08)',
        zIndex: 10,
      }}
    >
      {/* Card header */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          padding: '10px 14px 8px',
          borderBottom: '1px solid #f1f5f9',
        }}
      >
        <DocumentIcon />
        <span style={{ fontSize: '12px', fontWeight: 600, color: '#475569', flex: 1 }}>
          Source Context
        </span>
        <button
          onClick={onDismiss}
          aria-label="Dismiss source"
          style={{
            background: 'none',
            border: 'none',
            cursor: 'pointer',
            color: '#94a3b8',
            fontSize: '18px',
            lineHeight: 1,
            padding: '0 2px',
          }}
        >
          ×
        </button>
      </div>

      {/* Source item */}
      <div style={{ padding: '10px 14px 12px' }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: '10px', marginBottom: '8px' }}>
          <PdfBadge />
          <div style={{ minWidth: 0 }}>
            <div
              style={{
                fontSize: '13px',
                fontWeight: 700,
                color: '#0f172a',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              {source.file}
            </div>
            {(source.page != null || source.offset != null) && (
              <div style={{ fontSize: '11px', color: '#94a3b8', marginTop: '2px' }}>
                {source.page != null && `Page ${source.page}`}
                {source.page != null && source.offset != null && ' • '}
                {source.offset != null && `Line ${source.offset}`}
              </div>
            )}
          </div>
        </div>

        {source.brief_content && (
          <blockquote
            style={{
              margin: 0,
              paddingLeft: '10px',
              borderLeft: '3px solid #2563eb',
              color: '#475569',
              fontSize: '12px',
              lineHeight: 1.5,
              fontStyle: 'normal',
            }}
          >
            {source.brief_content}
          </blockquote>
        )}
      </div>
    </div>
  )
}

interface Props {
  title: string
  color: string
  messages: Message[]
  loading: boolean
  onSend: (text: string) => void
  onClose: () => void
}

export function ChatWindow({ title, color, messages, loading, onSend, onClose }: Props) {
  const [dismissedIds, setDismissedIds] = useState<Set<string>>(new Set())

  // Find the most recent non-streaming assistant message with sources that hasn't been dismissed
  const activeSourceMessage = [...messages]
    .reverse()
    .find(
      (m) =>
        m.role === 'assistant' &&
        !m.streaming &&
        m.sources &&
        m.sources.length > 0 &&
        !dismissedIds.has(m.id)
    )

  return (
    <div
      style={{
        width: '360px',
        height: '540px',
        borderRadius: '16px',
        boxShadow: '0 20px 60px rgba(0,0,0,0.18)',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        background: '#fff',
        fontFamily: 'system-ui, sans-serif',
        border: '1px solid #e2e8f0',
      }}
    >
      {/* Header */}
      <div
        style={{
          background: '#fff',
          padding: '12px 16px',
          display: 'flex',
          alignItems: 'center',
          gap: '10px',
          borderBottom: '1px solid #f1f5f9',
          flexShrink: 0,
        }}
      >
        <div
          style={{
            width: '36px',
            height: '36px',
            borderRadius: '8px',
            background: color,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}
        >
          <LightningIcon />
        </div>

        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 700, fontSize: '15px', color: '#0f172a', lineHeight: 1.2 }}>
            {title}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '4px', marginTop: '2px' }}>
            <span
              style={{
                width: '7px',
                height: '7px',
                borderRadius: '50%',
                background: '#22c55e',
                display: 'inline-block',
              }}
            />
            <span
              style={{ fontSize: '11px', color: '#22c55e', fontWeight: 600, letterSpacing: '0.04em' }}
            >
              ONLINE
            </span>
          </div>
        </div>

        <button
          onClick={onClose}
          aria-label="Close chat"
          style={{
            background: 'none',
            border: 'none',
            cursor: 'pointer',
            color: '#94a3b8',
            fontSize: '20px',
            lineHeight: 1,
            padding: '4px',
            letterSpacing: '2px',
          }}
        >
          ···
        </button>
      </div>

      {/* Message area with source overlay */}
      <div style={{ flex: 1, position: 'relative', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
        <MessageList messages={messages} />

        {activeSourceMessage && (
          <SourceOverlay
            sources={activeSourceMessage.sources!}
            onDismiss={() =>
              setDismissedIds((prev) => new Set(prev).add(activeSourceMessage.id))
            }
          />
        )}
      </div>

      <InputBar onSend={onSend} disabled={loading} color={color} />
    </div>
  )
}
