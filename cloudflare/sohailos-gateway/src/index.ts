export interface Env {
  SOHAILOS_GATEWAY_TOKEN?: string;
  SOHAILOS_AI_PROVIDER?: string;
  SOHAILOS_OPENAI_API_KEY?: string;
  SOHAILOS_OPENAI_MODEL?: string;
  SOHAILOS_GEMINI_API_KEY?: string;
  SOHAILOS_GEMINI_MODEL?: string;
  SOHAILOS_ANTHROPIC_API_KEY?: string;
  SOHAILOS_ANTHROPIC_MODEL?: string;
  SOHAILOS_SUPABASE_URL?: string;
  SOHAILOS_SUPABASE_SERVICE_ROLE_KEY?: string;
  SOHAILOS_SUPABASE_TABLE?: string;
  SOHAILOS_CORS_ORIGINS?: string;
  SOHAILOS_CLOUDFLARE_AI_MODEL?: string;
  AI?: { run: (model: string, input: Record<string, unknown>, options?: Record<string, unknown>) => Promise<unknown> };
}

type AgentRequest = { prompt: string; systemPrompt?: string; memoryKey?: string };
type MemorySnapshot = { updatedAt: string; user: string; assistant: string };
type Provider = "openai" | "gemini" | "anthropic" | "cloudflare";

const VERSION = "0.3.2";
const MAX_PROMPT_LENGTH = 20_000;
const MAX_MEMORY_KEY_LENGTH = 200;
const MAX_MEMORY_CONTEXT_LENGTH = 8_000;
const PROVIDER_TIMEOUT_MS = 45_000;
const DEFAULT_CLOUDFLARE_AI_MODEL = "@cf/meta/llama-3.1-8b-instruct-fast";
const DEFAULT_SYSTEM_PROMPT = `You are SohailOS, a personal AI operating system. Route each request to the most appropriate internal capability (thinking, sociology, cinema, research, statistics, AI, code, product, office, operations, strategy, or learning). Be precise, structured, evidence-aware, and practical. Do not claim actions or integrations that did not actually occur. Treat user data and credentials as confidential.`;

function responseHeaders(origin: string): Headers {
  const headers = new Headers({
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
    "x-content-type-options": "nosniff",
    "access-control-allow-headers": "authorization,content-type,mcp-session-id",
    "access-control-allow-methods": "GET,POST,OPTIONS"
  });
  if (origin) headers.set("access-control-allow-origin", origin);
  return headers;
}

const json = (body: unknown, status = 200, origin = "*") => new Response(JSON.stringify(body), { status, headers: responseHeaders(origin) });

function allowedOrigin(request: Request, env: Env): string {
  const configured = (env.SOHAILOS_CORS_ORIGINS ?? "*").split(",").map(x => x.trim()).filter(Boolean);
  if (configured.includes("*")) return "*";
  const origin = request.headers.get("origin") ?? "";
  return configured.includes(origin) ? origin : "";
}

function authorized(request: Request, env: Env): boolean {
  if (!env.SOHAILOS_GATEWAY_TOKEN) return false;
  return (request.headers.get("authorization") ?? "") === `Bearer ${env.SOHAILOS_GATEWAY_TOKEN}`;
}

function providerFor(prompt: string, env: Env): Provider {
  const configured = (env.SOHAILOS_AI_PROVIDER ?? "auto").toLowerCase();
  if (["openai", "gemini", "anthropic"].includes(configured)) return configured as Provider;
  if (configured === "cloudflare" && env.AI) return "cloudflare";
  if (env.AI) return "cloudflare";
  const text = prompt.toLowerCase();
  if (["کدنویسی", "کد", "github", "debug", "c#", "python", "برنامه نویسی"].some(x => text.includes(x))) return "anthropic";
  if (["پژوهش", "مقاله", "research", "منبع", "literature", "متاآنالیز"].some(x => text.includes(x))) return "gemini";
  if (["هگل", "مارکس", "فلسفه", "تحلیل عمیق", "reasoning"].some(x => text.includes(x))) return "anthropic";
  return "openai";
}

async function fetchWithTimeout(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), PROVIDER_TIMEOUT_MS);
  try { return await fetch(input, { ...init, signal: controller.signal }); }
  finally { clearTimeout(timeout); }
}

