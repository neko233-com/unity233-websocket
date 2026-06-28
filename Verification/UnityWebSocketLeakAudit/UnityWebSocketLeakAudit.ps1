param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "File not found: $Path"
}

$source = Get-Content -LiteralPath $Path -Raw

$checks = @(
    [pscustomobject]@{
        Id = "send-buffer-slice"
        Severity = "high"
        Pattern = "HEAPU8\.buffer\.slice|buffer\.slice\s*\("
        Expected = "absent"
        Explanation = "Per-send ArrayBuffer allocation/copy before WebSocket.send."
    },
    [pscustomobject]@{
        Id = "receive-malloc"
        Severity = "high"
        Pattern = "_malloc\s*\("
        Expected = "review"
        Explanation = "Per-message bridge allocations can create Emscripten heap churn."
    },
    [pscustomobject]@{
        Id = "blob-filereader"
        Severity = "medium"
        Pattern = "Blob|FileReader|readAsArrayBuffer"
        Expected = "absent"
        Explanation = "Blob/FileReader receive paths add async closures and payload retention risk."
    },
    [pscustomobject]@{
        Id = "text-receive-path"
        Severity = "medium"
        Pattern = "onMessageStr|MessageStr|UTF8ToString\s*\("
        Expected = "review"
        Explanation = "Text receive paths add UTF8/string allocation pressure."
    },
    [pscustomobject]@{
        Id = "handler-detach"
        Severity = "high"
        Pattern = "onopen\s*=\s*null|onmessage\s*=\s*null|onerror\s*=\s*null|onclose\s*=\s*null"
        Expected = "present"
        Explanation = "Close/free should detach WebSocket handlers so closures can be collected."
    },
    [pscustomobject]@{
        Id = "instance-delete"
        Severity = "high"
        Pattern = "delete\s+.*instances\s*\[|delete\s+instances\s*\["
        Expected = "present"
        Explanation = "Free should remove the JS instance table entry to avoid reconnect leaks."
    }
)

$results = foreach ($check in $checks) {
    $matched = [regex]::IsMatch($source, $check.Pattern)
    $status = switch ($check.Expected) {
        "absent" { if ($matched) { "risk" } else { "ok" } }
        "present" { if ($matched) { "ok" } else { "risk" } }
        default { if ($matched) { "review" } else { "ok" } }
    }

    [pscustomobject]@{
        Id = $check.Id
        Severity = $check.Severity
        Status = $status
        Matched = $matched
        Explanation = $check.Explanation
    }
}

$results | Format-Table -AutoSize

$riskCount = @($results | Where-Object { $_.Status -eq "risk" }).Count
$reviewCount = @($results | Where-Object { $_.Status -eq "review" }).Count

Write-Host ""
Write-Host "Risk findings: $riskCount"
Write-Host "Review findings: $reviewCount"

if ($riskCount -gt 0) {
    exit 2
}

exit 0
