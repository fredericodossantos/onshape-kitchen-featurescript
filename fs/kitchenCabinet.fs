FeatureScript 2985;
import(path : "onshape/std/geometry.fs",   version : "2985.0");
import(path : "onshape/std/properties.fs", version : "2985.0");

// =============================================================================
// kitchenCabinet.fs  —  Custom feature Onshape paramétrico, motor L3 v1.
// Porta o subconjunto v1 de hb-core/model.js (carcaça + zonas porta/nicho).
// NÃO inclui: gaveta, pia, corner, flipUp, pullout, tampo, appliances.
//
// CONVENÇÃO DE EIXOS (Z-UP, igual ao model.js):
//   X = largura  ·  Y = profundidade  ·  Z = altura (vertical)
//   Chão = plano Top (XY). Corpo projeta para +Y; costas no plano XZ.
//   pos = CENTRO da peça (mesma convenção do model.js).
//   corner1 = pos - dims/2 ; corner2 = pos + dims/2  (em cada eixo).
//
// Unidades internas: MILÍMETROS.
// Fórmulas: lineares → não dividir por 1000 (model.js usa metros; nós usamos mm diretamente).
// Puxadores: NÃO gerados (GERAR_PULLS = false, parity com model.js).
// =============================================================================

// ---------------------------------------------------------------------------
// Helper: cria 1 cuboide colorido entre corner1 e corner2.
// Garante corner1 < corner2 em cada eixo (evita fCuboid degenerar).
// ---------------------------------------------------------------------------
function addBox(context is Context, id is Id, c1 is Vector, c2 is Vector, col is Color)
{
    // Reordena para garantir corner1 < corner2 em cada componente.
    const lo = vector(
        min(c1[0], c2[0]),
        min(c1[1], c2[1]),
        min(c1[2], c2[2])
    );
    const hi = vector(
        max(c1[0], c2[0]),
        max(c1[1], c2[1]),
        max(c1[2], c2[2])
    );
    fCuboid(context, id, { "corner1" : lo, "corner2" : hi });
    setProperty(context, {
        "entities"     : qCreatedBy(id, EntityType.BODY),
        "propertyType" : PropertyType.APPEARANCE,
        "value"        : col
    });
}

// ---------------------------------------------------------------------------
// Constantes de perfil (portadas de CONSTRUCTION_DEFAULTS e mkBR do model.js).
// Todas em MILÍMETROS.
// ---------------------------------------------------------------------------

// HB (3/4" = 19.05 mm)
const HB_MT        = 19.05;   // materialThickness (mm)
const HB_GAP       = 3.175;   // door-to-cabinet gap (mm)
const HB_VGAP      = 3.175;   // verticalGap entre portas duplas (mm)
const HB_REV_TOP   = 1.5875;  // reveal top (mm)
const HB_REV_BOT   = 0.0;     // reveal bottom (mm)
const HB_REV_LEFT  = 1.5875;  // reveal left (mm)
const HB_REV_RIGHT = 1.5875;  // reveal right (mm)
const HB_INSET_REV = 3.175;   // insetReveal (mm)
const HB_TK_SET    = 63.5;    // toeKick setback (mm)

// BR (folgas 2 mm; espessura varia)
const BR_GAP       = 2.0;
const BR_VGAP      = 2.0;
const BR_REV       = 2.0;     // reveals uniformes
const BR_INSET_REV = 2.0;
const BR_TK_SET    = 50.0;    // toeKick setback BR (mm)

// ---------------------------------------------------------------------------
// Enums de entrada
// ---------------------------------------------------------------------------
enum KCProfile { HB, BR15, BR18, BR20 }
enum KCFront   { SOBREPOSTA, EMBUTIDA }
enum KCZoneType { PORTA, NICHO }
enum KCMao     { ESQ, DIR }
enum KCDoors   { ONE, TWO }

// ---------------------------------------------------------------------------
// Cores (L3 inline _L3_MAT)
// ---------------------------------------------------------------------------
const COL_CARCASS = color(0.831, 0.706, 0.541);   // carcaça, prateleira, painel, divisória
const COL_DOOR    = color(0.757, 0.604, 0.420);   // porta

// ---------------------------------------------------------------------------
// Estrutura interna que carrega o spec resolvido (equivale ao objeto `s` do model.js).
// FeatureScript não tem structs; usamos um map.
// ---------------------------------------------------------------------------

