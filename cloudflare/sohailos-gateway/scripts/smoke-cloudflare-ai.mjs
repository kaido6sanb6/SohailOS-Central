const baseUrl = (process.env.SOHAILOS_GATEWAY_URL ?? "").replace(/\/$/, "");
const token = process.env.SOHAILOS_GATEWAY_TOKEN ?? "";

if (!baseUrl) throw new Error("SOHAILOS_GATEWAY_URL is required");
if (!token) throw new Error("SOHAILOS_GATEWAY_TOKEN is required");

const response = await fetch(`${baseUrl}/mcp`, {
  method: "POST",
  headers: {
    "content-type": "application/json",
    accept: "application/json, text/event-stream",
    authorization: `Bearer ${token}`,
  },
  body: JSON.stringify({
    jsonrpc: "2.0",
    id: 1,
    method: "tools/call",
    params: {
      name: "sohailos_agent_run",
      arguments: {
        prompt: "Reply with exactly: SOHAILOS_CLOUDFLARE_AI_OK",
        memoryKey: "ci-cloudflare-ai",
      },
    },
  }),
});

const text = await response.text();
if (!response.ok) throw new Error(`MCP request failed with HTTP ${response.status}: ${text}`);
const payload = JSON.parse(text);
if (payload?.result?.isError) throw new Error(payload.result.content?.find((item) => item?.type === "text")?.text ?? "MCP tool returned an execution error");

const output = payload?.result?.content?.find((item) => item?.type === "text")?.text ?? "";
const provider = payload?.result?.structuredContent?.provider ?? "";
if (provider !== "cloudflare") throw new Error(`Expected Cloudflare provider, got: ${provider || "unknown"}`);
if (!output.includes("SOHAILOS_CLOUDFLARE_AI_OK")) throw new Error(`Unexpected AI output: ${output.slice(0, 500)}`);

console.log(JSON.stringify({ status: "ok", provider, responsePreview: output.slice(0, 200) }));
