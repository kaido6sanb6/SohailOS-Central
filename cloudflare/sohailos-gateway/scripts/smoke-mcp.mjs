const baseUrl = (process.env.SOHAILOS_GATEWAY_URL ?? "").replace(/\/$/, "");
const token = process.env.SOHAILOS_GATEWAY_TOKEN ?? "";
const prompt = process.env.SOHAILOS_SMOKE_PROMPT ?? "Reply with exactly: SOHAILOS_MCP_OK";

if (!baseUrl) throw new Error("SOHAILOS_GATEWAY_URL is required");
if (!token) throw new Error("SOHAILOS_GATEWAY_TOKEN is required");

async function rpc(method, params, id = 1, authorization = token) {
  const headers = {
    "content-type": "application/json",
    accept: "application/json, text/event-stream",
  };
  if (authorization) headers.authorization = `Bearer ${authorization}`;
  const response = await fetch(`${baseUrl}/mcp`, {
    method: "POST",
    headers,
    body: JSON.stringify({ jsonrpc: "2.0", id, method, ...(params ? { params } : {}) }),
  });
  const text = await response.text();
  if (!response.ok) throw new Error(`${method} failed with HTTP ${response.status}: ${text}`);
  if (!text) return null;
  return JSON.parse(text);
}

async function assertPublicDiscovery() {
  const initialized = await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "sohailos-ci-smoke", version: "1.0.0" },
  }, 1, "");
  if (initialized?.result?.serverInfo?.name !== "SohailOS Cloudflare Gateway") {
    throw new Error(`Unexpected initialize response: ${JSON.stringify(initialized)}`);
  }

  const tools = await rpc("tools/list", undefined, 2, "");
  const advertised = tools?.result?.tools ?? [];
  const conformanceTool = advertised.find((item) => item?.name === "sohailos_conformance_ping");
  const agentTool = advertised.find((item) => item?.name === "sohailos_agent_run");
  if (!conformanceTool || !agentTool) throw new Error(`Required MCP tools not advertised: ${JSON.stringify(tools)}`);

  const ping = await rpc("tools/call", { name: "sohailos_conformance_ping", arguments: {} }, 3, "");
  const pingText = ping?.result?.content?.find((item) => item?.type === "text")?.text ?? "";
  if (pingText !== "SOHAILOS_MCP_CONFORMANCE_OK") throw new Error(`Conformance ping failed: ${JSON.stringify(ping)}`);

  return { initialized, tools, conformanceTool, agentTool };
}

const discovery = await assertPublicDiscovery();

const call = await rpc("tools/call", {
  name: "sohailos_agent_run",
  arguments: { prompt, memoryKey: "ci-smoke" },
}, 4, token);
const errorText = call?.result?.isError
  ? call?.result?.content?.find((item) => item?.type === "text")?.text ?? "MCP tool execution failed"
  : "";
if (errorText) throw new Error(`tools/call returned an MCP execution error: ${errorText}`);
const text = call?.result?.content?.find((item) => item?.type === "text")?.text ?? "";
if (!text.trim()) throw new Error(`tools/call returned no text: ${JSON.stringify(call)}`);

console.log(JSON.stringify({
  status: "ok",
  protocol: discovery.initialized.result.protocolVersion,
  tools: discovery.tools.result.tools.map((item) => item.name),
  tool: discovery.agentTool.name,
  provider: call?.result?.structuredContent?.provider ?? "unknown",
  responsePreview: text.slice(0, 200),
}));
