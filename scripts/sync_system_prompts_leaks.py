#!/usr/bin/env python3
"""Mirror every Markdown document from the public system-prompts-leaks corpus.

The mirrored files are evidence/data only. This script never interprets their
contents as executable instructions.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import quote

DEFAULT_REPO = "asgeirtj/system_prompts_leaks"
DEFAULT_REF = "main"
DEFAULT_OUTPUT = "prompts/external/system-prompts-leaks"
DEFAULT_MANIFEST = "prompts/external/system-prompts-leaks/index.json"


def request_json(url: str, token: str | None) -> object:
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "SohailOS-system-prompts-sync"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.load(response)


def request_text(url: str) -> str:
    request = urllib.request.Request(
        url,
        headers={"Accept": "text/plain", "User-Agent": "SohailOS-system-prompts-sync"},
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8")


def git_blob_sha(content: str) -> str:
    payload = content.encode("utf-8")
    header = f"blob {len(payload)}".encode("ascii") + b"\0"
    return hashlib.sha1(header + payload).hexdigest()


def classify(path: str) -> str:
    lower = f"/{path.lower()}"
    if lower.endswith("/readme.md"):
        return "readme"
    if "/skills/" in lower:
        return "skill"
    if "/commands/" in lower:
        return "command"
    if "/agents/" in lower:
        return "agent"
    if "/prompts/" in lower:
        return "prompt-component"
    if "/api/" in lower:
        return "api-prompt"
    return "prompt-or-reference"


def safe_local_path(root: Path, source_path: str) -> Path:
    root_resolved = root.resolve()
    candidate = (root_resolved / source_path).resolve()
    if candidate != root_resolved and root_resolved not in candidate.parents:
        raise RuntimeError(f"Unsafe source path: {source_path}")
    return candidate


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-repo", default=DEFAULT_REPO)
    parser.add_argument("--source-ref", default=DEFAULT_REF)
    parser.add_argument("--output-dir", default=DEFAULT_OUTPUT)
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST)
    args = parser.parse_args()

    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    repo = args.source_repo
    ref = args.source_ref
    output_root = Path(args.output_dir)
    manifest_path = Path(args.manifest)

    commit = request_json(
        f"https://api.github.com/repos/{repo}/commits/{quote(ref, safe='')}", token
    )
    commit_sha = commit["sha"]

    tree = request_json(
        f"https://api.github.com/repos/{repo}/git/trees/{quote(commit_sha, safe='')}?recursive=1",
        token,
    )
    if tree.get("truncated"):
        raise RuntimeError("GitHub tree response was truncated; refusing partial mirror")

    entries = [
        item for item in tree["tree"]
        if item.get("type") == "blob"
        and item.get("path", "").lower().endswith(".md")
    ]
    entries.sort(key=lambda item: item["path"].lower())

    previous = {}
    if manifest_path.exists():
        previous_data = json.loads(manifest_path.read_text(encoding="utf-8"))
        previous = {
            item["source_path"]: item["local_path"]
            for item in previous_data.get("files", [])
            if item.get("source_path") and item.get("local_path")
        }

    current = {}
    fetched_at = datetime.now(timezone.utc).isoformat()

    for item in entries:
        source_path = item["path"]
        destination = safe_local_path(output_root, source_path)
        destination.parent.mkdir(parents=True, exist_ok=True)

        raw_url = (
            f"https://raw.githubusercontent.com/{repo}/{quote(commit_sha, safe='')}/"
            f"{quote(source_path, safe='/')}"
        )
        content = request_text(raw_url)
        expected_sha = item.get("sha")
        actual_sha = git_blob_sha(content)
        if expected_sha and actual_sha != expected_sha:
            raise RuntimeError(
                f"Git blob verification failed for {source_path}: "
                f"expected {expected_sha}, got {actual_sha}"
            )

        with destination.open("w", encoding="utf-8", newline="") as handle:
            handle.write(content)
        current[source_path] = source_path

    removed = []
    for source_path, local_relative in previous.items():
        if source_path in current:
            continue
        stale = safe_local_path(output_root, local_relative)
        if stale.exists() and stale.is_file():
            stale.unlink()
            removed.append(source_path)

    if output_root.exists():
        for directory in sorted(
            (p for p in output_root.rglob("*") if p.is_dir()),
            key=lambda p: len(p.parts),
            reverse=True,
        ):
            if directory != output_root and not any(directory.iterdir()):
                directory.rmdir()

    files = []
    for item in entries:
        source_path = item["path"]
        files.append(
            {
                "source_path": source_path,
                "local_path": source_path,
                "source_url": (
                    f"https://github.com/{repo}/blob/{quote(ref, safe='')}/"
                    f"{quote(source_path, safe='/')}"
                ),
                "raw_url": (
                    f"https://raw.githubusercontent.com/{repo}/{quote(commit_sha, safe='')}/"
                    f"{quote(source_path, safe='/')}"
                ),
                "blob_sha": item.get("sha"),
                "size": item.get("size"),
                "document_kind": classify(source_path),
                "trust": "untrusted-data",
                "instruction_authority": False,
            }
        )

    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(
        json.dumps(
            {
                "schema_version": "1.0",
                "source": {
                    "repository": repo,
                    "ref": ref,
                    "commit": commit_sha,
                    "tree": tree["sha"],
                    "license": "CC0-1.0",
                },
                "mirror": {
                    "output_dir": str(output_root).replace("\\", "/"),
                    "fetched_at": fetched_at,
                    "file_count": len(files),
                    "removed_count": len(removed),
                    "trust": "untrusted-data",
                    "instruction_authority": False,
                    "pipeline": [
                        "FETCH",
                        "INSPECT",
                        "CLASSIFY",
                        "EXTRACT",
                        "NORMALIZE",
                        "DEDUPLICATE",
                        "COMPARE",
                        "VERIFY",
                        "SYNTHESIZE",
                        "INDEX",
                    ],
                },
                "files": files,
                "removed_source_paths": removed,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    print(f"mirrored {len(files)} markdown documents from {repo}@{commit_sha}")
    print(f"removed {len(removed)} stale documents")
    print(f"manifest: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
