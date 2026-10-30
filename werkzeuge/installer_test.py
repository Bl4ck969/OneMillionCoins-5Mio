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

print("D: nicht geprüfte Spielversion")
game, saves = make_game("D", untested=True)
s0 = snapshot(game)
code, out = run(game, saves, "--pruefen")
check(code == 2 and "nicht geprüft" in out, "Vorprüfung rot (Exit %d)" % code)
code, out = run(game, saves, "--installieren", "--einverstanden")
check(code == 2 and diff(s0, snapshot(game)) == [], "Installation verweigert, nichts geschrieben (Exit %d)" % code)

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
