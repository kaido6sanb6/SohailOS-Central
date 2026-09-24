import importlib.util, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
S=importlib.util.spec_from_file_location("fork_ecosystem",ROOT/"scripts"/"fork_ecosystem.py")
M=importlib.util.module_from_spec(S); S.loader.exec_module(M)

class ForkEcosystemTests(unittest.TestCase):
    def test_strictly_behind_is_auto_sync(self):
        r={"full_name":"u/f","upstream":"o/f","default_branch":"main","archived":False}
        self.assertEqual(M.classify(r,{"status":"ok","ahead_by":0,"behind_by":3})[:2],("upstream_sync",True))
    def test_diverged_is_quarantine(self):
        r={"full_name":"u/f","upstream":"o/f","default_branch":"main","archived":False}
        self.assertEqual(M.classify(r,{"status":"ok","ahead_by":2,"behind_by":3})[:2],("quarantine",False))
    def test_missing_provenance_blocks(self):
        self.assertEqual(M.classify({"full_name":"u/f","upstream":None},{"status":"ok"})[:2],("blocked",False))
    def test_plan_is_non_mutating_and_deterministic(self):
        inv={"forks":[{"full_name":"u/f","upstream":"o/f","default_branch":"main","archived":False}]}
        rep={"fork_count":1,"results":[{"repo":"u/f","status":"ok","ahead_by":0,"behind_by":1,"upstream_sha":"a","target_sha":"b"}]}
        pol={"policy_id":"p","hub":"h","upstream_sync":{"enabled":True}}
        a,b=M.build(inv,rep,pol),M.build(inv,rep,pol)
        self.assertFalse(a["mutation"]); self.assertEqual(a["actions"][0]["operation_id"],b["actions"][0]["operation_id"])
if __name__=="__main__": unittest.main()
