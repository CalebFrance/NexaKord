param([string]$Action, [string]$Target)
$ErrorActionPreference = 'SilentlyContinue'
$sh = New-Object -ComObject WScript.Shell
$desk = [Environment]::GetFolderPath('Desktop')
$prog = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
$common = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'GamepadMIDI.lnk'
$mine = @((Join-Path $desk 'GamepadMIDI.lnk'), (Join-Path $prog 'GamepadMIDI.lnk'))
if ($Action -eq 'remove') {
    foreach ($l in ($mine + $common)) { if (Test-Path $l) { Remove-Item $l -Force; Write-Output ('removed ' + $l) } }
}
if ($Action -eq 'create') {
    foreach ($l in $mine) {
        $s = $sh.CreateShortcut($l)
        $s.TargetPath = $Target
        $s.WorkingDirectory = Split-Path $Target
        $s.Description = 'GamepadMIDI v2'
        $s.Save()
        Write-Output ('created ' + $l)
    }
}
