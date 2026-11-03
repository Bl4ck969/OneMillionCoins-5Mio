"""Installer an nachgebauten Spielordnern testen. Misst am Ergebnis: Prüfsummen aller Dateien und alle Ordner.

Aufruf: python -I installer_test.py <installer.exe> <plugin.dll> <testordner>
Der echte Spielordner und die echten Spielstände werden nur gelesen (vorher/nachher-Vergleich).
"""
import hashlib
import os
import shutil
import subprocess
import sys
import time
import zipfile

sys.stdout.reconfigure(encoding="utf-8")
EXE, PLUGIN, BASE = (os.path.abspath(a) for a in sys.argv[1:4])
REAL_GAME = r"D:\SteamLibrary\steamapps\common\One Million Coins"
REAL_SAVES = os.path.join(os.environ["LOCALAPPDATA"], "OneMillionCoins-5Mio")
ZIP = os.path.join(os.path.dirname(EXE), "..", "..", "pakete", "BepInEx_win_x64_5.4.23.6.zip")
CFG = r"BepInEx\config\de.bl4ck969.onemillioncoins.5mio.cfg"
PLUGIN_REL = r"BepInEx\plugins\OneMillionCoins5Mio\OneMillionCoins5Mio.dll"
assert BASE.lower().startswith(os.environ["LOCALAPPDATA"].lower() + "\\temp"), "Testordner muss im Temp liegen"

failures = []


def check(cond, text):
    print(("  ok    " if cond else "  FEHLT ") + text)
    if not cond:
        failures.append(text)


