# onshape-kitchen-featurescript

FeatureScripts **paramétricos** para reconstruir cozinhas (paredes, vãos e armários) no **Onshape**, portados do motor de marcenaria **L3** de um app de planejamento de ambientes. A geometria fica editável via **Part Studio** e **Variable Studio**, gerada a partir de um JSON de cena (contrato v3).

## Por quê
Reconstruir a cena por blocos sólidos (uma caixa por móvel) é infiel — sem portas, gavetas ou prateleiras. Aqui as fórmulas de marcenaria do L3 (`buildFromL3`) viram **custom features paramétricos** no Onshape: muda-se largura/zonas e o móvel regenera.

## Componentes
- `fs/kitchenCabinet.fs` — armário paramétrico (carcaça + portas + prateleiras; gavetas/pia/tampo no roadmap).
- `fs/kitchenWall.fs` — parede com vãos de porta/janela.
- `tools/` — conversor JSON v3 → Onshape (API REST), builders de parâmetros e **oráculo** de teste (compara `buildFromL3` vs Onshape).

## Convenções
- Mundo Onshape **Z-up**: `X=largura, Y=profundidade, Z=altura` (mesma convenção do L3).
- Unidades em **milímetros**.

## Roadmap
- **v1 (MVP)** — parede + vãos (porta/janela) · armário (carcaça, portas, prateleiras) · conversor parametrizado · validação por oráculo + render.
- **v2** — caixa de gaveta, pia, tampo/bancada, miter de cantos, puxadores.
- **Futuro — L2 completo** — meia-parede, sacada, vigas de teto, ambientes (piso/teto), materiais.
- **Futuro — L1 (planta 2D)** — planta baixa técnica (sketches cotados / Drawing) a partir do mesmo JSON.

## Segurança
Nunca versione credenciais (`.env`) nem dados reais de clientes — use a pasta `private/` (ignorada). Os `examples/` são genéricos.

## Licença
[MIT](LICENSE).
