import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { dashboard, onboarding } from '@/lib/api';
import type { DashboardUsage, OnboardingState } from '@/types';
import { MessageSquare, FileText, Users, Zap } from 'lucide-react';

// Mock daily query data for the chart
const mockChartData = [
  { day: 'Mon', queries: 42 },
  { day: 'Tue', queries: 67 },
  { day: 'Wed', queries: 53 },
  { day: 'Thu', queries: 89 },
  { day: 'Fri', queries: 74 },
  { day: 'Sat', queries: 31 },
  { day: 'Sun', queries: 25 },
];

function ProgressBar({ value, max, label }: { value: number; max: number; label: string }) {
  const pct = max > 0 ? Math.min((value / max) * 100, 100) : 0;
  const color = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-blue-500';

  return (
    <div>
      <div className="flex justify-between text-xs text-gray-500 mb-1">
        <span>{label}</span>
        <span>
          {value} / {max}
        </span>
      </div>
      <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
        <div className={`h-full ${color} rounded-full transition-all`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

export default function DashboardPage() {
  const [usage, setUsage] = useState<DashboardUsage | null>(null);
  const [onboardingState, setOnboardingState] = useState<OnboardingState | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [usageRes, onboardingRes] = await Promise.all([
          dashboard.getUsage(),
          onboarding.getState(),
        ]);
        setUsage(usageRes.data);
        setOnboardingState(onboardingRes.data);
      } catch {
        setError('Failed to load dashboard data.');
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <div className="px-4 py-3 rounded-md bg-red-50 border border-red-200 text-red-700 text-sm">
          {error}
        </div>
      </div>
    );
  }

  const statCards = usage
    ? [
        {
          label: 'Queries This Month',
          value: usage.queriesThisMonth.toLocaleString(),
          icon: <MessageSquare size={20} className="text-blue-600" />,
          bg: 'bg-blue-50',
        },
        {
          label: 'Documents',
          value: usage.documentCount.toLocaleString(),
          icon: <FileText size={20} className="text-green-600" />,
          bg: 'bg-green-50',
        },
        {
          label: 'Team Members',
          value: usage.teamMemberCount.toLocaleString(),
          icon: <Users size={20} className="text-purple-600" />,
          bg: 'bg-purple-50',
        },
        {
          label: 'Max File Size',
          value: `${usage.planLimits.maxFileSizeMb} MB`,
          icon: <Zap size={20} className="text-amber-600" />,
          bg: 'bg-amber-50',
        },
      ]
    : [];

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
      </div>

      {/* Onboarding banner */}
      {onboardingState && !onboardingState.isComplete && (
        <Link
          to="/onboarding"
          className="flex items-center justify-between px-5 py-4 bg-blue-50 border border-blue-200 rounded-lg hover:bg-blue-100 transition-colors"
        >
          <div>
            <p className="text-sm font-medium text-blue-800">Complete your setup</p>
            <p className="text-xs text-blue-600 mt-0.5">
              {onboardingState.completedSteps.length} of 4 steps completed
            </p>
          </div>
          <span className="text-blue-600 text-sm font-medium">Continue setup →</span>
        </Link>
      )}

      {/* Stat cards */}
      {usage && (
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          {statCards.map((card) => (
            <div key={card.label} className="bg-white rounded-lg border border-gray-200 p-5 shadow-sm">
              <div className={`inline-flex p-2 rounded-md ${card.bg} mb-3`}>{card.icon}</div>
              <p className="text-2xl font-bold text-gray-900">{card.value}</p>
              <p className="text-sm text-gray-500 mt-1">{card.label}</p>
            </div>
          ))}
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Usage limits */}
        {usage && (
          <div className="bg-white rounded-lg border border-gray-200 p-6 shadow-sm">
            <h2 className="text-sm font-semibold text-gray-700 mb-4">Plan Usage</h2>
            <div className="space-y-4">
              <ProgressBar
                label="Documents"
                value={usage.documentCount}
                max={usage.planLimits.maxDocuments}
              />
              <ProgressBar
                label="Queries / Month"
                value={usage.queriesThisMonth}
                max={usage.planLimits.maxQueriesPerMonth}
              />
              <ProgressBar
                label="Team Members"
                value={usage.teamMemberCount}
                max={usage.planLimits.maxTeamMembers}
              />
            </div>
          </div>
        )}

        {/* Chart */}
        <div className="bg-white rounded-lg border border-gray-200 p-6 shadow-sm">
          <h2 className="text-sm font-semibold text-gray-700 mb-4">Queries (Last 7 Days)</h2>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={mockChartData} barSize={24}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis dataKey="day" tick={{ fontSize: 12 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fontSize: 12 }} axisLine={false} tickLine={false} />
              <Tooltip
                contentStyle={{
                  borderRadius: 6,
                  border: '1px solid #e5e7eb',
                  fontSize: 12,
                }}
              />
              <Bar dataKey="queries" fill="#2563eb" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}
