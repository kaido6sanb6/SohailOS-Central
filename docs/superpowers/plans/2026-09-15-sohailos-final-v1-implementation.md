# SohailOS Final V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining V1 gaps, remove inconsistencies, and prove the deployed SohailOS gateway works end-to-end without adding duplicate or decorative integrations.

**Architecture:** Keep GitHub as source of truth and the existing 12-module Master Orchestrator architecture. The Cloudflare Worker remains the remote authenticated gateway; provider credentials and runtime tokens remain account-owned secrets; Supabase remains the remote memory boundary; MCP remains the remote-client contract.

**Tech Stack:** C#/.NET/WPF, ASP.NET Core, TypeScript, Cloudflare Workers/Wrangler, Supabase/PostgREST, GitHub Actions, xUnit, MCP JSON-RPC.

**Spec:** `docs/superpowers/specs/2026-09-15-sohailos-final-v1-architecture-design.md`

## Global Constraints

- Duplicate plugin names are ignored and no decorative dependency is added.
- The canonical modules remain THINK, SOCIOLOGY, CINEMA, RESEARCH, STATS, AI, CODE, PRODUCT, OFFICE, OPERATIONS, STRATEGY, LEARNING.
- No credential, API key, token, or secret may be committed or pasted into ChatGPT.
- Protected gateway routes require bearer authentication.
- Provider execution and request input remain bounded.
- Provider errors must be sanitized before returning to clients.
- Local JSON remains the development fallback; Supabase/PostgREST remains the remote persistence boundary.
- V1 completion requires successful live `/health`, `/`, authenticated `/v1/agent/run`, MCP initialize/tools/list/tools/call, and one remote-client end-to-end invocation where the client exposes remote MCP support.

---

### Task 1: Reconcile repository documentation and configuration

**Files:**
- Modify: `README.md`
- Modify: `docs/IMPLEMENTATION_STATUS.md`
- Modify: `cloudflare/sohailos-gateway/DEPLOYMENT.md`
- Inspect: `cloudflare/sohailos-gateway/wrangler.jsonc`
- Inspect: `.github/workflows/deploy-cloudflare.yml`

**Interfaces:**
- Consumes: the architecture spec and current `main` deployment contract.
- Produces: documentation that describes only the actual runtime and current V1 completion gates.

- [ ] **Step 1: Write the documentation consistency checks**

Verify that every documented endpoint, Worker name, deployment URL, runtime secret name, and completion criterion matches the current source. Record mismatches before editing.

- [ ] **Step 2: Run repository searches for stale names and malformed configuration**

Search for `sohailos-central`, `SOHAILOS_SUPABASE_URLl`, old Worker names, obsolete deployment URLs, and duplicate module lists. Any stale reference is either corrected or explicitly marked as historical evidence.

- [ ] **Step 3: Update documentation to the final contract**

Document the four gateway endpoints, the authenticated MCP contract, required runtime secrets, and the exact distinction between deployment secrets and Worker runtime secrets. Do not include secret values.

- [ ] **Step 4: Re-read the edited files**

Confirm that documentation does not claim provider configuration, authenticated MCP success, or end-to-end ChatGPT success until those tests actually pass.

- [ ] **Step 5: Commit**

Commit as `docs: reconcile final V1 runtime contract`.

---

### Task 2: Strengthen automated gateway contract tests

**Files:**
- Modify/Create: `cloudflare/sohailos-gateway/scripts/smoke-mcp.mjs`
- Modify: `.github/workflows/deploy-cloudflare.yml`
- Test: `cloudflare/sohailos-gateway/scripts/smoke-mcp.mjs`

**Interfaces:**
- Consumes: live Worker URL and `SOHAILOS_GATEWAY_TOKEN` GitHub Actions secret.
- Produces: deterministic failures for authentication, MCP lifecycle, tool discovery, and tool invocation.

- [ ] **Step 1: Define explicit assertions for each MCP lifecycle stage**

The smoke test must fail if `initialize` is not HTTP 200, if the response is not JSON-RPC, if `serverInfo.name` is wrong, if `tools/list` does not contain `sohailos_agent_run`, or if `tools/call` does not return the expected marker.

- [ ] **Step 2: Add authenticated REST agent smoke coverage**

Add a non-sensitive prompt such as `Reply with exactly: SOHAILOS_AGENT_OK` to `/v1/agent/run`. Assert HTTP success and a response containing the expected text. Never print authorization headers or provider credentials.

