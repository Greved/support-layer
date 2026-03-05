import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { settings as settingsApi } from '@/lib/api';
import type { NotificationPref } from '@/types';
import { useAuthStore } from '@/stores/authStore';
import { ShieldCheck } from 'lucide-react';

type Tab = 'general' | 'notifications';

const EVENT_LABELS: Record<string, string> = {
  'ingestion.complete': 'Document ingestion completed',
  'ingestion.error': 'Document ingestion failed',
  'quota.80': 'Usage reached 80% of plan limit',
  'quota.100': 'Usage reached 100% of plan limit',
};

export default function SettingsPage() {
  const user = useAuthStore((s) => s.user);
  const [tab, setTab] = useState<Tab>('general');
  const [prefs, setPrefs] = useState<NotificationPref[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useEffect(() => {
    const fetchPrefs = async () => {
      try {
        const res = await settingsApi.getNotifications();
        // Ensure all known event types are present
        const knownTypes = Object.keys(EVENT_LABELS);
        const loaded = res.data;
        const merged = knownTypes.map((eventType) => {
          const existing = loaded.find((p) => p.eventType === eventType);
          return existing || { eventType, emailEnabled: false, inAppEnabled: false };
        });
        setPrefs(merged);
      } catch {
        setError('Failed to load notification preferences.');
      } finally {
        setLoading(false);
      }
    };
    fetchPrefs();
  }, []);

  const togglePref = (eventType: string, field: 'emailEnabled' | 'inAppEnabled') => {
    setPrefs((prev) =>
      prev.map((p) =>
        p.eventType === eventType ? { ...p, [field]: !p[field] } : p
      )
    );
  };

  const handleSave = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      await settingsApi.updateNotifications(prefs);
      setSuccess('Notification preferences saved.');
      setTimeout(() => setSuccess(''), 3000);
    } catch {
      setError('Failed to save preferences.');
    } finally {
      setSaving(false);
    }
  };

  const tabs: { key: Tab; label: string }[] = [
    { key: 'general', label: 'General' },
    { key: 'notifications', label: 'Notifications' },
  ];

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-xl font-semibold text-gray-900">Settings</h1>

      {error && (
        <div className="px-4 py-3 rounded-md bg-red-50 border border-red-200 text-red-700 text-sm">
          {error}
        </div>
      )}
      {success && (
        <div className="px-4 py-3 rounded-md bg-green-50 border border-green-200 text-green-700 text-sm">
          {success}
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-1 border-b border-gray-200">
        {tabs.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              tab === t.key
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'general' && (
        <div className="space-y-5 max-w-lg">
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-5">
            <h2 className="text-sm font-semibold text-gray-700 mb-4">Account Information</h2>
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wide">
                  Email
                </label>
                <p className="text-sm text-gray-900">{user?.email || '—'}</p>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wide">
                  Role
                </label>
                <p className="text-sm text-gray-900 capitalize">{user?.role || '—'}</p>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1 uppercase tracking-wide">
                  Tenant ID
                </label>
                <p className="text-sm text-gray-600 font-mono">{user?.tenantId || '—'}</p>
              </div>
            </div>
          </div>

          <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-5">
            <h2 className="text-sm font-semibold text-gray-700 mb-1">Two-Factor Authentication</h2>
            <p className="text-sm text-gray-500 mb-3">
              Protect your account with an authenticator app.
            </p>
            <Link
              to="/settings/mfa"
              className="inline-flex items-center gap-1.5 px-3 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
            >
              <ShieldCheck size={14} />
              Set up 2FA
            </Link>
          </div>
        </div>
      )}

      {tab === 'notifications' && (
        <div className="max-w-2xl">
          {loading ? (
            <div className="flex items-center justify-center py-12">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
            </div>
          ) : (
            <form onSubmit={handleSave}>
              <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden mb-4">
                <div className="grid grid-cols-[1fr,auto,auto] gap-0">
                  {/* Header */}
                  <div className="px-5 py-3 border-b border-gray-200 bg-gray-50">
                    <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Event</span>
                  </div>
                  <div className="px-5 py-3 border-b border-gray-200 bg-gray-50 text-center">
                    <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Email</span>
                  </div>
                  <div className="px-5 py-3 border-b border-gray-200 bg-gray-50 text-center">
                    <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">In-App</span>
                  </div>

                  {/* Rows */}
                  {prefs.map((pref, idx) => (
                    <>
                      <div
                        key={`label-${pref.eventType}`}
                        className={`px-5 py-4 flex items-center ${idx < prefs.length - 1 ? 'border-b border-gray-100' : ''}`}
                      >
                        <div>
                          <p className="text-sm font-medium text-gray-900">
                            {EVENT_LABELS[pref.eventType] || pref.eventType}
                          </p>
                          <p className="text-xs text-gray-400 font-mono">{pref.eventType}</p>
                        </div>
                      </div>
                      <div
                        key={`email-${pref.eventType}`}
                        className={`px-5 py-4 flex items-center justify-center ${idx < prefs.length - 1 ? 'border-b border-gray-100' : ''}`}
                      >
                        <button
                          type="button"
                          onClick={() => togglePref(pref.eventType, 'emailEnabled')}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${
                            pref.emailEnabled ? 'bg-blue-600' : 'bg-gray-200'
                          }`}
                          role="switch"
                          aria-checked={pref.emailEnabled}
                        >
                          <span
                            className={`pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transform transition-transform ${
                              pref.emailEnabled ? 'translate-x-4' : 'translate-x-0'
                            }`}
                          />
                        </button>
                      </div>
                      <div
                        key={`inapp-${pref.eventType}`}
                        className={`px-5 py-4 flex items-center justify-center ${idx < prefs.length - 1 ? 'border-b border-gray-100' : ''}`}
                      >
                        <button
                          type="button"
                          onClick={() => togglePref(pref.eventType, 'inAppEnabled')}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${
                            pref.inAppEnabled ? 'bg-blue-600' : 'bg-gray-200'
                          }`}
                          role="switch"
                          aria-checked={pref.inAppEnabled}
                        >
                          <span
                            className={`pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transform transition-transform ${
                              pref.inAppEnabled ? 'translate-x-4' : 'translate-x-0'
                            }`}
                          />
                        </button>
                      </div>
                    </>
                  ))}
                </div>
              </div>

              <button
                type="submit"
                disabled={saving}
                className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
              >
                {saving ? 'Saving...' : 'Save Notifications'}
              </button>
            </form>
          )}
        </div>
      )}
    </div>
  );
}
