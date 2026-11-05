param(
    [string]$Database = "ShowroomPerf",
    [switch]$NoCache,
    [int]$Port = 5060
)

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Default = "Server=.\SQLEXPRESS01;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True"
$env:VehicleCache__Enabled = if ($NoCache) { "false" } else { "true" }
$env:Logging__LogLevel__Default = "Warning"
[Environment]::SetEnvironmentVariable("Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command", "Warning")

dotnet run --project "$PSScriptRoot\..\src\Showroom.Api" -c Release --no-build --no-launch-profile --urls "http://localhost:$Port"
