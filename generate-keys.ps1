# Script para generar claves seguras para CunaPay API
# Ejecuta este script en PowerShell: .\generate-keys.ps1

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Generador de Claves - CunaPay API" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Función para generar string aleatorio
function Generate-RandomString {
    param(
        [int]$Length,
        [string]$Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
    )
    $random = ""
    for ($i = 0; $i -lt $Length; $i++) {
        $random += $Chars[(Get-Random -Maximum $Chars.Length)]
    }
    return $random
}

# Función para generar hexadecimal
function Generate-HexString {
    param([int]$Length)
    $hex = "0123456789ABCDEF"
    $random = ""
    for ($i = 0; $i -lt $Length; $i++) {
        $random += $hex[(Get-Random -Maximum $hex.Length)]
    }
    return $random
}

Write-Host "Generando claves..." -ForegroundColor Yellow
Write-Host ""

# 1. JWT Secret (32 caracteres mínimo)
$jwtSecret = Generate-RandomString -Length 64
Write-Host "Jwt__Secret:" -ForegroundColor Green
Write-Host $jwtSecret -ForegroundColor White
Write-Host ""

# 2. Crypto Master Key (64 caracteres hex = 32 bytes)
$cryptoKey = Generate-HexString -Length 64
Write-Host "Crypto__MasterKeyHex:" -ForegroundColor Green
Write-Host $cryptoKey -ForegroundColor White
Write-Host ""

# 3. Tron Custody Private Key (64 caracteres hex)
$tronKey = Generate-HexString -Length 64
Write-Host "Tron__CustodyPrivateKey:" -ForegroundColor Green
Write-Host $tronKey -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "¡Claves generadas!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANTE: Copia estas claves y guárdalas en un lugar seguro." -ForegroundColor Red
Write-Host "No las compartas ni las subas a GitHub." -ForegroundColor Red
Write-Host ""
Write-Host "Puedes copiarlas directamente a las variables de entorno en Render." -ForegroundColor Yellow

