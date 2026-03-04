import { useState, useCallback, useRef } from 'react'
import type { Message, Source } from '../types'

const SESSION_KEY = 'sl_session_id'

export function useChat(apiBase: string, apiKey: string) {
  const [messages, setMessages] = useState<Message[]>([])
  const [loading, setLoading] = useState(false)
  const sessionIdRef = useRef<string | null>(localStorage.getItem(SESSION_KEY))

  const sendMessage = useCallback(
    async (query: string) => {
      const userMsg: Message = {
        id: crypto.randomUUID(),
        role: 'user',
        content: query,
        createdAt: Date.now(),
      }
      setMessages((prev) => [...prev, userMsg])
      setLoading(true)

      // Placeholder for the assistant streaming message
      const assistantId = crypto.randomUUID()
      setMessages((prev) => [
        ...prev,
        { id: assistantId, role: 'assistant', content: '', streaming: true, createdAt: Date.now() },
      ])

      try {
        const response = await fetch(`${apiBase}/v1/chat/stream`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${apiKey}`,
          },
          body: JSON.stringify({
            query,
            session_id: sessionIdRef.current ?? undefined,
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
                setMessages((prev) =>
                  prev.map((m) =>
                    m.id === assistantId
                      ? { ...m, content: fullAnswer, streaming: true }
                      : m
                  )
                )
              } else if (parsed.type === 'done') {
                const answer = parsed.answer ?? fullAnswer
                setMessages((prev) =>
                  prev.map((m) =>
                    m.id === assistantId
                      ? { ...m, content: answer, sources, streaming: false }
                      : m
                  )
                )
              }
            } catch {
              // ignore malformed JSON lines
            }
          }
        }
      } catch (err) {
        setMessages((prev) =>
          prev.map((m) =>
            m.id === assistantId
              ? {
                  ...m,
                  content: 'Sorry, something went wrong. Please try again.',
                  streaming: false,
                }
              : m
          )
        )
      } finally {
        setLoading(false)
        // Finalize any still-streaming message
        setMessages((prev) =>
          prev.map((m) => (m.streaming ? { ...m, streaming: false } : m))
        )
      }
    },
    [apiBase, apiKey]
  )

  return { messages, loading, sendMessage }
}