def sha(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def snapshot(root):
    files, dirs = {}, set()
    if not os.path.isdir(root):
        return files, dirs
    for d, subdirs, names in os.walk(root):
        for s in subdirs:
            dirs.add(os.path.relpath(os.path.join(d, s), root).lower())
        for n in names:
            p = os.path.join(d, n)
            files[os.path.relpath(p, root).lower()] = sha(p)
    return files, dirs


def diff(a, b):
    fa, da = a
    fb, db = b
    out = []
    out += ["+" + k for k in sorted(set(fb) - set(fa))]
    out += ["-" + k for k in sorted(set(fa) - set(fb))]
    out += ["~" + k for k in sorted(k for k in set(fa) & set(fb) if fa[k] != fb[k])]
    out += ["+dir " + k for k in sorted(db - da)]
    out += ["-dir " + k for k in sorted(da - db)]
    return out


def make_game(name, untested=False):
    root = os.path.join(BASE, name)
    if os.path.exists(root):
        shutil.rmtree(root)
    game = os.path.join(root, "steamapps", "common", "One Million Coins")
    os.makedirs(os.path.join(game, "One Million Coins_Data", "Managed"))
    shutil.copy(r"C:\Windows\System32\PING.EXE", os.path.join(game, "One Million Coins.exe"))
    for rel in (r"One Million Coins_Data\Managed\Assembly-CSharp.dll", r"One Million Coins_Data\globalgamemanagers"):
        shutil.copy(os.path.join(REAL_GAME, rel), os.path.join(game, rel))
    open(os.path.join(game, "UnityPlayer.dll"), "wb").write(b"original")
    if untested:
        with open(os.path.join(game, r"One Million Coins_Data\globalgamemanagers"), "ab") as f:
            f.write(b"x")
    saves = os.path.join(root, "saves")
    return game, saves


def run(game, saves, *args):
    r = subprocess.run([EXE, "--spielordner", game, "--spielstand-ordner", saves, *args],
                       capture_output=True, encoding="utf-8", errors="replace", timeout=120)
    return r.returncode, r.stdout


def extract_bepinex(game):
    with zipfile.ZipFile(ZIP) as z:
        for i in z.infolist():
            if i.filename == "changelog.txt" or i.filename.endswith("/"):
                continue
            target = os.path.join(game, i.filename.replace("/", "\\"))
            os.makedirs(os.path.dirname(target), exist_ok=True)
            open(target, "wb").write(z.read(i))


def runtime_files(game):
    """Was BepInEx und die Mod beim Spielen anlegen."""
    for rel, data in ((r"BepInEx\LogOutput.log", b"log"), (r"BepInEx\cache\chainloader_typeloader.dat", b"c"),
                      (r"BepInEx\config\BepInEx.cfg", b"cfg"), (CFG, b"[Etagen]")):
        p = os.path.join(game, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        open(p, "wb").write(data)


real_before = (snapshot(REAL_SAVES), sha(os.path.join(REAL_GAME, PLUGIN_REL)), snapshot(os.path.join(REAL_GAME, "BepInEx")))
plugin_sha = sha(PLUGIN)

print("A: sauberes Spiel – installieren, spielen, deinstallieren (Spielstände bleiben), dann mit Löschen")
game, saves = make_game("A")
s0 = snapshot(game)
code, out = run(game, saves, "--pruefen")
check(code == 0, "Vorprüfung grün (Exit %d)" % code)
code, out = run(game, saves, "--installieren")
check(code == 3 and "--einverstanden" in out and diff(s0, snapshot(game)) == [], "ohne Zustimmung: Exit 3, nichts geschrieben")
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 0, "Installation Exit %d" % code)
check(os.path.isfile(os.path.join(game, r"BepInEx\core\BepInEx.dll")) and os.path.isfile(os.path.join(game, "winhttp.dll")), "BepInEx und winhttp.dll da")
check(not os.path.exists(os.path.join(game, "changelog.txt")), "changelog.txt nicht im Spielordner")
check(sha(os.path.join(game, PLUGIN_REL)) == plugin_sha, "Plugin = gebaute DLL")
check("Nachgeprüft: 22 Dateien" in out, "Nachprüfung meldet 22 Dateien (21 BepInEx + Plugin)")
check(os.path.isfile(os.path.join(game, r"5Mio-Mod-Installation\installation.txt")), "Protokoll angelegt")
runtime_files(game)
os.makedirs(saves)
open(os.path.join(saves, "spielstand.json"), "w").write("{}")
code, out = run(game, saves, "--deinstallieren")
check(code == 0, "Deinstallation Exit %d" % code)
d = diff(s0, snapshot(game))
check(d == [], "Spielordner wie vorher" + ("" if not d else ": " + ", ".join(d[:8])))
check(os.path.isfile(os.path.join(saves, "spielstand.json")), "Spielstände behalten")
code, out = run(game, saves, "--installieren", "--einverstanden")
code2, out2 = run(game, saves, "--deinstallieren", "--spielstaende-loeschen")
check(code == 0 and code2 == 0 and diff(s0, snapshot(game)) == [], "zweiter Durchlauf: Spielordner wie vorher")
check(not os.path.exists(saves) and "Spielstände der Mod gelöscht (1 Dateien)" in out2, "Spielstände gelöscht")

print("B: BepInEx 5 schon da, von Hand installierte Mod, eigene Einstellungen, fremde Mod")
game, saves = make_game("B")
extract_bepinex(game)
for rel, data in ((PLUGIN_REL, b"alte Fassung"), (CFG, b"[Etagen] eigene"), (r"BepInEx\plugins\Fremd\fremd.dll", b"fremd"),
                  (r"BepInEx\LogOutput.log", b"log")):
    p = os.path.join(game, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    open(p, "wb").write(data)
s0 = snapshot(game)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 0 and "Gesichert und ersetzt: " + PLUGIN_REL in out, "Installation sichert die alte Fassung (Exit %d)" % code)
check(sha(os.path.join(game, PLUGIN_REL)) == plugin_sha, "neue Fassung liegt im Plugin-Ordner")
d = [x for x in diff(s0, snapshot(game)) if "5mio-mod-installation" not in x]
check(d == ["~" + PLUGIN_REL.lower()], "sonst nichts verändert: " + ", ".join(d))
code, out = run(game, saves, "--deinstallieren")
d = diff(s0, snapshot(game))
check(code == 0 and d == [], "Deinstallation: alte Fassung zurück, BepInEx/Einstellungen/fremde Mod unberührt" + ("" if not d else ": " + ", ".join(d)))

print("C: Installer bringt BepInEx mit, danach kommt eine fremde Mod dazu")
game, saves = make_game("C")
s0 = snapshot(game)
run(game, saves, "--installieren", "--einverstanden")
runtime_files(game)
fremd = os.path.join(game, r"BepInEx\plugins\Fremd\fremd.dll")
os.makedirs(os.path.dirname(fremd))
open(fremd, "wb").write(b"fremd")
code, out = run(game, saves, "--deinstallieren")
check(code == 0 and "BepInEx bleibt" in out, "Deinstallation meldet: BepInEx bleibt (Exit %d)" % code)
check(os.path.isfile(fremd) and os.path.isfile(os.path.join(game, r"BepInEx\core\BepInEx.dll")) and os.path.isfile(os.path.join(game, "winhttp.dll")),
      "fremde Mod und BepInEx noch da")
check(not os.path.exists(os.path.join(game, r"BepInEx\plugins\OneMillionCoins5Mio")) and not os.path.exists(os.path.join(game, CFG)),
      "Mod-Ordner und Mod-Einstellungen weg")
check(not os.path.exists(os.path.join(game, "5Mio-Mod-Installation")), "Protokollordner weg")

def fingerprint(game):
    """Wie ReleaseInfo.Fingerprint: je 16 Zeichen der Prüfsummen von Code und Einstellungen des Spiels."""
    return (sha(os.path.join(game, r"One Million Coins_Data\Managed\Assembly-CSharp.dll"))[:16] + "-" +
            sha(os.path.join(game, r"One Million Coins_Data\globalgamemanagers"))[:16])


print("D: nicht geprüfte Spielversion (seit 03.11.2026: Hinweis statt Sperre, Freigabe nur mit --ungeprueft)")
game, saves = make_game("D", untested=True)
s0 = snapshot(game)
code, out = run(game, saves, "--pruefen")
check(code == 0 and "[INFO]   Diese Spielversion ist nicht geprüft" in out, "Vorprüfung: Hinweis, installierbar (Exit %d)" % code)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 2 and "--ungeprueft" in out and diff(s0, snapshot(game)) == [], "ohne --ungeprueft: Exit 2, nichts geschrieben (Exit %d)" % code)
code, out = run(game, saves, "--installieren", "--einverstanden", "--ungeprueft")
fp = fingerprint(game)
cfg = os.path.join(game, CFG)
text = open(cfg, encoding="utf-8", newline="").read() if os.path.isfile(cfg) else ""   # newline="": CRLF nicht umwandeln
check(code == 0 and "Nachgeprüft: 23 Dateien" in out, "mit --ungeprueft installiert, 23 Dateien nachgeprüft (Exit %d)" % code)
check(text == "[Kompatibilitaet]\r\n\r\nFreigegebeneVersion = %s\r\n" % fp, "Einstellungen tragen die Freigabe für genau diesen Stand: %r" % text)
code, out = run(game, saves, "--deinstallieren")
d = diff(s0, snapshot(game))
check(code == 0 and d == [], "Deinstallation: Spielordner wie vorher, Einstellungen weg" + ("" if not d else ": " + ", ".join(d[:8])))

