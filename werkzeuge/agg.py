import subprocess, sys, re, collections
out = subprocess.run([sys.executable, "-I", "scene_bounds.py", "grep", "/"], capture_output=True, text=True).stdout
grp = collections.defaultdict(lambda: [1e9, -1e9, 0])
for line in out.splitlines():
    m = re.match(r"\s*(-?[\d.]+)\.\.\s*(-?[\d.]+)\s+(\S.*?)\s+\[", line)
    if not m: continue
    lo, hi, p = float(m.group(1)), float(m.group(2)), m.group(3)
    parts = p.split("/")
    key = "/".join(parts[:3]) if len(parts) >= 3 and parts[1] == "Architecture" else "/".join(parts[:2]) if len(parts)>=2 else p
    if parts[2:3] == ["Finished bunker metalwork"] or parts[2:3] == ["Vault fixture lighting"]:
        key = "/".join(parts[:4])
    g = grp[key]; g[0] = min(g[0], lo); g[1] = max(g[1], hi); g[2] += 1
for k in sorted(grp, key=lambda k: grp[k][0]):
    lo, hi, n = grp[k]
    print(f"{lo:7.2f}..{hi:7.2f}  n={n:4d}  {k}")
