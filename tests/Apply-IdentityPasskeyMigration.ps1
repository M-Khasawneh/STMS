param([switch]$Apply)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Explicit maintenance command only. Default is read-only preflight; never seed or create test records.
$root = Split-Path -Parent $PSScriptRoot
$target = '20261115034155_IdentityPasskeyStorage'
$expected = @('20261112224814_InitialIdentityAndProcurement','20261113001222_EmployeeDepartmentAssignment','20261113120000_SupplierRegistrationUniqueness','20261113180000_TenderInvitationIntegrity','20261114120000_ClarificationAttachments','20261114190000_EvaluationConfirmation','20261114220000_TopCommitteeVotingRounds')
$scriptPath = Join-Path $root 'docs/identity-passkey-migration.sql'
$sql = [IO.File]::ReadAllText($scriptPath).Replace("`r`n","`n").TrimEnd()+"`n"
$sha = [Security.Cryptography.SHA256]::Create()
try { $digest = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sql))).Replace('-','') } finally { $sha.Dispose() }
if ($digest -ne '828BD1893FD2176F875E87B8D2C443448BDC2CA08FB42ED518B17DECD834E55E') { throw 'The reviewed migration script changed. Stop for review.' }
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
    if ($Apply) { Execute "SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'STMS.IdentityPasskeyStorage',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Could not acquire the migration lock.',1;" }
    $history = Query 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId'
    $ids = @($history.Rows | ForEach-Object { [string]$_.MigrationId })
    if (($ids -join '|') -ne ($expected -join '|')) { throw 'Migration history differs from the seven reviewed predecessors; no migration will be applied.' }
    $oldHistory = @($history.Rows | ForEach-Object { [string]$_.MigrationId+'|'+[string]$_.ProductVersion }) -join ';'
    $existing = Query "SELECT OBJECT_ID(N'dbo.AspNetUserPasskeys',N'U') AS TableId"
    if ($existing.Rows[0].TableId -isnot [DBNull]) { throw 'Passkey table already exists; stop rather than overwrite.' }
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
    Write-Output ('Preflight passed: seven applied migrations; passkey table absent; captured counts and content fingerprints for {0} tables.' -f $baseline.Count)
    if (-not $Apply) { $tx.Rollback(); $tx.Dispose(); $tx=$null; Write-Output 'READ-ONLY PREFLIGHT ONLY. No schema or data changes.'; return }
    # Run exactly the reviewed script inside the verification transaction.
    # Its BEGIN/COMMIT are nested; any verification failure rolls back the outer transaction.
    $batch = [regex]::Replace($sql,'(?m)^\s*GO\s*$','')
    Execute $batch
    $afterHistory = Query 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId'
    $afterIds = @($afterHistory.Rows | ForEach-Object { [string]$_.MigrationId })
    if (($afterIds -join '|') -ne (($expected+@($target)) -join '|')) { throw 'Unexpected migration history after execution.' }
    $preservedHistory = @($afterHistory.Rows | Where-Object { $_.MigrationId -ne $target } | ForEach-Object { [string]$_.MigrationId+'|'+[string]$_.ProductVersion }) -join ';'
    if ($oldHistory -ne $preservedHistory) { throw 'Earlier migration-history records changed.' }
    $columns = Query "SELECT name,is_nullable,TYPE_NAME(user_type_id) AS Kind,max_length FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserPasskeys')"
    if ($columns.Rows.Count -ne 3 -or @($columns.Rows | Where-Object { $_.is_nullable }).Count -ne 0) { throw 'Unexpected passkey columns/nullability.' }
    $spec = @{ CredentialId=@('varbinary',1024); UserId=@('nvarchar',900); Data=@('nvarchar',-1) }
    foreach ($row in $columns.Rows) {
        if (-not $spec.ContainsKey([string]$row.name) -or $row.Kind -ne $spec[[string]$row.name][0] -or $row.max_length -ne $spec[[string]$row.name][1]) { throw 'Unexpected passkey column storage.' }
    }
    $indexes = Query "SELECT i.name,i.is_primary_key,i.is_unique,i.is_disabled,c.name AS ColumnName FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'dbo.AspNetUserPasskeys') AND ic.key_ordinal>0"
    if ($indexes.Rows.Count -ne 2 -or @($indexes.Rows | Where-Object { $_.is_disabled }).Count -ne 0) { throw 'Unexpected passkey indexes.' }
    $primary = @($indexes.Rows | Where-Object { $_.is_primary_key -and $_.is_unique -and $_.ColumnName -eq 'CredentialId' })
    $lookup = @($indexes.Rows | Where-Object { -not $_.is_primary_key -and $_.ColumnName -eq 'UserId' -and $_.name -eq 'IX_AspNetUserPasskeys_UserId' })
    if ($primary.Count -ne 1 -or $lookup.Count -ne 1) { throw 'Required passkey index missing.' }
    $foreign = Query "SELECT referenced_object_id,delete_referential_action,is_disabled,is_not_trusted FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.AspNetUserPasskeys')"
    $usersId = (Query "SELECT OBJECT_ID(N'dbo.AspNetUsers') AS Id").Rows[0].Id
    if ($foreign.Rows.Count -ne 1 -or $foreign.Rows[0].referenced_object_id -ne $usersId -or $foreign.Rows[0].delete_referential_action -ne 0 -or $foreign.Rows[0].is_disabled -or $foreign.Rows[0].is_not_trusted) { throw 'Passkey account foreign key is not the reviewed trusted no-cascade relationship.' }
    if ((Query 'SELECT COUNT_BIG(*) AS Records FROM dbo.AspNetUserPasskeys').Rows[0].Records -ne 0) { throw 'Unexpected passkey records created.' }
    $afterInventory = @(Tables)
    $preservedInventory = @($afterInventory | Where-Object { $_.TableName -ne 'AspNetUserPasskeys' })
    if ($afterInventory.Count -ne $inventory.Count+1 -or (($inventory | ForEach-Object { $_.Id }) -join ',') -ne (($preservedInventory | ForEach-Object { $_.Id }) -join ',')) { throw 'Unexpected table inventory change.' }
    foreach ($table in $baseline) {
        $row = (Query $table.Query).Rows[0]
        if ($row.Records -ne $table.Records -or $row.Digest -ne $table.Digest) { throw ('Existing records changed in '+$table.Name+'; rolling back.') }
    }
    $tx.Commit(); $tx.Dispose(); $tx=$null
    Write-Output ('APPLIED: '+$target)
    Write-Output ('VERIFIED: eight migration-history rows; empty passkey table; credential primary key and user lookup index; trusted account foreign key; unchanged records/counts across '+$baseline.Count+' tables.')
    Write-Output 'No test users, seed records or business-data writes were performed.'
} catch {
    if ($null -ne $tx) { try { $tx.Rollback() } catch { }; $tx.Dispose(); $tx=$null }
    throw
} finally { $conn.Dispose() }
