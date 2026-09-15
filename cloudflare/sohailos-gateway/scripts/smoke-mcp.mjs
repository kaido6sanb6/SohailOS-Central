const baseUrl = (process.env.SOHAILOS_GATEWAY_URL ?? "").replace(/\/$/, "");
const token = process.env.SOHAILOS_GATEWAY_TOKEN ?? "";
const prompt = process.env.SOHAILOS_SMOKE_PROMPT ?? "Reply with exactly: SOHAILOS_MCP_OK";

if (!baseUrl) throw new Error("SOHAILOS_GATEWAY_URL is required");
if (!token) throw new Error("SOHAILOS_GATEWAY_TOKEN is required");

async function rpc(method, params, id = 1) {
  const response = await fetch(`${baseUrl}/mcp`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${token}`,
      "content-type": "application/json",
      accept: "application/json, text/event-stream",
    },
    body: JSON.stringify({ jsonrpc: "2.0", id, method, ...(params ? { params } : {}) }),
  });
  const text = await response.text();
  if (!response.ok) throw new Error(`${method} failed with HTTP ${response.status}: ${text}`);
  if (!text) return null;
  return JSON.parse(text);
}

const initialized = await rpc("initialize", {
  protocolVersion: "2025-06-18",
  capabilities: {},
  clientInfo: { name: "sohailos-ci-smoke", version: "1.0.0" },
}, 1);
if (initialized?.result?.serverInfo?.name !== "SohailOS Cloudflare Gateway") {
  throw new Error(`Unexpected initialize response: ${JSON.stringify(initialized)}`);
}

const tools = await rpc("tools/list", undefined, 2);
const tool = tools?.result?.tools?.find((item) => item?.name === "sohailos_agent_run");
if (!tool) throw new Error(`sohailos_agent_run not advertised: ${JSON.stringify(tools)}`);

const call = await rpc("tools/call", {
  name: "sohailos_agent_run",
  arguments: { prompt, memoryKey: "ci-smoke" },
}, 3);
const text = call?.result?.content?.find((item) => item?.type === "text")?.text ?? "";
if (!text.trim()) throw new Error(`tools/call returned no text: ${JSON.stringify(call)}`);

console.log(JSON.stringify({
  status: "ok",
  protocol: initialized.result.protocolVersion,
  tool: tool.name,
  provider: call?.result?.structuredContent?.provider ?? "unknown",
  responsePreview: text.slice(0, 200),
}));
