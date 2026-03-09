export interface Source {
  file: string
  page: number | null
  offset: number | null
  relevance_score: number | null
  brief_content: string
}

export interface Message {
  id: string
  role: 'user' | 'assistant'
  content: string
  sources?: Source[]
  streaming?: boolean
  createdAt?: number
  serverId?: string
  feedbackDisabled?: boolean
  feedback?: MessageFeedback
}

export interface MessageFeedback {
  rating: 'up' | 'down' | null
  submitting: boolean
  submitted: boolean
  error: string | null
}

export interface WidgetConfig {
  apiKey: string
  apiBase: string
  title: string
  color: string
  position: 'bottom-right' | 'bottom-left' | 'inline'
  autoOpenDelaySeconds?: number
}
