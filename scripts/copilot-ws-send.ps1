param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Text,

    [Parameter(Position = 1)]
    [string]$Url = "http://localhost:5000",

    [int]$MaxChunkLength = 220
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$stateFile = Join-Path $repoRoot ".copilot\webservice-forwarding.state"
$clientProject = Join-Path $repoRoot "client\TtsClient.csproj"

if (-not (Test-Path $stateFile)) {
    Write-Host "Forwarding OFF (state file missing)"
    exit 0
}

$state = (Get-Content -Path $stateFile -Raw).Trim().ToUpperInvariant()
if ($state -ne "ON") {
    Write-Host "Forwarding OFF"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Text)) {
    exit 0
}

function Split-IntoChunks {
    param(
        [string]$InputText,
        [int]$ChunkSize
    )

    $sentences = $InputText -split '(?<=[\.\!\?])\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($sentences.Count -eq 0) {
        return @($InputText)
    }

    $chunks = New-Object System.Collections.Generic.List[string]
    $buffer = ""

    foreach ($sentence in $sentences) {
        $candidate = if ([string]::IsNullOrEmpty($buffer)) { $sentence } else { "$buffer $sentence" }
        if ($candidate.Length -le $ChunkSize) {
            $buffer = $candidate
            continue
        }

        if (-not [string]::IsNullOrEmpty($buffer)) {
            $chunks.Add($buffer.Trim())
            $buffer = ""
        }

        if ($sentence.Length -le $ChunkSize) {
            $buffer = $sentence
            continue
        }

        for ($i = 0; $i -lt $sentence.Length; $i += $ChunkSize) {
            $len = [Math]::Min($ChunkSize, $sentence.Length - $i)
            $chunks.Add($sentence.Substring($i, $len).Trim())
        }
    }

    if (-not [string]::IsNullOrEmpty($buffer)) {
        $chunks.Add($buffer.Trim())
    }

    return $chunks
}

$chunks = Split-IntoChunks -InputText $Text -ChunkSize $MaxChunkLength
foreach ($chunk in $chunks) {
    & dotnet run --project $clientProject -- --url $Url $chunk | Out-Null
}