print("D2: nicht geprüft, BepInEx und eigene Einstellungen schon da (alte Freigabe, andere Abschnitte)")
game, saves = make_game("D2", untested=True)
extract_bepinex(game)
own = "[Etagen]\r\n\r\nAnzahl = 30\r\n\r\n[Kompatibilitaet]\r\n\r\n## Beschreibung\r\nUngepruefteVersion = false\r\nFreigegebeneVersion = alt-alt\r\n\r\n[Menue]\r\n\r\nTresor = 5Mio\r\n"
os.makedirs(os.path.dirname(os.path.join(game, CFG)), exist_ok=True)
open(os.path.join(game, CFG), "wb").write(own.encode("utf-8"))
s0 = snapshot(game)
code, out = run(game, saves, "--installieren", "--einverstanden", "--ungeprueft")
text = open(os.path.join(game, CFG), encoding="utf-8", newline="").read()
check(code == 0 and text == own.replace("alt-alt", fingerprint(game)), "nur die Freigabe-Zeile ersetzt, alles andere gleich (Exit %d)" % code)
code, out = run(game, saves, "--deinstallieren")
d = diff(s0, snapshot(game))
check(code == 0 and d == [], "Deinstallation: Einstellungen byte-gleich zurück" + ("" if not d else ": " + ", ".join(d[:8])))
for label, own, expected in (
        ("Abschnitt ohne Schlüssel", "[Kompatibilitaet]\r\n\r\nUngepruefteVersion = false\r\n",
         "[Kompatibilitaet]\r\nFreigegebeneVersion = {fp}\r\n\r\nUngepruefteVersion = false\r\n"),
        ("Datei ohne Abschnitt", "[Etagen]\r\nAnzahl = 30\r\n\r\n",
         "[Etagen]\r\nAnzahl = 30\r\n\r\n[Kompatibilitaet]\r\n\r\nFreigegebeneVersion = {fp}\r\n")):
    open(os.path.join(game, CFG), "wb").write(own.encode("utf-8"))
    s1 = snapshot(game)
    code, out = run(game, saves, "--installieren", "--einverstanden", "--ungeprueft")
    text = open(os.path.join(game, CFG), encoding="utf-8", newline="").read()
    check(code == 0 and text == expected.format(fp=fingerprint(game)), "%s: Freigabe eingefügt: %r" % (label, text))
    run(game, saves, "--deinstallieren")
    check(diff(s1, snapshot(game)) == [], "%s: Deinstallation stellt die Datei wieder her" % label)

