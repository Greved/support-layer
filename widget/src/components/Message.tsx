import { useState } from 'react'
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
  onFeedback: (messageId: string, rating: 'up' | 'down', comment?: string) => Promise<boolean>
}

export function Message({ message, onFeedback }: Props) {
  const [downvoteOpen, setDownvoteOpen] = useState(false)
  const [downvoteComment, setDownvoteComment] = useState('')

  const isUser = message.role === 'user'
  const feedback = message.feedback
  const feedbackRating = feedback?.rating ?? null
  const feedbackSubmitting = feedback?.submitting ?? false

  const showFeedback =
    !isUser && !message.streaming && !message.feedbackDisabled && typeof onFeedback === 'function'

  const handleUpvote = async () => {
    if (feedbackSubmitting) return
    setDownvoteOpen(false)
    await onFeedback(message.id, 'up')
  }

  const handleDownvoteClick = () => {
    if (feedbackSubmitting) return
    setDownvoteOpen((open) => !open)
  }

  const handleDownvoteSubmit = async () => {
    if (feedbackSubmitting) return
    const success = await onFeedback(message.id, 'down', downvoteComment)
    if (success) {
      setDownvoteComment('')
      setDownvoteOpen(false)
    }
  }

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: isUser ? 'flex-end' : 'flex-start',
        marginBottom: '12px',
      }}
    >
      <div style={{ maxWidth: '78%', width: '100%' }}>
        <div
          style={{
            width: 'fit-content',
            maxWidth: '100%',
            marginLeft: isUser ? 'auto' : 0,
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

        {showFeedback && (
          <div style={{ marginTop: '6px' }}>
            <div
              style={{
                display: 'flex',
                justifyContent: 'flex-end',
                alignItems: 'center',
                gap: '6px',
              }}
            >
              <button
                type="button"
                onClick={() => void handleUpvote()}
                disabled={feedbackSubmitting}
                data-testid="widget-feedback-up"
                aria-label="Thumbs up"
                style={{
                  border: 'none',
                  background: feedbackRating === 'up' ? '#dcfce7' : 'transparent',
                  color: feedbackRating === 'up' ? '#166534' : '#64748b',
                  fontSize: '14px',
                  cursor: feedbackSubmitting ? 'default' : 'pointer',
                  padding: '2px 6px',
                  borderRadius: '999px',
                  opacity: feedbackSubmitting ? 0.65 : 1,
                }}
              >
                ??
              </button>
              <button
                type="button"
                onClick={handleDownvoteClick}
                disabled={feedbackSubmitting}
                data-testid="widget-feedback-down"
                aria-label="Thumbs down"
                style={{
                  border: 'none',
                  background: feedbackRating === 'down' || downvoteOpen ? '#fee2e2' : 'transparent',
                  color: feedbackRating === 'down' || downvoteOpen ? '#991b1b' : '#64748b',
                  fontSize: '14px',
                  cursor: feedbackSubmitting ? 'default' : 'pointer',
                  padding: '2px 6px',
                  borderRadius: '999px',
                  opacity: feedbackSubmitting ? 0.65 : 1,
                }}
              >
                ??
              </button>
              {feedback?.submitted && !feedbackSubmitting && (
                <span style={{ fontSize: '11px', color: '#10b981', fontWeight: 600 }}>Thanks</span>
              )}
            </div>

            {downvoteOpen && (
              <div
                data-testid="widget-feedback-down-editor"
                style={{
                  marginTop: '6px',
                  border: '1px solid #e2e8f0',
                  borderRadius: '8px',
                  padding: '8px',
                  background: '#ffffff',
                }}
              >
                <textarea
                  value={downvoteComment}
                  onChange={(e) => setDownvoteComment(e.target.value)}
                  placeholder="Optional: tell us what was wrong"
                  maxLength={2000}
                  rows={2}
                  style={{
                    width: '100%',
                    resize: 'vertical',
                    border: '1px solid #e2e8f0',
                    borderRadius: '6px',
                    padding: '6px 8px',
                    fontSize: '12px',
                    color: '#1f2937',
                    boxSizing: 'border-box',
                  }}
                />
                <div
                  style={{
                    marginTop: '6px',
                    display: 'flex',
                    justifyContent: 'flex-end',
                    gap: '6px',
                  }}
                >
                  <button
                    type="button"
                    onClick={() => setDownvoteOpen(false)}
                    disabled={feedbackSubmitting}
                    style={{
                      border: '1px solid #d1d5db',
                      background: '#fff',
                      borderRadius: '6px',
                      padding: '4px 8px',
                      fontSize: '11px',
                      color: '#374151',
                      cursor: 'pointer',
                    }}
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    onClick={() => void handleDownvoteSubmit()}
                    disabled={feedbackSubmitting}
                    data-testid="widget-feedback-down-submit"
                    style={{
                      border: 'none',
                      background: '#111827',
                      borderRadius: '6px',
                      padding: '4px 8px',
                      fontSize: '11px',
                      color: '#fff',
                      cursor: 'pointer',
                      opacity: feedbackSubmitting ? 0.7 : 1,
                    }}
                  >
                    {feedbackSubmitting ? 'Sending...' : 'Submit'}
                  </button>
                </div>
              </div>
            )}

            {feedback?.error && (
              <div style={{ marginTop: '4px', fontSize: '11px', color: '#b91c1c', textAlign: 'right' }}>
                {feedback.error}
              </div>
            )}
          </div>
        )}
      </div>

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
          <span style={{ fontSize: '11px', color: '#94a3b8' }}>{formatTime(message.createdAt)}</span>
        </div>
      )}
    </div>
  )
}
