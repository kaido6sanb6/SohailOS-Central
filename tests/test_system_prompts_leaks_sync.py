import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import scripts.sync_system_prompts_leaks as sync


class SystemPromptsLeaksSyncTests(unittest.TestCase):
    def test_classification(self):
        self.assertEqual(sync.classify("OpenAI/README.md"), "readme")
        self.assertEqual(
            sync.classify("Anthropic/claude-code/skills/foo/SKILL.md"), "skill"
        )
        self.assertEqual(
            sync.classify("Anthropic/claude-code/commands/compact.md"), "command"
        )
        self.assertEqual(
            sync.classify("Anthropic/claude-code/agents/Plan.md"), "agent"
        )
        self.assertEqual(
            sync.classify("OpenAI/Codex/gpt-5.6.md"), "prompt-or-reference"
        )

    def test_path_traversal_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(RuntimeError):
                sync.safe_local_path(Path(temp), "../outside.md")

    def test_sync_mirrors_content_and_records_provenance(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "mirror"
            manifest = output / "index.json"
            commit = {"sha": "commit123"}
            tree = {
                "sha": "tree123",
                "truncated": False,
                "tree": [
                    {"path": "OpenAI/gpt-test.md", "type": "blob", "sha": "blob123", "size": 14},
                    {"path": "OpenAI/README.md", "type": "blob", "sha": "readme123", "size": 8},
                ],
            }
            with patch.object(sync, "request_json", side_effect=[commit, tree]), patch.object(
                sync, "request_text", return_value="# prompt\n"
            ), patch.object(
                sys,
                "argv",
                [
                    "sync_system_prompts_leaks.py",
                    "--output-dir",
                    str(output),
                    "--manifest",
                    str(manifest),
                ],
            ):
                self.assertEqual(sync.main(), 0)

            self.assertEqual(
                (output / "OpenAI/gpt-test.md").read_text(encoding="utf-8"),
                "# prompt\n",
            )
            data = json.loads(manifest.read_text(encoding="utf-8"))
            self.assertEqual(data["source"]["commit"], "commit123")
            self.assertEqual(data["source"]["tree"], "tree123")
            self.assertEqual(data["mirror"]["trust"], "untrusted-data")
            self.assertFalse(data["mirror"]["instruction_authority"])
            self.assertEqual(len(data["files"]), 2)

    def test_removed_upstream_document_is_removed_only_from_managed_mirror(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "mirror"
            output.mkdir(parents=True)
            old = output / "Old.md"
            old.write_text("old\n", encoding="utf-8")
            manifest = output / "index.json"
            manifest.write_text(
                json.dumps({"files": [{"source_path": "Old.md", "local_path": "Old.md"}]}),
                encoding="utf-8",
            )
            with patch.object(sync, "request_json", side_effect=[{"sha": "commit123"}, {"sha": "tree123", "truncated": False, "tree": []}]), patch.object(
                sys,
                "argv",
                [
                    "sync_system_prompts_leaks.py",
                    "--output-dir",
                    str(output),
                    "--manifest",
                    str(manifest),
                ],
            ):
                self.assertEqual(sync.main(), 0)

            self.assertFalse(old.exists())


if __name__ == "__main__":
    unittest.main()