print("D3: geprüfte Spielversion mit --ungeprueft: keine Freigabe nötig, Einstellungen bleiben unberührt")
game, saves = make_game("D3")
s0 = snapshot(game)
code, out = run(game, saves, "--installieren", "--einverstanden", "--ungeprueft")
check(code == 0 and not os.path.exists(os.path.join(game, CFG)) and "FreigegebeneVersion" not in out, "keine Einstellungsdatei angelegt (Exit %d)" % code)
run(game, saves, "--deinstallieren")
check(diff(s0, snapshot(game)) == [], "Deinstallation: Spielordner wie vorher")

print("I: Update – neu installieren über eine bestehende Installation: Spielstände und Mod-Einstellungen bleiben (seit 03.11.2026)")
game, saves = make_game("I")
s0 = snapshot(game)
run(game, saves, "--installieren", "--einverstanden")
runtime_files(game)
played = b"[Menue]\r\n\r\nTresor = 5Mio\r\n\r\n[Aufzug]\r\n\r\nGeschwindigkeit = 9\r\n"
open(os.path.join(game, CFG), "wb").write(played)
os.makedirs(saves)
for name in ("spielstand.json", "spielstand.json.bak", "bestenliste.tsv"):
    open(os.path.join(saves, name), "wb").write(name.encode() * 100)
saves_before = snapshot(saves)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 0 and "Spielstände bleiben" in out and "Einstellungen der Mod übernommen" in out, "neu installiert (Exit %d)" % code)
check(snapshot(saves) == saves_before, "Spielstände und Bestenliste byte-gleich (%d Dateien)" % len(saves_before[0]))
check(open(os.path.join(game, CFG), "rb").read() == played, "Mod-Einstellungen byte-gleich (Tresor = 5Mio)")
code, out = run(game, saves, "--deinstallieren")
d = diff(s0, snapshot(game))
check(code == 0 and d == [], "Deinstallation danach: Spielordner wie vor der ersten Installation" + ("" if not d else ": " + ", ".join(d[:8])))

