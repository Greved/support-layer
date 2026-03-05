import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { config as configApi } from '@/lib/api';
import type { BotConfig } from '@/types';
import { MessageCircle } from 'lucide-react';

const MODELS = [
  { value: 'gpt-4o', label: 'GPT-4o' },
  { value: 'claude-sonnet-4-6', label: 'Claude Sonnet 4.6' },
  { value: 'gemini-flash', label: 'Gemini Flash' },
];

type Tab = 'behavior' | 'appearance';

export default function ConfigPage() {
  const [tab, setTab] = useState<Tab>('behavior');
  const [form, setForm] = useState<BotConfig>({
    systemPrompt: '',
    model: 'gpt-4o',
    temperature: 0.7,
    maxTokens: 1024,
    widgetTitle: 'Chat with us',
    widgetColor: '#2563eb',
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useEffect(() => {
    const fetchConfig = async () => {
      try {
        const res = await configApi.get();
        setForm(res.data);
      } catch {
        setError('Failed to load configuration.');
      } finally {
        setLoading(false);
      }
    };
    fetchConfig();
  }, []);

  const handleSave = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      await configApi.update(form);
      setSuccess('Configuration saved successfully.');
      setTimeout(() => setSuccess(''), 3000);
    } catch {
      setError('Failed to save configuration.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-xl font-semibold text-gray-900">Configuration</h1>

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
        {(['behavior', 'appearance'] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors capitalize ${
              tab === t
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      <form onSubmit={handleSave}>
        <div className="flex gap-6">
          {/* Left panel: form fields */}
          <div className="flex-1 space-y-5">
            {tab === 'behavior' && (
              <>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    System Prompt
                  </label>
                  <textarea
                    value={form.systemPrompt}
                    onChange={(e) => setForm({ ...form, systemPrompt: e.target.value })}
                    rows={6}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 resize-y"
                    placeholder="You are a helpful support assistant..."
                  />
                  <p className="text-xs text-gray-400 mt-1">
                    Instructions for how the bot should behave.
                  </p>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Model
                  </label>
                  <select
                    value={form.model}
                    onChange={(e) => setForm({ ...form, model: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white"
                  >
                    {MODELS.map((m) => (
                      <option key={m.value} value={m.value}>
                        {m.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Temperature:{' '}
                    <span className="text-blue-600 font-semibold">{form.temperature}</span>
                  </label>
                  <input
                    type="range"
                    min={0}
                    max={1}
                    step={0.1}
                    value={form.temperature}
                    onChange={(e) => setForm({ ...form, temperature: parseFloat(e.target.value) })}
                    className="w-full accent-blue-600"
                  />
                  <div className="flex justify-between text-xs text-gray-400 mt-1">
                    <span>Precise (0)</span>
                    <span>Creative (1)</span>
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Max Tokens
                  </label>
                  <input
                    type="number"
                    min={64}
                    max={8192}
                    value={form.maxTokens}
                    onChange={(e) => setForm({ ...form, maxTokens: parseInt(e.target.value) || 1024 })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  />
                </div>
              </>
            )}

            {tab === 'appearance' && (
              <>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Widget Title
                  </label>
                  <input
                    type="text"
                    value={form.widgetTitle}
                    onChange={(e) => setForm({ ...form, widgetTitle: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    placeholder="Chat with us"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Primary Color
                  </label>
                  <div className="flex items-center gap-3">
                    <input
                      type="color"
                      value={form.widgetColor}
                      onChange={(e) => setForm({ ...form, widgetColor: e.target.value })}
                      className="h-10 w-16 rounded-md border border-gray-300 cursor-pointer p-0.5"
                    />
                    <span className="text-sm text-gray-600 font-mono">{form.widgetColor}</span>
                  </div>
                </div>
              </>
            )}

            <div className="pt-2">
              <button
                type="submit"
                disabled={saving}
                className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
              >
                {saving ? 'Saving...' : 'Save Configuration'}
              </button>
            </div>
          </div>

          {/* Right panel: preview (Appearance tab only) */}
          {tab === 'appearance' && (
            <div className="w-72 shrink-0">
              <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-5 sticky top-6">
                <p className="text-xs font-semibold text-gray-500 uppercase mb-4">Widget Preview</p>
                <div className="relative h-48 bg-gray-50 rounded-md border border-dashed border-gray-200 flex items-end justify-end p-4">
                  <span className="text-xs text-gray-400 absolute inset-0 flex items-center justify-center">
                    Your website content
                  </span>
                  {/* Widget launcher button preview */}
                  <div className="relative z-10">
                    <button
                      type="button"
                      style={{ backgroundColor: form.widgetColor }}
                      className="flex items-center gap-2 px-4 py-2.5 rounded-full text-white text-sm font-medium shadow-lg"
                    >
                      <MessageCircle size={16} />
                      {form.widgetTitle || 'Chat with us'}
                    </button>
                  </div>
                </div>
                <p className="text-xs text-gray-400 mt-2 text-center">Launcher button preview</p>
              </div>
            </div>
          )}
        </div>
      </form>
    </div>
  );
}
