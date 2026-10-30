"""Release bauen: Plugin und Installer bauen, Installer-Tests, Paket mit Prüfsummen.

Aufruf (im Projektordner): python -I werkzeuge\\release.py
Ergebnis: release\\OMC-5Mio-<Version>\\ und release\\OMC-5Mio-<Version>.zip. Nichts wird ins Spiel kopiert
(-p:Deploy=false); die Installer-Tests lesen den echten Spielordner und die Spielstände nur.
"""
import hashlib
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def run(*args):
    print(">", " ".join(args))
    subprocess.run(args, cwd=ROOT, check=True)


def sha256(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


source = open(os.path.join(ROOT, "src", "ReleaseInfo.cs"), encoding="utf-8").read()
version = re.search(r'ModVersion = "([^"]+)"', source).group(1)
game_versions = ", ".join(re.findall(r'Version = "([^"]+)", SteamBuild', source))
print("Mod", version, "für Spielversion", game_versions)

run("dotnet", "build", "OneMillionCoins5Mio.csproj", "-c", "Release", "-p:Deploy=false")
run("dotnet", "build", os.path.join("installer", "Installer.csproj"), "-c", "Release")
exe = os.path.join(ROOT, "installer", "bin", "Release", "OMC-5Mio-Installer.exe")
plugin = os.path.join(ROOT, "bin", "Release", "OneMillionCoins5Mio.dll")

with tempfile.TemporaryDirectory(prefix="omc5mio-test-") as temp:
    result = subprocess.run([sys.executable, "-I", os.path.join(ROOT, "werkzeuge", "installer_test.py"), exe, plugin, temp],
                            cwd=ROOT, capture_output=True, encoding="utf-8")
    print(result.stdout)
    if "ERGEBNIS: alles grün" not in result.stdout:
        sys.exit("Installer-Tests nicht grün – kein Release.")

name = "OMC-5Mio-" + version
target = os.path.join(ROOT, "release", name)
if os.path.exists(target):
    shutil.rmtree(target)
os.makedirs(target)
shutil.copy(exe, os.path.join(target, "OMC-5Mio-Installer.exe"))
shutil.copy(os.path.join(ROOT, "LICENSE"), os.path.join(target, "LICENSE.txt"))
shutil.copy(os.path.join(ROOT, "installer", "pakete", "Drittanbieter.txt"), os.path.join(target, "Drittanbieter.txt"))
readme = open(os.path.join(ROOT, "installer", "pakete", "LIESMICH.txt"), encoding="utf-8").read()
readme = readme.replace("{VERSION}", version).replace("{SPIELVERSION}", game_versions)
open(os.path.join(target, "LIESMICH.txt"), "w", encoding="utf-8-sig", newline="\r\n").write(readme)

files = sorted(os.listdir(target))
with open(os.path.join(target, "SHA256SUMS.txt"), "w", encoding="utf-8", newline="\r\n") as f:
    for n in files:
        f.write("%s  %s\n" % (sha256(os.path.join(target, n)), n))

archive = target + ".zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
    for n in sorted(os.listdir(target)):
        z.write(os.path.join(target, n), name + "/" + n)
print("Paket:", archive)
print("SHA-256 ZIP:", sha256(archive))
print("SHA-256 EXE:", sha256(os.path.join(target, "OMC-5Mio-Installer.exe")))
