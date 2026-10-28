[Reflection.Assembly]::LoadFrom("d:\BEN9\3\ConcertShieldSystem\backend\CheckinAPI\bin\Debug\net8.0\Npgsql.dll") | Out-Null
$conn = New-Object Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=05_ticket_db;Username=postgres;Password=123456")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT current_database();"
$res = $cmd.ExecuteScalar()
Write-Host "Connected to:" $res
$conn.Close()
