# Startet eine Prüfaufnahme (original | mod | inventar), wartet bis das Spiel sich beendet, setzt den Modus zurück
# und zeigt die Mod-Zeilen aus dem BepInEx-Log.
param([Parameter(Mandatory = $true)][ValidateSet("original", "mod", "inventar", "leistung", "leistung-original", "spielstand", "fertigkeiten", "versionsschutz")][string]$Modus)

$game = "D:\SteamLibrary\steamapps\common\One Million Coins"
$cfg = Join-Path $game "BepInEx\config\de.bl4ck969.onemillioncoins.5mio.cfg"
$log = Join-Path $game "BepInEx\LogOutput.log"

if (Get-Process "One Million Coins" -ErrorAction SilentlyContinue) { throw "Das Spiel läuft bereits." }
(Get-Content $cfg -Encoding UTF8) -replace '^Modus = .*', "Modus = $Modus" | Set-Content $cfg -Encoding UTF8
try {
    Start-Process "steam://rungameid/5258340"
    # Warten, bis das Spiel wirklich läuft (Steam braucht beim Kaltstart deutlich länger als 15 s).
    $i = 0
    while (-not (Get-Process "One Million Coins" -ErrorAction SilentlyContinue) -and $i -lt 60) { Start-Sleep -Seconds 3; $i++ }
    if (-not (Get-Process "One Million Coins" -ErrorAction SilentlyContinue)) { throw "Spiel nicht gestartet." }
    Start-Sleep -Seconds 15
    $i = 0
    while ((Get-Process "One Million Coins" -ErrorAction SilentlyContinue) -and $i -lt 100) { Start-Sleep -Seconds 3; $i++ }
    Start-Sleep -Seconds 2
}
finally {
    (Get-Content $cfg -Encoding UTF8) -replace '^Modus = .*', "Modus = aus" | Set-Content $cfg -Encoding UTF8
}
Get-Content $log -Encoding UTF8 | Select-String "5 Mio.]|Exception|Error" | ForEach-Object { $_.Line.Substring(0, [Math]::Min(600, $_.Line.Length)) }
