param([Parameter(Mandatory=$true)][string]$CredentialsPath)
$ErrorActionPreference='Stop'
$googleSource=(Resolve-Path -LiteralPath $CredentialsPath).Path
try {
    $googleWeb=(Get-Content -LiteralPath $googleSource -Raw | ConvertFrom-Json -ErrorAction Stop).web
} catch {
    throw 'Não foi possível ler o JSON de credenciais. O conteúdo não foi exibido.'
}
if(!$googleWeb -or [string]::IsNullOrWhiteSpace($googleWeb.client_id) -or [string]::IsNullOrWhiteSpace($googleWeb.client_secret) -or !$googleWeb.client_id.EndsWith('.apps.googleusercontent.com')) {
    throw 'Selecione o JSON de um cliente OAuth Google do tipo aplicativo da Web.'
}
$googlePrivateDirectory=Join-Path $env:LOCALAPPDATA 'CricaStudio/Secrets'
$googlePrivatePath=Join-Path $googlePrivateDirectory 'blog-google.json'
New-Item -Path $googlePrivateDirectory -ItemType Directory -Force | Out-Null
# Restrict the directory before writing the secret. The API runs as this Windows user locally.
$googleUser=[Security.Principal.WindowsIdentity]::GetCurrent().User
$googleDirectoryAcl=Get-Acl -LiteralPath $googlePrivateDirectory
$googleDirectoryAcl.SetAccessRuleProtection($true,$false)
$googleDirectoryAcl.SetAccessRule([Security.AccessControl.FileSystemAccessRule]::new($googleUser,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
Set-Acl -LiteralPath $googlePrivateDirectory -AclObject $googleDirectoryAcl
$googlePrivateConfig=@{BlogSocial=@{Google=@{ClientId=$googleWeb.client_id;ClientSecret=$googleWeb.client_secret}}}
$googlePrivateConfig | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $googlePrivatePath -Encoding utf8
$googleFileAcl=Get-Acl -LiteralPath $googlePrivatePath
$googleFileAcl.SetAccessRuleProtection($true,$false)
$googleFileAcl.SetAccessRule([Security.AccessControl.FileSystemAccessRule]::new($googleUser,'FullControl','Allow'))
Set-Acl -LiteralPath $googlePrivatePath -AclObject $googleFileAcl
Write-Output 'Google configurado no arquivo privado deste usuário do Windows. Nenhum segredo foi exibido ou gravado no repositório.'
if(@($googleWeb.redirect_uris) -notcontains 'http://localhost:5031/api/blog/auth/callback/google') {
    Write-Output 'Antes do teste local, acrescente no Google Cloud: http://localhost:5031/api/blog/auth/callback/google'
}
