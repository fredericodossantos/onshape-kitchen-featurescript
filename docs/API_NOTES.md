# Notas de API & FeatureScript — armadilhas descobertas

Lições do desenvolvimento do `kitchenCabinet.fs` (validadas empiricamente contra a API REST do Onshape). Leia antes de mexer nos features ou no conversor — cada uma custou um ciclo de debug.

## Eixos e unidades
- Mundo Onshape é **Z-up**, **mesma convenção do motor L3**: `X=largura, Y=profundidade, Z=altura`. As fórmulas do `model.js` portam sem troca de eixos.
- Trabalhar em **milímetros** no `.fs` (as fórmulas L3 são lineares — não dividir por 1000). Cada peça `{dims:[dx,dy,dz], pos:[cx,cy,cz]}` (pos = centro) vira `fCuboid(corner1 = pos-dims/2, corner2 = pos+dims/2) * millimeter`.
- Frentes sobrepostas projetam para **−Y** (`cy = -(gap + ft/2)`); a frente do armário fica em Y negativo.

## FeatureScript (compilação)
1. **Enums exigem `export enum` + annotation em cada valor.** `enum X { A, B }` **não compila** como tipo de parâmetro de feature — o feature inteiro fica com `featureStatus=ERROR` e `featurespecs` vem vazio. Use:
   ```featurescript
   export enum KCProfile { annotation { "Name" : "HB" } HB, annotation { "Name" : "BR15" } BR15 }
   ```
2. **`LengthBoundSpec` na forma `{ (millimeter) : [min, def, max] } as LengthBoundSpec` é válida.** (Não precisa de `(meter)` base.)
3. Checar chave opcional de map: use `m["k"] != undefined`, **não** `has(m, "k")` (não é função nativa).

## API REST (instanciação de custom feature)
4. **Instanciar com `parameters: []` NÃO aplica os defaults da precondition.** Os inputs ficam `undefined` → a precondition falha → `featureStatus=ERROR`. **É obrigatório passar todos os parâmetros** ao adicionar via API. (A UI preenche defaults; a API não.)
5. **`BTMParameterEnum-145` exige o campo `namespace`** apontando pro Feature Studio onde o enum é definido (= o mesmo namespace do feature, `e{eidFS}::m{microversion}`). Sem ele: HTTP 400 *"Parameter ... does not match its feature spec"*. Vale para enums dentro de arrays também (injetar recursivamente).
6. **Array parameter**: `BTMParameterArray-2025` com `items: [ {btType:"BTMArrayParameterItem-1843", parameters:[...]} ]`. Cada item carrega TODOS os sub-parâmetros declarados na precondition.
7. **Debug de erro de compilação**: `GET /featurestudios/d/{did}/w/{wid}/e/{eid}/featurespecs` → se `featureSpecs: []`, o módulo não compilou. O endpoint também revela o formato exato esperado de cada parâmetro (enumName, namespace).
8. `POST /partstudios/.../featurescript` quer `queries` como **map `{}`**, não array.

## Verificação
- `featureStatus=OK` só garante que compilou/regenerou — **sempre conferir o render** (`Read` do PNG).
- Oráculo: `node tools/oracle.mjs` roda o `buildFromL3` real e imprime as peças esperadas (dims/centros em mm) para comparar contra os corpos do Onshape (`evBox3d`).
