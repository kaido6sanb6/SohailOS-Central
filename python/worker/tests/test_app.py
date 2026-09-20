import os
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from fastapi.testclient import TestClient

os.environ["SOHAILOS_CODE_TOKEN"] = "test-token"
os.environ["SOHAILOS_EXECUTION_ENABLED"] = "false"

from python.worker.app import app

client = TestClient(app)

def test_health_is_public_and_reports_disabled_execution():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"
    assert response.json()["executionEnabled"] is False

def test_run_requires_bearer_token():
    response = client.post("/v1/python/run", json={"job": "environment_info"})
    assert response.status_code == 401

def test_run_is_disabled_by_default():
    response = client.post(
        "/v1/python/run",
        headers={"Authorization": "Bearer test-token"},
        json={"job": "environment_info"},
    )
    assert response.status_code == 503

def test_unknown_job_is_rejected(monkeypatch):
    monkeypatch.setenv("SOHAILOS_EXECUTION_ENABLED", "true")
    response = client.post(
        "/v1/python/run",
        headers={"Authorization": "Bearer test-token"},
        json={"job": "not_allowed"},
    )
    assert response.status_code == 400

def test_allowlisted_job_runs(monkeypatch):
    monkeypatch.setenv("SOHAILOS_EXECUTION_ENABLED", "true")
    response = client.post(
        "/v1/python/run",
        headers={"Authorization": "Bearer test-token"},
        json={"job": "echo", "args": {"value": "ok"}},
    )
    assert response.status_code == 200
    assert response.json()["result"]["echo"] == "ok"
