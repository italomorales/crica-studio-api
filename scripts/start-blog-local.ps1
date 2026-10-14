param([switch]$Build)
$ErrorActionPreference = 'Stop'
$blogRoot = Split-Path $PSScriptRoot -Parent
$blogLocal = Join-Path $blogRoot '.blog-local'
New-Item -ItemType Directory -Force -Path $blogLocal | Out-Null
if ($Build) {
    dotnet build (Join-Path $blogRoot 'CricaStudio.Api/CricaStudio.Api.csproj') -o (Join-Path $blogRoot '.blog-build') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
$blogDbEnv = docker inspect postgres-local-postgres-1 --format '{{json .Config.Env}}' | ConvertFrom-Json
$blogDbPassword = ($blogDbEnv | Where-Object { $_.StartsWith('POSTGRES_PASSWORD=') }).Substring(18)
if (!$blogDbPassword) { throw 'Local PostgreSQL password unavailable' }
$blogKeyFile = Join-Path $blogLocal 'jwt-key.txt'
if (!(Test-Path -LiteralPath $blogKeyFile)) { [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48)) | Set-Content -LiteralPath $blogKeyFile }
$env:ASPNETCORE_ENVIRONMENT = 'BlogTest'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5031'
$env:ConnectionStrings__Database = "Host=127.0.0.1;Port=5432;Database=crica_blog_phase2_local;Username=postgres;Password=$blogDbPassword"
$env:Jwt__Key = (Get-Content -Raw -LiteralPath $blogKeyFile).Trim()
$env:CatalogMedia__Provider = 'local'
$env:CatalogMedia__LocalDirectory = Join-Path $blogLocal 'images'
$env:CatalogMedia__PublicBaseUrl = 'http://localhost:5031'
$env:Blog__FrontendDirectory = Join-Path (Split-Path $blogRoot -Parent) 'front/dist'
$env:Blog__PublicSiteUrl = 'http://localhost:5031'
$env:Cors__AllowedOrigins__0 = 'http://localhost:4174'
$env:Cors__AllowedOrigins__1 = 'http://localhost:5031'
$env:BlogSocial__EnableTestLogin = 'true'
Start-Process -FilePath 'dotnet' -ArgumentList @((Join-Path $blogRoot '.blog-build/CricaStudio.Api.dll')) -WorkingDirectory (Join-Path $blogRoot 'CricaStudio.Api') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $blogLocal 'api.log') -RedirectStandardError (Join-Path $blogLocal 'api.err.log') -PassThru | Select-Object Id