- [ ] **Step 3: Run the script locally against a test endpoint when credentials are available**

Run the script with environment variables supplied by the secure environment, not source files. Expected result is a nonzero exit for a bad token and zero exit for a valid token with a configured provider.

- [ ] **Step 4: Wire both smoke tests into deployment CI**

Keep public endpoint tests first, authenticated REST testing second, and MCP testing third. Deployment must not be declared successful if any contract test fails.

- [ ] **Step 5: Commit**

Commit as `test: verify authenticated gateway contracts`.

---

### Task 3: Harden provider selection and end-to-end execution

**Files:**
- Modify: `cloudflare/sohailos-gateway/src/index.ts`
- Test: `cloudflare/sohailos-gateway/scripts/smoke-agent.mjs`
- Inspect: provider configuration in Worker runtime only; never commit secret values.

**Interfaces:**
- Consumes: `POST /v1/agent/run`, provider abstraction, configured runtime provider secret.
- Produces: deterministic provider selection, bounded execution, and a stable agent response contract.

- [ ] **Step 1: Add a regression test for configured-provider selection**

Use a non-sensitive test prompt and assert that the runtime does not return `provider_not_configured` when a provider secret is configured.

- [ ] **Step 2: Verify timeout and error behavior**

Confirm provider calls use the configured bounded timeout and that upstream response bodies are never copied wholesale into gateway errors.

- [ ] **Step 3: Verify response shape**

The smoke test must accept only the documented success shape and expected text; it must reject HTML, raw provider error dumps, and malformed JSON.

- [ ] **Step 4: Run type checking and smoke tests**

Run `npm install`, `npm run typecheck`, `npx wrangler deploy --dry-run`, and the authenticated smoke tests in the same environment used by CI.

- [ ] **Step 5: Commit**

Commit as `feat: finalize provider execution contract`.

---

### Task 4: Verify persistent memory without exposing secrets

**Files:**
- Inspect: `cloudflare/sohailos-gateway/src/index.ts`
- Inspect: existing Supabase schema/migration files.
- Test: add a non-sensitive memory round-trip smoke check only if the existing API exposes a safe route for it.

**Interfaces:**
- Consumes: memory key, bounded context, Supabase/PostgREST configuration.
- Produces: verified load-before-execution and save-after-execution behavior.

- [ ] **Step 1: Verify the memory schema against the Worker persistence code**

Confirm table and column names, HTTP method, authorization mode, and serialization format agree exactly.

- [ ] **Step 2: Verify failure isolation**

Confirm a Supabase timeout or non-2xx response cannot leak the service-role key or crash an otherwise valid agent request.

- [ ] **Step 3: Execute a non-sensitive memory round trip**

Use a disposable key and harmless marker text. Verify the second request can retrieve the marker only when the configured runtime actually has working Supabase credentials.

- [ ] **Step 4: Remove any temporary test data**

Delete or overwrite the disposable memory key using the supported persistence mechanism. Do not leave personal data in test fixtures.

- [ ] **Step 5: Commit**

Commit as `test: verify persistent memory boundary` only if repository changes are required.

---

### Task 5: Validate security boundaries and secret hygiene

**Files:**
- Modify: `cloudflare/sohailos-gateway/src/index.ts` only if a verified gap exists.
- Modify: `.gitignore` only if a verified secret/artifact gap exists.
- Test: gateway smoke/security checks.

**Interfaces:**
- Consumes: HTTP gateway routes and runtime configuration.
- Produces: verified bearer auth, CORS restrictions, request limits, security headers, timeout bounds, and sanitized errors.

- [ ] **Step 1: Test unauthenticated access to protected routes**

Expect HTTP 401 from `/v1/agent/run` and `/mcp` without a valid bearer token.

- [ ] **Step 2: Test malformed and oversized requests**

Expect HTTP 400 for malformed JSON and bounded rejection for prompts or memory keys beyond configured limits.

- [ ] **Step 3: Test origin behavior**

Confirm allowed origins receive the configured CORS headers and disallowed origins do not receive permissive wildcard behavior when an allow-list is configured.

- [ ] **Step 4: Search the repository for credential-like material**

Search source, workflow files, documentation, and history-visible current files for known secret variable names and token-shaped accidental values. Do not reproduce any discovered secret in output.

- [ ] **Step 5: Commit**

Commit as `security: verify V1 gateway boundaries` only when code/config changes are actually required.

---

