import { useCallback, useEffect, useRef, useState } from 'react'
import type { Message, Source } from '../types'

const SESSION_KEY = 'sl_session_id'

function readStoredSessionId(): string | null {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    return window.localStorage.getItem(SESSION_KEY)
  } catch {
    return null
  }
}

function writeStoredSessionId(sessionId: string): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.setItem(SESSION_KEY, sessionId)
  } catch {
    // Ignore storage access errors (e.g. restricted browser contexts in tests).
  }
}

function generateClientMessageId(): string {
  if (typeof globalThis.crypto !== 'undefined' && typeof globalThis.crypto.randomUUID === 'function') {
    return globalThis.crypto.randomUUID()
  }

  return `m_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 10)}`
}

export function useChat(apiBase: string, apiKey: string) {
  const [messages, setMessages] = useState<Message[]>([])
  const [loading, setLoading] = useState(false)
  const sessionIdRef = useRef<string | null>(readStoredSessionId())
  const messagesRef = useRef<Message[]>([])

  useEffect(() => {
    messagesRef.current = messages
  }, [messages])

  const setSessionId = useCallback((sessionId: string) => {
    sessionIdRef.current = sessionId
    writeStoredSessionId(sessionId)
  }, [])

  const ensureSessionId = useCallback(async (): Promise<string> => {
    if (sessionIdRef.current) {
      return sessionIdRef.current
    }

    const response = await fetch(`${apiBase}/v1/session`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
    })

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`)
    }

    const data = (await response.json()) as { id?: string }
    if (!data.id) {
      throw new Error('session_id_missing')
    }

    setSessionId(data.id)
    return data.id
  }, [apiBase, apiKey, setSessionId])

  const updateMessage = useCallback(
    (id: string, updater: (message: Message) => Message) => {
      setMessages((prev) => prev.map((message) => (message.id === id ? updater(message) : message)))
    },
    []
  )

  const sendMessage = useCallback(
    async (query: string) => {
      let ensuredSessionId: string | null = null
      const userMsg: Message = {
        id: generateClientMessageId(),
        role: 'user',
        content: query,
        createdAt: Date.now(),
      }
      setMessages((prev) => [...prev, userMsg])
      setLoading(true)

      // Placeholder for the assistant streaming message
      const assistantId = generateClientMessageId()
      setMessages((prev) => [
        ...prev,
        { id: assistantId, role: 'assistant', content: '', streaming: true, createdAt: Date.now() },
      ])

      try {
        ensuredSessionId = await ensureSessionId()
        const response = await fetch(`${apiBase}/v1/chat/stream`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${apiKey}`,
          },
          body: JSON.stringify({
            query,
            sessionId: ensuredSessionId,
          }),
        })

        if (!response.ok || !response.body) {
          throw new Error(`HTTP ${response.status}`)
        }

        const reader = response.body.getReader()
        const decoder = new TextDecoder()
        let buffer = ''
        let sources: Source[] = []
        let fullAnswer = ''

        while (true) {
          const { done, value } = await reader.read()
          if (done) break
          buffer += decoder.decode(value, { stream: true })

          const events = buffer.split('\n\n')
          buffer = events.pop() ?? ''

          for (const event of events) {
            const line = event.trim()
            if (!line.startsWith('data:')) continue
            const data = line.slice('data:'.length).trim()
            if (data === '[DONE]') break

            try {
              const parsed = JSON.parse(data)
              if (parsed.type === 'sources') {
                sources = parsed.sources ?? []
              } else if (parsed.type === 'token') {
                fullAnswer += parsed.chunk ?? ''
                updateMessage(assistantId, (m) => ({ ...m, content: fullAnswer, streaming: true }))
              } else if (parsed.type === 'done') {
                const answer = parsed.answer ?? fullAnswer
                const sessionIdFromPayload =
                  typeof parsed.session_id === 'string' ? parsed.session_id : null
                const messageIdFromPayload =
                  typeof parsed.message_id === 'string' ? parsed.message_id : null

                if (sessionIdFromPayload) {
                  setSessionId(sessionIdFromPayload)
                }

                updateMessage(assistantId, (m) => ({
                  ...m,
                  content: answer,
                  sources,
                  streaming: false,
                  serverId: messageIdFromPayload ?? m.serverId,
                  feedbackDisabled: false,
                }))
              }
            } catch {
              // ignore malformed JSON lines
            }
          }
        }
      } catch (err) {
        updateMessage(assistantId, (m) => ({
          ...m,
          content: 'Sorry, something went wrong. Please try again.',
          streaming: false,
          feedbackDisabled: true,
        }))
      } finally {
        setLoading(false)
        // Finalize any still-streaming message
        setMessages((prev) =>
          prev.map((m) => (m.streaming ? { ...m, streaming: false } : m))
        )
      }
    },
    [apiBase, apiKey, ensureSessionId, setSessionId, updateMessage]
  )

  const submitFeedback = useCallback(
    async (messageId: string, rating: 'up' | 'down', comment?: string): Promise<boolean> => {
      const target = messagesRef.current.find((m) => m.id === messageId)
      if (!target || target.role !== 'assistant') {
        return false
      }

      if (!target.serverId) {
        updateMessage(messageId, (m) => ({
          ...m,
          feedback: {
            rating: m.feedback?.rating ?? null,
            submitted: m.feedback?.submitted ?? false,
            submitting: false,
            error: 'Feedback is temporarily unavailable for this message.',
          },
        }))
        return false
      }

      updateMessage(messageId, (m) => ({
        ...m,
        feedback: {
          rating,
          submitted: m.feedback?.submitted ?? false,
          submitting: true,
          error: null,
        },
      }))

      try {
        const trimmedComment = comment?.trim()
        const response = await fetch(`${apiBase}/v1/feedback`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${apiKey}`,
          },
          body: JSON.stringify({
            messageId: target.serverId,
            rating,
            comment: trimmedComment && trimmedComment.length > 0 ? trimmedComment : undefined,
          }),
        })

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`)
        }

        updateMessage(messageId, (m) => ({
          ...m,
          feedback: {
            rating,
            submitted: true,
            submitting: false,
            error: null,
          },
        }))
        return true
      } catch {
        updateMessage(messageId, (m) => ({
          ...m,
          feedback: {
            rating: m.feedback?.rating ?? null,
            submitted: m.feedback?.submitted ?? false,
            submitting: false,
            error: 'Failed to submit feedback. Please try again.',
          },
        }))
        return false
      }
    },
    [apiBase, apiKey, updateMessage]
  )

  return { messages, loading, sendMessage, submitFeedback }
}
