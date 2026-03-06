import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE_PORTAL } from '../config.js';

const ingestionChunks = new Counter('ingestion_chunks_written');

// Minimal 1-page PDF (binary representation)
const PDF_CONTENT = open('../../tests/fixtures/sample.pdf', 'b');

export const options = {
  vus: 10,
  duration: '3m',
  thresholds: {
    'http_req_failed': ['rate<0.01'],
    // Phase 5 acceptance: ingestion throughput >= 5 docs/min (0.0833 docs/s).
    ingestion_chunks_written: ['rate>=0.083'],
  },
};

export default function () {
  // Login as test tenant
  const loginRes = http.post(
    `${BASE_PORTAL}/portal/auth/login`,
    JSON.stringify({ email: 'loadtest@example.com', password: 'LoadTest123!' }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  if (loginRes.status !== 200) {
    console.error(`Login failed: ${loginRes.status}`);
    return;
  }

  const token = loginRes.json('accessToken');
  const authHeaders = {
    Authorization: `Bearer ${token}`,
  };

  // Upload PDF document
  const formData = {
    file: http.file(PDF_CONTENT, `test-${__VU}-${__ITER}.pdf`, 'application/pdf'),
  };

  const uploadRes = http.post(`${BASE_PORTAL}/portal/documents`, formData, {
    headers: authHeaders,
    tags: { type: 'upload' },
  });

  check(uploadRes, {
    'upload accepted': (r) => r.status === 200,
  });

  if (uploadRes.status === 200) {
    ingestionChunks.add(1);
  }

  sleep(2);
}
