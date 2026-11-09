# One Million Coins – Modus „5 Mio. Münzen“

Stand, Entscheidungen, Erkenntnisse und nächste Schritte. **Stand: 09.11.2026, morgens.** Dieses Dokument ist der Einstieg für die Fortsetzung.

Projekt: `E:\Setups\Spiele\Mods\One Million Coins` (bis 25.10.2026 `E:\Setups\OneMillionCoins-100M`). BepInEx-5-Plugin `OneMillionCoins5Mio` (bis 30.10.2026 `OneMillionCoins100M`), Version 1.1.1, MIT-Lizenz, Git-Repository (Abschnitt 8).

---

## 1. Kurz: Wo stehen wir, was kommt als Nächstes

**09.11.2026 – Spiel-Update, Mod 1.1.1 (nur neuer geprüfter Stand)** (Bl4ck969: „Es gab wieder ein Spielupdate“):
- **Steam-Update 09.11.2026, 07:12**, Build 26367772 → **26508511**, Spiel meldet weiter „1.2.2“. Neu: `Assembly-CSharp.dll`, `globalgamemanagers`, `boot.config`, `level1`, `resources.assets`, `sharedassets1.assets`, `ThirdPartyNotices\NotoSansJP-OFL.txt`; `lib_burst_generated.dll` unverändert.
- **Dekompilat-Diff (6 Dateien), nur Sprache Japanisch (9. Sprache):** `GameLocalization` (`ja`, `IsJapanese`), `LocalizationFonts` (Noto Sans JP), `InputPrompts` (japanische Tastennamen → Platzhalter), `LocalizedGUI.Fit` (schrumpft Schrift bei Japanisch/Chinesisch), `CreditsPanel`, `MainMenuUI.Draw` (Logo bei Japanisch tiefer; sonst nur umbenannte lokale Variablen). Keine gepatchte Methode geändert (die Mod hängt an `MainMenuUI.OnGUI/Play/DrawConfirmation/DrawPlayerRecords/OpenLeaderboards/DrawLeaderboards`, nicht an `Draw`); keine Transpiler-Stelle betroffen. Mod-Texte im japanischen Spiel englisch (wie Português BR).
- **Mod 1.1.1** = 1.1.0 + Eintrag in `ReleaseInfo.Tested`. DLL `ecfba920…`, im Spiel und in beiden Paketen byte-gleich (`release\OMC-5Mio-1.1.1.zip` `8ef3f716…`, EXE `2a96f7e7…`; `…-manuell.zip` `1131c835…`), `installer_test.py` alles grün.
- **Geprüft 09.11.2026, 07:16–07:24, mit dieser DLL:** `mod` – Bericht bis auf Reihenfolge und Abgrab-Zufall gleich mit 03.11. (87 von 722.298, 50, Loch-Test 100 %, Aufzug), `bauteile.csv` sortiert nur bei fliegenden Münzen/Staub anders; `fertigkeiten` (251/13/434, Automatengruppen, Original 238/409); `spielstand`; `spielstand-laden` mit Kopie von Bl4ck969s `spielstand.json` (Mod 1.1.0, Build 26367772): 3 Durchgänge, 251 Fertigkeiten, speicherbar, Kopie gelöscht; `versionsschutz` ×2 + `versionsschutz-fehler` (0 / 102 / 102 zurückgenommen); `leistung` (73 FPS, Netz 0,6 + 0,1 ms, Bandvergleich ≤ 86 Pixel, Bergtest verschüttet wie bekannt). Spielstand und Bestenliste vorher/nachher byte-gleich. Prüfergebnisse vom 03.11. lagen vor den Läufen im Scratchpad (`pruefung-1103`).
- **Handprobe bestanden** (Bl4ck969, 09.11.2026 bis 07:29: „Spiel durchgespielt, passt“; Log: Build 26508511 geprüft, 102 Patches, keine Fehler). Danach GitHub-Release `v1.1.1` mit beiden Paketen. Offen: Nexus von Hand (Changelog in `nexus\SEITE.md`). Nicht im Spiel angesehen: Mod-Menü in japanischer Spielsprache (die Sprache steht in den Spieleinstellungen von Bl4ck969).

