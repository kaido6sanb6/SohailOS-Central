import os
import platform
import sys
from fastapi import FastAPI, Header
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

app = FastAPI(title="SohailOS Python Worker", version="0.1.0")

class JobRequest(BaseModel):
    job: str = Field(min_length=1, max_length=80)
    args: dict[str, str] = Field(default_factory=dict)

def authorized(authorization: str | None) -> bool:
    token = os.getenv("SOHAILOS_CODE_TOKEN", "")
    return bool(token) and authorization == f"Bearer {token}"

def run_job(job: str, args: dict[str, str]) -> dict:
    if job == "environment_info":
        return {
            "python": sys.version.split()[0],
            "platform": platform.platform(),
            "cwd": os.getcwd(),
            "executionEnabled": os.getenv("SOHAILOS_EXECUTION_ENABLED", "false").lower() == "true",
        }
    if job == "echo":
        return {"echo": args.get("value", "")}
    raise ValueError("Unknown job")

@app.get("/health")
def health():
    return {
        "service": "SohailOS Python Worker",
        "version": "0.1.0",
        "status": "ok",
        "executionEnabled": os.getenv("SOHAILOS_EXECUTION_ENABLED", "false").lower() == "true",
    }

@app.post("/v1/python/run")
def run_python(payload: JobRequest, authorization: str | None = Header(default=None)):
    if not authorized(authorization):
        return JSONResponse({"error": "Unauthorized"}, status_code=401)
    if os.getenv("SOHAILOS_EXECUTION_ENABLED", "false").lower() != "true":
        return JSONResponse({"error": "Python execution is disabled"}, status_code=503)
    try:
        return {"job": payload.job, "result": run_job(payload.job, payload.args)}
    except ValueError as exc:
        return JSONResponse({"error": str(exc)}, status_code=400)
