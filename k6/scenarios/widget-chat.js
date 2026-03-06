import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_PUBLIC, TEST_API_KEY } from '../config.js';

const QUERIES = [
  'How do I reset my password?',
  'What are the system requirements?',
  'How do I contact support?',
  'Where can I find the documentation?',
  'How do I upgrade my plan?',
];

export const options = {
  vus: 100,
  duration: '5m',
  thresholds: {
    'http_req_duration{type:chat}': ['p(95)<8000'],
    'http_req_failed': ['rate<0.01'],
  },
};

function hasNonEmptyAnswer(response) {
  if (response.status !== 200) return false;
  try {
    const payload = response.json();
    return typeof payload.answer === 'string' && payload.answer.length > 0;
  } catch {
    return false;
  }
}

export default function () {
  const headers = {
    'Content-Type': 'application/json',
    'X-Api-Key': TEST_API_KEY,
  };

  // Create session
  const sessionRes = http.post(`${BASE_PUBLIC}/v1/session`, null, { headers });
  check(sessionRes, { 'session created': (r) => r.status === 200 });

  let sessionId = null;
  if (sessionRes.status === 200) {
    sessionId = sessionRes.json('id');
  }

  // Send 3 chat messages per session
  for (let i = 0; i < 3; i++) {
    const query = QUERIES[Math.floor(Math.random() * QUERIES.length)];
    const payload = JSON.stringify({
      query,
      sessionId,
    });

    const res = http.post(`${BASE_PUBLIC}/v1/chat`, payload, {
      headers,
      tags: { type: 'chat' },
    });

    check(res, {
      'chat status 200': (r) => r.status === 200,
      'has answer': hasNonEmptyAnswer,
    });

    sleep(1);
  }
}