**03.11.2026 – Spiel-Update, Mod 1.1.0, weicher Versionsschutz** (Bl4ck969: „es gab ein Update … nicht so radikal designen, dass sie nur mit 1.2.2 läuft, sondern fragt beim Installer ‚willst du wirklich‘ oder wenn es installiert ist, dass man es trotzdem zum Laufen bekommt“; danach: „die Savegames müssen auch funktionieren zu unserer vorherigen Version“):
- **Steam-Update 03.11.2026, 18:56**, Steam-Build 26290693 → **26367772**. ⚠ **Das Spiel meldet weiter „1.2.2“** (`globalgamemanagers`, Hauptmenü) – Stände unterscheiden sich nur am Build und an den Prüfsummen. Mod 1.0.0 zeigte deshalb „Spielversion 1.2.2 ist nicht geprüft (geprüft: 1.2.2)“. Anzeige jetzt mit Build: „1.2.2 (Build 26367772)“ (`ReleaseInfo.SteamBuildOf` liest `appmanifest_5258340.acf`).
- **Was sich geändert hat** (Dekompilat diff): `GranularCoinPile` kocht das Kollisionsnetz **im Hintergrund** (`RefreshCollisionInBackground`, zwei Netze im Wechsel, Bau in `BuildCollisionMesh`); im laufenden Spiel wird `RefreshCollision` nur noch beim Laden und am Ende aufgerufen. Neue Sprache Português BR (8 statt 7; Mod-Texte dort Englisch), `LocalizedGUI.Fit` und eine Breite in `DrawInventory` (beides nicht gepatcht). `level1`, `resources.assets`, `sharedassets1.assets`, `lib_burst_generated.dll` neu – der Prüfbericht `mod` ist trotzdem bis auf Zufallswerte identisch mit dem vom 30.10. (87 von 722.298, 50, 0, 25/25).
- ⚠ **Der Band-Patch hätte auf dem neuen Build still nicht gegriffen** (Prefix auf `RefreshCollision`), der ganze 5-Mio.-Haufen hätte wieder ein Netz bekommen. `PilePatches` setzt jetzt dort an, wo das Netz entsteht: neuer Build → Prefix auf `BuildCollisionMesh` (baut das Band ins Zwischennetz, Backen und Tausch macht das Spiel, Kochoptionen des Spiels), alter Build → wie bisher. Gemessen: 0,6 ms Netz + 0,1 ms Übergabe je Neubau, 0,17 Neubauten je Bild, `RefreshCollisionInBackground` 0,1 ms je Bild; 70–73 FPS beim Graben.
- **Weicher Versionsschutz:** Unbekannter Stand → Mod zunächst aus (0 Patches), Hinweis unten links mit **„Trotzdem einschalten …“** → Warnfeld (deckend, über dem Münzhaufen, fängt Klicks ab) → **„Einschalten“** schreibt die Kennung des Stands (`ReleaseInfo.Fingerprint`: je 16 Zeichen der SHA-256 von `Assembly-CSharp.dll` und `globalgamemanagers`) nach `[Kompatibilitaet] FreigegebeneVersion`; wirkt ab dem nächsten Start (Patches und die HUD-Felder `static readonly` gehen nur beim Laden), Knöpfe „Spiel beenden“/„Rückgängig“. Läuft die Mod freigegeben: Hinweis „läuft auf der nicht geprüften Spielversion … auf eigene Gefahr“ + „Ausschalten“. Nach dem nächsten Update passt die Kennung nicht mehr → fragt neu.
- **Rücknahme bei Fehler:** Wirft ein Modul beim Patchen (fehlende Methode/Feld auf einem fremden Stand), nimmt `Plugin.Awake` alle Patches zurück (`harmony.UnpatchSelf()`), Hinweis „ließ sich nicht laden“ + „Nicht mehr laden“. Was ein Modul außerhalb von Harmony gesetzt hat (HUD-Felder des Asts Bewegung), bleibt.
- **Installer:** ungeprüfter Stand ist ein gelber Hinweis statt rote Sperre; „Installieren …“ fragt nach dem Hinweistext: Ja = installieren und einschalten (schreibt die Freigabe per `Put` in die Mod-Einstellungen, vorhandene Datei wird gesichert und beim Deinstallieren zurückgelegt) / Nein = installieren, aber aus / Abbrechen. Kommandozeile: `--installieren --einverstanden --ungeprueft`. `installer_test.py` jetzt 11 Fälle (D neu, D2 mit drei Varianten der Einstellungsdatei, D3).
- **Spielstände der Vorversion laden** – gemessen mit einer Kopie von Bl4ck969s `spielstand.json` (Mod 1.0.0, Build 26290693) im neuen Prüfmodus `spielstand-laden`: 2 Durchgänge, 251 Fertigkeiten, Spielzeit, Haufen, speicherbar, keine Meldung; Kopie danach gelöscht, Original nur gelesen. ⚠ Der erste Versuch lief im Testmodus und scheiterte mit „SAVE COULD NOT BE LOADED“: `SaveManager.TryLoadFile` verlangt `testingLoadout == _testingSave`. Der Ladetest läuft deshalb **ohne** Testmodus auf eigener Datei `spielstand.ladetest.json`.
- **„Die Bestenliste ist resettet“** (Bl4ck969 während der Prüfläufe): war sie nicht – `bestenliste.tsv` byte-gleich, 3 Einträge (1. 1:52:20.83 vom 30.10.). Der Prüflauf `spielstand` öffnet sichtbar die **Prüfliste** `bestenliste.pruefung.tsv` (nur Testeinträge); das sah nach einer geleerten Liste aus. `spielstand-laden` öffnet zum Schluss die echte Liste (nur lesen, `Leaderboard.Record` gesperrt) – Bild `pruefung\spielstand-laden\bestenliste.png`.
- **Bergtest** (60-s-Leistungstest, danach 15 s unter den Füßen graben): Die Figur wird im bis zu 160 m hohen Berg **verschüttet** (fällt mit ~40 m/s, steckt unter der obersten Schicht) – gleich mit Band „sofort gebacken“ (alter Mechanismus, Versuch je 2 Durchgänge abwechselnd), „im Hintergrund“ und mit der Kollision des Spiels. Also kein Update- und kein Band-Fehler, sondern der Extremfall (Schacht von 4 m mit ~24 m/s). Der Test wählt die offene Säule nächst der Mitte, nicht die höchste: Der zweite Durchgang beginnt im Schacht des ersten. Ausgabe jetzt mit „unter der obersten Schicht“, „auf Boden“, „schnellster Fall“. Am 30.10. ohne Messung dieser Art – ob 1.2.2 im selben Fall genauso verschüttete, ist nicht belegt (der sofort-Versuch spricht dafür).
- Geprüft mit der DLL `98acdb60…` (im Spiel und in beiden Paketen byte-gleich): `mod`, `spielstand`, `spielstand-laden`, `fertigkeiten`, `leistung`, `leistung-original` (Original 66–90 FPS wie am 26.10.), `versionsschutz` ×2 + `versionsschutz-fehler`. Bl4ck969s Handprobe bestanden (03.11.2026 abends).
- **Update 1.0.0 → 1.1.0 mit dem Installer** (Bl4ck969: „Überleben die Spielstände der Menschen, die die Mod einfach downloaden?“): gemessen an einer Kopie seiner echten 1.0.0-Installation (Protokoll vom 1.0.0-Installer) mit Kopien seiner Spielstände – Spielstände byte-gleich. ⚠ Dabei aufgefallen: Das Neu-Installieren löschte mit dem BepInEx-Ordner die **Mod-Einstellungen**; `Tresor` stand danach auf „Original“, „Weiter“ führte ins normale Spiel – der 5-Mio.-Stand sah verschwunden aus. Behoben: `Setup.Install` hebt die Einstellungen auf und legt sie über `Put` wieder hin (Testfälle I, I2; `installer_test.py` jetzt 13 Fälle).
- **Zwei Pakete:** `release\OMC-5Mio-1.1.0.zip` (Installer, SHA-256 `74b81853…`, EXE `fa746b15…`) und `release\OMC-5Mio-1.1.0-manuell.zip` (nur `BepInEx\plugins\OneMillionCoins5Mio\` mit DLL, Anleitung `installer\pakete\LIESMICH-manuell.txt`, Lizenz, Prüfsummen; SHA-256 `661d20c3…`) – das Installer-Paket kommt bei Nexus voraussichtlich wieder in Quarantäne. Beide baut `release.py`. Die ZIP-Prüfsummen ändern sich bei jedem Bau (Dateizeiten), EXE und DLL nicht.
- **Veröffentlicht 03.11.2026, 20:11:** GitHub-Release [v1.1.0](https://github.com/Bl4ck969/OneMillionCoins-5Mio/releases/tag/v1.1.0) (Tag auf `153d4e0`, beide Pakete, Prüfsummen von GitHub bestätigt). Nexus: Bl4ck969 lädt von Hand hoch – Vorlage `nexus\SEITE.md` (zwei Dateien, Changelog, Version `1.1.0` ganz ausfüllen, 1.0.0 als „Old version“).
- **Nexus, 03.11.2026 abends:** Datei 8 (1.0.0) „Old“, vom Support freigegeben (`MANUALLY_VERIFIED`); Datei 12 (1.1.0, Installer) `QUARANTINED` wie erwartet, Mail an den Support ist raus; Datei 13 (1.1.0, nur DLL) `VERIFIED` – der zweite Weg trägt. Beschreibung mit Download-Kasten oben (GitHub oder DLL-Paket), nach der Freigabe wieder entfernen. Den Scan-Status liefert die öffentliche GraphQL-API: `modFiles(modId:4, gameId:10702) { fileId name category primary scannedV2 }`. Auf Nexus „Third-party content: Yes“ (BepInEx/UnityDoorstop im Installer-Paket).
- Dateinamen auf Nexus: „OMC 5Mio 1.1.0 - Installer“ (12) und „OMC 5Mio 1.1.0 - Manual install“ (13). Offen: Freigabe Datei 12 (danach Download-Kasten raus), Steam-Forumsbeitrag, Spieltest Jetpack/Automatengruppen.

**30.10.2026, abends – Installer am echten Spiel, Abschlussdialog, Bilder fürs README:**
- **Installer** zum ersten Mal an Bl4ck969s Spielordner (17:43, per Fenster aus dem Release-Entwurf): Dateien byte-gleich mit der Handinstallation, 229/229 Steam-Dateien unverändert. Die vorherige Handinstallation ist außerhalb des Spielordners gesichert (löschen nur auf sein Wort).
- **Abschlussdialog** (`CompletionPatches.cs`; Bl4ck969: „der Abschlussscreenshot ist falsch, ich habe ja 5 Mio eingesammelt“): Das Spiel schreibt fest „1,000,000 coins collected“ und zeigt rot „nicht eingereicht“, weil `SpeedrunSubmission.CompletionIssue` genau 1.000.000 Münzen verlangt. Im Mod-Modus jetzt die Münzzahl des Haufens in der Übersetzung des Spiels, kein roter Kasten, statt des Steam-Status „Platz N in deiner Bestenliste auf diesem PC“ (Testlauf: „nicht gewertet“). 5 Stellen per Transpiler, 102 gepatchte Methoden. ⚠ Das erste Suchmuster `1([^0-9]?)000\1000` traf nie: .NET liest `\1000` als Oktalzeichen, nicht als Rückverweis plus „000“ – aufgefallen nur am Bild (englische Zeile im deutschen Spiel). Benannte Gruppe `\k<sep>`.
- **Prüfmodus `bilder`** (`PictureRun.cs`; Bl4ck969: „Wir brauchen noch Screenshots“): nimmt Menü und Bestenliste auf, startet den Lauf auf dem Test-Spielstand, schaltet den Testmodus aus (keine Testknöpfe im Baum), gräbt bis Etage 12 ab, fotografiert Baum, Schacht, Jetpack-Flug, Haufen, Aufzug und Abschluss (bester echter Lauf aus `bestenliste.tsv`, nur gelesen) und beendet ohne Speichern. Die Steam-Karte im Menü (Name, Bestzeiten) wird nur in diesem Modus weggelassen (103 gepatchte Methoden). Auswahl verkleinert in `bilder\` (1600×900), eingebunden im README.
  - ⚠ Nach der Rückkehr **ohne Speichern** ins Menü stand oben links über 30 s „Bankverlauf wird geladen …“; nach einer Rückkehr mit Speichern nicht, und Bl4ck969 sieht im Spiel „Eingezahlt“. Deshalb nimmt der Modus das Menü vor dem Lauf auf. Ursache nicht geklärt (Vermutung: eine während des Mod-Laufs gesperrte Anfrage an die Sammler-Bestenliste).
  - ⚠ **Ein geänderter Spielstand zwischen zwei Prüfläufen war Bl4ck969 selbst:** `spielstand.json` um 20:48:16, eine eigene Spielsitzung bis 20:48:50 (`Player-prev.log`), die Prüfläufe davor endeten 20:45:50, danach begann der nächste 20:49:17. Vor jedem Alarm Zeitstempel und `Player-prev.log` ansehen; der alte Stand liegt dann als `.bak` daneben.
  - Gemessen: gesetzte Spielzeit läuft bei den langsamen Bildern des großen Haufens bis zur Aufnahme 0,2 s weiter → `Time.timeScale = 0` für die Aufnahme. Verdiente Punkte rechnet das Spiel bei jeder Münze neu → ausgegebene Punkte erst direkt vor der Aufnahme angleichen. Ein Tooltip folgt dem echten Mauszeiger → `SetCursorPos` aus dem Baum.

**30.10.2026, ~08:50 – Release 1.0.0 vorbereitet** (Bl4ck969: „ja bitte alles erledigen“; meine Empfehlungen umgesetzt):
- **Name** `OneMillionCoins5Mio` statt „100M“ (Abschnitt 8), **Version 1.0.0**, **MIT-Lizenz** (`LICENSE`, auch im Installer unter „Lizenzen“).
- **Englisch:** Tresor-Auswahl, Bestenliste, Bestätigungsdialog folgen der Spielsprache (`ModText`; Deutsch, sonst Englisch – das Spiel hat 7 Sprachen, Fertigkeiten und Aufzugsmenü waren schon zweisprachig). Der Versionshinweis folgt der Windows-Sprache (kein Spielcode auf ungeprüften Ständen).
- **Release-Paket** `release\OMC-5Mio-1.0.0.zip` über `werkzeuge\release.py` (Installer, LIESMICH de/en, Lizenzen, SHA-256). Plugin im Paket byte-gleich mit dem im Spiel geprüften.
- Geprüft im Spiel: `versionsschutz` (0 Patches der Mod), `fertigkeiten` (101 Patches, Werte wie vorher), `spielstand` (Menü und Bestenliste deutsch und mit `ForceEnglish` englisch, passt ohne Überlauf; im Test bleiben „RANG/ZEIT/ZURÜCK“ deutsch, weil das deutsch eingestellte Spiel bekannte englische Wörter selbst übersetzt). Installer-Tests 8/8 (neu: alter Plugin-Ordner wird abgelehnt). Bl4ck969s Spielstand und Einstellungen unverändert.
- **Steam-Seite** (30.10.2026): Entwickler und Publisher „OYU Games“, erschienen 13.10.2026; zu Mods, Workshop, EULA, Anti-Cheat keine Aussage, im Netz auch nichts gefunden.
- **Veröffentlicht als öffentliches GitHub-Repo** [Bl4ck969/OneMillionCoins-5Mio](https://github.com/Bl4ck969/OneMillionCoins-5Mio) (Entscheidung 30.10.2026: öffentlich, ohne Vornamen, frische Historie). Release v1.0.0 liegt als **Entwurf** bereit (ZIP seit 30.10.2026 abends SHA-256 `8a4689d9…`, mit Abschlussdialog-Fix) und wird erst nach Bl4ck969s Spieltest von Ast Bewegung/Jetpack/Automatengruppen freigegeben: `gh release edit v1.0.0 --repo Bl4ck969/OneMillionCoins-5Mio --draft=false`. Dependabot-Alerts an (tägliches Audit), Themen `bepinex`, `unity-mod`, `one-million-coins`, `game-mod`. Ein `pre-push`-Hook (`.git/hooks`, nur lokal) sperrt den Zweig `archiv-lokal`. Die DLL im Paket (`088791…`) ist mit den Prüfläufen `spielstand`, `fertigkeiten`, `versionsschutz`, `mod` und `bilder` im Spiel geprüft und dort installiert (vorher `945dac…`, Abschlussdialog mit 1.000.000). Restpunkte ohne Kollision (Etage 22/23) stehen in der LIESMICH.

**30.10.2026, vormittags – Spiel-Updates, Installer, Versionsschutz** (Bl4ck969: „Was passiert bei Updates? … Installer für eine Veröffentlichung“; Einzelheiten Abschnitt 9):
- **Steam-Updates lassen die Mod-Dateien in Ruhe, und keine Originaldatei fehlt.** Gemessen gegen Steams Dateiliste (`depotcache\5258341_<manifest>.manifest`): 229 von 229 Originaldateien byte-gleich (SHA-1), zusätzlich nur die 27 Dateien von BepInEx und Mod. Das Update vom 29.10. tauschte 6 Spieldateien und keine Mod-Datei; keiner der Mod-Namen steht in Steams Liste. Die Gefahr liegt woanders: Nach einem Update **lief die Mod ungeprüft auf neuem Code weiter**.
- **Versionsschutz im Plugin** (`src/ReleaseInfo.cs`, `Plugin.Awake`, `src/VersionNotice.cs`): SHA-256 von `Assembly-CSharp.dll` und `globalgamemanagers` gegen die Liste geprüfter Stände (jetzt: 1.2.2, Steam-Build 26290693). Unbekannt → **kein einziger Patch**, Hinweis unten links im Hauptmenü, das Spiel läuft unverändert. Für Prüfläufe nach einem Update: `[Kompatibilitaet] UngepruefteVersion = true` – die Prüfwerkzeuge bleiben dafür vollständig in der Mod (Bl4ck969: „die Entwicklerinstrumente vielleicht bei einem Update benötigen“).
  - **Geprüft 30.10.2026, ~08:30, im Spiel:** bekannte Version → „Spielversion 1.2.2: geprüft“, 101 gepatchte Methoden, Prüfmodus `fertigkeiten` mit denselben Werten wie zuvor; neuer Prüfmodus `versionsschutz` (spielt eine unbekannte Version vor) → **0 Patches der Mod** (die 5 übrigen gehören BepInEx: `Console.SetOut`, `Assembly.LoadFile/LoadFrom/get_Location/get_CodeBase`), Original-Hauptmenü mit Hinweis (`pruefung\versionsschutz\hauptmenue.png`). Kein Absturz, keine Mod-Zeile in `Player.log`, Spielstände byte-gleich.
  - **Absturz gefunden und behoben:** Die ersten beiden Fassungen ließen das Spiel im Inaktiv-Fall **nativ abstürzen** – also genau nach einem echten Update bei jedem Spieler. Der Chainloader läuft in `Application..cctor`; eine Komponente mit `Start`/`OnGUI`, dort angelegt, verträgt Unity 6000.6 nicht – weder per `AddComponent` aus `Plugin.Awake` noch als `Start()` am Plugin selbst (Abschnitt 10). Jetzt legt `SceneManager.sceneLoaded` den Hinweis an.
- **Voreinstellungen für neue Installationen** (gespeicherte Werte bleiben, bei Bl4ck969 unverändert): `[Pruefung] Ordner` = `<Spiel>\BepInEx\pruefung` statt `E:\…`, `[Prototyp] LeistungAuchImNormalspiel` = `false`; die Beschreibung von `Modus` nennt den Prüfmodus als Werkzeug für Entwicklung und Spiel-Updates.
- **Installer** `installer\` → `OMC-5Mio-Installer.exe` (eine Datei, .NET Framework 4.7.2, BepInEx und Plugin eingebettet): findet das Spiel über Steam, prüft Version und laufendes Spiel, zeigt den Hinweis (KI-geschrieben, inoffiziell, verändert das Spiel) mit Zustimmungs-Haken, protokolliert jede Änderung, sichert Überschriebenes, rollt bei Fehlern zurück; Deinstallation stellt den Zustand vor der Installation her, fragt nach den Mod-Spielständen und weist den Originalzustand gegen Steams Dateiliste nach. Deutsch/Englisch nach Windows-Sprache.
- Geprüft mit `werkzeuge\installer_test.py` (7 Fälle an nachgebauten Spielordnern, Prüfsummen aller Dateien vorher/nachher): alles grün; echte Spielstände und echter Spielordner unverändert. Fenster per `--bildschirmfoto` angesehen (de/en).

**30.10.2026 – Einzahlautomaten über Skills (Bl4ck969 spielt den Mod normal durch):** Im Mod waren alle Automaten von Anfang an offen – ein Überbleibsel aus der Prototyp-Zeit (`WorldPatches.StationUnlockedPrefix` gab immer „offen“). Jetzt schalten die fünf Skills „Unterer Trichter“ (1211, 1213–1216) **wachsende Gruppen** frei: **01–02, 03–05, 06–09, 10–15, 16–24** (Bl4ck969: „der erste Skillpunkt Etage 1 und 2, der zweite 3 bis 5 usw.“; der Rest von mir fortgesetzt). Einstellbar über `[Etagen] Automatengruppen` (Standard `2,5,9,15,24`, die letzte Gruppe reicht immer bis unten). Namen und Texte der Skills nennen die Gruppe (deutsch aus dem Wortlaut des Spiels abgeleitet: „Untere Trichter 03–05“, „…die Einzahlstationen auf den Ebenen 03–05…“). Umsetzung in `SkillPatches.cs`. Geprüft (`fertigkeiten`): je Skill herausgenommen sind genau seine Etagen gesperrt, mit allen Skills 25/25 offen, beide Spielstände unberührt.

**29.10.2026, ~21:30 – Abschluss (Bl4ck969: „Ich denke wir sind fertig“):**
- **Testschalter `AlleFertigkeiten`/`TestPunkte` entfernt** (Code und Konfiguration). Das normale Spiel startet bei null mit `spielstand.json`; das Testmenü „Test builds / Late builds“ im Baum erscheint nur im Testmodus und ist damit weg. Testmodus mit allen Fertigkeiten gibt es nur noch in Prüfläufen (`Plugin.TestLoadout` = Prüfmodus ≠ „aus“, Spielstand `spielstand.test.json`). Der alte Test-Spielstand bleibt als Datei liegen, wird aber nicht mehr geladen.
- **Punktedeckel = voller Baum:** Das Spiel deckelt die verdienten Punkte bei den Kosten seiner Skills (409). Im 5-Mio.-Modus endet die Vergabe jetzt bei **434** (409 + 25 für Ast Bewegung), damit alles erlernbar ist und nicht mehr. Kurve unverändert: 125 Punkte bis 900.000 Münzen, dann je 25.704 einer → Punkt 434 bei **8,84 Mio.** gesammelten Münzen (über Durchgänge hinweg, also im zweiten Lauf). Geprüft: 20 Mio. Münzen → 434 im Mod, 409 im Original.

**29.10.2026, ~20 Uhr – Fertigkeiten nach Bl4ck969s Wunsch (`src/SkillPatches.cs`, nur im 5-Mio.-Modus):**
- **Goldfluss** (`Gold river`, 30602, letzter Rutschen-Skill): Rutschenabfluss +300 % statt +100 %.
- **Filialleiter** (`Branch manager`, 30222, letzter Taschenbank-Skill): Sammelrate +300 % statt +100 %, Bankradius 15 statt 12 m (Bl4ck969 schrieb „15 %“, umgesetzt als 15 m). Mit allen Skills: Sammelrate 480/s statt 240/s, Rutschen 183,7/s statt 91,9/s.
- **[4] holt die abgestellte Taschenbank zurück** – von überall, direkt in die Hand (wie 1 s [F] daneben). [F] neben der Bank geht weiter. Tooltip der Taschenbank nennt das.
- Umsetzung: Die Werte stehen in den gemeinsamen `SkillDefinition`-Objekten des Spiels (`Bonuses`, `Description`); der Mod setzt sie vor jeder Boni-Berechnung (`RestoreSkills`, `ApplyLearnedReward`) passend zum Modus und stellt im Original die Werte des Spiels wieder her. Deutsche Texte als zusätzliche Zeilen im Übersetzungskatalog.
- Geprüft mit neuem Prüfmodus `fertigkeiten` (zweimal): Werte, Texte (Deutsch), Radius, Rückruf aus 8 m, danach im Hauptmenü wieder Originalwerte. Im Spiel von Bl4ck969 noch nicht gesehen.
- **Ast „Bewegung“ (Nr. 7, `src/MovementBranch.cs`, nur im 5-Mio.-Modus)**, gebaut auf Bl4ck969s „ich sehe den Ast nicht“: Wurzel „Leichtfüßig“ + 12 Ränge (IDs 1700–1712), abwechselnd Lauftempo +15 % und Sprunghöhe +20 %, zusammen **+100 % / +100 %** (6 → 12 m/s, 1,65 → 3,3 m); Kosten wie die Rutschen-Spur, 25 Punkte. Schlussstein **Jetpack**: Sprungtaste in der Luft halten → Schub 30 m/s² bis 8 m/s, loslassen → normaler Fall, kein Treibstoff. Unten neben [R] ein Feld mit Turbinen-Icon und Taste „SPACE“ (folgt der Tastenbelegung). Spalte zwischen „Zusatz“ und „Rutschen“, schmal wie die Rutschen, Icons aus dem Spiel (über `SourceId`).
  - Umsetzung: Skills nur im Mod-Modus in `SkillCatalog.ById` und über `SkillTreeDefinition.All` (Postfix) sichtbar, Route über `SkillCatalog.DevelopmentRoute`. Reihenfolge `{0,4,5,1,2,3,7,6}` im Konstruktor von `SkillTreeMap` (Transpiler) und an den 6 Stellen im HUD, die `SkillTreeLayout.BranchOrder` lesen; `_treeColumns` auf 8; `BrowserAccents`/`BrowserNames` beim Laden des Plugins auf 8 Einträge (vor dem ersten JIT, sonst bettet Mono das alte Feld ein). Die Kartenansicht `DrawProficiencyBrowser` ist in v1.2 toter Code – nicht angefasst.
  - Geprüft (Prüfmodus `fertigkeiten`): 251 Skills, 13/13 gelernt, Lauftempo und Sprunghöhe ×2, Jetpack steigt mit 8,0 m/s und landet nach dem Loslassen, Bilder `baum.png`, `jetpack_feld.png`; im Hauptmenü wieder 238 Skills und 7 Spalten. Bl4ck969 hat ihn noch nicht gespielt.

**Stand 29.10.2026, spätabends:** Prüfung „feste Punkte“ **1.951 → 87 von 722.298** offen (−96 %; die Gesamtzahl sinkt, weil weniger Splitter-Inseln entstehen). Rest: ein Handlaufstab und ein Gitterfuß an einer Treppenecke auf Etage 22/23 (siehe Nächste Schritte). Unsichtbare Kollider unverändert 50 (nur Lampenhalter), Treppen 0 Befunde, Loch-Test 0 Pixel, Aufzug 25/25, Spielstand-Rundlauf in Ordnung. Etagenbau im Normalspiel 2,7 s (committeter Stand gegengemessen: 2,5 s).

**Erledigt am 29.10.:**
- **Loch im Boden auf Etage 01:** An der Treppenankunft (West, x −14…−8,4, z −8…−5,2) fehlte der Deck-Kollider – wer von Etage 02 hochkam, lief ins Leere. Ursache: Der Partner eines Kolliders war die *zuletzt gefundene* Insel darin; an Deck 00 ein Deckteil (Baustein 0), an Deck 01–04 ein Treppenteil (Baustein darunter). Jetzt gehören **Deckplatten** (`Joined deck NN`) fest zum Boden ihres Decks; jede Etage hat ihr ganzes Deck.
- **Treppen-Kollider fest an ihrer Treppe:** Alles unter `Stair connection i to j` (Stufen, Podeste, Podestträger, Wangen, Pfosten, Handläufe – nicht die Lampenhalter) bekommt Baustein 5−j und folgt der Stufenlinie. Vorher übernahmen sie die Regel ihres nächsten sichtbaren Teils: Die oberste Stufe unter Deck 00 hing an Baustein 0 und fehlte auf Etage 02 (Schritt 0,38 m), Stufen-Kollider lagen bis 13 cm neben der sichtbaren Stufe.
- **Stufenecken:** Ein Punkt gehört auch zur Treppe, wenn er ≤ 0,25 m neben einer Trittfläche liegt (bisher nur ≤ 1,5 m von der Stufenmitte; die Ecken 2,5 m breiter Stufen liegen weiter weg). Kantenleisten und ein 16,8 m langer Messing-Handlauf der Nordtreppe gingen vorher „mit der Decke“ und schwebten.
- **Schutzgitter an der Ankunft:** Trägt ein Kollider ein Treppenteil, geht er mit dieser Treppe (Partner = Treppen-Insel mit den meisten gehaltenen Eckpunkten).
- **Aufzugshalt als Baugruppe:** Teile in `Atlas station N` gehören zu dessen Baustein und stehen am Boden; gestreckt wird nur, was bis an die Decke reicht (Führungsschiene). Mit dazu gehören sichtbare Teile aus Sammel-Meshes, die ein Kollider des Halts hält (Mitte oder mehr als die Hälfte der Eckpunkte ≤ 8 cm), und die Lichter der Lampe am Sturz (`Vault fixture lighting/Lift N`). Vorher schwebte der Sturz auf den unteren Etagen bis 1,4 m über den Pfosten, die Lampe leuchtete von oben, ein Pfosten-Kollider hing an Baustein 5 und fehlte auf Etage 14–22.
- **Untere 1,5 m stehen am Boden:** Teile ganz in den unteren 1,5 m einer Etage sind „Boden“ statt „anteilig“ (eine Stange am Schutzgitter der Pooltreppe schwebte).
- **Prüfskript:** wartet, bis das Spiel wirklich läuft (bei kaltem Steam setzte es den Prüfmodus nach 15 s zurück, bevor das Spiel ihn las). Der Inventar-Modus stürzte bei den eingefärbten Schnitten ab (Mauern abgeschaltet) – behoben.

**Abends 29.10. (Bl4ck969 nach dem Spieltest: „sieht super aus, aber der Fahrstuhl …“), `src/ElevatorPatches.cs`:**
- **Fahrprofil:** Das Spiel fährt jede Strecke als *eine* S-Kurve (Dauer = Strecke × 1,875 / 7 m/s, Spitze 7 m/s in der Mitte). Bei 136 m hieß das 36 s, davon je ein Drittel Anfahren und Bremsen. Jetzt: 1,5 s weich auf 7 m/s, gleichmäßig, 1,5 s weich bremsen (Konfig `[Aufzug]`). Gemessen mit dem Spiel selbst: 65 m in 10,8 s, 136 m in 20,9 s, 90 % der Spitze nach 1,2 s. Fahrten bis 10,5 m (Anfahr- plus Bremsweg) wie im Original (4,2 m in 2,1 s).
- **Kabinentasten** (Bl4ck969s Belegung, Leserichtung): `00` · `MENU` / zwei höher · eine höher / zwei tiefer · eine tiefer – auf Etage 12 also `00 MENU / 10 11 / 14 13`. Die Tasten wechseln nach jeder Fahrt; noch nicht freigeschaufelte Etagen zeigt die Lampe orange, und das Spiel meldet beim Drücken „Clear the gold …“. Ohne Etage (ganz oben/unten) steht „--“. `MENU` öffnet das Aufzugsmenü des HUD mit allen Etagen (Schrift halb so groß, sonst läuft sie über den Rand). Die Leserichtung kommt aus der Schreibrichtung der Tastenbeschriftung; die alte Belegung war `00 02 07 13 23 24`.
- **Spiel-Update am 29.10.2026, 18:28** (Assembly-CSharp.dll neu): Mod läuft, alle Offline-Sperren gesetzt. Das Münzziel wird an **36 Stellen in 27 Methoden** umgestellt – das ist *nicht* neu: 32 in `GameProgressManager` (unverändert, gegen den Code vom 25.10. verglichen) + 1 `SaveManager.TryLoadFile` + 2 `CoinFieldManager.TryDecodeDensity` + 1 Zählwerk. Geändert u. a. `DepositMachine` (Abgabe frei, wenn der Haufen unter der Trichterlippe liegt), `GoldDiggerHUD` (nur Lage des „Unstuck“-Knopfs), Lokalisierung, Credits. Aufzugsklassen unverändert.

**Stand 26.10.2026, mittags:** 25 Etagen (4,2 → 14,12 m), **genau 5.000.000 Münzen** (Konfig `[Etagen] Muenzen`); wie im Original legt das Spiel die letzte in die Kiste → gesammelt werden 4.999.999. Füllhöhe +10 cm gegenüber der Etagenplanung. Prüfmodus grün: Treppen 0 Befunde, Loch-Test 0 Pixel (Gegenprobe 100 %, mit der echten Sichtweite der Spielkamera), Aufzug 25/25 Etagen.

**Erledigt am 26.10.:**
- **Aufzug ist kein Fehler:** Spielregel „Then excavate its shaft and landing“ – Schacht und Landeplatz bis 0,3 m unter den Boden freisaugen. Original und Mod verhalten sich im Abgrab-Test gleich.
- **Himmel von unten:** Die Spielkamera sieht nur 100 m weit (Original 48 m hoch). Der Mod hebt die Sichtweite auf Gebäudehöhe + 80 m (285 m). Der Loch-Test nutzt jetzt die echte Kamerasichtweite – vorher rechnete er mit 1000 m und übersah das.
- **Leistung 10 → 69–71 FPS** beim Abgraben (Original 67–88), Haufen 8 ms je Bild. Drei Ursachen, je gemessen:
  1. Kollisionsnetz des ganzen Haufens bis 12×/s neu gekocht (70 ms) → `PilePatches`: Netz nur aus den Schichten ±10/8 m um den Spieler, PhysX-Option „CookForFasterSimulation“ aus. Jetzt 0,6 + 4–6 ms je Neubau.
  2. `Pose`/`Sides` liefen über alle 78.000 Schichten (16 ms) → `PoseBand`: dieselben Burst-Jobs des Spiels bekommen je Säule nur sichtbare Schichten (Band um den Spieler + oberste Münzschicht jeder Säule); übersprungene Münzen stehen als „Ersatzschicht“ weit unter dem Gebäude. Jetzt 2,8 ms.
  3. Prüfung **Bildvergleich**: je 6 Blickrichtungen mit und ohne Band, abweichende Pixel zählen. Erste Fassung (nur Höhenband) ließ Lichthof und Treppenlöcher leer – Bl4ck969 sah es auch. Regel „oberste Münzschicht je Säule ist immer sichtbar“ behebt das; Restabweichungen nur Magnet in der Hand und HUD.
- **Menü:** Auswahl **Tresor** rechts neben „Speicherplatz“ („1 Million · Original“ / „5 Millionen · Mod (offline)“, Konfig `[Menue] Tresor`). „Weiter“ und „Neues Spiel“ des Spiels richten sich danach. Alter Extra-Knopf entfällt.
- **Mod-Spielstände:** in `%LOCALAPPDATA%\OneMillionCoins-5Mio\spielstand.json` (Prüfläufe → `spielstand.test.json`). **Nicht** unter LocalLow – Steam Auto-Cloud lädt den ganzen LocalLow-Spielordner hoch (`remotecache.vdf`, sogar Player.log). Pfadwächter: Jeder Speicher-/Ladezugriff im Mod-Modus außerhalb des Mod-Ordners wird abgebrochen. Ladegrenzen 1.000.000 in `SaveManager.TryLoadFile` und `CoinFieldManager.TryDecodeDensity` umgestellt. Speichern 1,3 s / 8,9 MB, nur beim Verlassen. Rundlauf (Prüfmodus `spielstand`): speichern → Pause-Weg ins Hauptmenü → Weiter → geladen, speicherbar. Echter Spielstand per Prüfsumme unverändert.
- **Offline-Bestenliste:** jeder abgeschlossene Lauf → `bestenliste.tsv` (Prüfläufe: `bestenliste.pruefung.tsv`). Bei Tresor „5 Millionen“ zeigt der Knopf „Bestenlisten“ diese Liste statt Steam. Gewertet wie im Original nur Durchgang 1 ohne Testmodus. JsonUtility schreibt Klassen aus dem Mod-Assembly nur als `{}` – deshalb Zeilenformat.
- **Player.log:** BepInEx schrieb ohne Konsole über den `UnityLogListener` alle Mod-Zeilen ins Unity-Log, das Steam Cloud hochlädt. Der Mod entfernt den Listener; übrig bleibt nur BepInEx' Startzeile „Loading [One Million Coins - 5 Mio.]“.

**Nachmittags 26.10. (Bl4ck969: „Geländer haben an manchen Stellen keine Kollision, Fahrstuhl blockt noch“):**
- **Neue Prüfung „feste Punkte“:** Für jedes sichtbare Teil werden bis zu 16 Eckpunkte erfasst, die im Original in einem festen Kollider liegen (5 cm). Im Mod wird jeder dieser Punkte an seiner neuen Lage auf jeder Etage gegen die Kollider getestet (Haufen ausgeblendet). Ausgaben `durchlaufstellen.txt` (je Teil) und `durchlauf_nach_kollider.txt` (je haltendem Original-Kollider).
- **Ergebnis vorher: 56.211 von 735.456 Punkten offen.** Zwei Ursachen, beide behoben:
  1. **Durchgehende Kollider auf eine Etage geschrumpft.** Mauern (`South/East/West/North masonry`, `Masonry pier`), `Continuous service column`, `Continuous shaft frame` bekamen über die Partner-Suche die Etage ihres nächsten sichtbaren Teils und existierten nur dort. Jetzt bleibt ein als durchgehend erkannter Kollider durchgehend (`Inventory.cs`). → 9.575 offen.
  2. **Handlaufstäbe in alter Neigung.** `Continuous handrail` sind 7 × 7 cm dünne Stäbe über die ganze Treppe. Sie übernahmen die Verschiebung ihres nächsten Partnerpunkts – bei einem kurzen Partner an einem Ende bekamen beide Enden dieselbe Verschiebung, Neigung und Länge blieben wie im Original (13,24° statt gestreckt). Jetzt folgen alle Treppen-Kollider der Stufenlinie an jedem ihrer Enden, genau wie das sichtbare Geländer (`FloorBuilder.cs`). → **1.951 offen (−96,5 %)**, 187 Teile.
- **Aufzug:** Münzschichten in Schacht und Landeplatz (`CheckShaftColumns`) sind im Mod genauso aufgebaut wie im Original (je Etage eine Schicht, dazu 42 durchgehende Säulen im Schacht). Kein Bauunterschied. Die Sperre greift, sobald auf irgendeiner Etage zwischen Kabine und Ziel noch Münzen am Schachtrand liegen; bei 25 Etagen trifft das leichter. Meine Abgrab-Simulation leert alle Schichten ab der Zieletage und ist damit großzügiger als der Spieler.
- **Gesichert:** lokales Git-Repository im Projektordner (nichts auf GitHub), `pruefung/` ausgenommen.

**Nächste Schritte:**
1. Restliche 87 offene Punkte: ein Handlaufstab auf Etage 23 (gerader Kollider gegen stufige Linie) und ein Gitterfuß an der Ecke zwischen oberster Stufe und Deck auf Etage 22/23 (sichtbares Teil nach Mitte über der Stufe, Kollider nach Mitte über dem Deck; bis 8 cm).
2. Unabhängiger Prüfer über Gebäude und Bildvergleich, bevor die Fassung als „bereit“ gilt.
3. Fertigkeitsbaum-Erweiterung: Start bei null ✅ (29.10.), Ast Bewegung ✅; offen nur, falls gewünscht: Stationen über Skills, Punktekurve auf einen 5-Mio.-Lauf strecken.

✅ Schwebender Handlauf an der Wand der Bodenhalle (Bl4ck969s Bild 26.10.): nicht mehr vorhanden – weder in den Daten (Etage 24 ist das unveränderte Modul 05, `verzerrt.txt` leer) noch in Bl4ck969s Spieltest am 29.10. (zwei Bilder der Bodenhalle, „sehe den Fehler unten auch nicht“). Im selben Test: Lauf bis 4.999.999 Münzen, die letzte liegt in der Kiste – der 5-Mio.-Ablauf trägt bis zum Ende.

**Erledigt 29.10. spätabends (Bl4ck969s Spieltest, drei Bilder):**
- **Wandlampe auf Etage 22 zerrissen:** Gehäuse (mehrere Inseln, teils „Decke“, teils Treppenteil über einem Podest), Licht („anteilig“) und Halter (über den Partner) gingen nach drei Regeln mit – bis 0,85 m auseinander. Jetzt ist eine Lampe (Gruppe mit Licht und Gehäuse, höchstens 2 m) **ein Funktionsobjekt** (`Inventory.LampOf`), der unsichtbare Halter folgt ihr (`StaticCollider.Holder`). Lampen an Treppen/Podesten dürften der Treppe folgen; in Baustein 4 greift das bei keiner (alle hängen höher als 2,5 m über dem Podest).
- **Stufe im Boden und Blick durch den Boden an der Treppenankunft:** Treppenteile auf und unter dem oberen Deck gingen mit der Höhe der obersten Stufe (0,2 m unter dem Deck) – bei Faktor 1,34 knapp 7 cm zu tief. Die Bodenplatten sind große Dreiecke; eins ging mit der Treppe, das Nachbardreieck blieb am Deck → schräge Fuge, von unten ein heller Keil. Jetzt geht ein Treppenteil, dessen **Mitte** über den Platten des oberen Decks liegt (nicht über einer Stufe, höchstens 0,8 m darunter), mit dem Deck; Spiegelbild am Treppenanfang mit dem eigenen Deck (`OnUpperDeck`/`OnLowerDeck`). Je Teil statt je Eckpunkt, sonst würde an der Kante zwischen oberster Stufe und Deck eins von beiden schief.

**Erledigt 29.10. abends (zweite Runde):** Aufzugsmenü nennt den Sperrgrund („Landeplatz freischaufeln“ bzw. „Schacht bei NN freischaufeln“, Deutsch/Englisch nach Spielsprache; der Mod zeichnet das Menü jetzt immer). Das „Schild an der Aufzugstür mit 6 Etagen“ war die Kabinentafel (gleiche Nummern 00 01 04 08 13 14, durch die Tür sichtbar) – erledigt mit den Kabinentasten; die Etagennummer am Sockel neben dem Tor stimmt. Offene Fehler 1–3 (Abschnitt 4) im Bild nicht mehr zu finden (Etage 02 Nord, 03 West, 23 West). Streckung ×1,41 angesehen: steilere Treppe, größte Stufe 0,30 m (Steighöhe 0,42 m) – in Ordnung.

**Achtung Testläufe:** `werkzeuge/pruefung.ps1 -Modus original|mod|inventar|leistung|leistung-original|spielstand` startet das Spiel sichtbar, steuert dabei die Spielfigur (Leistungstest: setzt sie alle 2 s auf die höchste Münzoberfläche, gräbt einen Schacht unter den Füßen) und beendet es selbst. Am 26.10. hat Bl4ck969 in einem Testlauf gespielt; der Leistungstest setzte den Spieler bei leerem Haufen auf „minus unendlich“ → PhysX-Absturz (behoben). **Vor Testläufen Bl4ck969 Bescheid geben.**

---

## 2. Ziel und Entscheidungen von Bl4ck969

| Wann | Entscheidung |
|---|---|
| 25.10. | Offline-Modus als **eigener Eintrag im Hauptmenü**, eigener Spielstand, **nichts an Bestenlisten/Steam** (Sorge vor Bann). Echter Spielstand wird nie angefasst. |
| 25.10. | **Start bei null.** Ablauf wie im Original: unten erscheint die Kiste mit der letzten Münze → Zeit → „Hauptmenü“ oder „Weiterspielen“ (Tresor füllt sich, gelernter Baum bleibt). |
| 25.10. | **Fertigkeitsbaum sinnvoll erweitern**, Punkte nach **demselben Muster** wie im Original. **Abgabestation auf jeder Etage**, freigeschaltet über die erweiterte Spur „Hopper network“. |
| 25.10. | **Jede Etage etwas höher als die darüber** („logarithmisch“), weil man unten mehr Fertigkeiten hat. Etagen gleicher Höhe in Gruppen lehnt er ab. |
| 25.10. | Voll ausgebaut **unter 5 Minuten**. Ein 2,38-Mio.-Lauf mit dem heutigen vollen Baum dauerte 4:08. |
| 25.10. | **Testschalter**: alle Fertigkeiten lernen. Am 29.10. auf Bl4ck969s Wunsch wieder entfernt (nur noch in Prüfläufen). |
| 29.10. | Goldfluss/Filialleiter +300 %, Bankradius 15 m, [4] holt die Taschenbank zurück, Ast Bewegung mit Jetpack, Punkte bis zum vollen Baum (434). |
| 25.10., ~22 Uhr | **Keine 100 Mio. mehr. Ziel: ~5 Mio. echte Münzen, jede zählt 1, kein Münzwert.** Dafür mehr Etagen. Begründung: „so schnell geleert“. |
| 25.10., ~22 Uhr | Name: **„5 Mio. Münzen“** |
| 25.10., ~22 Uhr | „5 Mio. und fertig“, aber: **„Wenn wir tricksen können, gerne.“** → nach dem fehlerfreien 5-Mio.-Gebäude Streaming ernsthaft prüfen und Aufwand vorlegen. |

**Rechengrößen:**
- Ein Meter Haufenhöhe fasst ≈ 24.500 Münzen (56 × 56 Säulen, 0,128 m je Münze).
- Mögliche Varianten:

| Etagen | Höhen (oben → unten) | Gebäude | Münzen |
|---|---|---|---|
| 15 (heute) | 4,2 → 12 m | 114 m | 2,75 Mio. |
| 25 | 4,2 → 14 m | ≈ 205 m | ≈ 5,0 Mio. |
| 25 | 4,2 → 16 m | ≈ 220 m | ≈ 5,4 Mio. |
| 40 | 4,2 → 20 m | ≈ 390 m | ≈ 9,5 Mio. |

- Einstellbar über `[Etagen]` in der Konfiguration.
- Bei 2,75 Mio. und vollem Haufen: 28–37 FPS, die Haufen-Simulation kostet 18–24 ms je Bild. Ein Vergleichswert vom Original fehlt noch.

**Wege, mehr Münzen „zu tricksen“** (Antwort an Bl4ck969):
1. **Streaming:** Nur Etagen nahe der Oberfläche sind echte Münzen, tiefere nur Zählwerte je Säule. Das geht, weil der Haufen von oben nach unten abgetragen wird. Dafür muss aber der Haufen-Kern umgebaut werden (`GranularPileModel`, Burst-Jobs mit einer Münz-Id je Säule). Groß und anfällig für Spiel-Updates.
2. **Münzbündel:** Jede sichtbare Münze zählt N, die Werkzeuge holen N-mal weniger. Kostet keine Leistung. Höchstens als Einstellung mit Standardwert 1.
3. **Echte Münzen über mehr Etagen:** praktische Grenze etwa 10 Mio. (Speicher, Bildrate). Das ist die Basis.

**Folgen von 5 Mio.:**
- Das Punkte-Muster des Baums wird ×5 gestreckt statt ×100.
- Das Zählwerk an der Station reicht mit 7 Stellen.
- Der Abschlusstext „1.000.000 Münzen gesammelt“ muss angepasst werden.

---

## 3. Was funktioniert

- **Menü-Eintrag** „5 Mio. Münzen“ rechts neben „Neues Spiel“, im Stil des Spiels. F10 im Hauptmenü ist der Rückfall.
- **Offline-Schutz:**
  - Doppelt gesperrt: in der Spiel-Logik und an Steamworks.NET.
  - Kein Spielstand wird gelesen oder geschrieben, Verlassen ohne Speicherversuch.
  - Fehlt eine Pflicht-Sperre, erscheint der Menü-Eintrag nicht.
  - Das Spiel selbst meldet den Lauf zusätzlich „nicht für Bestenlisten eingereicht (Testausrüstung)“.
- **Münzziel dynamisch:** 32 Stellen der 1-Mio.-Konstanten in `GameProgressManager` und die Kappung des Zählwerks laufen per Transpiler auf die Haufengröße. Ein ganzer Lauf inkl. letzter Münze und Abschlussbildschirm lief durch.
- **Haufen größer:** Die Grenze im Konstruktor von `GranularPileModel` ist angehoben. Die Burst-Jobs kommen mit mehr Schichten zurecht.
- **Gebäude** (neu gebaut, siehe Abschnitt 6):
  - 15 Etagen mit allen Treppen, Wangen, Podesten, Trägern, Pooltreppen, Stützen, Pfeilern, Schacht, Wänden, Dach, Lampen, Stationen und Aufzugshalten.
  - Kabine startet oben, Taster, Tore und Stationen sind richtig nummeriert.
  - 14 Wandrutschen auf den richtigen Höhen.
- **Testmodus:** nur in Prüfläufen; alle Fertigkeiten werden gelernt (Testausrüstung des Spiels), im Mod-Modus 251 mit Ast Bewegung.

---

## 4. Offene Fehler

Grundlage: Prüfer-Abnahme vom 25.10., 21:50, und Rückmeldungen von Bl4ck969. Belege liegen in `pruefung\mod\` gegen `pruefung\original\`.

> 1–3: ✅ Am 29.10. im Bild nicht mehr zu finden (`mod\etage_02_nord.png`, `etage_03_west.png`, `etage_23_west.png`), nach den Regeln für Stufenecken, Treppen-Kollider und Podeste.

1. **Grauer, ~0,35 m dicker Stab parallel zum Treppenhandlauf**
   - Wo: Etagen 02 und 04 (Vorlage 02, Faktor 0,92 bzw. 1,09).
   - Beleg: `schnitt_sued_vorn.png` bei ≈ 97–108 m, `etage_02_nord.png`.
   - Vermutung: Eine Insel, deren Eckpunkte teils der Stufenlinie folgen und teils nicht, reißt auf. Der Grund ist die harte Grenze von 1,5 m horizontal / 2,5 m vertikal in `Inventory.NearestStairHeight`.
2. **Podestgeländer schief oder geknickt**
   - Wo: alle Etagen mit Faktor ≠ 1, oberes Podest Westseite und unteres Ostseite.
   - Ursache: Podestpunkte übernehmen die Höhe verschiedener Stufen.
   - Lösung: Punkte über der Grundfläche eines `Rest landing` (+0,3 m) bekommen dessen Höhe.
3. **Handlauf zerfällt in Messing-Splitter**
   - Wo: Etagen 02–11, auch 03 mit Faktor 1,00. Bl4ck969 hat es bestätigt.
   - Ursache: Die verschweißten Messing-Netze `Finish_0N_AgedBrass` werden nach dem Schwerpunkt der Dreiecke getrennt. Das zerschneidet Treppenhandläufe, die über das nächste Deck ragen.
   - Lösung: Handlauf-Dreiecke dem Baustein der nächsten Treppe zuordnen, nicht dem Höhenband.
   - Vergleich: `mod\etage_03_west.png` gegen `original\etage_02_west.png`.
4. ✅ **Erledigt 26.10. – kein Mod-Fehler.** Abgrab-Simulation im Prüfmodus: Original und Mod gleich, alle Etagen erreichbar, sobald Schacht und Landeplatz bis 0,3 m unter den Boden freigesaugt sind. Die letzte Münze bleibt im Haufen, bis alles abgegeben ist (`FinalCoinReady`), dann legt das Spiel sie in die Kiste. Ursprüngliche Meldung: **Aufzug fährt nicht.** Meldung „Schacht und Landeplatz erst freiräumen“, obwohl 2.745.461 von 2.745.462 Münzen gesammelt waren.
   - Ablauf im Spiel: `CargoElevator.IsLayerAccessible` → `GranularCoinPile.IsVolumeClear` blockiert, sobald eine Haufenschicht mit Münzen im Volumen zwischen Kabine und Ziel oder am Landeplatz liegt.
   - Verdacht: Die letzte Münze liegt im Schacht, oder die neuen Kollider/Stützflächen bilden dort eine Schicht.
   - Test: Im Prüfmodus den Haufen leeren, `CallTo` für jede Etage aufrufen, Ergebnis und Grund protokollieren.
5. ✅ **Erledigt 26.10. (Loch-Test 0 Pixel).** **Loch in der Decke:** Beim Blick vom Boden nach oben ist blauer Himmel zu sehen. Prüfen: Kassettendecke und Dachträger (Baustein 00) und Oberkante der gestreckten Außenwände.
6. ✅ **Erledigt 26.10. (x 9,8).** **Wandrutschen springen:** oben bei x ≈ 9,8, weiter unten mittig. Das ist die Regel des Spiels selbst (im Original 05/04 mittig, 03–01 rechts), wirkt bei vielen Etagen aber unruhig. Plan: alle in **eine** senkrechte Reihe, bei Hindernis nur in der Höhe ausweichen. Seite mit Bl4ck969 klären.
7. ✅ **Erledigt 26.10.** **Graue Stahlprofile an der Südwand** enden 0,91 m statt 0,34 m unter Deck 00. `MapContinuous` muss den oberen Rand mit Deck 00 mitschieben.
8. ✅ **Erledigt 29.10.** (Kabinentasten relativ zur Etage, Menütaste; das „Schild“ war die Kabinentafel). HUD-Menü zeigt seit 26.10. alle Etagen (mehrspaltig). Damals offen: Das Schild an der Aufzugstür nennt weiter 6 Etagen (00 01 04 08 13 14). **Kabine** hat nur 6 Zieltasten (00, 01, 04, 08, 13, 14). Das HUD-Aufzugsmenü baut sich aus der Zahl der Etagen auf und sollte alle zeigen; im Spieltest bestätigen.

Erledigt am 25.10.: Menü-Eintrag lag über „Mitwirkende“ (jetzt rechts neben „Neues Spiel“); „Speichern fehlgeschlagen“ beim Beenden (Verlassen ohne Speicherversuch); Kabine stand auf alter Höhe; Rutschen bekamen die neuen Etagenhöhen nicht.

---

## 5. Erkenntnisse über das Spiel (Code)

- **Technik:** Unity **6000.6.0f1**, **Mono**, Namespace `GoldDigger`, Teile mit Unity Entities/Burst. Kein Anti-Cheat, Steam ohne VAC (Store geprüft).
- **Fertigkeitsbaum:** Ruleset `vault-1.2`, Baum in `SkillCatalog.Build()` im Code: 238 Skills, 7 Äste, 12 Ränge je Spur, Gesamtkosten 409.
  - Punkte: feste Kurve `PointCoinThresholds` (125 Punkte bis 900.000), danach 25.704 Münzen je Punkt.
  - Gezählt wird über Neustarts hinweg (`CompletedVaults·1.000.000 + Collected`).
- **Stationen:** `IsDepositStationUnlocked(rank)`, Rang 1–5 entspricht den Skills 1211/1213/1214/1215/1216 (Ast Depots, Spur „Hopper network“). Rang = Etage von oben. Im Prototyp sind alle offen.
- **Haufen:** `GranularPileModel`, 56 × 56 Säulen, **jede Münze einzeln (int-Id)**, 0,128 m je Münze.
  - Stützflächen werden beim Start per Raycast gegen statische Kollider gesucht (`SampleStaticSupports`).
  - Kollider unter `ElevatorLandingGate` oder `CargoElevator` werden dabei ignoriert.
- **Eingebettete Methode:** `VaultLayerLayout.CreateHeights()` wird vom JIT eingebettet, ein Harmony-Patch darauf wirkt nicht. Die Höhen gehen deshalb direkt an `PassiveWallChutes.Initialize`.
- **Wandrutschen:** `WallChutePlacement.TryPlace` setzt sie 1,15 m unter das nächste Deck, an die Nordwand, in die Mitte, sonst in 1,4-m-Schritten seitlich.
- **Online-Pfade:** vollständig in `src/OfflinePatches.cs`. Der echte Spielstand versiegelt sich selbst (Receipt ohne Schlüssel), deshalb darf der Mod nie auf dem echten Pfad laufen.
- **Abschluss:** `FinalCoinSequence` baut die Kiste per Code, wenn `Remaining == 1`. `SaveManager.ContinueCompletedRun` füllt neu auf, die Skills bleiben.
- **Testmodus des Spiels:** `GameProgressManager.SetTestingLoadout(true)` gibt 1.000 Punkte und setzt „nicht wertbar“ und „keine Achievements“.

---

## 6. Erkenntnisse über das Level und wie der Mod es umbaut

**Original:** Szene `GoldBunker` = `level1`. Gebäude x/z ±14,5 m, 6 Etagen (00 oben … 05 Boden) bei 0,4 / 16,5 / 26,5 / 33,4 / 38,4 / 42,6 m.

- **Zwei Zählweisen in den Namen:**
  - Schild von oben: `Joined deck 01`, `Layer Equipment 01`, `Finish_01_*`, `Deposit Relay 1`, `Coin pool entry stair 01`.
  - Index von unten: `Atlas station 4`, `Stair connection 5 to 4`, Lampen `Lift 4`, `Landing 5 to 4`.
  - Etage 00 = Index 5.
- **Das Sichtbare steckt in Sammel-Meshes `Finish_NN_<Material>`** (alle lesbar):
  - AntiSlipSteel = Trittflächen
  - ConcreteSlabs04 = Zwischenpodeste
  - SatinSteel/AgedBrass = Geländer und Handläufe (AgedBrass je Etage als ein verschweißtes Netz)
  - CastConcrete = Betonband
  - GraphiteSteel = Wangen, Stufenkörper, Träger unter dem nächsthöheren Deck, dessen Pooltreppe, Schachtabschnitt
  - `Finish_04_CastConcrete`/`GraphiteSteel` enthalten zusätzlich die durchgehenden Pfeiler, Stützen und den Schacht.
- **Benannte Gruppen** (`Stair connection…`, Pooltreppen, Pfeiler) sind fast nur Kollider.
- **Baustein:** Etage N reicht vom Deck N bis einschließlich Unterseite und Pooltreppe des Decks darüber.
- **Treppenlauf:** Süd-Deck Ostseite → Ostwand hinauf → Podest Nordost → Nordwand → Podest Nordwest → Westwand → Südwest-Deck der nächsten Etage.
  - Unterste Stufe 0,24 m unter dem Deck, oberste 0,19–0,21 m unter dem nächsten.
  - Pooltreppe: 10 Stufen bis 2,26 m unter das Deck bei x 3,8…6,6 / z −8…−4,8.

**Mod-Umbau** (`src/Inventory.cs`, `src/FloorBuilder.cs`):
- **Inventar:**
  - 422 Meshes werden in 8.606 Inseln zerlegt (zusammenhängende Dreiecke; gleiche Position auf 0,5 mm gilt als verbunden).
  - Dazu 864 statische Kollider und 106 Funktionsobjekte. Alles außerhalb von „Excavation Bunker“ bleibt ganz. Drinnen sind Spiel-Logik, Lichter und Landetor-Schranken Funktionsobjekte; Aufzugshalte gelten als „Hüllen“.
  - Jedes Teil bekommt einen Baustein (nach Unterkante) und eine Rolle:
    - Floor (am Boden)
    - Ceiling (in den oberen 2,6 m oder am nächsten Deck)
    - Proportional (dazwischen); ganz in den unteren 1,5 m → Floor
    - Stretch (vom Boden bis unter die Decke)
    - Stair (0,8 m unter bis 2,5 m über einer Stufe und ≤ 1,5 m von ihrer Mitte oder ≤ 0,25 m neben ihrer Trittfläche)
    - Aufzugshalt (`Atlas station N`): Baustein des Halts, Floor; nur was bis an die Decke reicht, Stretch
  - Über Etagengrenzen verschweißte Inseln werden nach Dreiecks-Schwerpunkt getrennt (`ModuleFixed`). Das ist Fehlerquelle 3.
  - Jeder Kollider hat einen „Partner“ (nächste sichtbare Insel) und macht genau dessen Verschiebung mit. Das verhindert unsichtbare Hindernisse. Trägt er ein Treppenteil, ist dieses sein Partner.
  - Feste Regel statt Partner (`StaticCollider.Fixed`): Treppenteile (`Stair connection i to j` → Baustein 5−j, Stufenlinie), Deckplatten (`Joined deck NN` → Boden von Baustein NN), Teile eines Aufzugshalts.
- **Bau:**
  - Zielhöhen wachsen geometrisch. Je Etage wird der Original-Baustein mit der nächsten Höhe gewählt; der Faktor liegt bei 0,85–1,20.
  - Geometrie wird neu zusammengesetzt, je Etage und Material ein Mesh.
  - Eckpunkte in Treppennähe folgen der Stufenlinie, die übrigen ihrer Rolle.
  - Durchgehende Teile werden zwischen Boden und alter Etage 00 gestreckt, darüber verschoben.
  - Kollider werden neu angelegt; die in Aufzugshalten werden in deren Hülle eingehängt.
  - Funktionsobjekte und Hüllen werden kopiert bzw. verschoben, die Originale abgeschaltet.
  - Danach: Spieler, Kabine und Aufzugshöhen nach oben, Nummern neu.
  - Bauzeit ≈ 2,7 s bei 25 Etagen (gemessen 29.10.2026 im Modus „spielstand“; im Prüfmodus ≈ 5 s wegen der Prüfpunkte).

---

## 7. Prüfverfahren (gilt für jede Fassung, bevor Bl4ck969 sie bekommt)

- **Prüfmodus:** `werkzeuge\pruefung.ps1 -Modus original|mod|inventar|leistung|leistung-original|spielstand`.
  - Das Skript setzt den Modus in der Konfiguration, startet das Spiel über Steam und wartet, bis es sich selbst beendet. Danach setzt es `Modus = aus` zurück.
  - **Nicht laufen lassen, während Bl4ck969 spielt.**
  - Ergebnisse landen in `pruefung\<modus>\`.
- **Ausgaben je Lauf:**
  - Schnitte `schnitt_<richtung>_<vorn|mitte|hinten>.png`: orthografisch, nahe Wand weggeschnitten.
  - Etagenansichten `etage_NN_<richtung>.png`: 1,7 m über jedem Boden aus der Mitte.
  - `blick_nach_oben/unten.png`
  - `bauteile.csv`: alle Renderer und Kollider mit Welt-Grenzen.
  - `pruefbericht.txt`
  - `unsichtbare_kollider.csv`
  - `verzerrt.txt`: Teile, deren Höhe sich beim Umbau stark ändert, plus alle durchgehenden Teile.
  - Im Modus „inventar“ zusätzlich `inventar.csv` und Schnitte, eingefärbt nach Baustein und Rolle.
- **Automatische Prüfungen in `pruefbericht.txt`:**
  - Taster, Landetore und Stationen auf der richtigen Etage
  - unsichtbare Kollider (Soll: nur Lampenhalter, wie im Original 13)
  - Treppen: Anschluss an die Decks, größter Schritt ≤ Steighöhe der Spielfigur 0,42 m
  - Pooltreppen: oberste Stufe 0,21 m unter dem Deck
  - Zuordnungstabelle „Etage NN ← Original LL, Faktor“
  - **feste Punkte** (seit 26.10.): Punkte, die im Original in einem Kollider liegen, müssen es im Mod auch (Soll 0; Stand 29.10.: 249 von 723.091)
  - **Löcher nach draußen** (seit 26.10.): Hintergrund magenta statt Himmel, 5 Standpunkte × 6 Richtungen je Etage, mit der echten Sichtweite der Spielkamera; Gegenprobe über dem Dach muss 100 % magenta sein
  - **Aufzug beim Abgraben:** je Etage Haufen abbauen und nachfließen lassen, dann `IsLayerAccessible`; dazu die Münzschichten in Schacht und Landeplatz
  - **Aufzugsfahrten** (seit 29.10.): kurze, mittlere und längste Fahrt mit `Tick` in 20-ms-Schritten – Strecke, Fahrzeit, Spitze, Zeit bis 90 %
  - **Kabinentasten** (seit 29.10.): Beschriftung und Hinweis je Platz auf Etage 12, Bild `kabine.png`, Menütaste öffnet das Aufzugsmenü
  - Aufzugsmenü als Bildschirmfoto
- **Modus „leistung“:** 60 s Abgraben wie mit vollem Baum, Bildrate und Zeit je Haufen-Teil (Fließen, Lagen, Seiten, Darstellung, Kollision), **Bildvergleich** mit und ohne Sichtbarkeits-Band (6 Richtungen, abweichende Pixel; Rest nur Magnet in der Hand und HUD), Bergtest (Graben unter den Füßen). „leistung-original“ misst dasselbe im unveränderten Gebäude.
- **Modus „spielstand“:** speichern (Zeit, Größe) → Pause-Weg ins Hauptmenü → Menü mit beiden Tresor-Einstellungen fotografieren → Weiter → geladen und speicherbar? → Bestenliste eintragen (Prüfdatei) und fotografieren. Speichert in `spielstand.test.json` (seit 29.10. nur noch Prüfläufe; Bl4ck969 spielt mit `spielstand.json`).
- **Modus „fertigkeiten“** (seit 29.10.2026): Goldfluss, Filialleiter, Bankradius, Sammel- und Rutschenrate, deutsche Texte; Taschenbank 8 m entfernt abstellen und mit [4] zurückholen (Bilder `bank_abgestellt.png`, `bank_zurueck.png`); Ast Bewegung (Katalog, Spalten, Boni, Bild `baum.png`), Jetpack 0,6 s mit simulierter Sprungtaste (`MovementBranch.HoldJumpForTest`, Bild `jetpack_feld.png`); danach ohne Speichern ins Hauptmenü und Originalwerte prüfen (238 Skills, 7 Spalten).
- **Modus „bilder“** (seit 30.10.2026): Aufnahmen fürs README, siehe Abschnitt 1; schreibt nach `pruefung\bilder\`, speichert nichts, trägt nichts in die Bestenliste ein (liest die echte). Neue README-Bilder: Lauf fahren, Auswahl mit 1600×900 nach `bilder\` (JPG).
- **Modus „versionsschutz“** (seit 30.10.2026, seit 03.11.2026 in drei Läufen): spielt einen nicht geprüften Stand mit der erfundenen Kennung `pruefung-versionsschutz` vor (trifft nie einen echten). Nacheinander fahren: `versionsschutz` (ohne Freigabe: 0 Patches, Bestätigungsfeld, einschalten, Wert in der Datei), `versionsschutz` (mit Freigabe: Mod lädt, 102 Patches), `versionsschutz-fehler` (vorgetäuschter Fehler nach allen Modulen: 0 Patches der Mod, 102 zurückgenommen, „Nicht mehr laden“, Datei wieder leer). Jeder Lauf erkennt am Zustand, welcher er ist; Ergebnis in `<schritt>_ergebnis.txt` und Bilder `1-aus_*.png` usw. Gehört nach jeder Änderung an `Plugin.Awake`, `ReleaseInfo` oder `VersionNotice` dazu.
- **Modus „spielstand-laden“** (seit 03.11.2026): lädt `spielstand.ladetest.json` über „Weiter“ (**ohne** Testmodus, sonst lehnt das Spiel einen echten Spielstand ab), protokolliert Durchgänge, Fertigkeiten, Haufen, Spielzeit, speicherbar, geht ohne Speichern zurück und öffnet die echte Bestenliste (nur lesen). Vorher eine Kopie des zu prüfenden Spielstands als `spielstand.ladetest.json` (+ `.bak`) ablegen, danach löschen. Für jede Fassung, die das Speicherformat oder die Ladegrenzen berühren könnte, und nach Spiel-Updates.
- **Stand 25.10.:**
  - unsichtbare Kollider: nur Lampenhalter, 2 je Etage
  - größter Treppenschritt 0,30 m
  - Pooltreppen exakt wie im Original
- **Abnahme:** Ein unabhängiger Prüfer (Unteragent) vergleicht jede Mod-Etage mit ihrer Original-Vorlage (Bilder und CSV). Erst bei „bereit für Spielertest“ geht die Fassung an Bl4ck969.

---

## 8. Projektaufbau, Konfiguration, Werkzeuge

**Quellcode** (`src/`):

| Datei | Inhalt |
|---|---|
| `Plugin.cs` | Einstieg, Konfiguration, Logweg (kein Player.log) |
| `ModState.cs` | Modus aktiv ja/nein |
| `MenuPatches.cs` | Tresor-Auswahl im Hauptmenü, „Weiter“/„Neues Spiel“ für den Mod |
| `OfflinePatches.cs` | Offline-Sperren, Spielstand-Umleitung mit Pfadwächter |
| `Leaderboard.cs` | Offline-Bestenliste (Eintragen und Anzeige) |
| `WorldPatches.cs` | Haufen-Grenze und -Größe (fest 5 Mio.), Sichtweite, Testmodus, Stationen offen, Rutschen |
| `GoalPatches.cs` | Münzziel dynamisch, auch beim Laden |
| `HudPatches.cs` | Aufzugsmenü mehrspaltig |
| `ElevatorPatches.cs` | Aufzug: Fahrprofil (kurz anfahren/bremsen), Kabinentasten relativ zur Etage |
| `PilePatches.cs` | Kollisionsnetz des Haufens nur im Band um den Spieler |
| `PoseBand.cs` | Münzen nur anordnen/zeichnen, wo sichtbar (Band + oberste Schicht je Säule) |
| `Inventory.cs` | Teile-Inventar |
| `FloorBuilder.cs` | Gebäude-Neubau |
| `Diagnostics.cs` | Prüfmodus |
| `PerformanceLog.cs` | Leistungsprotokoll und Leistungstest |
| `SaveTest.cs` | Spielstand-Rundlauf; Prüfmodus „spielstand-laden“ (vorhandenen Spielstand laden) |
| `SkillPatches.cs` | Fertigkeiten im Mod-Modus: Goldfluss/Filialleiter +300 %, Bankradius 15 m, [4] holt die Taschenbank zurück |
| `MovementBranch.cs` | Ast „Bewegung“ (Lauftempo, Sprunghöhe, Jetpack) samt HUD-Anpassungen für den 8. Ast |
| `SkillTest.cs` | Prüfmodus „fertigkeiten“ |
| `CompletionPatches.cs` | Abschlussdialog im Mod-Modus: Münzzahl, kein roter Kasten, Platz in der Offline-Bestenliste |
| `PictureRun.cs` | Prüfmodus „bilder“ (Aufnahmen fürs README) |
| `ReleaseInfo.cs` | Mod-Version, GUID, Spielstand-Ordner und **geprüfte Spielstände** – auch vom Installer mitkompiliert |
| `VersionNotice.cs` | Hinweis im Hauptmenü bei nicht geprüfter Spielversion mit Ein-/Ausschalten; Prüfmodi „versionsschutz“, „versionsschutz-fehler“ |
| `ModText.cs` | Texte der Mod nach Spielsprache (Deutsch, sonst Englisch); `ForceEnglish` nur für Prüfläufe |

**Bauen:** `dotnet build OneMillionCoins5Mio.csproj -c Release` im Projektordner. Das kopiert nach `<Spiel>\BepInEx\plugins\OneMillionCoins5Mio\`; mit `-p:Deploy=false` nur nach `bin\Release\` (z. B. während Bl4ck969 spielt oder für den Installer). Die Projektdatei explizit angeben, weil unter `werkzeuge\metadump` und `installer\` weitere liegen.

**Installer bauen:** erst das Plugin (`-p:Deploy=false`), dann `dotnet build installer\Installer.csproj -c Release` → `installer\bin\Release\OMC-5Mio-Installer.exe`. Danach `python -I werkzeuge\installer_test.py installer\bin\Release\OMC-5Mio-Installer.exe bin\Release\OneMillionCoins5Mio.dll <Temp-Ordner>`.

**Release bauen:** `python -I werkzeuge\release.py` – baut Plugin (`-p:Deploy=false`) und Installer, fährt `installer_test.py` (bricht ab, wenn nicht alles grün ist) und legt `release\OMC-5Mio-<Version>\` samt ZIP an: Installer, `LIESMICH.txt` (de/en, aus `installer\pakete\LIESMICH.txt` mit eingesetzter Mod- und Spielversion), `LICENSE.txt`, `Drittanbieter.txt`, `SHA256SUMS.txt`. Seit 1.1.0 zusätzlich `release\OMC-5Mio-<Version>-manuell.zip` nur mit der DLL (für Nexus ohne Quarantäne). `release\` ist nicht im Git. Vorher die Prüfläufe im Spiel (Abschnitt 7) – das Skript startet das Spiel nicht.

**Konfiguration:** `<Spiel>\BepInEx\config\de.bl4ck969.onemillioncoins.5mio.cfg`. Der Dateiname kommt von der GUID; ändert sie sich, entsteht eine neue Datei mit Standardwerten (deshalb beim Umbenennen am 30.10.2026 die alte Datei umbenannt).

| Abschnitt | Schlüssel | Bedeutung |
|---|---|---|
| `[Etagen]` | `Anzahl` | Zahl der Etagen (25) |
| | `HoeheOben` | Höhe der obersten Etage (4,2 m) |
| | `HoeheUnten` | Höhe der untersten Etage über der Bodenhalle (14,12 m, ergibt die Füllhöhe für 5 Mio.) |
| | `Muenzen` | Münzen im Haufen (5.000.000; die letzte kommt in die Kiste) |
| | `Automatengruppen` | letzte Etage je Skill „Unterer Trichter“ 1–5 (`2,5,9,15,24`; der 5. reicht immer bis unten) |
| `[Menue]` | `Tresor` | Original / 5Mio (wird im Menü umgeschaltet) |
| `[Kompatibilitaet]` | `UngepruefteVersion` | Mod auf jedem nicht geprüften Stand laden, ohne zu fragen (Standard `false`; für Prüfläufe nach einem Update) |
| | `FreigegebeneVersion` | Kennung eines nicht geprüften Stands, auf dem die Mod trotzdem läuft (Knopf im Hauptmenü oder Installer; leer = keiner) |
| `[Aufzug]` | `Geschwindigkeit` | Höchstgeschwindigkeit in m/s (7, wie die Spitze im Original) |
| | `Anfahrzeit` | Sekunden bis zur Höchstgeschwindigkeit, ebenso zum Bremsen (1,5) |
| | `LeistungAuchImNormalspiel` | Leistungsprotokoll im Normalspiel |
| `[Pruefung]` | `Modus` | aus / original / mod / inventar / leistung / leistung-original / spielstand / spielstand-laden / fertigkeiten / versionsschutz / versionsschutz-fehler / bilder |
| | `Ordner` | Zielordner der Prüfaufnahmen |

Veraltete Schlüssel ohne Wirkung: `EtagenBausteine`, `ZusaetzlicheEtagen`. `AlleFertigkeiten`/`TestPunkte` am 29.10. aus Code und Konfiguration entfernt.

**Mod-Dateien des Spielers:** `%LOCALAPPDATA%\OneMillionCoins-5Mio\` – `spielstand.json` / `spielstand.test.json` (je mit `.bak`), `bestenliste.tsv`, im Prüfmodus `bestenliste.pruefung.tsv` (die zeigen die Prüfläufe auch sichtbar an), nur im Ladetest `spielstand.ladetest.json`. Der alte Ordner `OneMillionCoins-100M` ist unbenutzt.

**Umbenannt am 30.10.2026:** `OneMillionCoins100M` → `OneMillionCoins5Mio` (Projektdatei, DLL, Plugin-Ordner, Namensraum), GUID `…100m` → `…5mio`. Auf Bl4ck969s Rechner: Konfiguration umbenannt (alle 16 Werte gleich), alter Plugin-Ordner gelöscht – liegen beide Ordner da, laden zwei Fassungen. Der Installer verweigert deshalb, solange `BepInEx\plugins\OneMillionCoins100M` existiert. Der Spielstand-Ordner `OneMillionCoins-5Mio` bleibt.

**Versionsverwaltung:** Git, Autor `Bl4ck969@users.noreply.github.com`; `pruefung/`, `bin/`, `obj/`, `release/` sind ausgenommen. Seit 30.10.2026 **öffentlich** auf GitHub: `origin` = `Bl4ck969/OneMillionCoins-5Mio`, Zweig `master`, Historie beginnt dort mit dem Release 1.0.0. Die Historie davor (26.–30.10.) liegt **nur lokal** im Zweig `archiv-lokal` – **nie pushen**, sie enthält den Vornamen des Autors. Im öffentlichen Repo steht für den Spieler/Autor „Bl4ck969“; neue Notizen und Kommentare ebenso schreiben. Release: `release.py`, dann `gh release create v<Version> release\OMC-5Mio-<Version>.zip` (erst als Entwurf).

**Analyse-Skripte** in `werkzeuge\` (UnityPy, nur lesend):
- `scene_bounds.py`: Welt-Grenzen aller Meshes, Modus `stretch|keep|grep`
- `scene_tree.py`: Hierarchie
- `scene_probe2.py`, `agg.py`, `readable.py`
- Versionsprüfungen `vcheck.py`, `pycheck.py`, `depcheck.py`, `nugetcheck.py`
- `metadump`: Typen und Konstanten aus DLLs

**Neu aufsetzen** (der Scratchpad-Ordner ist flüchtig):
- Dekompilieren: `dotnet tool install ilspycmd --version 11.1.0.9782 --tool-path <ordner>`, dann `ilspycmd -p -o <ziel> -r <Managed> <Managed>\Assembly-CSharp.dll`
- UnityPy: `python -m venv venv`, dann `venv\Scripts\python -m pip install UnityPy==1.25.4`; danach pip auf eine geprüfte Version heben.

**Geprüfte Versionen (25.10.2026)**, jeweils OSV 0 und nicht abgekündigt. Vor dem Weiterbauen erneut prüfen.

| Paket | Version |
|---|---|
| BepInEx | 5.4.23.6 (SHA-256 `6c57a714…0755`, LTS; erneut geprüft 30.10.2026: latest, OSV 0, keine GitHub-Advisories, Lizenz MIT) |
| ilspycmd | 11.1.0.9782 |
| UnityPy | 1.25.4 |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 |
| pip | 26.2.2 |

---

## 9. Installation, Spiel-Updates, Rückbau

**Entwicklungsrechner (von Hand):** Im Spielordner liegen `BepInEx\`, `winhttp.dll`, `doorstop_config.ini` und `.doorstop_version`. Diese vier löschen ergibt den Originalzustand (nachgemessen 30.10.2026: alle 229 Steam-Dateien unverändert). Ohne Tresor „5 Millionen“ verhält sich das Spiel normal.

**Was ein Steam-Update tut** (gemessen 30.10.2026 an den Manifesten vom 24.10. und 29.10.): Steam ersetzt nur Dateien aus seiner Dateiliste (229 Dateien, 642.507.419 Bytes). Keiner der Mod-Namen steht darin – BepInEx und Plugin überstehen Updates und auch „Dateien auf Fehler überprüfen“ (das prüft nur die 229). Das Update vom 29.10. änderte `Assembly-CSharp.dll`, `boot.config`, `globalgamemanagers`, `level1`, `resources.assets`, `sharedassets1.assets` (269 KB Download). Prüfwerkzeug: `werkzeuge\manifest_pruefen.py` bzw. `OMC-5Mio-Installer.exe --spieldateien`.
- Spielversion: `Application.version`, steht in `globalgamemanagers` (`1.2.2`), das Hauptmenü zeigt „Version 1.2.2“.
- Risiko nach einem Update ist nicht Datenverlust, sondern **ungeprüfter Code**: Die Patches greifen an Methoden und IL-Stellen, die sich ändern können; nur die Offline-Sperren fingen das bisher einzeln ab. Deshalb der Versionsschutz (Abschnitt 1).

**Nach einem Spiel-Update** (die Mod bleibt dann aus, bis der Spieler sie freigibt):
1. Neu dekompilieren und gegen den alten Stand diffen (Memory `omc-dekompilat`). Für jede geänderte Datei nachsehen, ob die Mod dort patcht **und ob der gepatchte Weg im Spiel noch läuft** (03.11.2026: `RefreshCollision` gab es noch, aufgerufen wurde im Spiel aber `RefreshCollisionInBackground`). Version-abhängige Wege an vorhandenen Methoden erkennen, nicht an der Versionsnummer – die zählt das Spiel nicht immer hoch.
2. Prüfsummen bilden (`sha256sum "<Spiel>\One Million Coins_Data\Managed\Assembly-CSharp.dll" "<Spiel>\One Million Coins_Data\globalgamemanagers"`), `buildid` aus `appmanifest_5258340.acf`, Version aus dem Hauptmenü → neuen Eintrag in `ReleaseInfo.Tested`. Alternativ während der Arbeit `[Kompatibilitaet] UngepruefteVersion = true`.
3. Vor den Prüfläufen die alten Ergebnisse sichern (jeder Lauf leert seinen Ordner) und Prüfsummen der Spielstände nehmen. Prüfläufe (Abschnitt 7) mit genau der DLL, die ausgeliefert wird; `mod`-Bericht gegen den alten diffen; `spielstand-laden` mit einer Kopie eines echten Spielstands der Vorversion.
4. Plugin und Installer über `release.py` bauen (prüft auch, dass das Plugin im Paket das geprüfte ist), Bl4ck969 spielt an, dann veröffentlichen.

**Installer** (`installer\`, seit 30.10.2026):
- Suche: Registry `HKCU\Software\Valve\Steam\SteamPath` → `steamapps\libraryfolders.vdf` → `appmanifest_5258340.acf` (`installdir`, `buildid`, `StateFlags`, Depot/Manifest); sonst Ordner von Hand wählen.
- Sperren: Spiel läuft (aus genau diesem Ordner), Steam-Update offen (`StateFlags` ≠ 4), fremder Mod-Lader (winhttp.dll oder BepInEx-Ordner ohne BepInEx 5). Ein Stand, der nicht in `ReleaseInfo.Tested` steht, sperrt seit 03.11.2026 nicht mehr: Rückfrage Ja (installieren + `FreigegebeneVersion` in die Mod-Einstellungen, Protokollzeile `freigabe`) / Nein (installieren, Mod aus) / Abbrechen.
- Installieren: BepInEx 5.4.23.6 (eingebettet, SHA-256 geprüft, ohne `changelog.txt`) nur, wenn keins da ist; dann das Plugin. Jede Änderung steht **vorher** in `<Spiel>\5Mio-Mod-Installation\installation.txt` (angelegte Ordner, neue Dateien mit Prüfsumme, überschriebene Dateien mit Sicherung unter `sicherung\<n>_<name>` – flach, weil der Originalpfad die 260-Zeichen-Grenze reißen kann). Danach Nachprüfung jeder Datei; bei Fehler Rücknahme.
- Deinstallieren: neue Dateien weg; BepInEx-Ordner ganz weg (samt Log, Cache, Einstellungen), wenn der Installer es gebracht hat und keine fremden Mods in `plugins`/`patchers` liegen – sonst bleibt BepInEx und nur Mod-Datei und Mod-Einstellungen gehen; Sicherungen zurück (Prüfsumme); leere angelegte Ordner weg; Frage nach `%LOCALAPPDATA%\OneMillionCoins-5Mio` (Standard „Nein“); Abgleich mit Steams Dateiliste.
- Ohne Adminrechte (`asInvoker`), damit `%LOCALAPPDATA%` das Profil des Spielers ist. Kommandozeile für Tests: `--pruefen`, `--spieldateien`, `--installieren --einverstanden [--ungeprueft]`, `--deinstallieren [--spielstaende-loeschen]`, `--spielordner`, `--spielstand-ordner` (Tests!), `--englisch`, `--bildschirmfoto <ordner>`.
- Lizenzen (`installer\pakete\Drittanbieter.txt`, im Installer unter „Lizenzen“): BepInEx, HarmonyX, MonoMod, Mono.Cecil MIT; UnityDoorstop 4.6.0 (`winhttp.dll`) LGPL-2.1 – unverändert weitergegeben, Quelle verlinkt. Das BepInEx-Archiv selbst enthält keine Lizenzdatei.

---

## 10. Lehren aus den Fehlversuchen

- **Nicht nach Bauteil-Namen sortieren.** Die Namen beschreiben Kollider, das Sichtbare steckt in Sammel-Meshes. Erst zerlegen, dann nach Geometrie zuordnen.
- **Sichtbares und Kollision nie getrennt verschieben.** Sonst entstehen unsichtbare Hindernisse und Geisterstufen; der Kollider folgt seinem Partner.
- **Nicht nach Screenshots flicken.** Erst messen (Prüfmodus, CSV, automatische Prüfungen), dann ändern.
- **Bl4ck969 nichts ungeprüft zum Testen geben.** Prüfer und automatische Prüfungen zuerst.
- **Kleine Methoden können vom JIT eingebettet sein.** Ein Harmony-Patch darauf wirkt dann nicht; am Aufrufer ansetzen.
- **Eine Zuordnungsregel darf eine andere nicht still überschreiben.** Die Partner-Regel („Kollider geht mit seinem nächsten sichtbaren Teil“) hat die Einordnung „durchgehend“ überschrieben – Mauern hatten danach nur noch auf einer Etage Kollision. Gefunden erst durch eine Prüfung, die am Ergebnis misst (feste Punkte), nicht am Code.
- **Ein Test muss mit den Einstellungen des Spiels messen.** Der Loch-Test lief mit 1000 m Sichtweite und fand 0 Löcher, während die Spielkamera nach 100 m den Himmel zeigt. Erst mit der echten Sichtweite fand er Bl4ck969s Loch.
- **Eine Simulation darf nicht mehr können als der Spieler.** Der Aufzugstest leert Schichten, die man vom Landeplatz aus nie erreicht – sein „erreichbar“ ist deshalb nur eine Obergrenze.
- **Leistung: erst messen, wo die Zeit hingeht.** Vermutet war das Fließen der Münzen (1 ms); tatsächlich war es das Kollisionsnetz (70 ms), dann Lagen/Seiten (16 ms). Jede Einsparung bekommt einen Gegenbeweis (Bildvergleich mit/ohne), sonst fehlen sichtbare Münzen – die erste Fassung ließ den Lichthof leer.
- **Testläufe steuern die Spielfigur.** Wer darin spielt, sieht Teleports und Gräben, die das Testskript macht, nicht der Mod.
- **Shell und Werkzeuge fressen Backslashes.** `\t` in einem Heredoc wurde zweimal zum echten Tabulator im C#-Quelltext. Änderungsskripte als Datei schreiben, danach `grep -c $'\t'` auf 0 prüfen.
- **`JsonUtility` serialisiert Klassen aus dem Mod-Assembly nicht** (Ergebnis `{}`); eigene Zeilenformate verwenden.
- **Nähe ist keine Zugehörigkeit.** Der Partner eines Kolliders war die zuletzt gefundene Insel darin – an Deck 00 ein Deckteil, an den anderen Decks ein Treppenteil. Daraus wurde ein Loch im Boden von Etage 01. Wo die Zugehörigkeit aus dem Aufbau feststeht (Treppengruppe, Deckplatte, Aufzugshalt), wird sie festgelegt, nicht aus der Nähe geraten.
- **Eine Regel, die einen Prüfwert senkt, kann einen anderen heben.** Die erste Fassung der Aufzugshalt-Regel senkte die festen Punkte und hob die unsichtbaren Kollider von 50 auf 63; eine zu breite Treppen-Regel hob eine ganze Deckplatte um 10 cm. Nach jeder Änderung alle Prüfzahlen gegen den Vorstand vergleichen – und das Bild ansehen: Die Lampe am Sturz leuchtete falsch, als alle Zahlen schon stimmten.
- **„Liegt nur unter dem Deck“ ist kein Grund, einen Rest liegen zu lassen.** Die Träger-Abweichung unter den Decks hatte ich als nicht begehbar abgetan; dieselbe Ursache machte an der Oberfläche eine Stufe im Boden und einen Spalt, durch den man durchsah – gefunden hat es Bl4ck969, nicht die Prüfung. Bei einem Rest erst fragen, was an derselben Stelle *sichtbar* ist.
- **Ein Prüflauf darf den Spielstand nicht speichern, mit dem Bl4ck969 spielt.** Die erste Fassung des Fertigkeiten-Tests verließ den Lauf über `ReturnToMenu(true)` und schrieb `spielstand.test.json` neu – mit der Figur vor der Außenwand, weil der Test sie 10 m zurückversetzt hatte. Aus `.bak` wiederhergestellt (byte-gleich, 19:03-Stand). Jetzt `ReturnToMenu(false)` und kein Versetzen; danach Zeitstempel der Spielstände prüfen.
- **Den fertigen Baum messen, nicht den Code nachbauen.** `SkillCatalog.All` durchläuft fünf Umbaustufen (Build, ExpandMilestones, AuthorSignatureTree, AddChutes, BalanceV11). Ein kleines Lesewerkzeug (net10, lädt `Assembly-CSharp.dll` per Reflexion, ohne Unity) gibt alle 238 Skills mit Ast, Rang, Spur, Kosten und Boni aus – in Sekunden (`werkzeuge\skilldump`).
- **Erst nachsehen, ob Code überhaupt läuft.** Für die Kartenansicht des HUD (`DrawProficiencyBrowser`, `HandleBrowserNavigation`) entstanden zwei Transpiler – die Methoden ruft v1.2 nirgends auf. Einer davon (`RuntimeHelpers.InitializeArray` ohne Parametertypen: mehrdeutig) brach `Apply` ab, sodass Jetpack und HUD-Feld fehlten. Die Prüfung zeigte es sofort; die toten Teile sind wieder raus.
- **Harmony wiederholt Transpiler** bei jedem weiteren Patch derselben Methode. Prefix und Transpiler einer Methode deshalb in *einem* `Patch`-Aufruf, sonst stimmen Zähler (und alles, was der Transpiler nebenbei tut) nicht.
- **Das Plugin bekommt keine Unity-Ereignismethoden, und in `Awake` wird keine Komponente angelegt.** Der Chainloader von BepInEx läuft in `Application..cctor`; eine Komponente mit `Start`/`OnGUI` bringt Unity 6000.6 dort nativ zum Absturz (zweimal gemessen 30.10.2026, Stapel in `Player.log`: `AddComponent` ← `Plugin:Awake` bzw. ← `Application:.cctor`). Komponenten erst aus einem Szenen-Ereignis oder einem Patch auf Szenen-Code anlegen, wie Prüfläufe und `VersionNotice`. Gefunden hat es nur der Lauf im Spiel – der Code las sich richtig.
- **Ein Prüfordner, in dem eine Shell steht, lässt sich nicht löschen.** `Directory.Delete` des Prüfordners warf, weil die Bash-Sitzung ihren Arbeitsordner darin hatte; der Test brach ab, das Spiel blieb offen, `pruefung.ps1` gab nach 5 min auf. Prüfläufe leeren deshalb nur die Dateien; vor einem Lauf den Arbeitsordner aus `pruefung\` nehmen.
- **„Byte-gleich mit der geprüften DLL“ gilt nur für unveränderten Quelltext.** Nach dem Ersetzen eines Namens in Kommentaren war die DLL eine andere (`89d529…` → `945dac…`): Der Build legt Prüfsummen der Quelldateien ab. Der Code war gleich – trotzdem wurden die Prüfläufe mit der neuen DLL wiederholt, statt die Gleichheit zu behaupten. Nach jeder Quelländerung neu bauen und mit genau dieser DLL prüfen.
- **Vor dem Veröffentlichen die ganze Historie durchsuchen, nicht nur den Stand.** Namen, Pfade und Zugangsdaten bleiben in alten Commits stehen, auch wenn der letzte Stand sauber ist; geprüft wurde mit `git log -p --all | grep` (Namen, IPs, Steam-IDs, Tokens, Schlüssel, E-Mail-Adressen). Der Vorname stand 85-mal in der Historie – deshalb die frische Historie.
- **Ein Patch kann nach einem Update still ins Leere greifen, ohne dass etwas wirft.** Die Methode `RefreshCollision` gab es auf Build 26367772 noch, der Patch saß fehlerfrei – nur rief das Spiel sie im laufenden Spiel nicht mehr auf. Kein Log, keine Ausnahme; sichtbar nur im Diff (neuer Aufrufer) und im Leistungstest (Aufrufe je Bild). Nach einem Update deshalb auch prüfen, **wer** eine gepatchte Methode noch aufruft.
- **Das Spiel zählt seine Versionsnummer nicht bei jedem Update hoch** (03.11.2026: neuer Build, weiter „1.2.2“). Eine Meldung „1.2.2 nicht geprüft (geprüft: 1.2.2)“ ist dann richtig und trotzdem unverständlich – angezeigt wird der Steam-Build mit.
- **Ein Testaufbau kann das Ergebnis vorwegnehmen.** Die Kopie eines echten Spielstands als Test-Spielstand lud nicht – das Spiel lehnt Spielstände ab, deren Testkennzeichen nicht zum laufenden Spiel passt. Fast hätte das wie „alte Spielstände sind kaputt“ ausgesehen. Erst den Grund im Spielcode suchen, dann schließen; der Nachweis lief dann ohne Testmodus auf einer eigenen Datei.
- **Prüfläufe zeigen auf dem Bildschirm Prüfdaten.** Bl4ck969 sah während eines Laufs die Prüf-Bestenliste und hielt seine für gelöscht. Vor einem Lauf sagen, dass dabei Prüflisten und Test-Spielstände zu sehen sind; nach einem Alarm zuerst die Prüfsummen der echten Dateien vergleichen.
- **BepInEx schreibt sein Log gepuffert.** Ein Prüflauf, der direkt nach der letzten Zeile `Application.Quit()` ruft, verliert sie manchmal (03.11.2026, zweimal verschieden). Ergebnis zusätzlich in eine Datei schreiben und vor dem Beenden 2 s warten.
- **Ein Vergleichsversuch braucht abwechselnde Durchgänge.** Der Bergtest zeigte erst 0, dann 429 „versunkene“ Bilder – ohne dass sich am Band etwas geändert hatte; die Bedingungen (Haufen fast leer gegen 3,9 Mio., Start im Schacht des vorigen Durchgangs) waren verschieden. Erst „sofort/Hintergrund/sofort/Hintergrund“ im selben Lauf zeigte, dass der Unterschied nicht am Backen liegt.
- **Zahlen aus dem Dokument sind kein Vergleichswert.** Die „Bauzeit 1,2 s“ stammte aus der 15-Etagen-Zeit; erst der gegengemessene committete Stand (2,5 s) zeigte, dass die neuen Regeln nur 0,15 s kosten.
