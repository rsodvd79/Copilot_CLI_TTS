param(
    [Parameter(Position = 0)]
    [ValidateSet("on", "off", "status")]
    [string]$Action = "status"
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$stateDir = Join-Path $repoRoot ".copilot"
$stateFile = Join-Path $stateDir "webservice-forwarding.state"

if (-not (Test-Path $stateDir)) {
    New-Item -ItemType Directory -Path $stateDir | Out-Null
}

switch ($Action) {
    "on" {
        Set-Content -Path $stateFile -Value "ON" -NoNewline
        Write-Host "Webservice forwarding: ON"
    }
    "off" {
        Set-Content -Path $stateFile -Value "OFF" -NoNewline
        Write-Host "Webservice forwarding: OFF"
    }
    "status" {
        if (-not (Test-Path $stateFile)) {
            Write-Host "Webservice forwarding: OFF (default)"
            exit 0
        }

        $state = (Get-Content -Path $stateFile -Raw).Trim().ToUpperInvariant()
        if ($state -eq "ON") {
            Write-Host "Webservice forwarding: ON"
        } else {
            Write-Host "Webservice forwarding: OFF"
        }
    }
}

