param(
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][string]$Target,
    [Parameter(Mandatory=$true)][int]$Pid
)

while (Get-Process -Id $Pid -ErrorAction SilentlyContinue) {
    Start-Sleep -Milliseconds 500
}

$temp = Join-Path $env:TEMP ("SohailOS-update-" + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $temp | Out-Null
Expand-Archive -LiteralPath $Package -DestinationPath $temp -Force
Copy-Item -Path (Join-Path $temp "*") -Destination $Target -Recurse -Force
Remove-Item -LiteralPath $temp -Recurse -Force
Remove-Item -LiteralPath $Package -Force -ErrorAction SilentlyContinue
