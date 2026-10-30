"""Steam-Depotmanifest lesen und den Spielordner dagegen pruefen (nur lesend).

Aufruf: python -I manifest_pruefen.py <manifest> <spielordner> [<altes manifest>]
"""
import hashlib
import os
import struct
import sys

MAGIC_PAYLOAD = 0x71F617D0
MAGIC_METADATA = 0x1F4812BE
FLAG_DIRECTORY = 64


def varint(buf, pos):
    shift = 0
    value = 0
    while True:
        b = buf[pos]
        pos += 1
        value |= (b & 0x7F) << shift
        if b < 0x80:
            return value, pos
        shift += 7


def fields(buf):
    pos = 0
    while pos < len(buf):
        key, pos = varint(buf, pos)
        nr, typ = key >> 3, key & 7
        if typ == 0:
            val, pos = varint(buf, pos)
        elif typ == 2:
            n, pos = varint(buf, pos)
            val = buf[pos:pos + n]
            pos += n
        elif typ == 1:
            val = buf[pos:pos + 8]
            pos += 8
        elif typ == 5:
            val = buf[pos:pos + 4]
            pos += 4
        else:
            raise ValueError("unbekannter Typ %d" % typ)
        yield nr, val


def read_manifest(path):
    data = open(path, "rb").read()
    pos = 0
    files = {}
    meta = {}
    while pos + 8 <= len(data):
        magic, length = struct.unpack_from("<II", data, pos)
        pos += 8
        block = data[pos:pos + length]
        pos += length
        if magic == MAGIC_PAYLOAD:
            for nr, val in fields(block):
                if nr != 1:
                    continue
                m = {}
                for fnr, fval in fields(val):
                    if fnr in (1, 2, 3, 5):
                        m[fnr] = fval
                name = m[1].decode("utf-8").replace("\\", "/")
                files[name] = (m.get(2, 0), m.get(3, 0), (m.get(5) or b"").hex())
        elif magic == MAGIC_METADATA:
            for nr, val in fields(block):
                meta[nr] = val
        else:
            break
    return files, meta


def sha1(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    manifest, game = sys.argv[1], sys.argv[2]
    files, meta = read_manifest(manifest)
    print("Manifest:", os.path.basename(manifest))
    print("  Dateinamen verschluesselt:", meta.get(4))
    plain = {k: v for k, v in files.items() if not (v[1] & FLAG_DIRECTORY)}
    print("  Eintraege: %d, davon Dateien %d, Summe %d Bytes" % (
        len(files), len(plain), sum(v[0] for v in plain.values())))

    missing, changed, ok = [], [], 0
    for name, (size, flags, sha) in sorted(plain.items()):
        p = os.path.join(game, name)
        if not os.path.isfile(p):
            missing.append(name)
            continue
        if os.path.getsize(p) != size or sha1(p) != sha:
            changed.append(name)
        else:
            ok += 1
    print("  Dateien unveraendert: %d" % ok)
    print("  fehlen: %d %s" % (len(missing), missing[:20]))
    print("  abweichend: %d %s" % (len(changed), changed[:20]))

    known = {k.lower() for k in files}
    extra = []
    for root, dirs, names in os.walk(game):
        for n in names:
            rel = os.path.relpath(os.path.join(root, n), game).replace("\\", "/")
            if rel.lower() not in known:
                extra.append(rel)
    tops = sorted({e.split("/")[0] for e in extra})
    print("  nicht von Steam (%d Dateien), oberste Ebene: %s" % (len(extra), tops))

    if len(sys.argv) > 3:
        old, _ = read_manifest(sys.argv[3])
        oldf = {k: v for k, v in old.items() if not (v[1] & FLAG_DIRECTORY)}
        added = sorted(set(plain) - set(oldf))
        removed = sorted(set(oldf) - set(plain))
        diff = sorted(k for k in set(plain) & set(oldf) if plain[k][2] != oldf[k][2])
        print("Update alt -> neu:")
        print("  neu: %s" % added)
        print("  entfernt: %s" % removed)
        print("  geaendert (%d): %s" % (len(diff), diff))
        mod_names = [n for n in ("winhttp.dll", "doorstop_config.ini", ".doorstop_version")]
        print("  Mod-Dateinamen in einem der Manifeste:",
              [n for n in mod_names if n.lower() in known or n.lower() in {k.lower() for k in oldf}],
              "BepInEx/ im Manifest:", any(k.lower().startswith("bepinex") for k in files))


main()
