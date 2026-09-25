import { strict as assert } from "node:assert";
import { authorizeToolCall, attestGatewayTool } from "../src/gateway-policy.ts";

const tool = {
  name: "sohailos_agent_run",
  description: "agent",
  inputSchema: { type: "object", properties: { prompt: { type: "string" } }, required: ["prompt"] }
};

const attestation = await attestGatewayTool(tool);
assert.equal((await authorizeToolCall(tool.name, tool, attestation)).allowed, true);
assert.equal((await authorizeToolCall(tool.name, tool, undefined)).allowed, false);
assert.equal((await authorizeToolCall("unknown", tool, attestation)).allowed, false);

const changedTool = { ...tool, description: "changed" };
const changed = await authorizeToolCall(tool.name, changedTool, attestation);
assert.equal(changed.allowed, false);
assert.equal(changed.code, "CAPABILITY_SCHEMA_MISMATCH");

const incomplete = { ...attestation, permissions: [] };
const incompleteResult = await authorizeToolCall(tool.name, tool, incomplete);
assert.equal(incompleteResult.allowed, false);
assert.equal(incompleteResult.code, "CAPABILITY_ATTESTATION_INCOMPLETE");

console.log("gateway policy tests passed");
