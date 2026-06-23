#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Instancia um custom feature deste repo no Onshape via API REST, passando
parametros. Trata as armadilhas documentadas em docs/API_NOTES.md:
  - passa TODOS os parametros (a API nao aplica defaults da precondition);
  - injeta o `namespace` nos BTMParameterEnum (inclusive dentro de arrays);
  - sincroniza a versao da std lib; renderiza um PNG.

Uso:
  python instantiate.py --fs ../fs/kitchenCabinet.fs --params ../examples/cabinet-base.json [--public] [--out out]

params.json = lista de BTMParameter (formato Onshape). Veja examples/.
Credenciais: variaveis de ambiente ONSHAPE_ACCESS_KEY / ONSHAPE_SECRET_KEY.
"""
import argparse, base64, json, os, re, sys
from pathlib import Path
import requests

A = os.environ.get("ONSHAPE_ACCESS_KEY"); S = os.environ.get("ONSHAPE_SECRET_KEY")
BASE = (os.environ.get("ONSHAPE_BASE_URL") or "https://cad.onshape.com").rstrip("/")
api = f"{BASE}/api/v10"

ap = argparse.ArgumentParser()
ap.add_argument("--fs", required=True, help="caminho do .fs (Feature Studio)")
ap.add_argument("--params", required=True, help="JSON com a lista de BTMParameter")
ap.add_argument("--name", default="Kitchen feature (Claude)")
ap.add_argument("--public", action="store_true", help="documento publico (obrigatorio no plano free)")
ap.add_argument("--out", default="out")
args = ap.parse_args()

if not A or not S:
    sys.exit("ERRO: defina ONSHAPE_ACCESS_KEY / ONSHAPE_SECRET_KEY no ambiente.")

ss = requests.Session(); ss.auth = (A, S)
H = {"Accept": "application/json;charset=UTF-8; qs=0.09", "Content-Type": "application/json;charset=UTF-8; qs=0.09"}

def req(m, p, **k):
    r = ss.request(m, (p if p.startswith("http") else api + p), headers=H, timeout=90, **k)
    if not r.ok: sys.exit(f"[{r.status_code}] {m} {p}\n{r.text[:1500]}")
    return r
def fk(o, k):
    if isinstance(o, dict):
        if k in o: return o[k]
        for v in o.values():
            x = fk(v, k)
            if x is not None: return x
    elif isinstance(o, list):
        for v in o:
            x = fk(v, k)
            if x is not None: return x
    return None

out_dir = Path(args.out).resolve(); out_dir.mkdir(parents=True, exist_ok=True)
src = open(args.fs, encoding="utf-8").read()
feat = re.search(r"export\s+const\s+(\w+)\s*=\s*defineFeature", src).group(1)
params = json.loads(open(args.params, encoding="utf-8").read())

doc = req("POST", "/documents", data=json.dumps({"name": args.name, "isPublic": bool(args.public)})).json()
did = doc["id"]; wid = doc["defaultWorkspace"]["id"]
eid_ps = next(e["id"] for e in req("GET", f"/documents/d/{did}/w/{wid}/elements").json() if e["elementType"] == "PARTSTUDIO")
eid_fs = req("POST", f"/featurestudios/d/{did}/w/{wid}", data=json.dumps({"name": Path(args.fs).stem})).json()["id"]
fsget = req("GET", f"/featurestudios/d/{did}/w/{wid}/e/{eid_fs}").json()
ser = fsget.get("serializationVersion", "1.2.0"); smv = fsget.get("sourceMicroversion", "") or ""
std = (re.search(r"FeatureScript\s+(\d+);", fsget.get("contents", "") or "") or [None, "2985"])[1]
src = re.sub(r"FeatureScript\s+\d+;", f"FeatureScript {std};", src, count=1)
src = re.sub(r'version\s*:\s*"\d+\.0"', f'version : "{std}.0"', src)
req("POST", f"/featurestudios/d/{did}/w/{wid}/e/{eid_fs}", data=json.dumps(
    {"contents": src, "serializationVersion": ser, "sourceMicroversion": smv, "rejectMicroversionSkew": False}))
fs_mv = next((e.get("microversionId") for e in req("GET", f"/documents/d/{did}/w/{wid}/elements").json() if e["id"] == eid_fs), None)
ns = f"e{eid_fs}::m{fs_mv}"

# enums (em qualquer nivel, inclusive dentro de arrays) precisam do namespace do FS
def inject_ns(plist):
    for p in plist:
        if p.get("btType") == "BTMParameterEnum-145" and "namespace" not in p:
            p["namespace"] = ns
        for it in p.get("items", []):
            inject_ns(it.get("parameters", []))
inject_ns(params)

body = {"btType": "BTFeatureDefinitionCall-1406", "feature": {
    "btType": "BTMFeature-134", "featureType": feat, "name": feat, "namespace": ns,
    "parameters": params, "suppressed": False, "returnAfterSubfeatures": False, "subFeatures": []}}
resp = req("POST", f"/partstudios/d/{did}/w/{wid}/e/{eid_ps}/features", data=json.dumps(body)).json()
status = fk(resp, "featureStatus")
print("featureStatus:", status)
if str(status).upper() not in ("OK", "NONE"):
    print(json.dumps(resp, indent=1)[:1500])

(out_dir / "last_doc.json").write_text(json.dumps(
    {"did": did, "wid": wid, "eid_ps": eid_ps, "eid_fs": eid_fs, "feature": feat}, indent=2), encoding="utf-8")

iso = "0.612,0.612,0,0,-0.354,0.354,0.707,0,0.707,-0.707,0.707,0"
rr = ss.get(f"{api}/partstudios/d/{did}/w/{wid}/e/{eid_ps}/shadedviews",
            params={"viewMatrix": iso, "outputWidth": 1100, "outputHeight": 850, "pixelSize": 0, "edges": "show", "useAntiAliasing": True},
            headers={"Accept": H["Accept"]})
imgs = rr.json().get("images") if rr.ok else None
if imgs:
    (out_dir / "render.png").write_bytes(base64.b64decode(imgs[0]))
    print("render:", out_dir / "render.png")
print("ABRA:", f"{BASE}/documents/{did}/w/{wid}/e/{eid_ps}")