### Task 6: Execute deployment and live verification

**Files:**
- No source changes unless a preceding test identifies a defect.
- Inspect: `.github/workflows/deploy-cloudflare.yml`.

**Interfaces:**
- Consumes: GitHub Actions deployment secrets and Cloudflare Worker runtime secrets configured outside source control.
- Produces: a live deployed Worker passing all automated V1 gates.

- [ ] **Step 1: Configure runtime token alignment outside the repository**

Set the same intended bearer token in the GitHub Actions secret `SOHAILOS_GATEWAY_TOKEN` and the Cloudflare Worker runtime secret `SOHAILOS_GATEWAY_TOKEN`. Never send the value in ChatGPT.

- [ ] **Step 2: Configure at least one AI provider outside the repository**

Set exactly one preferred provider secret initially, for example `SOHAILOS_OPENAI_API_KEY`, and optionally set `SOHAILOS_AI_PROVIDER` and its model override. Keep the key only in the provider/Cloudflare secret store.

- [ ] **Step 3: Confirm Supabase runtime values outside the repository**

Verify `SOHAILOS_SUPABASE_URL` and `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY` are correct in the Worker runtime. Remove the malformed `SOHAILOS_SUPABASE_URLl` configuration if it still exists.

- [ ] **Step 4: Trigger deployment**

Run the Cloudflare deployment workflow from `main` after the runtime configuration is ready.

- [ ] **Step 5: Inspect every deployment step**

Require success for dependency installation, typecheck, Wrangler dry-run, Worker deployment, `/health`, `/`, authenticated `/v1/agent/run`, MCP initialize, `tools/list`, and `tools/call`.

- [ ] **Step 6: Record the deployment version and verification result**

Update implementation status with the actual successful run ID and Worker version only after verifying the logs.

- [ ] **Step 7: Commit documentation update**

Commit as `docs: record verified V1 deployment`.

---

### Task 7: Remote MCP client verification

**Files:**
- Modify: `docs/IMPLEMENTATION_STATUS.md` only to record the result.
- Inspect: `docs/` MCP integration documentation.

**Interfaces:**
- Consumes: deployed `POST /mcp` endpoint and authenticated MCP contract.
- Produces: one verified remote-client invocation or an explicit client-side availability blocker.

- [ ] **Step 1: Register the remote MCP endpoint in the supported client**

Use the client UI's remote MCP/app integration facility with the deployed endpoint. Do not put credentials into repository files or ChatGPT text.

- [ ] **Step 2: Complete MCP initialization and tool discovery**

Confirm the client sees `sohailos_agent_run` and can call it.

- [ ] **Step 3: Execute a harmless end-to-end request**

Use a non-sensitive prompt that returns a deterministic marker. Confirm the response came through the SohailOS gateway.

- [ ] **Step 4: Record the result accurately**

If the client feature is unavailable on the user's plan/workspace, record it as a client availability constraint rather than a server defect.

- [ ] **Step 5: Commit final status**

Commit as `docs: record remote MCP verification`.

---

### Task 8: Final verification and completion gate

**Files:**
- Inspect all changed files and final `main` commit.
- Modify: `docs/IMPLEMENTATION_STATUS.md` only if the evidence requires a final status correction.

**Interfaces:**
- Consumes: all prior test evidence and deployment logs.
- Produces: an evidence-backed V1 completion decision.

- [ ] **Step 1: Run the complete CI-equivalent validation**

Require typecheck, dry-run, public smoke tests, authenticated REST smoke test, and authenticated MCP smoke test to pass.

- [ ] **Step 2: Verify the live Worker manually**

Confirm `/health` reports `providerConfigured:true`, `/` reports the expected service metadata, and authenticated execution succeeds.

- [ ] **Step 3: Verify MCP lifecycle**

Confirm initialize, initialized notification handling, tools/list, and tools/call all succeed.

- [ ] **Step 4: Check repository consistency**

Search for stale Worker names, malformed secret names, duplicate canonical modules, placeholder text, and documentation claims unsupported by evidence.

- [ ] **Step 5: Review the final diff**

Ensure only necessary changes remain and no unrelated plugin/dependency was introduced.

- [ ] **Step 6: Make the completion decision**

Declare V1 `100%` only if every completion criterion in the architecture spec has evidence. Otherwise report the exact remaining blocker and percentage.

- [ ] **Step 7: Commit the final evidence**

Commit as `chore: finalize SohailOS V1 verification`.