async function providerError(response: Response): Promise<Error> {
  let detail = "request rejected";
  try {
    const data = await response.json() as any;
    const candidate = data?.error?.message ?? data?.message;
    if (typeof candidate === "string" && candidate.length <= 300) detail = candidate;
  } catch {}
  return new Error(`AI provider request failed (${response.status}): ${detail}`);
}

async function completeOpenAI(systemPrompt: string, userPrompt: string, endpoint: string, key: string, model: string): Promise<string> {
  const response = await fetchWithTimeout(`${endpoint.replace(/\/$/, "")}/chat/completions`, {
    method: "POST",
    headers: { "content-type": "application/json", authorization: `Bearer ${key}` },
    body: JSON.stringify({ model, messages: [{ role: "system", content: systemPrompt }, { role: "user", content: userPrompt }] })
  });
  if (!response.ok) throw await providerError(response);
  const data = await response.json() as any;
  const content = data?.choices?.[0]?.message?.content;
  if (typeof content !== "string" || !content.trim()) throw new Error("AI provider returned no text content");
  return content;
}

async function completeAnthropic(systemPrompt: string, userPrompt: string, env: Env): Promise<string> {
  const response = await fetchWithTimeout("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: { "content-type": "application/json", "x-api-key": env.SOHAILOS_ANTHROPIC_API_KEY ?? "", "anthropic-version": "2023-06-01" },
    body: JSON.stringify({ model: env.SOHAILOS_ANTHROPIC_MODEL ?? "claude-sonnet-4-5", max_tokens: 4096, system: systemPrompt, messages: [{ role: "user", content: userPrompt }] })
  });
  if (!response.ok) throw await providerError(response);
  const data = await response.json() as any;
  const content = Array.isArray(data?.content) ? data.content.filter((x: any) => x.type === "text").map((x: any) => x.text).join("\n") : "";
  if (!content.trim()) throw new Error("AI provider returned no text content");
  return content;
}

async function completeCloudflareAI(systemPrompt: string, userPrompt: string, env: Env): Promise<string> {
  if (!env.AI) throw new Error("Cloudflare Workers AI binding is not configured");
  const response = await env.AI.run(env.SOHAILOS_CLOUDFLARE_AI_MODEL ?? DEFAULT_CLOUDFLARE_AI_MODEL, { prompt: `${systemPrompt}\n\n${userPrompt}`, max_tokens: 4096 });
  const data = response as any;
  const content = data?.choices?.[0]?.message?.content ?? data?.response;
  if (typeof content !== "string" || !content.trim()) throw new Error("Cloudflare Workers AI returned no text content");
  return content;
}

async function complete(systemPrompt: string, userPrompt: string, env: Env): Promise<{ content: string; provider: string }> {
  const preferred = providerFor(userPrompt, env);
  const candidates: Provider[] = preferred === "openai"
    ? ["openai", "gemini", "anthropic", "cloudflare"]
    : preferred === "gemini"
      ? ["gemini", "openai", "anthropic", "cloudflare"]
      : preferred === "anthropic"
        ? ["anthropic", "openai", "gemini", "cloudflare"]
        : ["cloudflare", "openai", "gemini", "anthropic"];
  let lastError: unknown;
  for (const provider of candidates) {
    try {
      if (provider === "openai" && env.SOHAILOS_OPENAI_API_KEY) return { content: await completeOpenAI(systemPrompt, userPrompt, "https://api.openai.com/v1", env.SOHAILOS_OPENAI_API_KEY, env.SOHAILOS_OPENAI_MODEL ?? "gpt-5.6-luna"), provider };
      if (provider === "gemini" && env.SOHAILOS_GEMINI_API_KEY) return { content: await completeOpenAI(systemPrompt, userPrompt, "https://generativelanguage.googleapis.com/v1beta/openai", env.SOHAILOS_GEMINI_API_KEY, env.SOHAILOS_GEMINI_MODEL ?? "gemini-2.5-flash"), provider };
      if (provider === "anthropic" && env.SOHAILOS_ANTHROPIC_API_KEY) return { content: await completeAnthropic(systemPrompt, userPrompt, env), provider };
      if (provider === "cloudflare" && env.AI) return { content: await completeCloudflareAI(systemPrompt, userPrompt, env), provider };
    } catch (error) { lastError = error; }
  }
  throw lastError instanceof Error ? lastError : new Error("No AI provider is configured. Add at least one provider secret or the Cloudflare Workers AI binding.");
}

