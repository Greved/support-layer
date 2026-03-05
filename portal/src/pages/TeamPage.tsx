import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { team as teamApi, apiKeys as apiKeysApi } from '@/lib/api';
import type { TeamMember, ApiKey } from '@/types';
import { Trash2, Plus, X, Eye, EyeOff } from 'lucide-react';

function RoleBadge({ role }: { role: string }) {
  const colorMap: Record<string, string> = {
    admin: 'bg-purple-100 text-purple-700',
    owner: 'bg-blue-100 text-blue-700',
    member: 'bg-gray-100 text-gray-600',
    viewer: 'bg-green-100 text-green-700',
  };
  const cls = colorMap[role.toLowerCase()] || 'bg-gray-100 text-gray-600';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${cls}`}>
      {role}
    </span>
  );
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

// Create API Key Modal
interface CreateKeyModalProps {
  onClose: () => void;
  onCreated: (key: ApiKey & { key: string }) => void;
}

function CreateKeyModal({ onClose, onCreated }: CreateKeyModalProps) {
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    setLoading(true);
    setError('');
    try {
      const res = await apiKeysApi.create(name.trim());
      onCreated(res.data);
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        'Failed to create API key.';
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg border border-gray-200 shadow-xl w-full max-w-md p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-base font-semibold text-gray-900">Create API Key</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X size={18} />
          </button>
        </div>
        {error && (
          <div className="mb-3 px-3 py-2 rounded-md bg-red-50 border border-red-200 text-red-700 text-sm">
            {error}
          </div>
        )}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Key Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              placeholder="e.g. Production Widget"
              autoFocus
            />
          </div>
          <div className="flex gap-2 justify-end">
            <button
              type="button"
              onClick={onClose}
              className="px-3 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !name.trim()}
              className="px-3 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
            >
              {loading ? 'Creating...' : 'Create Key'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// New Key Display Modal
interface NewKeyDisplayProps {
  keyValue: string;
  onClose: () => void;
}

function NewKeyDisplay({ keyValue, onClose }: NewKeyDisplayProps) {
  const [visible, setVisible] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(keyValue);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg border border-gray-200 shadow-xl w-full max-w-md p-6">
        <h2 className="text-base font-semibold text-gray-900 mb-2">API Key Created</h2>
        <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-md px-3 py-2 mb-4">
          Copy this key now. You won't be able to see it again.
        </p>
        <div className="flex items-center gap-2 bg-gray-50 border border-gray-200 rounded-md px-3 py-2 mb-4">
          <code className="text-sm font-mono text-gray-800 flex-1 break-all">
            {visible ? keyValue : '•'.repeat(Math.min(keyValue.length, 32))}
          </code>
          <button
            onClick={() => setVisible((v) => !v)}
            className="text-gray-400 hover:text-gray-600 shrink-0"
          >
            {visible ? <EyeOff size={14} /> : <Eye size={14} />}
          </button>
        </div>
        <div className="flex gap-2">
          <button
            onClick={handleCopy}
            className="flex-1 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
          >
            {copied ? 'Copied!' : 'Copy Key'}
          </button>
          <button
            onClick={onClose}
            className="flex-1 py-2 text-sm border border-gray-300 text-gray-600 rounded-md hover:bg-gray-50 transition-colors"
          >
            Done
          </button>
        </div>
      </div>
    </div>
  );
}

// Invite Member Modal
interface InviteModalProps {
  onClose: () => void;
  onInvited: () => void;
}

function InviteModal({ onClose, onInvited }: InviteModalProps) {
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('member');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      await teamApi.invite(email.trim(), role);
      onInvited();
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        'Failed to send invite.';
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg border border-gray-200 shadow-xl w-full max-w-md p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-base font-semibold text-gray-900">Invite Team Member</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X size={18} />
          </button>
        </div>
        {error && (
          <div className="mb-3 px-3 py-2 rounded-md bg-red-50 border border-red-200 text-red-700 text-sm">
            {error}
          </div>
        )}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              placeholder="colleague@company.com"
              autoFocus
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Role</label>
            <select
              value={role}
              onChange={(e) => setRole(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white"
            >
              <option value="member">Member</option>
              <option value="admin">Admin</option>
              <option value="viewer">Viewer</option>
            </select>
          </div>
          <div className="flex gap-2 justify-end">
            <button
              type="button"
              onClick={onClose}
              className="px-3 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="px-3 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
            >
              {loading ? 'Inviting...' : 'Send Invite'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default function TeamPage() {
  const [members, setMembers] = useState<TeamMember[]>([]);
  const [keys, setKeys] = useState<ApiKey[]>([]);
  const [loadingMembers, setLoadingMembers] = useState(true);
  const [loadingKeys, setLoadingKeys] = useState(true);
  const [error, setError] = useState('');
  const [showCreateKey, setShowCreateKey] = useState(false);
  const [showInvite, setShowInvite] = useState(false);
  const [newKeyValue, setNewKeyValue] = useState<string | null>(null);
  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [removingId, setRemovingId] = useState<string | null>(null);

  const fetchMembers = async () => {
    try {
      const res = await teamApi.list();
      setMembers(res.data);
    } catch {
      setError('Failed to load team members.');
    } finally {
      setLoadingMembers(false);
    }
  };

  const fetchKeys = async () => {
    try {
      const res = await apiKeysApi.list();
      setKeys(res.data);
    } catch {
      setError('Failed to load API keys.');
    } finally {
      setLoadingKeys(false);
    }
  };

  useEffect(() => {
    fetchMembers();
    fetchKeys();
  }, []);

  const handleRemoveMember = async (id: string) => {
    if (!window.confirm('Remove this team member?')) return;
    setRemovingId(id);
    try {
      await teamApi.remove(id);
      setMembers((prev) => prev.filter((m) => m.id !== id));
    } catch {
      setError('Failed to remove member.');
    } finally {
      setRemovingId(null);
    }
  };

  const handleRevokeKey = async (id: string) => {
    if (!window.confirm('Revoke this API key? Any apps using it will stop working.')) return;
    setRevokingId(id);
    try {
      await apiKeysApi.revoke(id);
      setKeys((prev) => prev.map((k) => (k.id === id ? { ...k, isActive: false } : k)));
    } catch {
      setError('Failed to revoke key.');
    } finally {
      setRevokingId(null);
    }
  };

  const handleKeyCreated = (key: ApiKey & { key: string }) => {
    setKeys((prev) => [key, ...prev]);
    setShowCreateKey(false);
    setNewKeyValue(key.key);
  };

  return (
    <div className="p-6 space-y-8">
      <h1 className="text-xl font-semibold text-gray-900">Team</h1>

      {error && (
        <div className="px-4 py-3 rounded-md bg-red-50 border border-red-200 text-red-700 text-sm flex justify-between">
          <span>{error}</span>
          <button onClick={() => setError('')} className="text-red-400 hover:text-red-600">×</button>
        </div>
      )}

      {/* Team Members section */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold text-gray-700">Active Members</h2>
          <button
            onClick={() => setShowInvite(true)}
            className="flex items-center gap-1.5 px-3 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
          >
            <Plus size={14} />
            Invite Member
          </button>
        </div>

        {loadingMembers ? (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-blue-600" />
          </div>
        ) : members.length === 0 ? (
          <div className="text-center py-8 text-gray-500 text-sm bg-white rounded-lg border border-gray-200">
            No team members yet.
          </div>
        ) : (
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Email</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Role</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Joined</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {members.map((member) => (
                  <tr key={member.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 text-gray-900">{member.email}</td>
                    <td className="px-4 py-3">
                      <RoleBadge role={member.role} />
                    </td>
                    <td className="px-4 py-3 text-gray-600">{formatDate(member.createdAt)}</td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={() => handleRemoveMember(member.id)}
                        disabled={removingId === member.id}
                        className="inline-flex items-center gap-1 text-red-500 hover:text-red-700 disabled:opacity-40 text-xs font-medium transition-colors"
                      >
                        <Trash2 size={13} />
                        {removingId === member.id ? 'Removing...' : 'Remove'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* API Keys section */}
      <div id="api-keys">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold text-gray-700">API Keys</h2>
          <button
            onClick={() => setShowCreateKey(true)}
            className="flex items-center gap-1.5 px-3 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
          >
            <Plus size={14} />
            Create API Key
          </button>
        </div>

        {loadingKeys ? (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-blue-600" />
          </div>
        ) : keys.length === 0 ? (
          <div className="text-center py-8 text-gray-500 text-sm bg-white rounded-lg border border-gray-200">
            No API keys yet.
          </div>
        ) : (
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Name</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Key</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Created</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {keys.map((key) => (
                  <tr key={key.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900">{key.name}</td>
                    <td className="px-4 py-3">
                      <code className="text-xs font-mono text-gray-600 bg-gray-50 border border-gray-200 rounded px-1.5 py-0.5">
                        {key.keyPreview}
                      </code>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${
                          key.isActive
                            ? 'bg-green-100 text-green-700'
                            : 'bg-gray-100 text-gray-500'
                        }`}
                      >
                        {key.isActive ? 'Active' : 'Revoked'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-600">{formatDate(key.createdAt)}</td>
                    <td className="px-4 py-3 text-right">
                      {key.isActive && (
                        <button
                          onClick={() => handleRevokeKey(key.id)}
                          disabled={revokingId === key.id}
                          className="inline-flex items-center gap-1 text-red-500 hover:text-red-700 disabled:opacity-40 text-xs font-medium transition-colors"
                        >
                          <Trash2 size={13} />
                          {revokingId === key.id ? 'Revoking...' : 'Revoke'}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {showCreateKey && (
        <CreateKeyModal
          onClose={() => setShowCreateKey(false)}
          onCreated={handleKeyCreated}
        />
      )}

      {newKeyValue && (
        <NewKeyDisplay
          keyValue={newKeyValue}
          onClose={() => setNewKeyValue(null)}
        />
      )}

      {showInvite && (
        <InviteModal
          onClose={() => setShowInvite(false)}
          onInvited={() => {
            setShowInvite(false);
            fetchMembers();
          }}
        />
      )}
    </div>
  );
}