// Retorna o map do spec resolvido a partir dos inputs de UI.
// Equivale a resolve() do model.js, mas recebe valores já em mm (UI).
function resolveSpec(
    W is number, H is number, D is number,
    profile is KCProfile,
    frontInset is boolean,
    tkHeight is number  // em mm
) returns map
{
    var mt = HB_MT;
    var gap = HB_GAP;
    var vgap = HB_VGAP;
    var revTop = HB_REV_TOP; var revBot = HB_REV_BOT;
    var revLeft = HB_REV_LEFT; var revRight = HB_REV_RIGHT;
    var insetRev = HB_INSET_REV;
    var tkSet = HB_TK_SET;

    if (profile == KCProfile.BR15) { mt = 15.0; gap = BR_GAP; vgap = BR_VGAP; revTop = BR_REV; revBot = BR_REV; revLeft = BR_REV; revRight = BR_REV; insetRev = BR_INSET_REV; tkSet = BR_TK_SET; }
    if (profile == KCProfile.BR18) { mt = 18.0; gap = BR_GAP; vgap = BR_VGAP; revTop = BR_REV; revBot = BR_REV; revLeft = BR_REV; revRight = BR_REV; insetRev = BR_INSET_REV; tkSet = BR_TK_SET; }
    if (profile == KCProfile.BR20) { mt = 20.0; gap = BR_GAP; vgap = BR_VGAP; revTop = BR_REV; revBot = BR_REV; revLeft = BR_REV; revRight = BR_REV; insetRev = BR_INSET_REV; tkSet = BR_TK_SET; }

    return {
        "W" : W, "H" : H, "D" : D,
        "mt" : mt,
        "tkHeight" : tkHeight,
        "tkSetback" : tkSet,
        "frontInset" : frontInset,
        "frontGap" : gap,
        "vertGap" : vgap,
        "revTop" : revTop, "revBot" : revBot,
        "revLeft" : revLeft, "revRight" : revRight,
        "insetReveal" : insetRev
    };
}

// ---------------------------------------------------------------------------
// frontGeom — portada de frontGeom() do model.js.
// Retorna { ft, lo, ro, to, bo, cy } em mm.
// ov = map opcional com campos "to" e/ou "bo" para sobrepor o cálculo padrão.
// ---------------------------------------------------------------------------
function frontGeom(s is map, ov is map) returns map
{
    const ft = s.mt;  // front thickness = materialThickness (null => mt no model.js)

    if (s.frontInset)
    {
        const ir = s.insetReveal;
        const toVal = has(ov, "to") ? ov["to"] : -ir;
        const boVal = has(ov, "bo") ? ov["bo"] : -ir;
        return { "ft" : ft, "lo" : -ir, "ro" : -ir, "to" : toVal, "bo" : boVal, "cy" : ft / 2.0 };
    }

    // overlay
    const toVal = has(ov, "to") ? ov["to"] : (ft - s.revTop);
    const boVal = has(ov, "bo") ? ov["bo"] : (ft - s.revBot);
    return {
        "ft" : ft,
        "lo" : ft - s.revLeft,
        "ro" : ft - s.revRight,
        "to" : toVal,
        "bo" : boVal,
        "cy" : -(s.frontGap + ft / 2.0)
    };
}

// ---------------------------------------------------------------------------
// carcass — portada de carcass() do model.js.
// Retorna array de maps { name, dims:[dx,dy,dz], pos:[cx,cy,cz] } em mm.
// ---------------------------------------------------------------------------
function buildCarcass(s is map) returns array
{
    const W = s.W; const H = s.H; const D = s.D; const mt = s.mt;
    const tkh = s.tkHeight; const tks = s.tkSetback;
    const innerLen = W - mt * 2.0;       // dim_x do Bottom/Top/Back
    const backH    = H - tkh - mt;       // altura do Back

    var parts = [
        { "name" : "Left Side",  "dims" : [mt, D, H],            "pos" : [mt / 2.0,       D / 2.0,            H / 2.0] },
        { "name" : "Right Side", "dims" : [mt, D, H],            "pos" : [W - mt / 2.0,   D / 2.0,            H / 2.0] },
        { "name" : "Bottom",     "dims" : [innerLen, D, mt],     "pos" : [W / 2.0,         D / 2.0,            tkh + mt / 2.0] },
        { "name" : "Top",        "dims" : [innerLen, D, mt],     "pos" : [W / 2.0,         D / 2.0,            H - mt / 2.0] },
        { "name" : "Back",       "dims" : [innerLen, mt, backH], "pos" : [W / 2.0,         D - mt / 2.0,       tkh + mt + backH / 2.0] }
    ];

    if (tkh > 0.0)
        parts = append(parts, { "name" : "Toe Kick", "dims" : [innerLen, mt, tkh], "pos" : [W / 2.0, D - tks - mt / 2.0, tkh / 2.0] });

    return parts;
}

