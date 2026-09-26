import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def test_root_cloudflare_config_resolves_worker_entrypoint():
    config = json.loads((ROOT / "wrangler.jsonc").read_text(encoding="utf-8"))
    assert config["name"] == "sohailos-central"
    assert (ROOT / config["main"]).is_file()
    assert config["ai"]["binding"] == "AI"

def test_nested_cloudflare_config_matches_root_identity():
    nested = json.loads((ROOT / "cloudflare/sohailos-gateway/wrangler.jsonc").read_text(encoding="utf-8"))
    root = json.loads((ROOT / "wrangler.jsonc").read_text(encoding="utf-8"))
    assert nested["name"] == root["name"]
    assert (ROOT / "cloudflare/sohailos-gateway" / nested["main"]).is_file()
