export interface User {
  id: string;
  email: string;
  role: string;
  tenantId: string;
}

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
}

export interface Document {
  id: string;
  fileName: string;
  status: 'pending' | 'processing' | 'ready' | 'error';
  chunkCount: number;
  sizeBytes: number;
  createdAt: string;
  contentType: string;
}

export interface BotConfig {
  systemPrompt: string;
  model: string;
  temperature: number;
  maxTokens: number;
  widgetTitle: string;
  widgetColor: string;
}

export interface ApiKey {
  id: string;
  name: string;
  keyPreview: string;
  isActive: boolean;
  createdAt: string;
}

export interface TeamMember {
  id: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
}

export interface DashboardUsage {
  queriesThisMonth: number;
  documentCount: number;
  teamMemberCount: number;
  planLimits: {
    maxDocuments: number;
    maxQueriesPerMonth: number;
    maxTeamMembers: number;
    maxFileSizeMb: number;
  };
}

export interface NotificationPref {
  eventType: string;
  emailEnabled: boolean;
  inAppEnabled: boolean;
}

export interface OnboardingState {
  completedSteps: number[];
  isComplete: boolean;
}

export interface EvalMetricsSnapshot {
  faithfulness: number | null;
  answerRelevancy: number | null;
  contextPrecision: number | null;
  contextRecall: number | null;
  hallucinationScore: number | null;
  answerCompleteness: number | null;
  avgLatencyMs: number | null;
}

export interface PortalEvalSummary {
  currentScores: EvalMetricsSnapshot | null;
  previousScores: EvalMetricsSnapshot | null;
  currentRunId: string | null;
  currentRunFinishedAt: string | null;
  currentRunResultCount: number;
}

export interface PortalEvalRunItem {
  runId: string;
  runType: string;
  triggeredBy: string;
  status: string;
  startedAt: string;
  finishedAt: string | null;
  resultCount: number;
  metrics: EvalMetricsSnapshot;
  configSnapshotJson?: string | null;
}

export interface PortalEvalResultItem {
  resultId: string;
  datasetItemId: string | null;
  question: string;
  groundTruth: string;
  answer: string;
  faithfulness: number | null;
  answerRelevancy: number | null;
  contextPrecision: number | null;
  contextRecall: number | null;
  hallucinationScore: number | null;
  answerCompleteness: number | null;
  latencyMs: number;
  retrievedChunksJson?: string | null;
  contextSnapshotJson?: string | null;
}

export interface PortalEvalRunDetail {
  run: PortalEvalRunItem;
  results: PortalEvalResultItem[];
}

export interface PortalEvalRunAcceptedResponse {
  runId: string;
  status: string;
}

export interface PagedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
