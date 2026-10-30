import json, urllib.request
def get(u): return json.load(urllib.request.urlopen(u))
idx = get("https://api.nuget.org/v3-flatcontainer/ilspycmd/index.json")["versions"]
stable=[v for v in idx if "-" not in v]
print("letzte stabile:", stable[-3:], "| neueste insgesamt:", idx[-1])
v=stable[-1]
reg=get(f"https://api.nuget.org/v3/registration5-semver1/ilspycmd/{v}.json")
print("published:", reg.get("published"))
leaf=get(reg["catalogEntry"])
print("deprecation:", leaf.get("deprecation"), "| vulnerabilities:", leaf.get("vulnerabilities"))
print("deps/frameworks:", [g.get("targetFramework") for g in leaf.get("dependencyGroups",[])])
def osv(name, ver, eco):
    body=json.dumps({"package":{"name":name,"ecosystem":eco},"version":ver}).encode()
    r=urllib.request.Request("https://api.osv.dev/v1/query",data=body,headers={"Content-Type":"application/json"})
    return len(json.load(urllib.request.urlopen(r)).get("vulns",[]))
print("Gegenprobe axios 1.6.0 (muss 29):", osv("axios","1.6.0","npm"))
print("OSV ilspycmd", v, ":", osv("ilspycmd", v, "NuGet"), "| ICSharpCode.Decompiler:", osv("ICSharpCode.Decompiler", v, "NuGet"))