async function loadMemory(env: Env, key: string): Promise<MemorySnapshot | null> {
  if (!env.SOHAILOS_SUPABASE_URL || !env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY) return null;
  const table = env.SOHAILOS_SUPABASE_TABLE ?? "sohailos_memory";
  const url = `${env.SOHAILOS_SUPABASE_URL.replace(/\/$/, "")}/rest/v1/${encodeURIComponent(table)}?select=value&key=eq.${encodeURIComponent(key)}&limit=1`;
  try {
    const response = await fetchWithTimeout(url, { headers: { apikey: env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY, authorization: `Bearer ${env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY}` } });
    if (!response.ok) return null;
    const rows = await response.json() as any[];
    const value = rows?.[0]?.value;
    if (!value) return null;
    try { return JSON.parse(value) as MemorySnapshot; } catch { return null; }
  } catch { return null; }
}

async function saveMemory(env: Env, key: string, snapshot: MemorySnapshot): Promise<void> {
  if (!env.SOHAILOS_SUPABASE_URL || !env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY) return;
  const table = env.SOHAILOS_SUPABASE_TABLE ?? "sohailos_memory";
  const url = `${env.SOHAILOS_SUPABASE_URL.replace(/\/$/, "")}/rest/v1/${encodeURIComponent(table)}`;
  try {
    await fetchWithTimeout(url, { method: "POST", headers: { "content-type": "application/json", prefer: "resolution=merge-duplicates,return=minimal", apikey: env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY, authorization: `Bearer ${env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY}` }, body: JSON.stringify({ key, value: JSON.stringify(snapshot), updated_at: snapshot.updatedAt }) });
  } catch {}
}

async function runAgent(request: AgentRequest, env: Env) {
  if (!request || typeof request.prompt !== "string" || !request.prompt.trim()) throw new Error("prompt is required");
  if (request.prompt.length > MAX_PROMPT_LENGTH) throw new Error(`prompt exceeds ${MAX_PROMPT_LENGTH} characters`);
  const memoryKey = (typeof request.memoryKey === "string" ? request.memoryKey : "global").slice(0, MAX_MEMORY_KEY_LENGTH);
  const memory = await loadMemory(env, memoryKey);
  const previous = memory ? `\nPrevious exchange:\nUser: ${memory.user}\nAssistant: ${memory.assistant}\n`.slice(-MAX_MEMORY_CONTEXT_LENGTH) : "";
  const result = await complete(request.systemPrompt ?? DEFAULT_SYSTEM_PROMPT, `${previous}\nCurrent request:\n${request.prompt}`, env);
  await saveMemory(env, memoryKey, { updatedAt: new Date().toISOString(), user: request.prompt, assistant: result.content });
  return { content: result.content, provider: result.provider, memoryKey };
}

async function parseJson(request: Request): Promise<any> {
  try { return await request.json(); } catch { throw new Error("Request body must be valid JSON"); }
}

function mcpTools() {
  return [
    { name: "sohailos_conformance_ping", description: "Deterministic, side-effect-free MCP connectivity and conformance probe.", inputSchema: { type: "object", properties: {}, additionalProperties: false } },
    { name: "sohailos_agent_run", description: "Run a request through the authenticated SohailOS agent runtime facade.", inputSchema: { type: "object", properties: { prompt: { type: "string" }, memoryKey: { type: "string" } }, required: ["prompt"], additionalProperties: false } }
  ];
}

