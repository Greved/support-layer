import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_ADMIN } from '../config.js';

export const options = {
  vus: 20,
  duration: '2m',
  thresholds: {
    'http_req_duration': ['p(95)<2000'],
    'http_req_failed': ['rate<0.01'],
  },
};

export default function () {
  const headers = { 'Content-Type': 'application/json' };

  // Login as admin
  const loginRes = http.post(
    `${BASE_ADMIN}/admin/auth/login`,
    JSON.stringify({ email: 'admin@supportlayer.io', password: 'AdminPassword123!' }),
    { headers }
  );

  check(loginRes, { 'admin login 200': (r) => r.status === 200 });

  if (loginRes.status !== 200) {
    console.error(`Admin login failed: ${loginRes.status}`);
    return;
  }

  const token = loginRes.json('accessToken');
  const authHeaders = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };

  // List tenants
  const tenantsRes = http.get(`${BASE_ADMIN}/admin/tenants`, { headers: authHeaders });
  check(tenantsRes, { 'tenants 200': (r) => r.status === 200 });

  // Get stats
  const statsRes = http.get(`${BASE_ADMIN}/admin/stats/global`, { headers: authHeaders });
  check(statsRes, { 'stats 200': (r) => r.status === 200 });

  sleep(1);
}
