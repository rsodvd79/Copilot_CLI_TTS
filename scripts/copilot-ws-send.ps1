param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Text,

    [Parameter(Position = 1)]
    [string]$Url = "http://localhost:5000"
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$clientProject = Join-Path $repoRoot "client\TtsClient.csproj"

if ([string]::IsNullOrWhiteSpace($Text)) {
    exit 0
}

function Convert-MarkdownToPlainText {
    param([string]$InputText)

    $plain = $InputText
    $plain = [Regex]::Replace($plain, '\[(.*?)\]\((.*?)\)', '$1')
    $plain = [Regex]::Replace($plain, '(```[\s\S]*?```)', '')
    $plain = [Regex]::Replace($plain, '`([^`]+)`', '$1')
    $plain = [Regex]::Replace($plain, '(^|\s)[#>*_~`-]+', ' ')
    $plain = [Regex]::Replace($plain, '\r?\n', ' ')
    $plain = [Regex]::Replace($plain, '\s{2,}', ' ')
    return $plain.Trim()
}

try {
    $plainText = Convert-MarkdownToPlainText -InputText $Text
    if ([string]::IsNullOrWhiteSpace($plainText)) {
        exit 0
    }

    & dotnet run --project $clientProject -- --url $Url $plainText | Out-Null
    exit $LASTEXITCODE
}
catch {
    Write-Error $_
    exit 1
}