async function mcp(request: Request, env: Env) {
  const body = await parseJson(request);
  const id = body?.id ?? null;
  const origin = allowedOrigin(request, env);
  if (body?.method === "notifications/initialized") return new Response(null, { status: 202, headers: responseHeaders(origin) });
  if (body?.method === "initialize") return json({ jsonrpc: "2.0", id, result: { protocolVersion: "2025-06-18", capabilities: { tools: {} }, serverInfo: { name: "SohailOS Cloudflare Gateway", version: VERSION } } }, 200, origin);
  if (body?.method === "tools/list") return json({ jsonrpc: "2.0", id, result: { tools: mcpTools() } }, 200, origin);
  if (body?.method === "tools/call") {
    const name = body?.params?.name;
    if (name === "sohailos_conformance_ping") return json({ jsonrpc: "2.0", id, result: { content: [{ type: "text", text: "SOHAILOS_MCP_CONFORMANCE_OK" }], structuredContent: { ok: true, deterministic: true } } }, 200, origin);
    if (name !== "sohailos_agent_run") return json({ jsonrpc: "2.0", id, error: { code: -32602, message: "Unknown tool" } }, 400, origin);
    if (!authorized(request, env)) return json({ jsonrpc: "2.0", id, error: { code: -32001, message: "Unauthorized" } }, 401, origin);
    const args = body?.params?.arguments ?? {};
    try {
      const result = await runAgent({ prompt: String(args.prompt ?? ""), memoryKey: args.memoryKey ? String(args.memoryKey) : "global" }, env);
      return json({ jsonrpc: "2.0", id, result: { content: [{ type: "text", text: result.content }], structuredContent: result } }, 200, origin);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Agent execution failed";
      return json({ jsonrpc: "2.0", id, result: { isError: true, content: [{ type: "text", text: message }] } }, 200, origin);
    }
  }
  return json({ jsonrpc: "2.0", id, error: { code: -32601, message: "Method not found" } }, 404, origin);
}

function isClientError(message: string): boolean { return message === "Request body must be valid JSON" || message === "prompt is required" || message.startsWith("prompt exceeds"); }
function isProviderConfigurationError(message: string): boolean { return message.startsWith("No AI provider is configured.") || message === "Cloudflare Workers AI binding is not configured"; }
function wellKnownCard(origin: string) { return json({ name: "SohailOS Cloudflare Gateway", description: "SohailOS personal AI operating system gateway with authenticated agent execution and MCP discovery.", url: origin || "https://sohailos-central.mydominetestq.workers.dev", protocol: "MCP", compensation: { paid_by: "buyer", referral_fee: false, listing_fee: false, disclosure_url: "https://sohailos-central.mydominetestq.workers.dev/.well-known/agent-card.json" } }, 200, origin); }
function wellKnownConsent(origin: string) { return json({ allow_tool_call: true, endpoints: ["https://sohailos-central.mydominetestq.workers.dev/mcp"], listing: "owner-consented deterministic conformance probe only; authenticated agent execution remains protected" }, 200, origin); }

function dashboardHtml(env: Env): Response {
  const memoryConfigured = Boolean(env.SOHAILOS_SUPABASE_URL && env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY);
  const provider = env.AI ? "cloudflare" : env.SOHAILOS_OPENAI_API_KEY ? "openai" : env.SOHAILOS_GEMINI_API_KEY ? "gemini" : env.SOHAILOS_ANTHROPIC_API_KEY ? "anthropic" : "none";
  
  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>SohailOS Control Plane</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      color: #333;
    }
    .container {
      background: white;
      border-radius: 16px;
      box-shadow: 0 20px 60px rgba(0,0,0,0.3);
      padding: 48px;
      max-width: 800px;
      width: 90%;
    }
    h1 {
      font-size: 2.5rem;
      margin-bottom: 8px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }
    .version {
      color: #666;
      font-size: 0.9rem;
      margin-bottom: 32px;
    }
    .status {
      display: inline-block;
      padding: 4px 12px;
      background: #10b981;
      color: white;
      border-radius: 12px;
      font-size: 0.85rem;
      font-weight: 600;
      margin-bottom: 32px;
    }
    .endpoints {
      background: #f9fafb;
      border-radius: 12px;
      padding: 24px;
      margin-bottom: 24px;
    }
    .endpoints h2 {
      font-size: 1.25rem;
      margin-bottom: 16px;
      color: #374151;
    }
    .endpoint {
      display: flex;
      align-items: center;
      padding: 12px;
      background: white;
      border-radius: 8px;
      margin-bottom: 8px;
      font-family: "Courier New", monospace;
      font-size: 0.9rem;
    }
    .endpoint .method {
      background: #667eea;
      color: white;
      padding: 4px 8px;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
      margin-right: 12px;
      min-width: 50px;
      text-align: center;
    }
    .endpoint .path {
      color: #4b5563;
    }
    .config {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
      margin-top: 24px;
    }
    .config-item {
      background: #f9fafb;
      padding: 16px;
      border-radius: 8px;
      border-left: 4px solid #667eea;
    }
    .config-item .label {
      font-size: 0.85rem;
      color: #6b7280;
      margin-bottom: 4px;
    }
    .config-item .value {
      font-size: 1.1rem;
      font-weight: 600;
      color: #1f2937;
    }
    .footer {
      margin-top: 32px;
      text-align: center;
      color: #9ca3af;
      font-size: 0.85rem;
    }
  </style>
