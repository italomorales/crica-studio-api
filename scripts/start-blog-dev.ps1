param([switch]$Restart)
$ErrorActionPreference='Stop'
$blogRoot=Split-Path $PSScriptRoot -Parent
$apiDirectory=Join-Path $blogRoot 'CricaStudio.Api'
$assembly=Join-Path $blogRoot '.blog-dev-build/CricaStudio.Api.dll'
if(!(Test-Path -LiteralPath $assembly)){throw 'Build the API into .blog-dev-build first.'}
$listeners=@(Get-NetTCPConnection -LocalPort 5030 -State Listen -ErrorAction SilentlyContinue)
foreach($processId in @($listeners.OwningProcess | Select-Object -Unique)){
    $apiProcess=Get-CimInstance Win32_Process -Filter "ProcessId=$processId"
    $originalExecutable=Join-Path $apiDirectory 'bin/Debug/net10.0/CricaStudio.Api.exe'
    if(!$Restart){throw 'Port 5030 is in use. Pass -Restart to restart the local API.'}
    if($apiProcess.ExecutablePath -ne $originalExecutable -and !$apiProcess.CommandLine.Contains($assembly)){
        throw 'Port 5030 belongs to another application; refusing to stop it.'
    }
    Stop-Process -Id $processId
}
$logDirectory=Join-Path $blogRoot '.blog-local'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5030'
Start-Process -FilePath dotnet -ArgumentList @($assembly) -WorkingDirectory $apiDirectory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDirectory 'dev-api.log') -RedirectStandardError (Join-Path $logDirectory 'dev-api.err.log') -PassThru | Select-Object Id
