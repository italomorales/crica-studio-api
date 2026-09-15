param(
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$Name = ""
)

$ErrorActionPreference = "Stop"

function Get-Pbkdf2Sha512Hash {
    param(
        [Parameter(Mandatory = $true)][string]$PlainPassword,
        [Parameter(Mandatory = $true)][byte[]]$Salt,
        [Parameter(Mandatory = $true)][int]$Iterations,
        [Parameter(Mandatory = $true)][int]$Length
    )

    # Implementação PBKDF2-HMAC-SHA512 compatível com Windows PowerShell 5.1/.NET Framework.
    $passwordBytes = [System.Text.Encoding]::UTF8.GetBytes($PlainPassword)
    $hmac = New-Object System.Security.Cryptography.HMACSHA512 -ArgumentList (,$passwordBytes)
    try {
        $saltAndBlock = New-Object byte[] ($Salt.Length + 4)
        [Array]::Copy($Salt, 0, $saltAndBlock, 0, $Salt.Length)
        $saltAndBlock[$Salt.Length] = 0
        $saltAndBlock[$Salt.Length + 1] = 0
        $saltAndBlock[$Salt.Length + 2] = 0
        $saltAndBlock[$Salt.Length + 3] = 1

        $current = $hmac.ComputeHash($saltAndBlock)
        $combined = New-Object byte[] $current.Length
        [Array]::Copy($current, $combined, $current.Length)

        for ($round = 2; $round -le $Iterations; $round++) {
            $current = $hmac.ComputeHash($current)
            for ($index = 0; $index -lt $combined.Length; $index++) {
                $combined[$index] = $combined[$index] -bxor $current[$index]
            }
        }

        $result = New-Object byte[] $Length
        [Array]::Copy($combined, $result, $Length)
        return $result
    }
    finally {
        $hmac.Dispose()
    }
}

$normalizedEmail = $Email.Trim().ToLowerInvariant()
if ($normalizedEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$' -or $normalizedEmail.Length -gt 320) {
    throw "Informe um e-mail válido com até 320 caracteres."
}
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "A senha não pode ser vazia."
}

$iterations = 210000
$salt = New-Object byte[] 16
$random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $random.GetBytes($salt)
}
finally {
    $random.Dispose()
}

$hash = Get-Pbkdf2Sha512Hash -PlainPassword $Password -Salt $salt -Iterations $iterations -Length 32
$safeName = $Name.Replace("'", "''")
$safeEmail = $normalizedEmail.Replace("'", "''")

@"
INSERT INTO cricastudio.admin_users (id, email, name, password_hash, password_salt, password_iterations)
VALUES ('$(New-Guid)', '$safeEmail', '$safeName', '$([Convert]::ToBase64String($hash))', '$([Convert]::ToBase64String($salt))', $iterations);
"@