// ---------------------------------------------------------------------------
// doorsInOpening — portada de doorsInOpening() do model.js.
// 2 portas cobrindo a abertura completa da zona.
// z0 = base Z da abertura, h = altura da abertura (em mm).
// ---------------------------------------------------------------------------
function doorsInOpening(s is map, h is number, z0 is number, ov is map) returns array
{
    const W = s.W; const mt = s.mt;
    const fg = frontGeom(s, ov);
    const ft = fg["ft"]; const lo = fg["lo"]; const ro = fg["ro"];
    const toV = fg["to"]; const bo = fg["bo"]; const cy = fg["cy"];
    const vg = s.vertGap;
    const Wop = W - 2.0 * mt;
    const x0 = mt;
    const doorW = (Wop + lo + ro - vg) / 2.0;
    const doorH = h + toV + bo;
    const cz    = z0 - bo + doorH / 2.0;
    const leftCx  = (x0 - lo) + doorW / 2.0;
    const rightCx = (W - mt + ro) - doorW / 2.0;

    // Puxadores omitidos (GERAR_PULLS = false).
    return [
        { "name" : "Left Door",  "dims" : [doorW, ft, doorH], "pos" : [leftCx,  cy, cz] },
        { "name" : "Right Door", "dims" : [doorW, ft, doorH], "pos" : [rightCx, cy, cz] }
    ];
}

// ---------------------------------------------------------------------------
// doorSingleZone — portada de doorSingleZone() do model.js.
// 1 porta cobrindo a zona inteira; mao = ESQ|DIR (dobradica).
// ---------------------------------------------------------------------------
function doorSingleZone(s is map, h is number, z0 is number, mao is KCMao, ov is map) returns array
{
    const W = s.W; const mt = s.mt;
    const fg = frontGeom(s, ov);
    const ft = fg["ft"]; const lo = fg["lo"]; const ro = fg["ro"];
    const toV = fg["to"]; const bo = fg["bo"]; const cy = fg["cy"];
    const Wop = W - 2.0 * mt; const x0 = mt;
    const doorW = Wop + lo + ro;
    const doorH = h + toV + bo;
    const cx = (x0 - lo) + doorW / 2.0;
    const cz = z0 - bo + doorH / 2.0;

    // Puxador omitido (GERAR_PULLS = false).
    return [
        { "name" : "Door", "dims" : [doorW, ft, doorH], "pos" : [cx, cy, cz] }
    ];
}

// ---------------------------------------------------------------------------
// shelvesParts — portada de shelvesParts() do model.js.
// qty prateleiras divididas uniformemente na abertura (z0, h em mm).
// ---------------------------------------------------------------------------
function buildShelves(s is map, qty is number, z0 is number, h is number) returns array
{
    if (qty < 1) return [];

    const W = s.W; const D = s.D; const mt = s.mt;
    const folga   = 3.175;   // 1/8" clip gap (mm)
    const setback = 6.35;    // 1/4" shelf setback (mm)
    const sx = W - 2.0 * mt - folga * 2.0;   // largura da prateleira
    const sy = D - mt - setback;               // profundidade (encosta no Back, recua setback da frente)
    const spacing = (h - mt * qty) / (qty + 1.0);

    var parts = [];
    for (var i = 0; i < qty; i += 1)
    {
        const cz = z0 + spacing + mt / 2.0 + (spacing + mt) * i;
        const cx = W / 2.0;
        const cy = setback + sy / 2.0;
        parts = append(parts, { "name" : "Shelf " ~ toString(i + 1), "dims" : [sx, sy, mt], "pos" : [cx, cy, cz] });
    }
    return parts;
}

