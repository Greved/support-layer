import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { auth } from '@/lib/api';
import { ShieldCheck, Copy } from 'lucide-react';

interface EnrollData {
  secret: string;
  totpUri: string;
  backupCodes: string[];
}

export default function MfaEnrollPage() {
  const navigate = useNavigate();
  const [enrollData, setEnrollData] = useState<EnrollData | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [enrollLoading, setEnrollLoading] = useState(true);
  const [verified, setVerified] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    const enroll = async () => {
      try {
        const res = await auth.mfaEnroll();
        setEnrollData(res.data);
      } catch (err: unknown) {
        const message =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
          'Failed to initialize MFA setup.';
        setError(message);
      } finally {
        setEnrollLoading(false);
      }
    };
    enroll();
  }, []);

  const handleVerify = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await auth.mfaVerify(code);
      setVerified(true);
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        'Invalid code. Please try again.';
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  const handleCopySecret = () => {
    if (enrollData?.secret) {
      navigator.clipboard.writeText(enrollData.secret);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  if (enrollLoading) {
    return (
      <div className="p-8 max-w-lg mx-auto">
        <div className="flex items-center justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
        </div>
      </div>
    );
  }

  if (verified && enrollData) {
    return (
      <div className="p-8 max-w-lg mx-auto">
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
          <div className="flex items-center gap-3 mb-4">
            <ShieldCheck size={24} className="text-green-600" />
            <h2 className="text-lg font-semibold text-gray-900">MFA Enabled</h2>
          </div>
          <p className="text-sm text-gray-600 mb-4">
            Two-factor authentication is now active on your account. Save these backup codes in a
            secure location — each can only be used once.
          </p>
          <div className="bg-gray-50 rounded-md border border-gray-200 p-4 mb-4">
            <p className="text-xs font-semibold text-gray-500 uppercase mb-3">Backup Codes</p>
            <div className="grid grid-cols-2 gap-2">
              {enrollData.backupCodes.map((c) => (
                <code key={c} className="text-sm font-mono text-gray-800 bg-white border border-gray-200 rounded px-2 py-1">
                  {c}
                </code>
              ))}
            </div>
          </div>
          <button
            onClick={() => navigate('/settings')}
            className="w-full py-2 px-4 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 transition-colors"
          >
            Done
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="p-8 max-w-lg mx-auto">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-gray-900">Set up Two-Factor Authentication</h1>
        <p className="text-sm text-gray-500 mt-1">
          Scan the QR code with your authenticator app, then enter the code to verify.
        </p>
      </div>

      {error && (
        <div className="mb-4 px-4 py-3 rounded-md bg-red-50 border border-red-200 text-red-700 text-sm">
          {error}
        </div>
      )}

      {enrollData && (
        <div className="space-y-6">
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
            <h2 className="text-sm font-medium text-gray-700 mb-4">
              1. Scan this QR code with your authenticator app
            </h2>
            <div className="flex justify-center mb-4">
              <img
                src={`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(enrollData.totpUri)}`}
                alt="TOTP QR Code"
                className="border border-gray-200 rounded-md"
                width={200}
                height={200}
              />
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-2 text-center">Or enter the secret key manually:</p>
              <div className="flex items-center gap-2 bg-gray-50 border border-gray-200 rounded-md px-3 py-2">
                <code className="text-sm font-mono text-gray-800 flex-1 break-all">
                  {enrollData.secret}
                </code>
                <button
                  onClick={handleCopySecret}
                  className="text-gray-400 hover:text-gray-600 shrink-0"
                  title="Copy secret"
                >
                  <Copy size={14} />
                </button>
              </div>
              {copied && (
                <p className="text-xs text-green-600 mt-1 text-center">Copied to clipboard!</p>
              )}
            </div>
          </div>

          <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
            <h2 className="text-sm font-medium text-gray-700 mb-4">
              2. Enter the 6-digit code to verify
            </h2>
            <form onSubmit={handleVerify} className="space-y-4">
              <input
                type="text"
                inputMode="numeric"
                pattern="[0-9]{6}"
                maxLength={6}
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
                required
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-center tracking-widest font-mono focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                placeholder="000000"
                autoComplete="one-time-code"
              />
              <button
                type="submit"
                disabled={loading || code.length !== 6}
                className="w-full py-2 px-4 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {loading ? 'Verifying...' : 'Verify & Enable'}
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
