import json, urllib.request, sys
def osv(name, ver, eco):
    body=json.dumps({"package":{"name":name,"ecosystem":eco},"version":ver}).encode()
    r=urllib.request.Request("https://api.osv.dev/v1/query",data=body,headers={"Content-Type":"application/json"})
    return len(json.load(urllib.request.urlopen(r)).get("vulns",[]))
print("Gegenprobe axios 1.6.0:", osv("axios","1.6.0","npm"))
for p in sys.argv[1:]:
    d=json.load(urllib.request.urlopen(f"https://pypi.org/pypi/{p}/json"))
    v=d["info"]["version"]; rel=d["releases"][v]
    print(p, v, "upload:", rel[0]["upload_time"][:10] if rel else "?", "yanked:", d["info"]["yanked"], "requires_python:", d["info"]["requires_python"], "OSV:", osv(p, v, "PyPI"))