// ---------------------------------------------------------------------------
// ovEdge — calcula o overlay de borda (topo ou base) para uma fronteira dada.
// Portado da lógica de seamKind/ovEdge de zonesToParts() do model.js.
// kind: "panel" | "divisoria"
// isTop: true = borda superior, false = borda inferior
// ---------------------------------------------------------------------------
function ovEdge(s is map, kind is string, isTop is boolean) returns number
{
    const mt = s.mt; const ir = s.insetReveal; const inset = s.frontInset;
    const half = s.vertGap / 2.0;

    if (kind == "panel")
        return inset
            ? -(mt + ir)
            : (isTop ? -(s.revTop) : -(s.revBot));

    // "divisoria" (entre zonas distintas)
    return inset ? -(mt / 2.0 + ir) : -half;
}

// ---------------------------------------------------------------------------
// slotFront — portada de slotFront() do model.js para tipos porta/nicho.
// zoneType: PORTA | NICHO
// portas: KCDoors.ONE | KCDoors.TWO
// mao: KCMao.ESQ | KCMao.DIR  (só para porta única)
// numShelves: número de prateleiras (para nicho)
// h = altura da zona (mm); z0 = base Z (mm)
// ov = map { "to", "bo" } com overlays já calculados
// ---------------------------------------------------------------------------
function slotFront(s is map, zoneType is KCZoneType, portas is KCDoors, mao is KCMao, numShelves is number, h is number, z0 is number, ov is map) returns array
{
    if (zoneType == KCZoneType.NICHO)
        return buildShelves(s, numShelves, z0, h);

    // PORTA
    var doorParts = [];
    if (portas == KCDoors.ONE)
        doorParts = doorSingleZone(s, h, z0, mao, ov);
    else
        doorParts = doorsInOpening(s, h, z0, ov);

    // Prateleiras internas (porta com nicho)
    const shelfParts = buildShelves(s, numShelves, z0, h);
    return concatenateArrays([doorParts, shelfParts]);
}

// ---------------------------------------------------------------------------
// divisoriaPart — peça de divisória entre zonas.
// Portada de: parts.push({ name: `Divisoria ${i+1}`, ... }) em zonesToParts().
// ---------------------------------------------------------------------------
function divisoriaPart(s is map, z0cz is number, idx is number) returns map
{
    const W = s.W; const D = s.D; const mt = s.mt;
    return { "name" : "Divisoria " ~ toString(idx), "dims" : [W - 2.0 * mt, D - mt, mt], "pos" : [W / 2.0, (D - mt) / 2.0, z0cz] };
}

// ---------------------------------------------------------------------------
// emit — converte um array de parts (maps) em geometria Onshape.
// col: cor a usar (carcass ou door).
// ---------------------------------------------------------------------------
function emitPart(context is Context, id is Id, p is map, col is Color)
{
    const dx = p["dims"][0]; const dy = p["dims"][1]; const dz = p["dims"][2];
    const cx = p["pos"][0];  const cy = p["pos"][1];  const cz = p["pos"][2];
    const mm = millimeter;
    const c1 = vector(cx - dx / 2.0, cy - dy / 2.0, cz - dz / 2.0) * mm;
    const c2 = vector(cx + dx / 2.0, cy + dy / 2.0, cz + dz / 2.0) * mm;
    addBox(context, id, c1, c2, col);
}

