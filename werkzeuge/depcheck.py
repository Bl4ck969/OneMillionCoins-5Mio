import json, urllib.request
def osv(name, ver):
    body=json.dumps({"package":{"name":name,"ecosystem":"PyPI"},"version":ver}).encode()
    r=urllib.request.Request("https://api.osv.dev/v1/query",data=body,headers={"Content-Type":"application/json"})
    return [v["id"] for v in json.load(urllib.request.urlopen(r)).get("vulns",[])]
for line in open("installed.txt"):
    n,v=line.strip().split("==")
    print(n, v, osv(n,v))
