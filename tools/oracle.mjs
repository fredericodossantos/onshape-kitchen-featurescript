// Oraculo de teste: roda o motor L3 real (buildFromL3) e imprime as pecas
// esperadas (dims/centros em MM) para comparar contra os corpos gerados no
// Onshape (via evBox3d). Mesma logica = verdade-fonte.
//
// Requer o model.js do app L3. Informe o caminho via env MODEL_JS, ou ajuste o default.
//   MODEL_JS="C:/.../hb-core/model.js" node tools/oracle.mjs [spec.json]
// spec.json (opcional) = { "box": {"ext": {"sx","sy","sz"}}, "config": {...} } em mm.

import { readFileSync } from "node:fs";

const MODEL = (process.env.MODEL_JS || "C:/dev/experimentos/exp-02.1-planner-grafo/hb-core/model.js").replace(/\\/g, "/");
const { buildFromL3 } = await import("file:///" + MODEL);

let spec;
const specArg = process.argv[2];
if (specArg) {
  spec = JSON.parse(readFileSync(specArg, "utf8"));
} else {
  // Caso base: 914.4 x 587.37 x 876.3 mm, perfil HB, rodape 100mm, 1 porta dupla.
  spec = {
    box: { ext: { sx: 914.4, sy: 587.37, sz: 876.3 } },
    config: { profile: "hb", frente: "sobreposta", rodape: { altura_mm: 100 }, zonas: [{ tipo: "porta", auto: true, portas: 2 }] },
  };
}

const { parts } = buildFromL3(spec.box, spec.config);
for (const p of parts) {
  const d = p.dims.map((v) => +(v * 1000).toFixed(2));
  const c = p.pos.map((v) => +(v * 1000).toFixed(2));
  console.log(`${(p.name + "").padEnd(13)} kind=${(p.kind + "").padEnd(9)} dims=[${d}] centro=[${c}]`);
}
console.log(`TOTAL: ${parts.length} pecas`);
