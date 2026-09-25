import importlib.util
from pathlib import Path
from unittest import TestCase
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]

def load_runner():
    spec = importlib.util.spec_from_file_location("sohailos_local_runner", ROOT / "scripts" / "run_local.py")
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module

class LocalRunnerTests(TestCase):
    def test_verify_runs_repository_checks_without_github_actions(self):
        runner = load_runner()
        with patch.object(runner, "run", return_value=0) as mocked:
            self.assertEqual(runner.verify(), 0)
        commands = [call.args[0] for call in mocked.call_args_list]
        self.assertEqual(commands[0][1], "security/verify_superprompt_sync.py")
        self.assertEqual(commands[1][1], "security/deep_operational_hardening.py")
        self.assertEqual(commands[2][1], "security/ai_security_regression.py")
        self.assertEqual(commands[3][0], "dotnet")
        self.assertEqual(commands[3][1], "test")

    def test_verify_stops_on_first_failed_check(self):
        runner = load_runner()
        with patch.object(runner, "run", side_effect=[1]) as mocked:
            self.assertEqual(runner.verify(), 1)
        self.assertEqual(mocked.call_count, 1)

    def test_fork_plan_is_read_only_entrypoint(self):
        runner = load_runner()
        with patch.object(runner, "run", return_value=0) as mocked:
            self.assertEqual(runner.fork_plan(), 0)
        self.assertEqual(mocked.call_args.args[0][1:], ["scripts/fork_ecosystem.py"])

    def test_fork_apply_uses_existing_bounded_plan(self):
        runner = load_runner()
        with patch.object(runner, "run", return_value=0) as mocked:
            self.assertEqual(runner.fork_apply(), 0)
        self.assertEqual(mocked.call_args.args[0][1:], ["scripts/apply_fork_plan.py"])

if __name__ == "__main__":
    import unittest
    unittest.main()
