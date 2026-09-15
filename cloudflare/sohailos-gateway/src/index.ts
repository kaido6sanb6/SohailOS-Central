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
}

type AgentRequest = {
  prompt: string;
  systemPrompt?: string;
  memoryKey?: string;
};

type MemorySnapshot = {
  updatedAt: string;
  user: string;
  assistant: string;
};

const VERSION = "0.2.1";
const MAX_PROMPT_LENGTH = 20_000;
const MAX_MEMORY_KEY_LENGTH = 200;
const MAX_MEMORY_CONTEXT_LENGTH = 8_000;
const PROVIDER_TIMEOUT_MS = 45_000;

const DEFAULT_SYSTEM_PROMPT = `You are SohailOS, a personal AI operating system. Route each request to the most appropriate internal capability (thinking, sociology, cinema, research, statistics, AI, code, product, office, operations, strategy, or learning). Be precise, structured, evidence-aware, and practical. Do not claim actions or integrations that did not actually occur. Treat user data and credentials as confidential.`;

function responseHeaders(origin: string): Headers {
  const headers = new Headers({
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
    "x-content-type-options": "nosniff",
    "access-control-allow-headers": "authorization,content-type,mcp-session-id",
    "access-control-allow-methods": "GET,POST,OPTIONS",
  });
  if (origin) headers.set("access-control-allow-origin", origin);
  return headers;
}

const json = (body: unknown, status = 200, origin = "*") =>
  new Response(JSON.stringify(body), { status, headers: responseHeaders(origin) });

function allowedOrigin(request: Request, env: Env): string {
  const configured = (env.SOHAILOS_CORS_ORIGINS ?? "*").split(",").map(x => x.trim()).filter(Boolean);
  if (configured.includes("*")) return "*";
  const origin = request.headers.get("origin") ?? "";
  return configured.includes(origin) ? origin : "";
}

function authorized(request: Request, env: Env): boolean {
  if (!env.SOHAILOS_GATEWAY_TOKEN) return false;
  const header = request.headers.get("authorization") ?? "";
  return header === `Bearer ${env.SOHAILOS_GATEWAY_TOKEN}`;
}

function providerFor(prompt: string, env: Env): "openai" | "gemini" | "anthropic" {
  const configured = (env.SOHAILOS_AI_PROVIDER ?? "auto").toLowerCase();
  if (["openai", "gemini", "anthropic"].includes(configured)) return configured as "openai" | "gemini" | "anthropic";
  const text = prompt.toLowerCase();
  if (["کدنویسی", "کد", "github", "debug", "c#", "python", "برنامه نویسی"].some(x => text.includes(x))) return "anthropic";
  if (["پژوهش", "مقاله", "research", "منبع", "literature", "متاآنالیز"].some(x => text.includes(x))) return "gemini";
  if (["هگل", "مارکس", "فلسفه", "تحلیل عمیق", "reasoning"].some(x => text.includes(x))) return "anthropic";
  return "openai";
}

async function fetchWithTimeout(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), PROVIDER_TIMEOUT_MS);
  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

async function providerError(response: Response): Promise<Error> {
  let detail = "request rejected";
  try {
    const data = await response.json() as any;
    const candidate = data?.error?.message ?? data?.message;
    if (typeof candidate === "string" && candidate.length <= 300) detail = candidate;
  } catch {
    // Keep provider failures generic when the upstream response is not JSON.
  }
  return new Error(`AI provider request failed (${response.status}): ${detail}`);
}

async function completeOpenAI(systemPrompt: string, userPrompt: string, endpoint: string, key: string, model: string): Promise<string> {
  const response = await fetchWithTimeout(`${endpoint.replace(/\/$/, "")}/chat/completions`, {
    method: "POST",
    headers: { "content-type": "application/json", authorization: `Bearer ${key}` },
    body: JSON.stringify({ model, messages: [{ role: "system", content: systemPrompt }, { role: "user", content: userPrompt }] }),
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
    headers: {
      "content-type": "application/json",
      "x-api-key": env.SOHAILOS_ANTHROPIC_API_KEY ?? "",
      "anthropic-version": "2023-06-01",
    },
    body: JSON.stringify({
      model: env.SOHAILOS_ANTHROPIC_MODEL ?? "claude-sonnet-4-5",
      max_tokens: 4096,
      system: systemPrompt,
      messages: [{ role: "user", content: userPrompt }],
    }),
  });
  if (!response.ok) throw await providerError(response);
  const data = await response.json() as any;
  const content = Array.isArray(data?.content)
    ? data.content.filter((x: any) => x.type === "text").map((x: any) => x.text).join("\n")
    : "";
  if (!content.trim()) throw new Error("AI provider returned no text content");
  return content;
}

