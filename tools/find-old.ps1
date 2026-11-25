# Prints the full path of every older GamepadMIDI.exe this PC points at (Start-with-Windows entry and shortcuts).
$ErrorActionPreference = 'SilentlyContinue'
$dest = Join-Path $env:LOCALAPPDATA 'GamepadMIDI\GamepadMIDI.exe'
$found = New-Object System.Collections.ArrayList
$run = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name GamepadMIDI).GamepadMIDI
if ($run) {
    $p = $run -replace '^\s*"([^"]+)".*$', '$1'
    if ($p -eq $run) { $p = ($run -split ' /')[0] }
    [void]$found.Add($p)
}
$sh = New-Object -ComObject WScript.Shell
$dirs = @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('CommonDesktopDirectory'), (Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'))
foreach ($d in $dirs) {
    $l = Join-Path $d 'GamepadMIDI.lnk'
    if (Test-Path $l) { $t = $sh.CreateShortcut($l).TargetPath; if ($t) { [void]$found.Add($t) } }
}
$found | Where-Object { $_ -and ((Split-Path $_ -Leaf) -ieq 'GamepadMIDI.exe') -and ($_ -ine $dest) } | Sort-Object -Unique
