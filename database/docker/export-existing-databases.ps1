param(
    [string]$HostName = "host.docker.internal",
    [int]$Port = 5432,
    [string]$Username = "postgres"
)

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI was not found. Start Docker Desktop before exporting databases."
}

$null = & docker version --format '{{.Server.Version}}'
if ($LASTEXITCODE -ne 0) {
    throw "Docker Engine is unavailable. Start Docker Desktop before exporting databases."
}

$securePassword = Read-Host "Password for source PostgreSQL user '$Username'" -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
}

$importDirectory = Join-Path (Get-Location) "database\import"
New-Item -ItemType Directory -Path $importDirectory -Force | Out-Null
$resolvedImportDirectory = (Resolve-Path $importDirectory).Path
$clientImage = "postgres:16-alpine"

$serverVersion = & docker run --rm --env PGPASSWORD $clientImage psql `
    --no-password --host=$HostName --port=$Port --username=$Username --dbname=postgres `
    --no-align --tuples-only --command="SHOW server_version_num;"
if ($LASTEXITCODE -ne 0) {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    throw "Could not connect to source PostgreSQL. Verify host, port, password, and PostgreSQL network access."
}

$serverVersionNumber = 0
if (-not [int]::TryParse(($serverVersion | Select-Object -Last 1).Trim(), [ref]$serverVersionNumber)) {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    throw "Could not determine the source PostgreSQL server version."
}
$serverMajorVersion = [math]::Floor($serverVersionNumber / 10000)
$dumpImage = "postgres:$serverMajorVersion-alpine"

$databases = & docker run --rm --env PGPASSWORD $clientImage psql `
    --no-password --host=$HostName --port=$Port --username=$Username --dbname=postgres `
    --no-align --tuples-only `
    --command="SELECT datname FROM pg_database WHERE datistemplate = false AND datallowconn = true AND datname <> 'postgres' ORDER BY datname;"
if ($LASTEXITCODE -ne 0) {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    throw "Could not list databases on source PostgreSQL."
}

$databases = @($databases | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($databases.Count -eq 0) {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    throw "No non-template databases other than postgres were found on the source server."
}

try {
    foreach ($database in $databases) {
        if ($database -notmatch '^[A-Za-z0-9_]+$') {
            throw "Unsupported database name '$database'. Export it manually because its name contains characters not supported by this script."
        }

        $containerDumpPath = "/dumps/$database.dump"
        Write-Output "Exporting database '$database' using PostgreSQL $serverMajorVersion client."
        & docker run --rm --env PGPASSWORD `
            --mount "type=bind,source=$resolvedImportDirectory,target=/dumps" `
            $dumpImage pg_dump --no-password --host=$HostName --port=$Port --username=$Username `
            --format=custom --file=$containerDumpPath --dbname=$database
        if ($LASTEXITCODE -ne 0) {
            throw "pg_dump failed for '$database'. Check PostgreSQL access and Docker Desktop host connectivity."
        }
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Output "Database exports complete: $($databases -join ', '). Keep dump files private; they are excluded from Git."
