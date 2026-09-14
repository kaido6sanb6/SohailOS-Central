# SohailOS inside ChatGPT

The target integration is a remote MCP app, packaged for ChatGPT using the OpenAI Apps SDK.

## Architecture

ChatGPT -> remote HTTPS MCP endpoint -> SohailOS Gateway -> Agent Runtime -> tools / AI providers / memory.

The gateway exposes:

- `GET /health`
- `POST /v1/agent/run`
- `POST /mcp`

The MCP endpoint is authenticated with `SOHAILOS_GATEWAY_TOKEN`.

## Why the gateway must be remote

A desktop WPF process on the user's computer is not directly reachable by ChatGPT. The gateway therefore needs a public HTTPS deployment or a supported secure tunnel. Do not expose the desktop memory directory or unrestricted local HTTP endpoints.

## ChatGPT integration path

1. Deploy `SohailOS.Gateway` behind HTTPS.
2. Configure a strong gateway token or OAuth in the deployment environment.
3. Restrict tools and write permissions.
4. Register the remote MCP server as a custom ChatGPT app in an environment that supports custom MCP apps.
5. Test read-only tools first.
6. Add write tools only after approval flows are verified.

The desktop application and the remote gateway are two interfaces to the same SohailOS core. They should share the same system specification and provider abstraction rather than becoming separate products.

## Current limitation

The repository contains the gateway and MCP foundation, but the final ChatGPT publication/connection step depends on ChatGPT workspace/app availability and the gateway being deployed to a reachable HTTPS endpoint.
