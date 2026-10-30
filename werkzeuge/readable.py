import UnityPy
DATA = r"D:\SteamLibrary\steamapps\common\One Million Coins\One Million Coins_Data"
env = UnityPy.load(DATA + r"\level1", DATA + r"\sharedassets1.assets")
n = 0
for obj in env.objects:
    if obj.type.name == "Mesh":
        try:
            d = obj.read_typetree()
        except Exception as e:
            print("Fehler", e); break
        if d["m_Name"].startswith("Finish_"):
            print(d["m_Name"], "readable:", d.get("m_IsReadable"), "verts:", d.get("m_VertexData", {}).get("m_VertexCount"), "submeshes:", len(d.get("m_SubMeshes", [])))
            n += 1
print(n)
