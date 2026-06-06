<#
.SYNOPSIS
  Fuerza el nivel de elevacion del instalador a "requireAdministrator".

.DESCRIPTION
  Inno Setup compila el stub (SetupLdr) con el manifiesto "asInvoker" y se
  auto-eleva por relanzamiento. En algunos equipos ese relanzamiento no dispara
  el UAC al hacer doble clic, y el instalador solo funciona con clic derecho >
  "Ejecutar como administrador".

  Este script reescribe el manifiesto embebido a "requireAdministrator" para que
  Windows muestre el UAC inmediatamente al ejecutar el .exe. El cambio se hace
  IN-PLACE consumiendo los espacios de relleno del manifiesto, de modo que el
  tamano del archivo NO cambia y los datos comprimidos de Inno no se corrompen.

.NOTES
  Ejecutar despues de compilar el instalador con ISCC:
    pwsh installer\set-admin-manifest.ps1 installer\Output\SportsMax-Setup-X.Y.Z.exe
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath
)

if (-not (Test-Path $ExePath)) { Write-Error "No existe: $ExePath"; exit 1 }

$bytes = [System.IO.File]::ReadAllBytes($ExePath)
$ascii = [System.Text.Encoding]::ASCII.GetString($bytes)

$rx = [regex] 'level="asInvoker"( *)uiAccess'
$m  = $rx.Match($ascii)
if (-not $m.Success) {
    if ($ascii -match 'level="requireAdministrator"') {
        Write-Host "Ya esta en requireAdministrator. Nada que hacer."; exit 0
    }
    Write-Error "No se encontro el manifiesto 'asInvoker' esperado."; exit 1
}

$pad  = $m.Groups[1].Value.Length
$diff = 'requireAdministrator'.Length - 'asInvoker'.Length   # = 11
if ($pad -lt $diff) {
    Write-Error "Relleno insuficiente ($pad espacios) para parchear sin cambiar el tamano."; exit 1
}

$new      = 'level="requireAdministrator"' + (' ' * ($pad - $diff)) + 'uiAccess'
$oldBytes = [System.Text.Encoding]::ASCII.GetBytes($m.Value)
$newBytes = [System.Text.Encoding]::ASCII.GetBytes($new)
if ($newBytes.Length -ne $oldBytes.Length) { Write-Error "Longitud distinta; abortado."; exit 1 }

[Array]::Copy($newBytes, 0, $bytes, $m.Index, $newBytes.Length)
[System.IO.File]::WriteAllBytes($ExePath, $bytes)
Write-Host "OK: manifiesto -> requireAdministrator (tamano sin cambios)."
