"""Welt-Grenzen aller Mesh-Objekte in GoldBunker berechnen und die Etagen-Einteilung des Mods nachstellen (nur lesend)."""
import sys, collections
import UnityPy

DATA = r"D:\SteamLibrary\steamapps\common\One Million Coins\One Million Coins_Data"
env = UnityPy.load(DATA + r"\level1", DATA + r"\sharedassets1.assets")

tf = {}
for obj in env.objects:
    if obj.type.name == "Transform" and obj.assets_file.name.endswith("level1"):
        tf[obj.path_id] = obj.read()

def qmat(q):
    x, y, z, w = q.x, q.y, q.z, q.w
    return [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
            [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
            [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]

def mmul(a, b):
    return [[sum(a[i][k]*b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]

def world(t):
    """Gibt (M, p) mit Welt = M*lokal + p zurück (M enthält Rotation und Skalierung)."""
    chain = []
    while t is not None:
        chain.append(t)
        f = t.m_Father
        t = tf.get(f.path_id) if f and f.path_id else None
    M = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]; p = [0.0, 0.0, 0.0]
    for t in reversed(chain):
        lp = [t.m_LocalPosition.x, t.m_LocalPosition.y, t.m_LocalPosition.z]
        p = [p[i] + sum(M[i][k]*lp[k] for k in range(3)) for i in range(3)]
        R = qmat(t.m_LocalRotation)
        S = [[t.m_LocalScale.x, 0, 0], [0, t.m_LocalScale.y, 0], [0, 0, t.m_LocalScale.z]]
        M = mmul(M, mmul(R, S))
    return M, p

def aabb(t, center, ext):
    M, p = world(t)
    c = [p[i] + sum(M[i][k]*center[k] for k in range(3)) for i in range(3)]
    e = [sum(abs(M[i][k])*ext[k] for k in range(3)) for i in range(3)]
    return [c[i]-e[i] for i in range(3)], [c[i]+e[i] for i in range(3)]

def path(t):
    names = []
    while t is not None:
        names.append(t.m_GameObject.read().m_Name)
        f = t.m_Father
        t = tf.get(f.path_id) if f and f.path_id else None
    return "/".join(reversed(names))

go_tf = {t.m_GameObject.path_id: t for t in tf.values()}
rows = []
for obj in env.objects:
    if not obj.assets_file.name.endswith("level1"):
        continue
    if obj.type.name == "MeshFilter":
        mf = obj.read()
        try:
            mesh = mf.m_Mesh.read()
        except Exception:
            continue
        a = mesh.m_LocalAABB
        t = go_tf.get(mf.m_GameObject.path_id)
        if t is None: continue
        lo, hi = aabb(t, [a.m_Center.x, a.m_Center.y, a.m_Center.z], [a.m_Extent.x, a.m_Extent.y, a.m_Extent.z])
        rows.append((path(t), lo, hi, mesh.m_Name))

mode = sys.argv[1] if len(sys.argv) > 1 else "stretch"
BMIN, TMIN, BMAX = 37.9, 42.0, 42.2
for p, lo, hi, mesh in sorted(rows, key=lambda r: r[0]):
    mn, mx = lo[1], hi[1]
    if mode == "stretch" and mn < BMIN and mx > BMAX:
        print(f"STRETCH {mn:7.2f}..{mx:7.2f}  {p}  [{mesh}]")
    if mode == "keep" and mn >= BMIN and mn < TMIN and mx > BMAX:
        print(f"KEEP    {mn:7.2f}..{mx:7.2f}  {p}  [{mesh}]")
    if mode == "grep" and len(sys.argv) > 2 and sys.argv[2].lower() in p.lower():
        print(f"{mn:7.2f}..{mx:7.2f}  {p}  [{mesh}]")
