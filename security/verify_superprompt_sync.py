#!/usr/bin/env python3
"""Verify canonical SuperPrompt XLM synchronization across runtime artifacts."""
from pathlib import Path
import hashlib
import re

ROOT = Path(__file__).resolve().parents[1]
canonical = (ROOT / "prompts" / "SohailOS-SuperPrompt.xlm").read_text(encoding="utf-8").strip()
assert canonical.startswith("<SO>") and canonical.endswith("</SO>")
assert len(canonical) <= 1500, f"canonical prompt exceeds 1500 characters: {len(canonical)}"
digest = hashlib.sha256(canonical.encode()).hexdigest()

csharp = (ROOT / "src" / "SohailOS.Core" / "CanonicalPrompt.cs").read_text(encoding="utf-8")
match = re.search(r'public const string Sha256 = "([0-9a-f]{64})";', csharp)
assert match and match.group(1) == digest, "C# prompt digest drift"
m = re.search(r'public const string Compact = "(.*)";', csharp)
assert m, "C# compact prompt constant missing"
csharp_prompt = bytes(m.group(1), "utf-8").decode("unicode_escape")
assert csharp_prompt == canonical, "C# prompt text drift"

ts = (ROOT / "cloudflare" / "sohailos-gateway" / "src" / "canonical-prompt.ts").read_text(encoding="utf-8")
match = re.search(r'CANONICAL_SUPERPROMPT_SHA256 = "([0-9a-f]{64})"', ts)
assert match and match.group(1) == digest, "Cloudflare prompt digest drift"
m = re.search(r'CANONICAL_SUPERPROMPT = "((?:\\.|[^"\\])*)" as const;', ts)
assert m, "Cloudflare canonical prompt constant missing"
ts_prompt = bytes(m.group(1), "utf-8").decode("unicode_escape")
assert ts_prompt == canonical, "Cloudflare prompt text drift"

worker = (ROOT / "cloudflare" / "sohailos-gateway" / "src" / "index.ts").read_text(encoding="utf-8")
assert 'CANONICAL_SUPERPROMPT' in worker and 'CANONICAL_SUPERPROMPT_SHA256' in worker, "Worker does not consume canonical prompt artifact"

print(f"PASS prompt synchronization: {len(canonical)} chars; sha256={digest}")
