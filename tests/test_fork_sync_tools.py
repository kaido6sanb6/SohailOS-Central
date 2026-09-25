import importlib.util
from pathlib import Path
from unittest import TestCase
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]

def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module

class ForkSyncToolTests(TestCase):
    def test_classify_strictly_behind_is_eligible(self):
        tool = load("fork_ecosystem")
        record = {"full_name": "owner/fork", "upstream": "upstream/repo", "archived": False}
        result = {"status": "ok", "ahead_by": 0, "behind_by": 3}
        self.assertEqual(tool.classify(record, result), ("upstream_sync", True, "strictly behind upstream with no local divergence"))

    def test_classify_divergence_is_quarantined(self):
        tool = load("fork_ecosystem")
        record = {"full_name": "owner/fork", "upstream": "upstream/repo", "archived": False}
        result = {"status": "ok", "ahead_by": 1, "behind_by": 2}
        status, auto, _ = tool.classify(record, result)
        self.assertEqual(status, "quarantine")
        self.assertFalse(auto)

    def test_classify_missing_provenance_is_blocked(self):
        tool = load("fork_ecosystem")
        status, auto, reason = tool.classify({"full_name": "owner/fork"}, {"status": "ok"})
        self.assertEqual(status, "blocked")
        self.assertFalse(auto)
        self.assertIn("provenance", reason)

    def test_build_never_marks_observe_as_mutation(self):
        tool = load("fork_ecosystem")
        inventory = {"forks": [{"full_name": "owner/fork", "upstream": "upstream/repo", "default_branch": "main"}]}
        report = {"results": [{"repo": "owner/fork", "status": "ok", "ahead_by": 2, "behind_by": 0,
                               "upstream_sha": "u", "target_sha": "t"}]}
        policy = {"policy_id": "test-policy", "hub": "owner/hub"}
        plan = tool.build(inventory, report, policy)
        self.assertFalse(plan["mutation"])
        self.assertEqual(plan["actions"][0]["operation"], "OBSERVE")
        self.assertFalse(plan["actions"][0]["auto_apply"])

class ForkApplyTests(TestCase):
    def test_apply_requires_explicit_automation_flag(self):
        tool = load("apply_fork_plan")
        with patch.dict("os.environ", {"SOHAILOS_AUTOMATION_ENABLED": "false"}, clear=False):
            with patch("builtins.print") as printed:
                result = tool.main()
        self.assertEqual(result, 0)
        printed.assert_called()

if __name__ == "__main__":
    import unittest
    unittest.main()
