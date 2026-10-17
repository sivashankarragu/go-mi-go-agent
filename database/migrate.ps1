<#
.SYNOPSIS
    Applies GoMigo SQL migrations (database/migrations/*.sql) that have not been applied yet.

.DESCRIPTION
    Forward-only and non-destructive: each pending file runs in its own transaction together with
    its row in schema_migrations. Nothing is ever dropped or rolled back automatically.
    The password is read from the PGPASSWORD environment variable (never pass it on the command line).

.EXAMPLE
    $env:PGPASSWORD = '<your password>'
    .\database\migrate.ps1 -CreateDatabase
#>
[CmdletBinding()]
param(
    [string]$DbHost = 'localhost',
    [int]$Port = 5432,
    [string]$Database = 'gomigo',
    [string]$User = 'postgres',
    [string]$Psql = '',
    [switch]$CreateDatabase,
    [switch]$WithSeeds
)

$ErrorActionPreference = 'Stop'

if (-not $Psql) {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { $Psql = $cmd.Source }
    else {
        $Psql = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $Psql -or -not (Test-Path $Psql)) { throw 'psql.exe not found. Add PostgreSQL bin to PATH or pass -Psql.' }
if (-not $env:PGPASSWORD) { Write-Warning 'PGPASSWORD is not set; psql may prompt or fail.' }

$common = @('-h', $DbHost, '-p', $Port, '-U', $User, '-v', 'ON_ERROR_STOP=1', '-X', '-q')

function Invoke-Psql([string]$db, [string[]]$extra) {
    & $Psql @common -d $db @extra
    if ($LASTEXITCODE -ne 0) { throw "psql failed (exit $LASTEXITCODE)." }
}

if ($CreateDatabase) {
    $exists = & $Psql @common -d postgres -tA -c "SELECT 1 FROM pg_database WHERE datname = '$Database'"
    if ($LASTEXITCODE -ne 0) { throw 'Could not connect to PostgreSQL.' }
    if ("$exists".Trim() -ne '1') {
        Write-Host "Creating database $Database"
        Invoke-Psql 'postgres' @('-c', "CREATE DATABASE `"$Database`" ENCODING 'UTF8'")
    }
}

Invoke-Psql $Database @('-c', @'
CREATE TABLE IF NOT EXISTS schema_migrations (
    version     text        PRIMARY KEY,
    applied_at  timestamptz NOT NULL DEFAULT now()
);
'@)

$applied = (& $Psql @common -d $Database -tA -c 'SELECT version FROM schema_migrations') -split "`r?`n" | Where-Object { $_ }

function Apply-Folder([string]$folder, [string]$prefix) {
    foreach ($file in Get-ChildItem (Join-Path $PSScriptRoot $folder) -Filter *.sql | Sort-Object Name) {
        $version = "$prefix$($file.BaseName)"
        if ($applied -contains $version) { Write-Host "  skip    $version"; continue }
        Write-Host "  apply   $version"
        Invoke-Psql $Database @('--single-transaction', '-f', $file.FullName,
            '-c', "INSERT INTO schema_migrations (version) VALUES ('$version')")
    }
}

Write-Host "Migrating $Database on ${DbHost}:$Port"
Apply-Folder 'migrations' ''
if ($WithSeeds) { Apply-Folder 'seeds' 'seed:' }
Write-Host 'Done.'
