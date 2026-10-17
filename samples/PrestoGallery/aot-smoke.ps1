<#
.SYNOPSIS
Opens every gallery page in a published gallery and fails if any page logs an exception or a binding error, or exits on its own.
.EXAMPLE
dotnet publish samples/PrestoGallery/PrestoGallery.Desktop -c Release -r win-x64 -p:PublishAot=true -o artifacts/gallery
samples/PrestoGallery/aot-smoke.ps1 artifacts/gallery/PrestoGallery.Desktop.exe
#>
param(
    [Parameter(Mandatory)][string]$Exe,
    [int]$Seconds = 6
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$logs = New-Item -ItemType Directory -Force (Join-Path ([IO.Path]::GetTempPath()) "presto-aot-smoke-$PID")

$list = Join-Path $logs 'list-pages.txt'
$p = Start-Process $Exe -ArgumentList '--list-pages' -RedirectStandardOutput $list -NoNewWindow -Wait -PassThru
if ($p.ExitCode -ne 0) { throw "--list-pages exited with $($p.ExitCode)" }
$slugs = Get-Content $list | Where-Object { $_.Trim() } | ForEach-Object { ($_.Trim() -split '\s+')[0] }

$failed = @()
foreach ($slug in $slugs) {
    $out = Join-Path $logs "$slug.out.txt"
    $err = Join-Path $logs "$slug.err.txt"
    $p = Start-Process $Exe -ArgumentList '--page', $slug -RedirectStandardOutput $out -RedirectStandardError $err -PassThru
    $null = $p.Handle  # keeps ExitCode readable after the process exits
    $reason = $null
    if ($p.WaitForExit($Seconds * 1000)) {
        $reason = "exited on its own with $($p.ExitCode)"
    } else {
        $p.Kill()
        $p.WaitForExit()
    }
    if (-not $reason -and (Select-String -Path $err -Pattern 'Exception|\[Binding\]' -Quiet)) {
        $reason = 'stderr has an exception or a binding error'
    }
    if ($reason) {
        $failed += $slug
        Write-Host "FAIL $slug`: $reason"
        Get-Content $err | Write-Host
    } else {
        Write-Host "PASS $slug"
    }
}

Write-Host "Logs: $logs"
if ($failed) {
    Write-Host "$($failed.Count) of $($slugs.Count) pages failed: $($failed -join ', ')"
    exit 1
}
Write-Host "All $($slugs.Count) pages passed."
