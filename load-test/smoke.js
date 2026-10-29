import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Trend } from "k6/metrics";

const baseUrl = __ENV.BASE_URL || "http://api:8080";
const stages = __ENV.PROFILE === "stress"
  ? [
      { target: 50, duration: "15s" },
      { target: 100, duration: "20s" },
      { target: 200, duration: "30s" },
      { target: 400, duration: "30s" },
      { target: 0, duration: "15s" }
    ]
  : __ENV.PROFILE === "controlled"
    ? [
        { target: 25, duration: "30s" },
        { target: 50, duration: "45s" },
        { target: 75, duration: "45s" },
        { target: 100, duration: "45s" },
        { target: 0, duration: "15s" }
      ]
    : __ENV.PROFILE === "ceiling"
      ? [
          { target: 25, duration: "30s" },
          { target: 50, duration: "45s" },
          { target: 75, duration: "60s" },
          { target: 0, duration: "15s" }
        ]
      : __ENV.PROFILE === "ceiling50"
        ? [
            { target: 25, duration: "30s" },
            { target: 40, duration: "45s" },
            { target: 50, duration: "60s" },
            { target: 0, duration: "15s" }
          ]
    : [
      { target: 10, duration: "30s" },
      { target: 25, duration: "60s" },
      { target: 50, duration: "60s" },
      { target: 0, duration: "15s" }
    ];
export const options = {
  scenarios: {
    mixed: {
      executor: "ramping-arrival-rate",
      startRate: 2,
      timeUnit: "1s",
      preAllocatedVUs: 10,
      maxVUs: 100,
      stages
    }
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    payment_latency: ["p(95)<1000"],
    read_latency: ["p(95)<500"]
  },
  summaryTrendStats: ["avg", "min", "med", "p(90)", "p(95)", "p(99)", "max"]
};

const paymentLatency = new Trend("payment_latency");
const readLatency = new Trend("read_latency");
const committedPayments = new Counter("committed_payments");
const expectedFailures = new Counter("expected_business_failures");

function request(method, path, body, token, headers = {}) {
  const requestHeaders = {
    "Content-Type": "application/json",
    ...headers
  };
  if (token) requestHeaders.Authorization = `Bearer ${token}`;
  return http.request(method, `${baseUrl}${path}`, body ? JSON.stringify(body) : null, {
    headers: requestHeaders,
    tags: { endpoint: path }
  });
}

function registerAndLogin(role) {
  const email = `load-${role}-${Date.now()}-${__VU}@example.test`;
  const password = "LoadTestPassword123!";
  const handle = `l${role[0]}${Date.now().toString(36)}${__VU}`;
  const registration = request("POST", "/v1/auth/register", {
    email,
    password,
    displayName: `Load ${role}`,
    handle
  });
  check(registration, { "register succeeds": (response) => response.status === 201 });

  const login = request("POST", "/v1/auth/login?useCookies=false", { email, password });
  const loginOk = check(login, { "login succeeds": (response) => response.status === 200 });
  if (!loginOk) return { token: null, handle };

  const token = login.json("accessToken");
  return { token, handle };
}

export function setup() {
  for (let attempt = 0; attempt < 60; attempt += 1) {
    const health = http.get(`${baseUrl}/health/ready`);
    if (health.status === 200) break;
    sleep(1);
  }

  const sender = registerAndLogin("sender");
  const recipient = registerAndLogin("recipient");
  return { sender, recipient };
}

export default function (data) {
  if (!data.sender.token || !data.recipient.handle) return;

  const read = request("GET", "/v1/wallet/", null, data.sender.token);
  readLatency.add(read.timings.duration);
  check(read, { "wallet read succeeds": (response) => response.status === 200 });

  const activity = request("GET", "/v1/operations/status", null, data.sender.token);
  readLatency.add(activity.timings.duration);
  check(activity, { "status read succeeds": (response) => response.status === 200 });

  const idempotencyKey = `load-${Date.now()}-${__VU}-${__ITER}`;
  const payment = request("POST", "/v1/payments/", {
    recipientHandle: data.recipient.handle,
    amountMinor: 1,
    currency: "USD",
    note: "Docker load test"
  }, data.sender.token, { "Idempotency-Key": idempotencyKey });
  paymentLatency.add(payment.timings.duration);

  if (payment.status === 201) {
    committedPayments.add(1);
  } else if ([400, 404, 409].includes(payment.status)) {
    expectedFailures.add(1);
  }
  check(payment, {
    "payment is committed or expected business failure": (response) =>
      [201, 400, 404, 409].includes(response.status)
  });

  const wasRateLimited = [read, activity, payment]
      .some((response) => response.status === 429);
  sleep(wasRateLimited ? 0.5 : 0.1);
}

export function handleSummary(data) {
  return {
    "/results/summary.json": JSON.stringify(data, null, 2)
  };
}
