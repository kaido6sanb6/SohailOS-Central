import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INDEX = ROOT / "index.html"

class VercelLandingContractTests(unittest.TestCase):
    def test_root_landing_page_contains_architecture_contract(self):
        self.assertTrue(INDEX.is_file(), "Vercel root must contain index.html")
        html = INDEX.read_text(encoding="utf-8")
        for marker in (
            "SohailOS-Central",
            "Architecture",
            "Route",
            "Discover",
            "Probe",
            "Plan",
            "Preview",
            "Approve",
            "Exec",
            "Verify",
            "Validate",
            "Deliver",
            "mermaid",
        ):
            self.assertIn(marker, html)

    def test_landing_page_exposes_code_and_visual_views(self):
        html = INDEX.read_text(encoding="utf-8")
        self.assertIn("Architecture source", html)
        self.assertIn("Architecture chart", html)
        self.assertIn('id="mermaid"', html)
        self.assertIn("<svg", html)

if __name__ == "__main__":
    unittest.main()
