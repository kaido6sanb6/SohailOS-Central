#!/usr/bin/env python3
"""Verify canonical SuperPrompt XLM synchronization across runtime artifacts."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
canonical = (ROOT / "prompts" / "SohailOS-SuperPrompt.xlm").read_text(encoding="utf-8").strip()
assert canonical.startswith("<SO>") and canonical.endswith("</SO>")
assert len(canonical) <= 1500, f"canonical prompt exceeds 1500 characters: {len(canonical)}"
digest = hashlib.sha256(canonical.encode()).hexdigest()

csharp = (ROOT / "src" / "SohailOS.Core" / "CanonicalPrompt.cs").read_text(encoding="utf-8")
match = re.search(r'public const string Sha256 = "([0-9a-f]{64})";', csharp)
assert match and match.group(1) == digest, "C# prompt digest drift"
prefix = '    public const string Compact = '
start = csharp.find(prefix)
assert start >= 0, "C# compact prompt constant missing"
start += len(prefix)
end = csharp.find('";', start)
assert end > start, "C# compact prompt terminator missing"
csharp_prompt = json.loads(csharp[start:end+1])
assert csharp_prompt == canonical, "C# prompt text drift"

ts = (ROOT / "cloudflare" / "sohailos-gateway" / "src" / "canonical-prompt.ts").read_text(encoding="utf-8")
match = re.search(r'CANONICAL_SUPERPROMPT_SHA256 = "([0-9a-f]{64})"', ts)
assert match and match.group(1) == digest, "Cloudflare prompt digest drift"
prefix = 'export const CANONICAL_SUPERPROMPT = '
start = ts.find(prefix)
assert start >= 0, "Cloudflare canonical prompt constant missing"
start += len(prefix)
end = ts.find('" as const;', start)
assert end > start, "Cloudflare canonical prompt terminator missing"
ts_prompt = json.loads(ts[start:end+1])
assert ts_prompt == canonical, "Cloudflare prompt text drift"

worker = (ROOT / "cloudflare" / "sohailos-gateway" / "src" / "index.ts").read_text(encoding="utf-8")
assert 'CANONICAL_SUPERPROMPT' in worker and 'CANONICAL_SUPERPROMPT_SHA256' in worker, "Worker does not consume canonical prompt artifact"

print(f"PASS prompt synchronization: {len(canonical)} chars; sha256={digest}")
