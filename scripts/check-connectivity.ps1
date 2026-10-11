# End-to-end connectivity check: starts the real quotePublisher, runs the engine in --check mode
# against it, then stops the publisher. Exit code 0 = PASS, 1 = FAIL.
#
# Usage (from the repo root):
#   .\scripts\check-connectivity.ps1
#   .\scripts\check-connectivity.ps1 -Count 50 -DelayMs 10 -Port 58966
param(
    [string]$PublisherProject = "$PSScriptRoot\..\..\quotePublisher\QuotePublisher\quotePublisher.csproj",
    [int]$Port = 58965,
    [int]$Count = 10,
    [int]$DelayMs = 100
)

$EngineProject = "$PSScriptRoot\..\marketMatchingEngine.csproj"

if (-not (Test-Path $PublisherProject)) {
    Write-Host "Publisher project not found at $PublisherProject (pass -PublisherProject <path to quotePublisher.csproj>)"
    exit 1
}

Write-Host "Building publisher and engine..."
dotnet build $PublisherProject --nologo -v q
if ($LASTEXITCODE -ne 0) { exit 1 }
dotnet build $EngineProject --nologo -v q
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Starting publisher on port $Port (one quote every $DelayMs ms)..."
# Start the built dll directly (a hidden `dotnet run` never got as far as starting the app)
$publisherDll = dotnet msbuild $PublisherProject -getProperty:TargetPath
$publisher = Start-Process dotnet -ArgumentList "`"$publisherDll`"", $Port, $DelayMs -PassThru -WindowStyle Hidden

try {
    dotnet run --no-build --project $EngineProject -- --check $Count --port $Port
    $result = $LASTEXITCODE
}
finally {
    taskkill /PID $publisher.Id /T /F | Out-Null
}

exit $result
