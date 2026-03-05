import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { onboarding as onboardingApi, documents as docsApi } from '@/lib/api';
import type { OnboardingState } from '@/types';
import { CheckCircle2, Upload, Settings2, Code, MessageSquare } from 'lucide-react';
import { useAuthStore } from '@/stores/authStore';

const STEPS = [
  { id: 1, title: 'Upload a Document', icon: <Upload size={20} /> },
  { id: 2, title: 'Configure Your Bot', icon: <Settings2 size={20} /> },
  { id: 3, title: 'Embed the Widget', icon: <Code size={20} /> },
  { id: 4, title: 'Test the Chat', icon: <MessageSquare size={20} /> },
];

export default function OnboardingPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const [state, setState] = useState<OnboardingState | null>(null);
  const [currentStep, setCurrentStep] = useState(1);
  const [loading, setLoading] = useState(true);
  const [completing, setCompleting] = useState(false);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [uploadedFileName, setUploadedFileName] = useState('');
  const [testMessage, setTestMessage] = useState('');
  const [dragging, setDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const redirectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const apiKeyPreview = `sl_live_${'•'.repeat(20)}`;
  const publicBaseUrl = `${window.location.origin}`;

  const fetchState = useCallback(async () => {
    try {
      const res = await onboardingApi.getState();
      setState(res.data);
      // Jump to first incomplete step
      const completed = res.data.completedSteps;
      for (let i = 1; i <= 4; i++) {
        if (!completed.includes(i)) {
          setCurrentStep(i);
          break;
        }
      }
    } catch {
      setError('Failed to load onboarding state.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchState();
    return () => {
      if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
    };
  }, [fetchState]);

  const completeStep = async (step: number) => {
    if (state?.completedSteps.includes(step)) return;
    setCompleting(true);
    try {
      await onboardingApi.completeStep(step);
      setState((prev) => {
        if (!prev) return prev;
        const newSteps = [...prev.completedSteps, step];
        const isComplete = newSteps.length >= 4;
        if (isComplete) {
          redirectTimerRef.current = setTimeout(() => navigate('/dashboard'), 3000);
        }
        return { completedSteps: newSteps, isComplete };
      });
    } catch {
      setError('Failed to mark step as complete.');
    } finally {
      setCompleting(false);
    }
  };

  const isStepDone = (step: number) => state?.completedSteps.includes(step) ?? false;

  const handleFileUpload = async (file: File) => {
    setUploading(true);
    setError('');
    try {
      await docsApi.upload(file);
      setUploadedFileName(file.name);
      await completeStep(1);
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
        'Upload failed.';
      setError(message);
    } finally {
      setUploading(false);
    }
  };

  const handleFileDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragging(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFileUpload(file);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      handleFileUpload(file);
      e.target.value = '';
    }
  };

  const goToNext = async () => {
    if (currentStep < 4) setCurrentStep((s) => s + 1);
  };

  const goToPrev = () => {
    if (currentStep > 1) setCurrentStep((s) => s - 1);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600" />
      </div>
    );
  }

  if (state?.isComplete) {
    return (
      <div className="flex items-center justify-center min-h-full p-6">
        <div className="text-center max-w-sm">
          <div className="flex justify-center mb-4">
            <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center">
              <CheckCircle2 size={32} className="text-green-600" />
            </div>
          </div>
          <h2 className="text-xl font-bold text-gray-900 mb-2">Setup Complete!</h2>
          <p className="text-sm text-gray-500 mb-6">
            SupportLayer is fully configured. Redirecting to your dashboard...
          </p>
          <Link
            to="/dashboard"
            className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 transition-colors"
          >
            Go to Dashboard
          </Link>
        </div>
      </div>
    );
  }

  const embedSnippet = `<script
  src="${publicBaseUrl}/widget/dist/widget.umd.js"
  data-api-key="${apiKeyPreview}"
  data-api-base="${publicBaseUrl}"
  data-title="Chat with us"
  data-color="#2563eb"
></script>`;

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-gray-900">Get Started with SupportLayer</h1>
        <p className="text-sm text-gray-500 mt-1">Complete these steps to set up your support bot.</p>
      </div>

      {error && (
        <div className="mb-4 px-4 py-3 rounded-md bg-red-50 border border-red-200 text-red-700 text-sm flex justify-between">
          <span>{error}</span>
          <button onClick={() => setError('')} className="text-red-400 hover:text-red-600">×</button>
        </div>
      )}

      <div className="flex gap-8">
        {/* Steps sidebar */}
        <div className="w-56 shrink-0">
          <div className="space-y-1">
            {STEPS.map((step) => {
              const done = isStepDone(step.id);
              const active = step.id === currentStep;
              return (
                <button
                  key={step.id}
                  onClick={() => setCurrentStep(step.id)}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-sm transition-colors text-left ${
                    active
                      ? 'bg-blue-50 text-blue-700 font-medium'
                      : done
                      ? 'text-gray-500'
                      : 'text-gray-600 hover:bg-gray-100'
                  }`}
                >
                  <span
                    className={`flex items-center justify-center w-6 h-6 rounded-full text-xs font-bold shrink-0 ${
                      done
                        ? 'bg-green-100 text-green-600'
                        : active
                        ? 'bg-blue-100 text-blue-600'
                        : 'bg-gray-100 text-gray-400'
                    }`}
                  >
                    {done ? <CheckCircle2 size={14} /> : step.id}
                  </span>
                  {step.title}
                </button>
              );
            })}
          </div>
        </div>

        {/* Step content */}
        <div className="flex-1 max-w-2xl">
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
            {currentStep === 1 && (
              <div>
                <h2 className="text-base font-semibold text-gray-900 mb-1">Upload a Document</h2>
                <p className="text-sm text-gray-500 mb-5">
                  Upload a PDF, DOCX, TXT, or CSV file to build your knowledge base.
                </p>

                {isStepDone(1) ? (
                  <div className="flex items-center gap-3 p-4 bg-green-50 border border-green-200 rounded-md">
                    <CheckCircle2 size={20} className="text-green-600" />
                    <div>
                      <p className="text-sm font-medium text-green-800">Document uploaded</p>
                      {uploadedFileName && (
                        <p className="text-xs text-green-600">{uploadedFileName}</p>
                      )}
                    </div>
                  </div>
                ) : (
                  <>
                    <input
                      ref={fileInputRef}
                      type="file"
                      className="hidden"
                      accept=".pdf,.docx,.txt,.csv"
                      onChange={handleFileChange}
                    />
                    <div
                      onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
                      onDragLeave={() => setDragging(false)}
                      onDrop={handleFileDrop}
                      onClick={() => fileInputRef.current?.click()}
                      className={`border-2 border-dashed rounded-lg p-10 text-center cursor-pointer transition-colors ${
                        dragging
                          ? 'border-blue-400 bg-blue-50'
                          : 'border-gray-300 hover:border-gray-400 hover:bg-gray-50'
                      }`}
                    >
                      <Upload size={24} className="mx-auto text-gray-400 mb-3" />
                      <p className="text-sm font-medium text-gray-600">
                        {uploading ? 'Uploading...' : 'Drop a file here or click to browse'}
                      </p>
                      <p className="text-xs text-gray-400 mt-1">PDF, DOCX, TXT, CSV up to 50 MB</p>
                    </div>
                  </>
                )}
              </div>
            )}

            {currentStep === 2 && (
              <div>
                <h2 className="text-base font-semibold text-gray-900 mb-1">Configure Your Bot</h2>
                <p className="text-sm text-gray-500 mb-5">
                  Set up how your support bot behaves and looks.
                </p>
                <Link
                  to="/config"
                  className="inline-flex items-center gap-2 px-4 py-2.5 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 transition-colors mb-4"
                >
                  <Settings2 size={16} />
                  Open Configuration
                </Link>
                <p className="text-xs text-gray-400">
                  Configure your system prompt, model, temperature, and widget appearance.
                </p>
                {!isStepDone(2) && (
                  <button
                    onClick={() => completeStep(2)}
                    disabled={completing}
                    className="mt-4 px-3 py-1.5 text-xs border border-gray-300 text-gray-600 rounded-md hover:bg-gray-50 transition-colors disabled:opacity-50"
                  >
                    {completing ? 'Marking...' : 'Mark as done'}
                  </button>
                )}
                {isStepDone(2) && (
                  <div className="flex items-center gap-2 mt-4 text-green-600 text-sm">
                    <CheckCircle2 size={16} />
                    Completed
                  </div>
                )}
              </div>
            )}

            {currentStep === 3 && (
              <div>
                <h2 className="text-base font-semibold text-gray-900 mb-1">Embed the Widget</h2>
                <p className="text-sm text-gray-500 mb-5">
                  Add this snippet before the closing <code className="text-xs bg-gray-100 px-1 rounded">&lt;/body&gt;</code> tag on your website.
                </p>
                <div className="bg-gray-900 rounded-md p-4 mb-4 overflow-x-auto">
                  <pre className="text-xs text-green-400 font-mono whitespace-pre">{embedSnippet}</pre>
                </div>
                <p className="text-xs text-gray-500 mb-4">
                  Replace <code className="bg-gray-100 px-1 rounded text-xs">data-api-key</code> with your actual API key from the{' '}
                  <Link to="/team#api-keys" className="text-blue-600 hover:underline">Team page</Link>.
                </p>
                {!isStepDone(3) && (
                  <button
                    onClick={() => completeStep(3)}
                    disabled={completing}
                    className="px-3 py-1.5 text-xs border border-gray-300 text-gray-600 rounded-md hover:bg-gray-50 transition-colors disabled:opacity-50"
                  >
                    {completing ? 'Marking...' : 'Mark as done'}
                  </button>
                )}
                {isStepDone(3) && (
                  <div className="flex items-center gap-2 text-green-600 text-sm">
                    <CheckCircle2 size={16} />
                    Completed
                  </div>
                )}
              </div>
            )}

            {currentStep === 4 && (
              <div>
                <h2 className="text-base font-semibold text-gray-900 mb-1">Test the Chat</h2>
                <p className="text-sm text-gray-500 mb-5">
                  Send a test message to verify your bot is working.
                </p>
                <div className="bg-gray-50 rounded-md border border-gray-200 p-4 mb-4">
                  <p className="text-xs text-gray-400 mb-2">Try asking:</p>
                  {[
                    'What can you help me with?',
                    'How do I get started?',
                    'What are your support hours?',
                  ].map((suggestion) => (
                    <button
                      key={suggestion}
                      onClick={() => setTestMessage(suggestion)}
                      className="block w-full text-left text-sm text-blue-600 hover:text-blue-700 py-1 hover:underline"
                    >
                      "{suggestion}"
                    </button>
                  ))}
                </div>
                {testMessage && (
                  <div className="bg-blue-50 border border-blue-200 rounded-md px-4 py-3 mb-4">
                    <p className="text-sm text-blue-700">
                      Open the widget on your site and send: <strong>"{testMessage}"</strong>
                    </p>
                  </div>
                )}
                {!isStepDone(4) && (
                  <button
                    onClick={() => completeStep(4)}
                    disabled={completing}
                    className="px-3 py-1.5 text-xs border border-gray-300 text-gray-600 rounded-md hover:bg-gray-50 transition-colors disabled:opacity-50"
                  >
                    {completing ? 'Marking...' : 'Mark as done'}
                  </button>
                )}
                {isStepDone(4) && (
                  <div className="flex items-center gap-2 text-green-600 text-sm">
                    <CheckCircle2 size={16} />
                    Completed
                  </div>
                )}
              </div>
            )}

            {/* Navigation */}
            <div className="flex items-center justify-between mt-8 pt-4 border-t border-gray-100">
              <button
                onClick={goToPrev}
                disabled={currentStep === 1}
                className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                Back
              </button>
              <div className="flex gap-1.5">
                {STEPS.map((s) => (
                  <div
                    key={s.id}
                    className={`w-2 h-2 rounded-full transition-colors ${
                      s.id === currentStep
                        ? 'bg-blue-600'
                        : isStepDone(s.id)
                        ? 'bg-green-500'
                        : 'bg-gray-200'
                    }`}
                  />
                ))}
              </div>
              <button
                onClick={goToNext}
                disabled={currentStep === 4}
                className="px-4 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                Continue
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Ignored: user is displayed in layout sidebar */}
      <div className="hidden">{user?.email}</div>
    </div>
  );
}
