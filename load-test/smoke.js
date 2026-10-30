import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Trend } from "k6/metrics";

const baseUrl = __ENV.BASE_URL || "http://api:8080";
const isReadOnly = __ENV.PROFILE === "read150";
const isWriteOnly = __ENV.PROFILE === "write150";
const maxVUs = __ENV.PROFILE === "concurrency"
  ? 500
  : __ENV.PROFILE === "capacity200" ? 300
  : ["capacity105wide", "capacity110wide", "capacity125wide", "capacity150", "capacity150wide", "capacity150widest", "read150", "write150", "steady100wide", "steady101wide", "steady102wide", "steady105wide"].includes(__ENV.PROFILE) ? 250
  : __ENV.PROFILE === "capacity" ? 200 : 100;
const stages = __ENV.PROFILE === "stress"
  ? [
      { target: 50, duration: "15s" },
      { target: 100, duration: "20s" },
      { target: 200, duration: "30s" },
      { target: 400, duration: "30s" },
      { target: 0, duration: "15s" }
    ]
  : __ENV.PROFILE === "concurrency"
    ? [
        { target: 100, duration: "30s" },
        { target: 200, duration: "45s" },
        { target: 400, duration: "60s" },
        { target: 800, duration: "60s" },
        { target: 0, duration: "30s" }
      ]
  : __ENV.PROFILE === "capacity"
    ? [
        { target: 50, duration: "45s" },
        { target: 75, duration: "60s" },
        { target: 100, duration: "60s" },
        { target: 0, duration: "15s" }
      ]
  : __ENV.PROFILE === "capacity200"
    ? [
        { target: 100, duration: "45s" },
        { target: 125, duration: "60s" },
        { target: 150, duration: "60s" },
        { target: 200, duration: "60s" },
        { target: 0, duration: "15s" }
      ]
  : __ENV.PROFILE === "capacity150"
    ? [
        { target: 100, duration: "45s" },
        { target: 125, duration: "60s" },
        { target: 150, duration: "60s" },
        { target: 0, duration: "15s" }
      ]
  : __ENV.PROFILE === "capacity125wide"
    ? [
        { target: 75, duration: "45s" },
        { target: 100, duration: "60s" },
        { target: 125, duration: "60s" },
        { target: 0, duration: "15s" }
      ]
  : __ENV.PROFILE === "capacity110wide"
    ? [
        { target: 75, duration: "45s" },
        { target: 100, duration: "60s" },
        { target: 110, duration: "60s" },
        { target: 0, duration: "15s" }
      ]
  : __ENV.PROFILE === "capacity105wide"
    ? [
        { target: 75, duration: "45s" },
        { target: 100, duration: "60s" },
        { target: 105, duration: "60s" },
        { target: 0, duration: "15s" }
      ]
  : __ENV.PROFILE === "steady105wide"
    ? [
        { target: 105, duration: "45s" },
        { target: 105, duration: "5m" },
        { target: 0, duration: "30s" }
      ]
  : __ENV.PROFILE === "steady102wide"
    ? [
        { target: 102, duration: "45s" },
        { target: 102, duration: "5m" },
        { target: 0, duration: "30s" }
      ]
  : __ENV.PROFILE === "steady101wide"
    ? [
        { target: 101, duration: "45s" },
        { target: 101, duration: "5m" },
        { target: 0, duration: "30s" }
      ]
  : __ENV.PROFILE === "steady100wide"
    ? [
        { target: 100, duration: "45s" },
        { target: 100, duration: "5m" },
        { target: 0, duration: "30s" }
      ]
  : ["capacity150wide", "capacity150widest", "read150", "write150"].includes(__ENV.PROFILE)
    ? [
        { target: 100, duration: "45s" },
        { target: 125, duration: "60s" },
        { target: 150, duration: "60s" },
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
        : __ENV.PROFILE === "distributed50"
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
  setupTimeout: ["capacity105wide", "capacity110wide", "capacity125wide", "capacity150widest", "steady100wide", "steady101wide", "steady102wide", "steady105wide"].includes(__ENV.PROFILE) ? "5m" : "1m",
  scenarios: {
    mixed: {
      executor: "ramping-arrival-rate",
      startRate: 2,
      timeUnit: "1s",
      preAllocatedVUs: 10,
      maxVUs,
      stages
    }
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    ...(isReadOnly || !isWriteOnly ? {
      read_latency: ["p(95)<200"],
      wallet_read_latency: ["p(95)<200"],
      status_read_latency: ["p(95)<200"]
    } : {}),
    ...(isWriteOnly || !isReadOnly ? {
      payment_latency: ["p(95)<300"]
    } : {})
  },
  summaryTrendStats: ["avg", "min", "med", "p(90)", "p(95)", "p(99)", "max"]
};

const paymentLatency = new Trend("payment_latency");
const readLatency = new Trend("read_latency");
const walletReadLatency = new Trend("wallet_read_latency");
const statusReadLatency = new Trend("status_read_latency");
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

function registerAndLogin(role, index) {
  const email = `load-${role}-${index}-${Date.now()}-${__VU}@example.test`;
  const password = "LoadTestPassword123!";
  const handle = `l${role[0]}${index}${Date.now().toString(36)}${__VU}`;
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

  const pairCount = ["concurrency", "capacity", "capacity150", "capacity200", "read150", "write150"].includes(__ENV.PROFILE)
    ? 32
    : __ENV.PROFILE === "capacity150wide" ? 128
    : ["capacity105wide", "capacity110wide", "capacity125wide", "capacity150widest", "steady100wide", "steady101wide", "steady102wide", "steady105wide"].includes(__ENV.PROFILE) ? 256
    : __ENV.PROFILE === "distributed50" ? 8 : 1;
  const pairs = [];
  for (let index = 0; index < pairCount; index += 1) {
    pairs.push({
      sender: registerAndLogin("sender", index),
      recipient: registerAndLogin("recipient", index)
    });
  }

  return { pairs };
}

export default function (data) {
  const pair = data.pairs[(__VU - 1) % data.pairs.length];
  if (!pair.sender.token || !pair.recipient.handle) return;

  const responses = [];

  if (!isWriteOnly) {
    const read = request("GET", "/v1/wallet/", null, pair.sender.token);
    responses.push(read);
    readLatency.add(read.timings.duration);
    walletReadLatency.add(read.timings.duration);
    check(read, { "wallet read succeeds": (response) => response.status === 200 });

    const activity = request("GET", "/v1/operations/status", null, pair.sender.token);
    responses.push(activity);
    readLatency.add(activity.timings.duration);
    statusReadLatency.add(activity.timings.duration);
    check(activity, { "status read succeeds": (response) => response.status === 200 });
  }

  if (!isReadOnly) {
    const idempotencyKey = `load-${Date.now()}-${__VU}-${__ITER}`;
    const payment = request("POST", "/v1/payments/", {
      recipientHandle: pair.recipient.handle,
      amountMinor: 1,
      currency: "USD",
      note: "Docker load test"
    }, pair.sender.token, { "Idempotency-Key": idempotencyKey });
    responses.push(payment);
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
  }

  const wasRateLimited = responses.some((response) => response.status === 429);
  sleep(wasRateLimited ? 0.5 : 0.1);
}

export function handleSummary(data) {
  return {
    "/results/summary.json": JSON.stringify(data, null, 2)
  };
}
