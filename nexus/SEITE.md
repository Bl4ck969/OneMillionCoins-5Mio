# Nexus-Mods-Seite „5 Million Coins“

Veröffentlicht am 30.10.2026: https://www.nexusmods.com/onemillioncoins/mods/4 (Mod-ID 4, Spiel-ID 10702).
Vorlage zum Einfügen unter https://www.nexusmods.com/onemillioncoins → „Upload mod“. Stand 1.1.0 (03.11.2026).
Bei einer neuen Version: Version, Datei und Änderungen anpassen; die Seite selbst bleibt.

## Felder

| Feld | Inhalt |
|---|---|
| Game | One Million Coins |
| Mod name | `5 Million Coins (5 Mio. Münzen)` |
| Category | Miscellaneous (die einzige, die die drei vorhandenen Mods nutzen) |
| Language | English (Seite zweisprachig, Mod Deutsch/Englisch nach Spielsprache) |
| Version | `1.1.0` (Nexus zeigte bei 1.0.0 nur „1“ – Feld vollständig ausfüllen) |
| Author | `Bl4ck969` |
| Tags | **AI-Generated Content** (Code und Installer mit Claude geschrieben), **AI Media** (Seitentext mit Claude geschrieben); dazu Gameplay/Offline, falls angeboten |
| Adult content | nein |
| Requirements | keine – der Installer bringt BepInEx 5.4.23.6 mit (nutzt ein vorhandenes BepInEx 5) |
| Permissions | MIT-Lizenz: Weitergabe, Änderung und Nutzung in eigenen Mods erlaubt, mit Namensnennung |
| Source | https://github.com/Bl4ck969/OneMillionCoins-5Mio |

## Credits (Pflichtfeld „Credit original authors“)

```
BepInEx 5.4.23.6 – BepInEx team (MIT) – https://github.com/BepInEx/BepInEx
UnityDoorstop 4.6.0 – NeighTools (LGPL-2.1) – https://github.com/NeighTools/UnityDoorstop
HarmonyX – BepInEx team (MIT) – https://github.com/BepInEx/HarmonyX
MonoMod (MIT) – https://github.com/MonoMod/MonoMod
Mono.Cecil – Jb Evain (MIT) – https://github.com/jbevain/cecil
One Million Coins – OYU Games. The mod rebuilds the vault at runtime from the parts of the installed game and ships no game files.
Code and installer written with Claude (Anthropic).
```

## Summary

Nexus schneidet bei rund 250 Zeichen ab (30.10.2026 gemessen: deutsche und englische Fassung über die API beide dort
gekürzt; die englische endet auf „Installer included“). Kürzere Fassung für das nächste Mal:

```
Offline mode with a 25-floor vault and exactly 5,000,000 real coins: extended skill tree with a jetpack branch, faster elevator, local leaderboard. The normal game stays untouched, nothing goes to Steam. Installer included.
```

Fassung vom 30.10.2026:

```
Offline mode with a 25-floor vault and exactly 5,000,000 real coins: extended skill tree with a jetpack branch, faster elevator, local leaderboard. The normal 1 million game stays untouched and nothing goes to Steam. Installer included (BepInEx 5). Deutsch/English.
```

## Description (BBCode)

