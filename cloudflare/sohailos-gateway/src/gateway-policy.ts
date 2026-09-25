export type GatewayCapabilityAttestation = {
  capability_id: string;
  version: string;
  schema_fingerprint: string;
  permissions: string[];
  evidence_digest: string;
  issued_at: string;
  expires_at: string;
};

function canonicalTool(tool: any): string {
  return JSON.stringify({
    name: tool?.name ?? "",
    description: tool?.description ?? "",
    inputSchema: tool?.inputSchema ?? {}
  });
}

async function sha256(value: string): Promise<string> {
  const bytes = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, "0")).join("");
}

export async function attestGatewayTool(
  tool: any,
  now = Date.now(),
  ttlMs = 10 * 60 * 1000
): Promise<GatewayCapabilityAttestation> {
  if (!tool?.name) throw new Error("Cannot attest an unnamed gateway tool.");
  if (!Number.isFinite(ttlMs) || ttlMs <= 0) throw new Error("Attestation TTL must be positive.");

  const schemaFingerprint = await sha256(canonicalTool(tool));
  const issued = new Date(now).toISOString();
  const expires = new Date(now + ttlMs).toISOString();
  return {
    capability_id: `mcp:${String(tool.name).toLowerCase()}`,
    version: schemaFingerprint.slice(0, 12),
    schema_fingerprint: schemaFingerprint,
    permissions: ["gateway:mcp-call"],
    evidence_digest: await sha256(`${schemaFingerprint}\n${issued}`),
    issued_at: issued,
    expires_at: expires
  };
}

export async function authorizeToolCall(
  toolName: string,
  tool: any,
  attestation: GatewayCapabilityAttestation | undefined,
  now = Date.now()
): Promise<{ allowed: boolean; code: string }> {
  if (!attestation) return { allowed: false, code: "CAPABILITY_ATTESTATION_REQUIRED" };
  if (attestation.capability_id !== `mcp:${toolName.toLowerCase()}`) {
    return { allowed: false, code: "CAPABILITY_ID_MISMATCH" };
  }

  const expiry = Date.parse(attestation.expires_at);
  const issued = Date.parse(attestation.issued_at);
  if (!Number.isFinite(issued) || !Number.isFinite(expiry) || issued > now || expiry <= now) {
    return { allowed: false, code: "CAPABILITY_ATTESTATION_STALE" };
  }

  if (!attestation.schema_fingerprint ||
      !attestation.evidence_digest ||
      !Array.isArray(attestation.permissions) ||
      attestation.permissions.length === 0) {
    return { allowed: false, code: "CAPABILITY_ATTESTATION_INCOMPLETE" };
  }

  const actualFingerprint = await sha256(canonicalTool(tool));
  if (actualFingerprint !== attestation.schema_fingerprint) {
    return { allowed: false, code: "CAPABILITY_SCHEMA_MISMATCH" };
  }

  const expectedEvidenceDigest = await sha256(`${attestation.schema_fingerprint}\n${attestation.issued_at}`);
  if (expectedEvidenceDigest !== attestation.evidence_digest) {
    return { allowed: false, code: "CAPABILITY_EVIDENCE_MISMATCH" };
  }

  return { allowed: true, code: "CAPABILITY_ATTESTED" };
}
