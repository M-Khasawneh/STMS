param([switch]$Apply)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Explicit maintenance command only. Default is read-only preflight; never seed or create test records.
$root = Split-Path -Parent $PSScriptRoot
$target = '20261114220000_TopCommitteeVotingRounds'
$expected = @('20261112224814_InitialIdentityAndProcurement','20261113001222_EmployeeDepartmentAssignment','20261113120000_SupplierRegistrationUniqueness','20261113180000_TenderInvitationIntegrity','20261114120000_ClarificationAttachments','20261114190000_EvaluationConfirmation')
$scriptPath = Join-Path $root 'docs/top-committee-voting-migration.sql'
$sql = [IO.File]::ReadAllText($scriptPath).Replace("`r`n","`n").TrimEnd()+"`n"
$sha = [Security.Cryptography.SHA256]::Create()
try { $digest = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sql))).Replace('-','') } finally { $sha.Dispose() }
if ($digest -ne '3748F774F01AA0A1C08D85FC165936E0684529744271AF301FB3877E599C28EA') { throw 'The reviewed migration script changed. Stop for review.' }
$config = Get-Content -Raw (Join-Path $root 'src/SourcingTenderManagementSystem.Web/appsettings.json') | ConvertFrom-Json
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.ConnectionStrings.DefaultConnection)
if (-not $builder.IntegratedSecurity -or $builder.DataSource -ne 'localhost\MSSQLSERVER02' -or $builder.InitialCatalog -ne 'SourcingTenderDB') { throw 'Unexpected database target.' }
$builder['Connect Timeout'] = 5
$builder['ApplicationIntent'] = $(if ($Apply) { 'ReadWrite' } else { 'ReadOnly' })
$conn = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$tx = $null
function Query([string]$text) {
    $command = $conn.CreateCommand(); $command.Transaction = $tx; $command.CommandTimeout = 45; $command.CommandText = $text
    try { $reader = $command.ExecuteReader(); try { $table = New-Object System.Data.DataTable; $table.Load($reader); return ,$table } finally { $reader.Dispose() } } finally { $command.Dispose() }
}
function Execute([string]$text) {
    $command = $conn.CreateCommand(); $command.Transaction = $tx; $command.CommandTimeout = 45; $command.CommandText = $text
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
}
function Q([string]$name) { return '['+$name.Replace(']',']]')+']' }
function Tables { return (Query "SELECT t.object_id AS Id,s.name AS SchemaName,t.name AS TableName FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE t.is_ms_shipped=0 AND t.name<>N'__EFMigrationsHistory' ORDER BY s.name,t.name").Rows }
try {
    $conn.Open()
    $tx = $conn.BeginTransaction([Data.IsolationLevel]::Serializable)
    if ($Apply) { Execute "SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'STMS.TopCommitteeVotingRounds',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Could not acquire the migration lock.',1;" }
    $history = Query 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId'
    $ids = @($history.Rows | ForEach-Object { [string]$_.MigrationId })
    if (($ids -join '|') -ne ($expected -join '|')) { throw 'Migration history differs from the six reviewed predecessors; no migration will be applied.' }
    $oldHistory = @($history.Rows | ForEach-Object { [string]$_.MigrationId+'|'+[string]$_.ProductVersion }) -join ';'
    $columns = Query "SELECT name,is_nullable,default_object_id FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TenderApprovals') AND name IN(N'DecidedAt',N'RoundOpenedAt',N'RoundClosedAt',N'RoundCloseReason',N'RoundMemberSnapshot')"
    if ($columns.Rows.Count -ne 1 -or $columns.Rows[0].name -ne 'DecidedAt' -or $columns.Rows[0].is_nullable -or $columns.Rows[0].default_object_id -ne 0) { throw 'Approval columns differ from the reviewed pre-migration state.' }
    $duplicates = Query "SELECT (SELECT COUNT_BIG(*) FROM(SELECT TenderApprovalId,CommitteeMemberId FROM dbo.CommitteeVotes GROUP BY TenderApprovalId,CommitteeMemberId HAVING COUNT_BIG(*)>1)v) AS Votes,(SELECT COUNT_BIG(*) FROM(SELECT TenderId FROM dbo.TenderAwards GROUP BY TenderId HAVING COUNT_BIG(*)>1)a) AS Awards"
    if ($duplicates.Rows[0].Votes -ne 0 -or $duplicates.Rows[0].Awards -ne 0) { throw 'Existing duplicate vote or award records detected. No changes applied.' }
    $constraintNames = Query "SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.TenderApprovals') AND name=N'CK_TenderApprovals_TopRound'"
    $indexNames = Query "SELECT name FROM sys.indexes WHERE (object_id=OBJECT_ID(N'dbo.TenderApprovals') AND name=N'UX_TenderApprovals_OpenTopRound') OR (object_id=OBJECT_ID(N'dbo.CommitteeVotes') AND name=N'IX_CommitteeVotes_TenderApprovalId_CommitteeMemberId')"
    if ($constraintNames.Rows.Count -ne 0 -or $indexNames.Rows.Count -ne 0) { throw 'Unexpected pre-existing round constraint/index detected.' }
    $awardIndex = Query "SELECT is_unique,is_disabled FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TenderAwards') AND name=N'IX_TenderAwards_TenderId'"
    if ($awardIndex.Rows.Count -ne 1 -or $awardIndex.Rows[0].is_unique -or $awardIndex.Rows[0].is_disabled) { throw 'Award lookup index differs from the reviewed state.' }
    $baseline = @(); $inventory = @(Tables)
    foreach ($table in $inventory) {
        $objectId = [int]$table.Id
        $names = @((Query "SELECT name FROM sys.columns WHERE object_id=$objectId ORDER BY column_id").Rows | ForEach-Object { Q ([string]$_.name) })
        $keys = @((Query "SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=$objectId AND i.is_primary_key=1 ORDER BY ic.key_ordinal").Rows | ForEach-Object { Q ([string]$_.name) })
        if ($keys.Count -eq 0) { throw 'A table has no deterministic primary key; stop for review.' }
        $qualified = (Q ([string]$table.SchemaName))+'.'+(Q ([string]$table.TableName))
        # JSON stays on SQL Server. Only an aggregate count and SHA-256 digest leave the server.
        # Keep the original column projection so new nullable columns cannot alter the comparison.
        $query = 'SELECT (SELECT COUNT_BIG(*) FROM '+$qualified+') AS Records,CONVERT(varchar(64),HASHBYTES(''SHA2_256'',(SELECT '+($names -join ',')+' FROM '+$qualified+' ORDER BY '+($keys -join ',')+' FOR JSON PATH,INCLUDE_NULL_VALUES)),2) AS Digest'
        $row = (Query $query).Rows[0]
        $baseline += [pscustomobject]@{ Query=$query; Records=$row.Records; Digest=$row.Digest; Name=$qualified }
    }
    Write-Output ('Preflight passed: six applied migrations; no duplicate votes/awards; captured counts and content fingerprints for {0} tables.' -f $baseline.Count)
    if (-not $Apply) { $tx.Rollback(); $tx.Dispose(); $tx=$null; Write-Output 'READ-ONLY PREFLIGHT ONLY. No schema or data changes.'; return }
    # Execute only the hash-verified, user-approved migration. Its BEGIN/COMMIT remain nested
    # inside this outer transaction so verification failure still rolls the entire change back.
    $batch = [regex]::Replace($sql,'(?m)^\s*GO\s*$','')
    # EF scripts concatenate operation command texts. SQL Server must compile the
    # new-column constraint only after the ADD COLUMN command batches have run.
    # Blank lines are reviewed operation boundaries in this exact hash-pinned script;
    # the initial declaration/default-constraint block remains in one batch.
    $commands = @([regex]::Split($batch,"\r?\n[ \t]*\r?\n") | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($commands.Count -ne 12) { throw 'Unexpected migration command boundaries; stop for review.' }
    foreach ($text in $commands) { Execute $text }
    $afterHistory = Query 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId'
    $afterIds = @($afterHistory.Rows | ForEach-Object { [string]$_.MigrationId })
    if (($afterIds -join '|') -ne (($expected+@($target)) -join '|')) { throw 'Unexpected migration history after execution.' }
    $preservedHistory = @($afterHistory.Rows | Where-Object { $_.MigrationId -ne $target } | ForEach-Object { [string]$_.MigrationId+'|'+[string]$_.ProductVersion }) -join ';'
    if ($oldHistory -ne $preservedHistory) { throw 'Earlier migration-history records changed.' }
    $afterColumns = Query "SELECT name,is_nullable,TYPE_NAME(user_type_id) AS Kind,max_length FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TenderApprovals') AND name IN(N'DecidedAt',N'RoundOpenedAt',N'RoundClosedAt',N'RoundCloseReason',N'RoundMemberSnapshot')"
    if ($afterColumns.Rows.Count -ne 5 -or @($afterColumns.Rows | Where-Object { -not $_.is_nullable }).Count -ne 0) { throw 'Unexpected round column nullability.' }
    foreach ($row in $afterColumns.Rows) {
        if ($row.name -in @('DecidedAt','RoundOpenedAt','RoundClosedAt') -and $row.Kind -ne 'datetimeoffset') { throw 'Unexpected timestamp type.' }
        if ($row.name -eq 'RoundCloseReason' -and ($row.Kind -ne 'nvarchar' -or $row.max_length -ne -1)) { throw 'Unexpected closure reason storage.' }
        if ($row.name -eq 'RoundMemberSnapshot' -and ($row.Kind -ne 'nvarchar' -or $row.max_length -ne 1024)) { throw 'Unexpected roster snapshot storage.' }
    }
    $checks = Query "SELECT is_disabled,is_not_trusted FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.TenderApprovals') AND name=N'CK_TenderApprovals_TopRound'"
    if ($checks.Rows.Count -ne 1 -or $checks.Rows[0].is_disabled -or $checks.Rows[0].is_not_trusted) { throw 'The new round check is missing, disabled or untrusted.' }
    $indexes = Query "SELECT name,is_unique,is_disabled,has_filter,filter_definition FROM sys.indexes WHERE (object_id=OBJECT_ID(N'dbo.TenderApprovals') AND name=N'UX_TenderApprovals_OpenTopRound') OR (object_id=OBJECT_ID(N'dbo.CommitteeVotes') AND name=N'IX_CommitteeVotes_TenderApprovalId_CommitteeMemberId') OR (object_id=OBJECT_ID(N'dbo.TenderAwards') AND name=N'IX_TenderAwards_TenderId')"
    if ($indexes.Rows.Count -ne 3 -or @($indexes.Rows | Where-Object { -not $_.is_unique -or $_.is_disabled }).Count -ne 0) { throw 'A required unique index is missing or disabled.' }
    $openIndex = @($indexes.Rows | Where-Object { $_.name -eq 'UX_TenderApprovals_OpenTopRound' })[0]
    if (-not $openIndex.has_filter -or $openIndex.filter_definition -notmatch 'TopCommittee' -or $openIndex.filter_definition -notmatch 'RoundClosedAt') { throw 'Unexpected open-round index filter.' }
    $afterInventory = @(Tables)
    if ((($inventory | ForEach-Object { $_.Id }) -join ',') -ne (($afterInventory | ForEach-Object { $_.Id }) -join ',')) { throw 'Existing table inventory changed.' }
    foreach ($table in $baseline) {
        $row = (Query $table.Query).Rows[0]
        if ($row.Records -ne $table.Records -or $row.Digest -ne $table.Digest) { throw ('Existing records changed in '+$table.Name+'; rolling back.') }
    }
    $tx.Commit(); $tx.Dispose(); $tx=$null
    Write-Output ('APPLIED: '+$target)
    Write-Output ('VERIFIED: seven migration-history rows; five nullable approval fields; trusted round check; three unique indexes; unchanged records/counts across '+$baseline.Count+' tables.')
    Write-Output 'No test users, seed records or business-data writes were performed.'
} catch {
    if ($null -ne $tx) { try { $tx.Rollback() } catch { }; $tx.Dispose(); $tx=$null }
    throw
} finally { $conn.Dispose() }
