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
        Write-Output "ON"
    }
    "off" {
        Set-Content -Path $stateFile -Value "OFF" -NoNewline
        Write-Output "OFF"
    }
    "status" {
        $state = "OFF"
        if (Test-Path $stateFile) {
            $raw = (Get-Content -Path $stateFile -Raw).Trim().ToUpperInvariant()
            if ($raw -eq "ON" -or $raw -eq "OFF") {
                $state = $raw
            }
        }
        Write-Output $state
    }
}
