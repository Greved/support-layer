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