</head>
<body>
  <div class="container">
    <h1>SohailOS Control Plane</h1>
    <div class="version">Version ${VERSION}</div>
    <div class="status">● Operational</div>
    
    <div class="endpoints">
      <h2>API Endpoints</h2>
      <div class="endpoint">
        <span class="method">GET</span>
        <span class="path">/health</span>
      </div>
      <div class="endpoint">
        <span class="method">POST</span>
        <span class="path">/v1/agent/run</span>
      </div>
      <div class="endpoint">
        <span class="method">POST</span>
        <span class="path">/mcp</span>
      </div>
      <div class="endpoint">
        <span class="method">GET</span>
        <span class="path">/.well-known/agent-card.json</span>
      </div>
      <div class="endpoint">
        <span class="method">GET</span>
        <span class="path">/.well-known/mcp-conduct.json</span>
      </div>
    </div>

    <div class="config">
      <div class="config-item">
        <div class="label">AI Provider</div>
        <div class="value">${provider}</div>
      </div>
      <div class="config-item">
        <div class="label">Memory Backend</div>
        <div class="value">${memoryConfigured ? "Supabase" : "None"}</div>
      </div>
      <div class="config-item">
        <div class="label">Gateway Type</div>
        <div class="value">Cloudflare Workers</div>
      </div>
    </div>

    <div class="footer">
      Personal AI Operating System • Model-Agnostic • Research & Automation
    </div>
  </div>
  
  <script defer src="/_vercel/insights/script.js"></script>
</body>
</html>`;

  return new Response(html, {
    headers: {
      "content-type": "text/html; charset=utf-8",
      "cache-control": "no-store",
      "x-content-type-options": "nosniff"
    }
  });
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const origin = allowedOrigin(request, env);
    if (request.method === "OPTIONS") return new Response(null, { status: 204, headers: responseHeaders(origin) });
    const url = new URL(request.url);
    if (url.pathname === "/" && request.method === "GET") return json({ service: "SohailOS Cloudflare Gateway", version: VERSION, status: "ok", endpoints: { health: "/health", agent: "/v1/agent/run", mcp: "/mcp", agentCard: "/.well-known/agent-card.json", conductConsent: "/.well-known/mcp-conduct.json", dashboard: "/dashboard" } }, 200, origin);
    if (url.pathname === "/dashboard" && request.method === "GET") return dashboardHtml(env);
    if (url.pathname === "/health" && request.method === "GET") {
      const memoryConfigured = Boolean(env.SOHAILOS_SUPABASE_URL && env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY);
      const provider = env.AI ? "cloudflare" : env.SOHAILOS_OPENAI_API_KEY ? "openai" : env.SOHAILOS_GEMINI_API_KEY ? "gemini" : env.SOHAILOS_ANTHROPIC_API_KEY ? "anthropic" : "none";
      return json({ service: "SohailOS Cloudflare Gateway", version: VERSION, status: "ok", memory: memoryConfigured ? "supabase" : "none", provider, providerConfigured: provider !== "none" }, 200, origin);
    }
    if (url.pathname === "/.well-known/agent-card.json" && request.method === "GET") return wellKnownCard(request.url);
    if (url.pathname === "/.well-known/mcp-conduct.json" && request.method === "GET") return wellKnownConsent(request.url);
    if (url.pathname === "/mcp" && request.method === "POST") return await mcp(request, env);
    if (!authorized(request, env)) return json({ error: "Unauthorized" }, 401, origin);
    try {
      if (url.pathname === "/v1/agent/run" && request.method === "POST") return json(await runAgent(await parseJson(request) as AgentRequest, env), 200, origin);
      return json({ error: "Not found" }, 404, origin);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Internal error";
      const status = isClientError(message) ? 400 : isProviderConfigurationError(message) ? 503 : 500;
      return json({ error: message }, status, origin);
    }
  }
};
