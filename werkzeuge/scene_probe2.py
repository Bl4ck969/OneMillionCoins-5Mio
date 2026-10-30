"""GoldBunker (level1) nur lesend vermessen: Weltpositionen, Skriptklassen, Rohfelder ausgewaehlter Komponenten."""
import struct, collections
class np:
    @staticmethod
    def array(v): return list(v)
    @staticmethod
    def zeros(n): return [0.0]*n
    @staticmethod
    def ones(n): return [1.0]*n
    @staticmethod
    def dot(a,b): return sum(x*y for x,y in zip(a,b))
    @staticmethod
    def cross(a,b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
    @staticmethod
    def round(v,n): return [round(x,n) for x in v] if v is not None else None
def vadd(a,b): return [x+y for x,y in zip(a,b)]
def vmul(a,b): return [x*y for x,y in zip(a,b)]
def vs(k,a): return [k*x for x in a]
import UnityPy

DATA = r"D:\SteamLibrary\steamapps\common\One Million Coins\One Million Coins_Data"
env = UnityPy.load(DATA + r"\globalgamemanagers.assets", DATA + r"\sharedassets1.assets", DATA + r"\level1")

tf = {}      # path_id -> transform-Objekt (gelesen)
for obj in env.objects:
    if obj.type.name == "Transform" and obj.assets_file.name.endswith("level1"):
        tf[obj.path_id] = obj.read()

def quat_rot(q, v):
    x, y, z, w = q
    u = np.array([x, y, z]); v = np.array(v)
    return vadd(vadd(vs(2*np.dot(u, v),u), vs(w*w - np.dot(u, u),v)), vs(2*w,np.cross(u, v)))

def world(t):
    """Weltposition und -skalierung (Rotation/Skalierung vereinfacht: nichtuniforme Skalen mit Rotation ignoriert)."""
    chain = []
    while t is not None:
        chain.append(t)
        f = t.m_Father
        t = tf.get(f.path_id) if f and f.path_id else None
    pos = np.zeros(3); scale = np.ones(3); rot = (0, 0, 0, 1)
    for t in reversed(chain):
        lp = vmul([t.m_LocalPosition.x, t.m_LocalPosition.y, t.m_LocalPosition.z], scale)
        pos = vadd(pos, quat_rot(rot, lp))
        q = t.m_LocalRotation
        a = rot; b = (q.x, q.y, q.z, q.w)
        rot = (a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1], a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0],
               a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3], a[3]*b[3]-a[0]*b[0]-a[1]*b[1]-a[2]*b[2])
        scale = vmul(scale, [t.m_LocalScale.x, t.m_LocalScale.y, t.m_LocalScale.z])
    return pos, scale

def go_name(t):
    return t.m_GameObject.read().m_Name

def path(t):
    names = []
    while t is not None:
        names.append(go_name(t))
        f = t.m_Father
        t = tf.get(f.path_id) if f and f.path_id else None
    return "/".join(reversed(names))

# Skriptklasse je GameObject (MonoBehaviour-Kopf von Hand lesen, Typbaum passt nicht)
scripts = {}
for obj in env.objects:
    if obj.type.name == "MonoScript":
        try:
            scripts[(obj.assets_file.name, obj.path_id)] = obj.read().m_ClassName
        except Exception:
            pass
def mb_head(obj):
    raw = obj.get_raw_data()
    go_fid, go_pid = struct.unpack_from("<iq", raw, 0)
    sc_fid, sc_pid = struct.unpack_from("<iq", raw, 16)
    n = struct.unpack_from("<i", raw, 28)[0]
    off = 32 + n
    off = (off + 3) & ~3
    ext = obj.assets_file.externals
    fname = obj.assets_file.name if sc_fid == 0 else ext[sc_fid-1].path.split("/")[-1]
    cls = next((c for (f,pid),c in scripts.items() if pid == sc_pid and f.endswith(fname)), "?")
    return go_pid, cls, raw, off
by_go = collections.defaultdict(list)
classes = collections.Counter()
for obj in env.objects:
    if obj.type.name == "MonoBehaviour" and obj.assets_file.name.endswith("level1"):
        go_pid, cls, raw, off = mb_head(obj)
        classes[cls] += 1
        by_go[go_pid].append((cls, obj, raw, off))
print("Skriptklassen:", dict(classes.most_common()))

go_tf = {t.m_GameObject.path_id: t for t in tf.values()}
WANT = {"CargoElevator", "GranularCoinPile", "DepositMachine", "ElevatorControl", "ElevatorLandingGate",
        "ElevatorFloorIndicator", "CoinFieldManager", "FinalCoinSequence", "PassiveWallChutes", "VaultCurseEnding"}
for go_id, comps in by_go.items():
    for cls, obj, raw, off in comps:
        if cls not in WANT:
            continue
        t = go_tf.get(go_id)
        pos, sc = world(t) if t else (None, None)
        print(f"\n[{cls}] {path(t) if t else go_id}\n  welt={np.round(pos,2)} skala={np.round(sc,3)} rohbytes={len(raw)}")
        print("  felder:", raw[off:off+160].hex())

print("\nWurzeln mit Weltposition:")
for t in tf.values():
    if not t.m_Father or t.m_Father.path_id == 0:
        pos, sc = world(t)
        print(f"  {go_name(t):40s} pos={np.round(pos,2)} skala={np.round(sc,3)} kinder={len(t.m_Children)}")
