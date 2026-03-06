# k6 Load Scenarios (Phase 5)

Scenarios:
- `k6/scenarios/widget-chat.js`: 100 concurrent widget chat sessions for 5 minutes.
- `k6/scenarios/pdf-upload.js`: 10 concurrent tenant uploads, with throughput threshold `>= 5 docs/min`.
- `k6/scenarios/admin-dashboard.js`: 20 concurrent admin users with `p95 < 2s`.

## Run all scenarios

```powershell
powershell -ExecutionPolicy Bypass -File k6/run-all.ps1 `
  -BasePublic "http://localhost:5000" `
  -BasePortal "http://localhost:5001" `
  -BaseAdmin "http://localhost:5002" `
  -TestApiKey "sl_live_test_key"
```

Summaries are written to `k6/results/`.

## Run a single scenario

```powershell
$env:BASE_PUBLIC="http://localhost:5000"
$env:TEST_API_KEY="sl_live_test_key"
k6 run --summary-export k6/widget-chat-summary.json k6/scenarios/widget-chat.js
```
