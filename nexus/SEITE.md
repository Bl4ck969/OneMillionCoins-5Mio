# Nexus-Mods-Seite „5 Million Coins“

Veröffentlicht am 30.10.2026: https://www.nexusmods.com/onemillioncoins/mods/4 (Mod-ID 4, Spiel-ID 10702).
Vorlage zum Einfügen unter https://www.nexusmods.com/onemillioncoins → „Upload mod“. Stand 1.0.0.
Bei einer neuen Version: Version, Datei und Änderungen anpassen; die Seite selbst bleibt.

## Felder

| Feld | Inhalt |
|---|---|
| Game | One Million Coins |
| Mod name | `5 Million Coins (5 Mio. Münzen)` |
| Category | Miscellaneous (die einzige, die die drei vorhandenen Mods nutzen) |
| Language | English (Seite zweisprachig, Mod Deutsch/Englisch nach Spielsprache) |
| Version | `1.0.0` |
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
[size=5][b]5 Million Coins[/b][/size]
[i]Unofficial fan mod for One Million Coins · Deutsch weiter unten[/i]

An offline mode of its own: a vault rebuilt from the game's own parts into [b]25 floors[/b] holding exactly [b]5,000,000 real coins[/b]. The last one lies in the chest, just like in the original. The normal 1 million game is unchanged.

[size=4][b]Features[/b][/size]
[list]
[*][b]25 floors[/b] from 4.2 m at the top to 14.1 m at the bottom, built at runtime from the original building parts.
[*][b]Extended skill tree[/b]: new branch [b]"Movement"[/b] (running speed, jump height, jetpack). Gold river and Branch manager +300 %, pocket bank radius 15 m, [b][4][/b] recalls the placed pocket bank from anywhere. The "Lower hoppers" skills unlock the deposit stations in growing groups (01–02, 03–05, 06–09, 10–15, 16–24). Start at zero, the full tree costs 434 points.
[*][b]Faster elevator[/b]: 1.5 s ramp, then a steady 7 m/s. Cabin buttons: 00, MENU (all floors), two and one floor up, two and one down. Locked floors show the reason.
[*][b]Local leaderboard[/b] and a completion screen with your rank on this PC.
[*][b]Offline[/b]: the 5 million mode sends nothing to Steam (no leaderboards, achievements, stats or Steam Cloud) and keeps its own save in %LOCALAPPDATA%\OneMillionCoins-5Mio\.
[*][b]Version protection[/b]: after a game update the mod switches itself off until a matching version is released.
[*]Mod texts in German or English, following the game language.
[/list]

[size=4][b]Install[/b][/size]
[list=1]
[*]Requires Windows 10/11 64-bit and One Million Coins (Steam) [b]version 1.2.2[/b].
[*]Unpack the ZIP, quit the game, run [b]OMC-5Mio-Installer.exe[/b] → "Install …".
[*]In the main menu, switch [b]Vault[/b] to "5 million · mod (offline)".
[/list]
The installer finds the game through Steam, checks the game version, backs up every file it would replace and brings BepInEx 5.4.23.6 if none is installed. [b]Uninstall[/b] with the same installer restores the original state. The installer is not signed – Windows may warn ("More info" → "Run anyway").
Not a mod manager download: please use the installer.

[size=4][b]Known issues[/b][/size]
A few spots lack collision (floors 22/23: one handrail bar and one railing foot at a stair corner).

[size=4][b]Written with AI[/b][/size]
Code, installer and this page were written with the help of Claude (Anthropic). The mod was tested in the game by its author and with automated test runs that steer the player and check the building. Source code: [url=https://github.com/Bl4ck969/OneMillionCoins-5Mio]GitHub[/url].
Not made or supported by the game's developer. Use at your own risk.

[size=4][b]License and credits[/b][/size]
MIT license. Bundled unmodified: BepInEx 5.4.23.6 (MIT) and UnityDoorstop 4.6.0 (LGPL-2.1).

[line]

[size=5][b]5 Mio. Münzen[/b][/size]
[i]Inoffizielle Fan-Mod für One Million Coins[/i]

Ein eigener Offline-Modus: ein Tresor aus [b]25 Etagen[/b], gebaut aus den Bauteilen des Originals, mit genau [b]5.000.000 echten Münzen[/b]. Die letzte liegt wie im Original in der Kiste. Das normale Spiel mit 1 Million Münzen bleibt unverändert.

[list]
[*][b]Fertigkeitsbaum[/b] mit neuem Ast [b]„Bewegung“[/b] (Lauftempo, Sprunghöhe, Jetpack); Goldfluss und Filialleiter +300 %, Bankradius 15 m, [b][4][/b] holt die abgestellte Taschenbank von überall zurück; „Untere Trichter“ schalten die Einzahlautomaten in Gruppen frei. Start bei null, der volle Baum kostet 434 Punkte.
[*][b]Schnellerer Aufzug[/b] (1,5 s Anfahrt, dann 7 m/s), Kabinentasten relativ zur aktuellen Etage, gesperrte Etagen nennen den Grund.
[*][b]Lokale Bestenliste[/b] und Abschlussdialog mit deinem Platz auf diesem PC.
[*][b]Offline[/b]: nichts geht an Steam, eigener Spielstand unter %LOCALAPPDATA%\OneMillionCoins-5Mio\.
[*][b]Versionsschutz[/b]: nach einem Spiel-Update schaltet sich die Mod ab, bis eine passende Fassung erscheint.
[/list]

[b]Installation:[/b] ZIP entpacken, Spiel beenden, [b]OMC-5Mio-Installer.exe[/b] starten → „Installieren …“, im Hauptmenü [b]Tresor[/b] auf „5 Millionen · Mod (offline)“ stellen. Voraussetzung: Windows 10/11 64 Bit, Spielversion [b]1.2.2[/b]. Deinstallieren über denselben Installer stellt den Originalzustand her.

[b]Mit KI geschrieben:[/b] Code, Installer und diese Seite entstanden mit Hilfe von Claude (Anthropic), getestet im Spiel und mit automatischen Prüfläufen. Nicht vom Entwickler des Spiels, nicht von ihm unterstützt. Nutzung auf eigene Gefahr. Quelltext auf [url=https://github.com/Bl4ck969/OneMillionCoins-5Mio]GitHub[/url].
```

## Datei

| Feld | Inhalt |
|---|---|
| File | `release\OMC-5Mio-1.0.0.zip` (dieselbe wie im GitHub-Release, SHA-256 `8a4689d921cb25fc987dcfe45087fa60abbb463e1f6d21e901e1acf66dfef88c`) |
| File name | `5 Million Coins 1.0.0` |
| Version | `1.0.0` |
| Category | Main file |
| Mod manager download | aus (Installer) |
| Description | `Installer with BepInEx 5.4.23.6. Unpack, quit the game, run OMC-5Mio-Installer.exe.` |

## Bilder (Reihenfolge, erstes = Titelbild)

Aus `bilder\`: `titel.jpg`, `haufen.jpg`, `menue.jpg`, `fertigkeiten.jpg`, `aufzug.jpg`, `abschluss.jpg`, `bestenliste.jpg`.
Alle sind echte Aufnahmen aus dem Spiel (Prüfmodus `bilder`), keine KI-Bilder.
