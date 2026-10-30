"""Hierarchie von 'Excavation Bunker' (GoldBunker/level1) nur lesend ausgeben: Name, Welt-Y, Kinder, Komponenten."""
import sys, collections
sys.path.insert(0, r"C:\Users\Andre\AppData\Local\Temp\claude\C--Users-Andre--claude-spares-spare-UTfsc6\7e0e7d45-4611-4b4a-935d-b3ec73a78518\scratchpad")
import UnityPy

DATA = r"D:\SteamLibrary\steamapps\common\One Million Coins\One Million Coins_Data"
env = UnityPy.load(DATA + r"\level1")
tf = {}
comps = collections.defaultdict(list)
for obj in env.objects:
    if obj.type.name == "Transform":
        tf[obj.path_id] = obj.read()
for obj in env.objects:
    if obj.type.name in ("MeshCollider", "BoxCollider", "MeshFilter", "MonoBehaviour", "Light"):
        try:
            d = obj.read(check_read=False)
            comps[d.m_GameObject.path_id].append(obj.type.name.replace("Collider", "C").replace("MeshFilter", "MF").replace("MonoBehaviour", "MB"))
        except Exception:
            pass

def wy(t):
    y = 0.0; s = 1.0
    chain = []
    while t is not None:
        chain.append(t)
        f = t.m_Father
        t = tf.get(f.path_id) if f and f.path_id else None
    for t in reversed(chain):
        y += t.m_LocalPosition.y * s   # Rotation vernachlaessigt
        s *= t.m_LocalScale.y
    return y

maxdepth = int(sys.argv[1]) if len(sys.argv) > 1 else 3
root_name = sys.argv[2] if len(sys.argv) > 2 else "Excavation Bunker"

def subtree_counts(t):
    n = 1
    for c in t.m_Children:
        ct = tf.get(c.path_id)
        if ct: n += subtree_counts(ct)
    return n

def walk(t, d):
    go = t.m_GameObject.read()
    c = collections.Counter(comps.get(t.m_GameObject.path_id, []))
    cs = ",".join(f"{k}{'x'+str(v) if v>1 else ''}" for k, v in c.items())
    print(f"{'  '*d}{go.m_Name}  y={wy(t):.2f}  kinder={len(t.m_Children)} gesamt={subtree_counts(t)} [{cs}]")
    if d < maxdepth:
        for ch in t.m_Children:
            ct = tf.get(ch.path_id)
            if ct: walk(ct, d + 1)

for t in tf.values():
    if t.m_GameObject.read().m_Name == root_name:
        walk(t, 0)