```
[b]Note / Hinweis:[/b] The file is being reviewed by Nexus – meanwhile, download it from [url=https://github.com/Bl4ck969/OneMillionCoins-5Mio/releases/tag/v1.1.0]GitHub[/url] (same file). / Die Datei wird von Nexus geprüft – bis dahin bitte von [url=https://github.com/Bl4ck969/OneMillionCoins-5Mio/releases/tag/v1.1.0]GitHub[/url] laden (dieselbe Datei).

[line]

[size=5][b]5 Million Coins[/b][/size]
[i]Unofficial fan mod for One Million Coins · Deutsch weiter unten[/i]

An offline mode of its own: a vault rebuilt from the game's own parts into [b]25 floors[/b] holding exactly [b]5,000,000 real coins[/b]. The last one lies in the chest, just like in the original. The normal 1 million game is unchanged.

[size=4][b]Features[/b][/size]
[list]
[*][b]25 floors[/b] – 4.2 m at the top to 14.1 m at the bottom, built at runtime from the original building parts.
[*][b]"Movement" branch[/b] – a new skill branch with running speed, jump height and a jetpack.
[*][b]Stronger skills[/b] – Gold river and Branch manager +300 %, pocket bank radius 15 m, [4] recalls the placed pocket bank from anywhere.
[*][b]Deposit stations[/b] – the "Lower hoppers" skills unlock them in groups: 01–02, 03–05, 06–09, 10–15, 16–24. You start at zero, the full tree costs 434 points.
[*][b]Faster elevator[/b] – 1.5 s ramp, then a steady 7 m/s. Cabin buttons: 00, MENU (all floors), one and two floors up or down. Locked floors show the reason.
[*][b]Local leaderboard[/b] – plus a completion screen with your rank on this PC.
[*][b]Offline[/b] – nothing goes to Steam (no leaderboards, achievements, stats or Steam Cloud), own save in %LOCALAPPDATA%\OneMillionCoins-5Mio\.
[*][b]Version protection[/b] – after a game update the mod stays off at first. "Switch on anyway …" at the bottom left of the main menu turns it on for exactly that game version, at your own risk; if it cannot load there, it undoes all its changes.
[*][b]Two languages[/b] – mod texts in German or English, following the game language.
[/list]

[size=4][b]Install[/b][/size]
[list=1]
[*]Requires Windows 10/11 64-bit and One Million Coins (Steam). Tested with [b]version 1.2.2[/b] (Steam builds 26290693 and 26367772 of 3 Nov 2026).
[*]Unpack the ZIP, quit the game, run [b]OMC-5Mio-Installer.exe[/b] → "Install …".
[*]In the main menu, switch [b]Vault[/b] to "5 million · mod (offline)".
[/list]
The installer finds the game through Steam, checks the game version, backs up every file it would replace and brings BepInEx 5.4.23.6 if none is installed. On a game version it has not been tested with, it asks first. [b]Updating:[/b] run the new installer → "Reinstall …" – saves, leaderboard and settings are kept. Uninstalling with the same installer restores the original state. The installer is not signed – Windows may warn ("More info" → "Run anyway"). Not a mod manager download.

[b]Without the installer:[/b] the file "Manual install" contains only the mod DLL. Install [url=https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6]BepInEx 5.4.23.6 (x64)[/url] into the game folder, then unpack the mod into the game folder (it lands in BepInEx\plugins\OneMillionCoins5Mio). Updating: replace the DLL. Removing: delete that folder.

[size=4][b]Known issues[/b][/size]
A few spots lack collision (floors 22/23: one handrail bar and one railing foot at a stair corner).

[size=4][b]Written with AI[/b][/size]
Code, installer and this page were written with the help of Claude (Anthropic). The mod was tested in the game by its author and with automated test runs that steer the player and check the building. Not made or supported by the game's developer. Use at your own risk. Source code on [url=https://github.com/Bl4ck969/OneMillionCoins-5Mio]GitHub[/url].

[size=4][b]License and credits[/b][/size]
MIT license. Bundled unmodified: BepInEx 5.4.23.6 (MIT) and UnityDoorstop 4.6.0 (LGPL-2.1).

[line]

[size=5][b]5 Mio. Münzen[/b][/size]
[i]Inoffizielle Fan-Mod für One Million Coins[/i]

Ein eigener Offline-Modus: ein Tresor, aus den Bauteilen des Spiels neu gebaut, mit [b]25 Etagen[/b] und genau [b]5.000.000 echten Münzen[/b]. Die letzte liegt wie im Original in der Kiste. Das normale Spiel mit 1 Million Münzen bleibt unverändert.

[size=4][b]Funktionen[/b][/size]
[list]
[*][b]25 Etagen[/b] – oben 4,2 m, unten 14,1 m hoch, zur Laufzeit aus den Bauteilen des Originals gebaut.
[*][b]Ast „Bewegung“[/b] – ein neuer Fertigkeitsast mit Lauftempo, Sprunghöhe und Jetpack.
[*][b]Stärkere Fertigkeiten[/b] – Goldfluss und Filialleiter +300 %, Bankradius 15 m, [4] holt die abgestellte Taschenbank von überall zurück.
[*][b]Einzahlautomaten[/b] – die Fertigkeiten „Untere Trichter“ schalten sie in Gruppen frei: 01–02, 03–05, 06–09, 10–15, 16–24. Start bei null, der volle Baum kostet 434 Punkte.
[*][b]Schnellerer Aufzug[/b] – 1,5 s Anfahrt, dann gleichmäßig 7 m/s. Kabinentasten: 00, MENU (alle Etagen), eine und zwei Etagen hoch oder runter. Gesperrte Etagen nennen den Grund.
[*][b]Lokale Bestenliste[/b] – dazu ein Abschlussdialog mit deinem Platz auf diesem PC.
[*][b]Offline[/b] – nichts geht an Steam (keine Bestenlisten, Erfolge, Statistiken oder Steam Cloud), eigener Spielstand unter %LOCALAPPDATA%\OneMillionCoins-5Mio\.
[*][b]Versionsschutz[/b] – nach einem Spiel-Update bleibt die Mod zunächst aus. Mit „Trotzdem einschalten …“ unten links im Hauptmenü läuft sie auf genau dieser Spielversion, auf eigene Gefahr; lässt sie sich dort nicht laden, nimmt sie alle Änderungen zurück.
[*][b]Zwei Sprachen[/b] – Texte der Mod auf Deutsch oder Englisch, je nach Spielsprache.
[/list]

[size=4][b]Installation[/b][/size]
[list=1]
[*]Voraussetzung: Windows 10/11 64 Bit und One Million Coins (Steam). Geprüft mit [b]Version 1.2.2[/b] (Steam-Build 26290693 und 26367772 vom 03.11.2026).
[*]ZIP entpacken, Spiel beenden, [b]OMC-5Mio-Installer.exe[/b] starten → „Installieren …“.
[*]Im Hauptmenü [b]Tresor[/b] auf „5 Millionen · Mod (offline)“ stellen.
[/list]
Der Installer findet das Spiel über Steam, prüft die Spielversion, sichert jede Datei, die er ersetzen würde, und bringt BepInEx 5.4.23.6 mit, falls keins installiert ist. Auf einer ungeprüften Spielversion fragt er vorher. [b]Update:[/b] den neuen Installer starten → „Neu installieren …“ – Spielstände, Bestenliste und Einstellungen bleiben. Deinstallieren über denselben Installer stellt den Originalzustand her. Der Installer ist nicht signiert – Windows kann warnen („Weitere Informationen“ → „Trotzdem ausführen“). Kein Download für Mod-Manager.

[b]Ohne Installer:[/b] Die Datei „Manual install“ enthält nur die Mod-DLL. [url=https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6]BepInEx 5.4.23.6 (x64)[/url] in den Spielordner entpacken, danach die Mod in den Spielordner entpacken (sie landet in BepInEx\plugins\OneMillionCoins5Mio). Update: DLL ersetzen. Entfernen: diesen Ordner löschen.

[size=4][b]Bekannte Probleme[/b][/size]
An wenigen Stellen fehlt die Kollision (Etage 22/23: ein Handlaufstab und ein Geländerfuß an einer Treppenecke).

[size=4][b]Mit KI geschrieben[/b][/size]
Code, Installer und diese Seite entstanden mit Hilfe von Claude (Anthropic). Getestet im Spiel vom Autor und mit automatischen Prüfläufen, die die Spielfigur steuern und das Gebäude prüfen. Nicht vom Entwickler des Spiels, nicht von ihm unterstützt. Nutzung auf eigene Gefahr. Quelltext auf [url=https://github.com/Bl4ck969/OneMillionCoins-5Mio]GitHub[/url].

[size=4][b]Lizenz und Credits[/b][/size]
MIT-Lizenz. Unverändert mitgeliefert: BepInEx 5.4.23.6 (MIT) und UnityDoorstop 4.6.0 (LGPL-2.1).
```

