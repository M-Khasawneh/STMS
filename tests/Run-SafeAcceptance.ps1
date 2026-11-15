#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArtifactsPath,
    [Parameter(Mandatory)][string]$LogDirectory,
    [switch]$SkipBuild,
    [switch]$IncludeLocalModel,
    [switch]$IncludeSqlReadOnly,
    [ValidateRange(60,1800)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function ExternalPath([string]$value) {
    $resolved = [IO.Path]::GetFullPath($value)
    if ($resolved.Equals($repository, [StringComparison]::OrdinalIgnoreCase) -or $resolved.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Artifacts and private QA logs must be outside the repository.'
    }
    return $resolved
}
$ArtifactsPath = ExternalPath $ArtifactsPath
$LogDirectory = ExternalPath $LogDirectory
# A fresh run directory avoids replacing earlier acceptance evidence.
$runDirectory = Join-Path $LogDirectory ('safe-acceptance-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$results = [Collections.Generic.List[object]]::new()
$completed = $false
function RunCheck([string]$name, [string[]]$arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $repository
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $watch = [Diagnostics.Stopwatch]::StartNew()
    Write-Host "Running $name"
    try {
        if (!$process.Start()) { throw "Cannot start $name" }
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = !$process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
        [IO.File]::WriteAllText((Join-Path $runDirectory ($name + '.log')), $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult())
        $code = if ($timedOut) { 124 } else { $process.ExitCode }
        $results.Add([pscustomobject]@{ Name=$name; ExitCode=$code; Seconds=[Math]::Round($watch.Elapsed.TotalSeconds,2); TimedOut=$timedOut })
        Write-Host "$name completed with exit code $code"
        return $code
    }
    finally { $process.Dispose(); $watch.Stop() }
}
try {
    if (!$SkipBuild) {
        if ((RunCheck 'build' @('build','SourcingTenderManagementSystem.slnx','--artifacts-path',$ArtifactsPath,'--no-restore','-m:1','-v','quiet')) -ne 0) { throw 'Build failed; tests were not started.' }
    }
    $authentication = Join-Path $ArtifactsPath 'bin/SourcingTenderManagementSystem.AuthenticationChecks/debug/SourcingTenderManagementSystem.AuthenticationChecks.dll'
    $schema = Join-Path $ArtifactsPath 'bin/SourcingTenderManagementSystem.SchemaChecks/debug/SourcingTenderManagementSystem.SchemaChecks.dll'
    if (!(Test-Path -LiteralPath $authentication) -or !(Test-Path -LiteralPath $schema)) { throw 'Build outputs are missing. Use the existing Debug artifact layout.' }
    RunCheck 'schema-model' @($schema) | Out-Null
    RunCheck 'authentication-employees-departments' @($authentication) | Out-Null
    # Fixed allowlist: no migrations, provisioning commands, SQL mutations, browser fixtures or seeding.
    foreach ($name in @('work-hub','experience','provisioning','workspace-ui','admin-dashboard','profile-picture','supplier-expiry','bid-phase3','bid-phase4','compliance','evaluation','report','top-committee','internal-clarification','contact','inbox','reporting','voice-search','passkey','ai','document-extraction','access-copy')) {
        RunCheck $name @($authentication, "--$name-checks-only") | Out-Null
    }
    if ($IncludeLocalModel) {
        RunCheck 'ai-local-model' @($authentication,'--ai-local-model-checks-only') | Out-Null
        RunCheck 'ai-document-model' @($authentication,'--ai-document-model-checks-only') | Out-Null
    }
    if ($IncludeSqlReadOnly) {
        RunCheck 'sql-select-only' @($authentication,'--reporting-read-only-checks') | Out-Null
        RunCheck 'workspace-sql-select-only' @($authentication,'--workspace-read-only-checks') | Out-Null
    }
    $completed = $true
}
finally {
    $summary = [pscustomobject]@{ Repository=$repository; FinishedUtc=[DateTimeOffset]::UtcNow; Completed=$completed; LocalModelIncluded=$IncludeLocalModel.IsPresent; SqlReadOnlyIncluded=$IncludeSqlReadOnly.IsPresent; BuildSkipped=$SkipBuild.IsPresent; Results=$results.ToArray(); Failed=@($results | Where-Object ExitCode -ne 0).Count }
    [IO.File]::WriteAllText((Join-Path $runDirectory 'summary.json'), ($summary | ConvertTo-Json -Depth 5))
    Write-Host "Evidence saved outside the repository: $runDirectory"
}
if (!$summary.Completed -or $summary.Failed -gt 0) { exit 1 }
