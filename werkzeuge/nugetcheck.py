import json, urllib.request, sys
def get(u): return json.load(urllib.request.urlopen(u))
def osv(name, ver):
    body=json.dumps({"package":{"name":name,"ecosystem":"NuGet"},"version":ver}).encode()
    r=urllib.request.Request("https://api.osv.dev/v1/query",data=body,headers={"Content-Type":"application/json"})
    return len(json.load(urllib.request.urlopen(r)).get("vulns",[]))
for p in sys.argv[1:]:
    vs=get(f"https://api.nuget.org/v3-flatcontainer/{p.lower()}/index.json")["versions"]
    v=[x for x in vs if "-" not in x][-1]
    leaf=get(get(f"https://api.nuget.org/v3/registration5-semver1/{p.lower()}/{v}.json")["catalogEntry"])
    print(p, v, leaf.get("published","")[:10], "deprecation:", leaf.get("deprecation"), "OSV:", osv(p, v))