print("I2: dasselbe, BepInEx und eigene Einstellungen waren schon vor der ersten Installation da")
game, saves = make_game("I2")
extract_bepinex(game)
os.makedirs(os.path.dirname(os.path.join(game, CFG)), exist_ok=True)
open(os.path.join(game, CFG), "wb").write(b"[Etagen]\r\n\r\nAnzahl = 25\r\n")
s0 = snapshot(game)
run(game, saves, "--installieren", "--einverstanden")
open(os.path.join(game, CFG), "wb").write(played)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 0 and open(os.path.join(game, CFG), "rb").read() == played, "neu installiert, gespielte Einstellungen bleiben (Exit %d)" % code)
code, out = run(game, saves, "--deinstallieren")
d = diff(s0, snapshot(game))
# Eine schon vorhandene Einstellungsdatei hat der Installer nie geschrieben: Sie bleibt, wie das Spiel sie zuletzt schrieb (wie Fall B).
check(code == 0 and d == ["~" + CFG.lower()] and open(os.path.join(game, CFG), "rb").read() == played,
      "Deinstallation: sonst alles wie vorher, vorhandene Einstellungen bleiben in ihrem letzten Stand" + ("" if not d else ": " + ", ".join(d[:8])))

print("E: anderer Mod-Lader (winhttp.dll ohne BepInEx)")
game, saves = make_game("E")
open(os.path.join(game, "winhttp.dll"), "wb").write(b"fremder lader")
s0 = snapshot(game)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 2 and "anderer Mod-Lader" in out and diff(s0, snapshot(game)) == [], "verweigert, nichts geschrieben (Exit %d)" % code)

print("F: Spiel läuft (Ersatzprozess „One Million Coins.exe“ aus Ordner F)")
game, saves = make_game("F")
other, other_saves = make_game("F2")
p = subprocess.Popen([os.path.join(game, "One Million Coins.exe"), "-n", "30", "127.0.0.1"], stdout=subprocess.DEVNULL)
time.sleep(1)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 2 and "Das Spiel läuft" in out, "eigener Ordner: verweigert (Exit %d)" % code)
code, out = run(other, other_saves, "--pruefen")
check(code == 0 and "läuft" not in out, "anderer Ordner: nicht betroffen (Exit %d)" % code)
p.kill()

print("G: Abbruch mitten in der Installation (winhttp.dll ist ein Ordner, Schreiben scheitert)")
game, saves = make_game("G")
os.makedirs(os.path.join(game, "winhttp.dll"))
s0 = snapshot(game)
code, out = run(game, saves, "--installieren", "--einverstanden")
d = diff(s0, snapshot(game))
check(code == 1 and "rückgängig" in out, "Fehler gemeldet und zurückgerollt (Exit %d)" % code)
check(d == [], "Spielordner wie vorher" + ("" if not d else ": " + ", ".join(d[:8])))

print("H: alte Entwicklungsfassung (Ordner OneMillionCoins100M, vor dem 30.10.2026)")
game, saves = make_game("H")
extract_bepinex(game)
old = os.path.join(game, r"BepInEx\plugins\OneMillionCoins100M")
os.makedirs(old)
open(os.path.join(old, "OneMillionCoins100M.dll"), "wb").write(b"alt")
s0 = snapshot(game)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 2 and "ältere Entwicklungsfassung" in out and diff(s0, snapshot(game)) == [], "verweigert, nichts geschrieben (Exit %d)" % code)

real_after = (snapshot(REAL_SAVES), sha(os.path.join(REAL_GAME, PLUGIN_REL)), snapshot(os.path.join(REAL_GAME, "BepInEx")))
print("Echte Daten")
check(real_before[0] == real_after[0], "echte Spielstände unverändert (%d Dateien)" % len(real_before[0][0]))
check(real_before[1:] == real_after[1:], "echter Spielordner (BepInEx, Plugin) unverändert")

print()
print("ERGEBNIS:", "alles grün" if not failures else "%d Fehler" % len(failures))