// ---------------------------------------------------------------------------
// Feature principal
// ---------------------------------------------------------------------------
annotation { "Feature Type Name" : "Kitchen Cabinet" }
export const kitchenCabinet = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        // --- Dimensões gerais ---
        annotation { "Name" : "Largura" }
        isLength(definition.largura, { (millimeter) : [100.0, 914.4, 3000.0] } as LengthBoundSpec);

        annotation { "Name" : "Altura" }
        isLength(definition.altura, { (millimeter) : [100.0, 876.3, 3000.0] } as LengthBoundSpec);

        annotation { "Name" : "Profundidade" }
        isLength(definition.profundidade, { (millimeter) : [100.0, 587.37, 1200.0] } as LengthBoundSpec);

        // --- Perfil de material ---
        annotation { "Name" : "Perfil", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        definition.perfil is KCProfile;

        // --- Tipo de frente ---
        annotation { "Name" : "Frente", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        definition.frente is KCFront;

        // --- Rodapé ---
        annotation { "Name" : "Altura do Rodapé" }
        isLength(definition.rodapeAltura, { (millimeter) : [0.0, 100.0, 300.0] } as LengthBoundSpec);

        // --- Zonas (array de itens) ---
        annotation { "Name" : "Zonas", "Item name" : "Zona", "UIHint" : UIHint.PREVENT_ARRAY_REORDER }
        definition.zonas is array;
        for (var zona in definition.zonas)
        {
            annotation { "Name" : "Tipo" }
            zona.tipo is KCZoneType;

            annotation { "Name" : "Altura automática (preenche o resto)" }
            zona.auto is boolean;

            annotation { "Name" : "Altura da zona", "UIHint" : UIHint.SHOW_EXPRESSION }
            isLength(zona.zonaAltura, { (millimeter) : [1.0, 400.0, 3000.0] } as LengthBoundSpec);

            annotation { "Name" : "Portas" }
            zona.portas is KCDoors;

            annotation { "Name" : "Mão da porta única" }
            zona.mao is KCMao;

            annotation { "Name" : "Prateleiras" }
            isInteger(zona.prateleiras, { (unitless) : [0, 0, 20] } as IntegerBoundSpec);
        }
    }
    {
        // -----------------------------------------------------------------------
        // 1. Converter inputs de UI para números em mm
        // -----------------------------------------------------------------------
        const W = definition.largura / millimeter;
        const H = definition.altura  / millimeter;
        const D = definition.profundidade / millimeter;
        const tkHeight = definition.rodapeAltura / millimeter;
        const frontInset = (definition.frente == KCFront.EMBUTIDA);

        // -----------------------------------------------------------------------
        // 2. Resolve spec (equivale a resolve() do model.js)
        // -----------------------------------------------------------------------
        const s = resolveSpec(W, H, D, definition.perfil, frontInset, tkHeight);

        // -----------------------------------------------------------------------
        // 3. Carcaça
        // -----------------------------------------------------------------------
        const carcassParts = buildCarcass(s);
        var boxCount = 0;
        for (var p in carcassParts)
        {
            emitPart(context, id + ("c" ~ toString(boxCount)), p, COL_CARCASS);
            boxCount += 1;
        }

        // -----------------------------------------------------------------------
        // 4. Zonas → partes (frentes + prateleiras + divisórias)
        //    Portado de zonesToParts() do model.js.
        // -----------------------------------------------------------------------
        const zonas = definition.zonas;
        const mt = s.mt;

        // corpoH = região útil das zonas (sem rodapé; v1 não tem tampo).
        const corpoH = H - tkHeight;

        if (size(zonas) == 0)
        {
            // DEFAULT: 1 zona PORTA dupla cobrindo corpoH inteiro (spec diz).
            const z0 = tkHeight;
            const h  = corpoH;
            const ov = { "to" : ovEdge(s, "panel", true), "bo" : ovEdge(s, "panel", false) };
            var zoneParts = doorsInOpening(s, h, z0, ov);
            for (var p in zoneParts)
            {
                emitPart(context, id + ("d" ~ toString(boxCount)), p, COL_DOOR);
                boxCount += 1;
            }
        }
        else
        {
            // Calcular altura de cada zona (auto distribui o restante).
            // O espaço total disponível para as zonas = corpoH menos as divisórias entre elas.
            // Número de divisórias = nSlots - 1 (sempre 1 divisória por fronteira interior em v1).
            var fixedSum = 0.0;
            var nAuto = 0;
            const nZonas = size(zonas);
            for (var z in zonas)
            {
                if (z.auto)
                    nAuto += 1;
                else
                    fixedSum += z.zonaAltura / millimeter;
            }
            // Divisórias ocupam mt cada, entre zonas adjacentes (nZonas-1 fronteiras).
            const divSpace = (nZonas > 1) ? (nZonas - 1) * mt : 0.0;
            const autoH = (nAuto > 0) ? (corpoH - fixedSum - divSpace) / nAuto : 0.0;

            // Montar lista de slots (zTop → zBase, topo para base).
            // Cada slot: { zh, zoneIdx } onde zh = altura em mm.
            var slotHeights = [];   // array de numbers (mm)
            var slotZoneRefs = [];  // array de índices em zonas[]
            var zIdx = 0;
            for (var z in zonas)
            {
                const zh = z.auto ? autoH : (z.zonaAltura / millimeter);
                slotHeights   = append(slotHeights,   zh);
                slotZoneRefs  = append(slotZoneRefs,  zIdx);
                zIdx += 1;
            }

            // Emitir cada slot (topo → base).
            // z0 = base do slot; subtrai zh + mt (divisória) a cada fronteira interior.
            // Portado de: zTop = z0 - div (model.js zonesToParts, div=mt entre zonas distintas).
            var slotZ0s = [];
            {
                var zTop = tkHeight + corpoH;
                const nSlotsCalc = size(slotHeights);
                for (var si = 0; si < nSlotsCalc; si += 1)
                {
                    const zhItem = slotHeights[si];
                    const z0slot = zTop - zhItem;
                    slotZ0s = append(slotZ0s, z0slot);
                    // Após o slot, subtrai a divisória se não for o último slot.
                    if (si < nSlotsCalc - 1)
                        zTop = z0slot - mt;  // divisória de mt entre zonas (sempre em v1)
                    else
                        zTop = z0slot;
                }
            }

            // Emitir frentes + divisórias.
            const nSlots = size(slotHeights);
            var divIdx = 1;   // contador de divisórias (para nome "Divisoria N")

            for (var i = 0; i < nSlots; i += 1)
            {
                const zh   = slotHeights[i];
                const z0   = slotZ0s[i];
                const zona = zonas[slotZoneRefs[i]];

                // Calcular seamKind nas bordas superior e inferior.
                // panel = borda externa (i=0 → topo; i=nSlots-1 → base).
                // divisoria = fronteira entre zonas de tipos distintos.
                var kindTop = "panel";
                var kindBot = "panel";

                if (i > 0)
                {
                    // Qualquer fronteira interior → divisória de material (inclui zona mesma tipo).
                    // No model.js v1 cada zona é independente — sem gavetas empilhadas, não existe "stack".
                    kindTop = "divisoria";
                }
                if (i < nSlots - 1)
                {
                    kindBot = "divisoria";
                }

                // Recalcular z0 real levando em conta divisórias na borda superior.
                // No model.js o divisória ocupa mt na fronteira INFERIOR do slot de cima.
                // O z0 da zona já está correto pois não subtraímos divisórias no loop acima.
                // A divisória é emitida na fronteira inferior do slot (z0 do slot atual = topo da divisória).

                // Overlays da frente para este slot.
                var ovT = ovEdge(s, kindTop == "none" ? "panel" : kindTop, true);
                var ovB = ovEdge(s, kindBot == "none" ? "panel" : kindBot, false);

                // Para nicho: borda interna sem overlay de frente (sem porta).
                // Para porta: overlay conforme o seamKind.
                const ov = { "to" : ovT, "bo" : ovB };

                // Frentes + prateleiras do slot.
                const isNicho = (zona.tipo == KCZoneType.NICHO);
                const numShelves = zona.prateleiras;  // integer declarado no precondition
                const portasEnum = zona.portas;
                const maoEnum    = zona.mao;

                var zoneParts = slotFront(s, zona.tipo, portasEnum, maoEnum, numShelves, zh, z0, ov);

                for (var p in zoneParts)
                {
                    const isPorta = (p["name"] == "Door" || p["name"] == "Left Door" || p["name"] == "Right Door");
                    const col = (isPorta && !isNicho) ? COL_DOOR : COL_CARCASS;
                    emitPart(context, id + ("z" ~ toString(boxCount)), p, col);
                    boxCount += 1;
                }

                // Divisória na fronteira INFERIOR deste slot (se necessário).
                // A divisória ocupa a posição z0 (base do slot atual = topo da próxima zona).
                if (kindBot == "divisoria" && i < nSlots - 1)
                {
                    // z0 é a BASE do slot atual = z do centro da divisória (mt/2 abaixo de z0).
                    // No model.js: pos.cz = sl.z0 (base do slot = centro da divisória).
                    const div = divisoriaPart(s, z0, divIdx);
                    emitPart(context, id + ("div" ~ toString(boxCount)), div, COL_CARCASS);
                    boxCount += 1;
                    divIdx += 1;
                }
            }
        }
    });