async function complete(systemPrompt: string, userPrompt: string, env: Env): Promise<{ content: string; provider: string }> {
  const preferred = providerFor(userPrompt, env);
  const candidates = preferred === "openai" ? ["openai", "gemini", "anthropic"] : preferred === "gemini" ? ["gemini", "openai", "anthropic"] : ["anthropic", "openai", "gemini"];
  let lastError: unknown;
  for (const provider of candidates) {
    try {
      if (provider === "openai" && env.SOHAILOS_OPENAI_API_KEY) {
        return { content: await completeOpenAI(systemPrompt, userPrompt, "https://api.openai.com/v1", env.SOHAILOS_OPENAI_API_KEY, env.SOHAILOS_OPENAI_MODEL ?? "gpt-5.6-luna"), provider };
      }
      if (provider === "gemini" && env.SOHAILOS_GEMINI_API_KEY) {
        return { content: await completeOpenAI(systemPrompt, userPrompt, "https://generativelanguage.googleapis.com/v1beta/openai", env.SOHAILOS_GEMINI_API_KEY, env.SOHAILOS_GEMINI_MODEL ?? "gemini-2.5-flash"), provider };
      }
      if (provider === "anthropic" && env.SOHAILOS_ANTHROPIC_API_KEY) {
        return { content: await completeAnthropic(systemPrompt, userPrompt, env), provider };
      }
    } catch (error) {
      lastError = error;
    }
  }
  throw lastError instanceof Error ? lastError : new Error("No AI provider is configured. Add at least one provider secret.");
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
  } catch {
    return null;
  }
}

async function saveMemory(env: Env, key: string, snapshot: MemorySnapshot): Promise<void> {
  if (!env.SOHAILOS_SUPABASE_URL || !env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY) return;
  const table = env.SOHAILOS_SUPABASE_TABLE ?? "sohailos_memory";
  const url = `${env.SOHAILOS_SUPABASE_URL.replace(/\/$/, "")}/rest/v1/${encodeURIComponent(table)}`;
  try {
    await fetchWithTimeout(url, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        prefer: "resolution=merge-duplicates,return=minimal",
        apikey: env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY,
        authorization: `Bearer ${env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY}`,
      },
      body: JSON.stringify({ key, value: JSON.stringify(snapshot), updated_at: snapshot.updatedAt }),
    });
  } catch {
    // Memory persistence is best-effort and must not turn a successful model call into a failure.
  }
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
  try {
    return await request.json();
  } catch {
    throw new Error("Request body must be valid JSON");
  }
}

async function mcp(request: Request, env: Env) {
  const body = await parseJson(request);
  const id = body?.id ?? null;
  const origin = allowedOrigin(request, env);
  if (body?.method === "notifications/initialized") return new Response(null, { status: 202, headers: responseHeaders(origin) });
  if (body?.method === "initialize") {
    return json({ jsonrpc: "2.0", id, result: { protocolVersion: "2025-06-18", capabilities: { tools: {} }, serverInfo: { name: "SohailOS Cloudflare Gateway", version: VERSION } } }, 200, origin);
  }
  if (body?.method === "tools/list") {
    return json({ jsonrpc: "2.0", id, result: { tools: [{ name: "sohailos_agent_run", description: "Run a request through the SohailOS agent runtime facade.", inputSchema: { type: "object", properties: { prompt: { type: "string" }, memoryKey: { type: "string" } }, required: ["prompt"] } }] } }, 200, origin);
  }
  if (body?.method === "tools/call") {
    const name = body?.params?.name;
    if (name !== "sohailos_agent_run") return json({ jsonrpc: "2.0", id, error: { code: -32602, message: "Unknown tool" } }, 400, origin);
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

function isClientError(message: string): boolean {
  return message === "Request body must be valid JSON" || message === "prompt is required" || message.startsWith("prompt exceeds");
}

function isProviderConfigurationError(message: string): boolean {
  return message === "No AI provider is configured. Add at least one provider secret.";
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const origin = allowedOrigin(request, env);
    if (request.method === "OPTIONS") return new Response(null, { status: 204, headers: responseHeaders(origin) });
    const url = new URL(request.url);

    if (url.pathname === "/" && request.method === "GET") {
      return json({ service: "SohailOS Cloudflare Gateway", version: VERSION, status: "ok", endpoints: { health: "/health", agent: "/v1/agent/run", mcp: "/mcp" } }, 200, origin);
    }

    if (url.pathname === "/health" && request.method === "GET") {
      const memoryConfigured = Boolean(env.SOHAILOS_SUPABASE_URL && env.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY);
      return json({ service: "SohailOS Cloudflare Gateway", version: VERSION, status: "ok", memory: memoryConfigured ? "supabase" : "none", providerConfigured: Boolean(env.SOHAILOS_OPENAI_API_KEY || env.SOHAILOS_GEMINI_API_KEY || env.SOHAILOS_ANTHROPIC_API_KEY) }, 200, origin);
    }

    if (!authorized(request, env)) return json({ error: "Unauthorized" }, 401, origin);

    try {
      if (url.pathname === "/v1/agent/run" && request.method === "POST") return json(await runAgent(await parseJson(request) as AgentRequest, env), 200, origin);
      if (url.pathname === "/mcp" && request.method === "POST") return await mcp(request, env);
      return json({ error: "Not found" }, 404, origin);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Internal error";
      const status = isClientError(message) ? 400 : isProviderConfigurationError(message) ? 503 : 500;
      return json({ error: message }, status, origin);
    }
  },
};
