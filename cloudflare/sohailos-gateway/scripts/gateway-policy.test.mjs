import { strict as assert } from "node:assert";
import { authorizeToolCall, attestGatewayTool } from "../src/gateway-policy.ts";

const tool = {
  name: "sohailos_agent_run",
  description: "agent",
  inputSchema: { type: "object", properties: { prompt: { type: "string" } }, required: ["prompt"] }
};

const attestation = await attestGatewayTool(tool);
assert.equal(authorizeToolCall(tool.name, attestation).allowed, true);
assert.equal(authorizeToolCall(tool.name, undefined).allowed, false);
assert.equal(authorizeToolCall("unknown", attestation).allowed, false);
console.log("gateway policy tests passed");
