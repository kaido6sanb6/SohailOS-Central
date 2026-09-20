# SohailOS persistent Python worker

The `python/worker` service is the long-lived Python execution layer for SohailOS. Source code stays in `SohailOS-Central`; deployment can run as a private Render web service.

## Endpoints

- `GET /health` — readiness/status.
- `POST /v1/python/run` — authenticated job execution.

## Safety defaults

- Execution is disabled until `SOHAILOS_EXECUTION_ENABLED=true`.
- Execution requires `Authorization: Bearer <SOHAILOS_CODE_TOKEN>`.
- Only explicitly registered jobs execute; unknown jobs are rejected.
- Secrets must never be committed to GitHub or sent through chat.

The service is an operator-controlled execution layer, not a public multi-tenant sandbox. Hostile untrusted code should be isolated in a dedicated VM/container sandbox before enabling arbitrary execution.