## Dateien

Zwei Dateien ab 1.1.0 (03.11.2026): Das Installer-Paket kam bei 1.0.0 in Quarantäne (nicht signierte EXE mit eingebettetem
BepInEx-Archiv) und wird es voraussichtlich wieder. Das DLL-Paket hat weder EXE noch Archiv im Archiv.

| Feld | Installer-Paket | DLL-Paket |
|---|---|---|
| File | `release\OMC-5Mio-1.1.0.zip` (SHA-256 `74b81853b5558831955bdf67259b113aacadc1f8ee8f668e78f6e0c9121f22d1`) | `release\OMC-5Mio-1.1.0-manuell.zip` (SHA-256 `661d20c3e24c44cdfb5fda0b247ea48aa1282aa19b5a7d1ef66d384f5b4a1a04`) |
| File name | `5 Million Coins 1.1.0` | `5 Million Coins 1.1.0 - Manual install` |
| Version | `1.1.0` | `1.1.0` |
| Category | Main file | Main file |
| Mod manager download | aus (Installer) | aus (eigene Ordnerstruktur `BepInEx\plugins\…`, in den Spielordner entpacken) |
| Description | `Installer with BepInEx 5.4.23.6. Unpack, quit the game, run OMC-5Mio-Installer.exe.` | `Mod DLL only, no installer. Needs BepInEx 5.4.23.6 x64. Unpack into the game folder.` |

Beide Dateien sind dieselben wie im GitHub-Release v1.1.0; die DLL darin ist in beiden die geprüfte (`98acdb60…`).

## Changelog 1.1.0 (Nexus: „Changelogs“ der Version)

```
- Works with the One Million Coins update of 3 Nov 2026 (Steam build 26367772; the game still shows version 1.2.2).
- Soft version protection: on an untested game version the mod stays off until you choose "Switch on anyway …" in the main menu; the installer asks instead of refusing. If the mod cannot load, it undoes all its changes.
- Updating with the installer keeps saves, leaderboard and mod settings. Saves from 1.0.0 load unchanged.
```

Die alte Datei 1.0.0 als „Old version“ einordnen (nicht löschen). Den Quarantäne-Hinweis oben in der Beschreibung erst nach der Freigabe entfernen.

## Bilder (Reihenfolge, erstes = Titelbild)

Aus `bilder\`: `titel.jpg`, `haufen.jpg`, `menue.jpg`, `fertigkeiten.jpg`, `aufzug.jpg`, `abschluss.jpg`, `bestenliste.jpg`.
Alle sind echte Aufnahmen aus dem Spiel (Prüfmodus `bilder`), keine KI-Bilder.
