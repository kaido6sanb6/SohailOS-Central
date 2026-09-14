param(
    [string]$AppName = "sohailos"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking Koyeb CLI..."
koyeb version | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Koyeb CLI is not installed or not available in PATH."
}

Write-Host "This script never asks for or prints API keys. Create the required Koyeb Secrets first using the Koyeb CLI or control panel."
Write-Host "Required secrets:"
Write-Host "  SOHAILOS_OPENAI_API_KEYS"
Write-Host "  SOHAILOS_GEMINI_API_KEYS"
Write-Host "  SOHAILOS_ANTHROPIC_API_KEYS"
Write-Host "  SOHAILOS_GATEWAY_TOKEN"
Write-Host "  SOHAILOS_SUPABASE_URL"
Write-Host "  SOHAILOS_SUPABASE_SERVICE_ROLE_KEY"

$repo = "github.com/kaido6sanb6/SohailOS-Central"

$arguments = @(
    "apps", "init", $AppName,
    "--git", $repo,
    "--git-branch", "main",
    "--git-builder", "docker",
    "--ports", "10000:http",
    "--routes", "/:10000",
    "--instance-type", "nano",
    "--max-scale", "1",
    "--min-scale", "1",
    "--env", "PORT=10000",
    "--env", "SOHAILOS_AI_PROVIDER=auto",
    "--env", "SOHAILOS_OPENAI_MODEL=gpt-5",
    "--env", "SOHAILOS_GEMINI_MODEL=gemini-3.8-flash",
    "--env", "SOHAILOS_ANTHROPIC_MODEL=claude-sonnet-5",
    "--env", "SOHAILOS_OPENAI_API_KEYS={{secret.SOHAILOS_OPENAI_API_KEYS}}",
    "--env", "SOHAILOS_GEMINI_API_KEYS={{secret.SOHAILOS_GEMINI_API_KEYS}}",
    "--env", "SOHAILOS_ANTHROPIC_API_KEYS={{secret.SOHAILOS_ANTHROPIC_API_KEYS}}",
    "--env", "SOHAILOS_GATEWAY_TOKEN={{secret.SOHAILOS_GATEWAY_TOKEN}}",
    "--env", "SOHAILOS_SUPABASE_URL={{secret.SOHAILOS_SUPABASE_URL}}",
    "--env", "SOHAILOS_SUPABASE_SERVICE_ROLE_KEY={{secret.SOHAILOS_SUPABASE_SERVICE_ROLE_KEY}}"
)

Write-Host "Creating Koyeb app/service from GitHub..."
& koyeb @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Koyeb deployment failed. Verify authentication, repository access, and that the referenced Secrets exist."
}

Write-Host "Deployment command completed. Check: koyeb apps get $AppName"
