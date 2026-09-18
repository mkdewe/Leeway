#!/usr/bin/env node
// Generator diagramów architektury Leeway (Mermaid).
//
// Świadomość Unity polega tu na trzech konkretnych regułach:
//   1. Krawędź powstaje tylko wtedy, gdy OBA jej końce są typem z Assets/_Project.
//      UnityEngine / FishNet / VContainer / MessagePipe są granicą, nie węzłem.
//   2. Dziedziczenie po typie silnika nie jest krawędzią, tylko stereotypem
//      węzła (<<NetworkBehaviour>>, <<MonoBehaviour>>, <<ScriptableObject>>).
//   3. Metody cyklu życia i składowe generowane przez codegen FishNet są szumem
//      i wypadają z diagramu.
//
// Uruchomienie:  node Tools/DiagramGen/gen.mjs

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { stripNoise, parseTypes, splitMembers, parseMember } from './parse.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, '..', '..');
const CFG = JSON.parse(fs.readFileSync(path.join(HERE, 'diagram.config.json'), 'utf8'));
const MAX_MEMBERS = 14;

const SRC = path.join(ROOT, CFG.sourceRoot);
const OUT = path.join(ROOT, CFG.outDir);

// ─────────────────────────────────────────────────────────── zbieranie plików

function walk(dir, acc = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, acc);
    else if (e.name.endsWith('.cs')) acc.push(p);
  }
  return acc;
}

const rel = p => path.relative(ROOT, p).split(path.sep).join('/');
const files = walk(SRC).filter(p => !CFG.excludePathParts.some(x => rel(p).includes(x)));

// ────────────────────────────────────────────── przypisanie plików do assembly

const asmdefCache = new Map();
function assemblyOf(filePath) {
  let dir = path.dirname(filePath);
  while (dir.startsWith(SRC) || dir === SRC) {
    if (!asmdefCache.has(dir)) {
      const hit = fs.readdirSync(dir).find(f => f.endsWith('.asmdef'));
      asmdefCache.set(dir, hit ? JSON.parse(fs.readFileSync(path.join(dir, hit), 'utf8')) : null);
    }
    const found = asmdefCache.get(dir);
    if (found) return found;
    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null; // → Assembly-CSharp (predefiniowana assembly Unity)
}

// ───────────────────────────────────────────────────────────── budowa modelu

const types = [];
for (const file of files) {
  const clean = stripNoise(fs.readFileSync(file, 'utf8'));
  const asm = assemblyOf(file);
  for (const t of parseTypes(clean, rel(file))) {
    t.assembly = asm ? asm.name : 'Assembly-CSharp';
    t.assemblyRefs = asm ? (asm.references || []) : [];
    types.push(t);
  }
}

const byName = new Map();
for (const t of types) if (!byName.has(t.name)) byName.set(t.name, t);
const ownNames = new Set(byName.keys());

// Stereotyp: przynależność do silnika czytamy z klasy bazowej, ale NIE tworzymy
// z niej krawędzi — inaczej diagram wsiąkłby w hierarchię UnityEngine.
function stereotypeOf(t) {
  for (const b of t.bases) {
    const head = b.replace(/<.*$/s, '').split('.').pop();
    if (CFG.stereotypeByBase[head]) return CFG.stereotypeByBase[head];
  }
  if (t.kind === 'interface') return 'interface';
  if (t.kind === 'enum') return 'enum';
  if (t.isStatic) return 'static';
  if (/Message$/.test(t.name)) return 'message';
  if (t.filePath.includes('/Shared/Domain/')) return 'domain';
  if (t.kind === 'struct' || t.kind === 'record') return t.kind;
  return null;
}

const isNoise = name =>
  CFG.unityLifecycle.includes(name) ||
  CFG.fishnetLifecycle.includes(name) ||
  CFG.boilerplate.includes(name) ||
  CFG.codegenPrefixes.some(p => name.startsWith(p));

function unwrapSync(typeStr) {
  const m = /^(\w+)\s*<(.+)>$/s.exec(typeStr.trim());
  if (m && CFG.syncWrappers.includes(m[1])) return { type: m[2].trim(), sync: true };
  if (CFG.syncWrappers.includes(typeStr.trim())) return { type: typeStr.trim(), sync: true };
  return { type: typeStr.trim(), sync: false };
}

// Stereotyp propagujemy w dół po własnych klasach bazowych: PlayerCreatureController
// dziedziczy po CreatureEntity, więc też jest NetworkBehaviour — tyle że tę
// informację gubiłby diagram, bo krawędź prowadzi do typu z projektu, nie do FishNet.
function resolveStereotype(t, seen = new Set()) {
  const direct = stereotypeOf(t);
  if (direct && direct !== "domain" && direct !== "struct" && direct !== "record") return direct;
  if (seen.has(t.name)) return direct;
  seen.add(t.name);
  for (const b of t.bases) {
    const head = b.replace(/<.*$/s, "").split(".").pop();
    const owner = byName.get(head);
    if (!owner || owner.kind === "interface") continue;
    const inherited = resolveStereotype(owner, seen);
    if (inherited) return inherited;
  }
  return direct;
}

for (const t of types) {
  t.stereotype = resolveStereotype(t);
  t.members = [];
  if (!t.body) continue;

  for (const chunk of splitMembers(t.body)) {
    const m = parseMember(chunk, t.name);
    if (!m || m.kind === 'ctor') continue;
    if (isNoise(m.name)) continue;

    const { type: bare, sync } = unwrapSync(m.type);
    m.type = bare;

    const markers = m.attrs
      .map(a => CFG.memberMarkerAttributes[a.split('.').pop()])
      .filter(Boolean);
    if (sync) markers.unshift('Sync');
    m.markers = [...new Set(markers)];

    // Kontrakt publiczny: wpuszczamy też prywatne pola z [SerializeField] i SyncVar,
    // bo w Unity to one są realnym interfejsem klasy — jedno wpina się w inspektorze,
    // drugie replikuje po sieci.
    const visible = m.visibility === 'public' || m.visibility === 'protected';
    // Każdy marker Unity/FishNet oznacza składową, która JEST kontraktem, choć bywa
    // prywatna: [Replicate]/[Reconcile] to granica predykcji, [ServerRpc] granica
    // autorytetu, [SerializeField] wpięcie z inspektora.
    const unityWiring = m.markers.length > 0;
    if (!visible && !unityWiring) continue;

    t.members.push(m);
  }
}

// ──────────────────────────────────────────────────────────────── krawędzie

const RANK = { extends: 3, implements: 3, serialized: 2, field: 1, uses: 0 };
const edges = new Map();

function addEdge(from, to, kind) {
  if (from === to || !ownNames.has(to)) return;
  const key = `${from}|${to}`;
  const prev = edges.get(key);
  if (!prev || RANK[kind] > RANK[prev.kind]) edges.set(key, { from, to, kind });
}

const identsIn = s => new Set(s.match(/[A-Za-z_]\w*/g) || []);

for (const t of types) {
  for (const b of t.bases) {
    const head = b.replace(/<.*$/s, '').split('.').pop();
    if (!ownNames.has(head)) continue;
    addEdge(t.name, head, byName.get(head).kind === 'interface' ? 'implements' : 'extends');
  }
  for (const m of t.members) {
    for (const id of identsIn(`${m.type} ${m.params || ''}`)) {
      addEdge(t.name, id, m.markers.includes('SF') ? 'serialized' : 'field');
    }
  }
  for (const id of identsIn(t.body || '')) addEdge(t.name, id, 'uses');
}

// ────────────────────────────────────────────────────────── grupy / diagramy

function groupKey(filePath) {
  return path.posix.dirname(filePath).replace(`${CFG.sourceRoot}/`, '');
}
const groups = new Map();
for (const t of types) {
  const k = groupKey(t.filePath);
  if (!groups.has(k)) groups.set(k, []);
  groups.get(k).push(t);
}
// Katalogi z garstką typów wciągamy do rodzica — inaczej powstaje wysyp
// jednoelementowych diagramów (np. GamePhases/*).
let merged = true;
while (merged) {
  merged = false;
  for (const [k, list] of [...groups]) {
    if (list.length >= CFG.minTypesPerGroup || !k.includes('/')) continue;
    const parent = k.slice(0, k.lastIndexOf('/'));
    // Nie wciągamy wyżej niż do samego feature'a — inaczej Camera/Network/UI
    // wpadłyby do jednego worka "Features", który niczego nie nazywa.
    if (!parent.includes('/')) continue;
    groups.set(parent, [...(groups.get(parent) || []), ...list]);
    groups.delete(k);
    merged = true;
  }
}
const prettyGroup = k => k.replace(/\/Scripts(\/|$)/, '$1').replace(/\/$/, '');
const groupOf = new Map();
for (const [k, list] of groups) for (const t of list) groupOf.set(t.name, k);

// ─────────────────────────────────────────────────────────────── MessagePipe

const messages = new Map(); // nazwa → { publishers, subscribers, registered }
const touch = n => {
  if (!messages.has(n)) messages.set(n, { publishers: new Set(), subscribers: new Set(), registered: false });
  return messages.get(n);
};
for (const t of types) {
  for (const m of (t.body || '').matchAll(/GetPublisher\s*<\s*([\w.]+)\s*>/g)) touch(m[1].split('.').pop()).publishers.add(t.name);
  for (const m of (t.body || '').matchAll(/GetSubscriber\s*<\s*([\w.]+)\s*>/g)) touch(m[1].split('.').pop()).subscribers.add(t.name);
  for (const m of (t.body || '').matchAll(/AddMessageBroker\s*<\s*([\w.]+)\s*>/g)) touch(m[1].split('.').pop()).registered = true;
}

// ───────────────────────────────────────────────────────────────── renderery

const mm = s => String(s).replace(/</g, '~').replace(/>/g, '~').replace(/"/g, "'");
const paramTypes = p =>
  !p ? '' : p.split(/,(?![^<>\[\]]*[>\]])/)
    .map(x => x.trim().replace(/^(ref|out|in|params|this)\s+/, '').split(/\s+/)[0])
    .filter(Boolean).join(', ');

function renderMember(m) {
  const prefix = m.markers.length ? m.markers.map(x => `[${x}]`).join(' ') + ' ' : '';
  const sym = m.visibility === 'public' ? '+' : m.visibility === 'protected' ? '#' : '';
  const st = m.isStatic ? '$' : '';
  if (m.kind === 'method') return `${prefix}${sym}${mm(m.name)}(${mm(paramTypes(m.params))}) ${mm(m.type)}${st}`;
  if (m.kind === 'event') return `${prefix}${sym}event ${mm(m.name)}`;
  return `${prefix}${sym}${mm(m.type)} ${mm(m.name)}${st}`;
}

const ORDER = { field: 0, property: 1, event: 2, method: 3 };

function renderClassBlock(t, withMembers) {
  const lines = [`    class ${t.name} {`];
  if (t.stereotype) lines.push(`        <<${t.stereotype}>>`);
  if (withMembers) {
    const sorted = [...t.members].sort((a, b) => ORDER[a.kind] - ORDER[b.kind]);
    for (const m of sorted.slice(0, MAX_MEMBERS)) lines.push(`        ${renderMember(m)}`);
    if (sorted.length > MAX_MEMBERS) lines.push(`        +… i ${sorted.length - MAX_MEMBERS} dalszych`);
  }
  lines.push('    }');
  return lines.join('\n');
}

const ARROW = {
  extends: '<|--',
  implements: '<|..',
  serialized: '-->',
  field: '-->',
  uses: '..>',
};
const EDGE_LABEL = { serialized: ' : inspektor', field: '', uses: '', extends: '', implements: '' };

function renderGroupDiagram(key, list) {
  const inGroup = new Set(list.map(t => t.name));
  // Wychodzące pokazujemy zawsze (od czego ten obszar zależy). Przychodzące tylko
  // gdy to dziedziczenie — reszta "kto nas używa" należy do diagramu tamtego obszaru
  // i tutaj rozdmuchuje graf o kilkanaście pustych pudełek.
  const relevant = [...edges.values()].filter(e => {
    if (inGroup.has(e.from)) return true;
    if (!inGroup.has(e.to)) return false;
    return e.kind === "extends" || e.kind === "implements";
  });
  const foreign = new Set();
  for (const e of relevant) {
    if (!inGroup.has(e.from)) foreign.add(e.from);
    if (!inGroup.has(e.to)) foreign.add(e.to);
  }

  const out = ['```mermaid', 'classDiagram', '    direction LR'];
  for (const t of [...list].sort((a, b) => a.name.localeCompare(b.name))) out.push(renderClassBlock(t, true));
  for (const name of [...foreign].sort()) out.push(renderClassBlock(byName.get(name), false));
  out.push('');
  for (const e of relevant.sort((a, b) => `${a.from}${a.to}`.localeCompare(`${b.from}${b.to}`))) {
    const a = e.kind === 'extends' || e.kind === 'implements'
      ? `    ${e.to} ${ARROW[e.kind]} ${e.from}`
      : `    ${e.from} ${ARROW[e.kind]} ${e.to}`;
    out.push(a + EDGE_LABEL[e.kind]);
  }
  out.push('```');
  return { body: out.join('\n'), foreign };
}

// ──────────────────────────────────────────────────────────────────── zapis

fs.mkdirSync(OUT, { recursive: true });

// Zmiana reguł grupowania zmienia zestaw plików. Bez sprzątania zostają sieroty
// po poprzednim przebiegu — kasujemy wyłącznie to, co sami wcześniej wypisaliśmy
// (rozpoznajemy po nagłówku), więc ręczne notatki obok są bezpieczne.
const STAMP_MARK = 'PLIK GENEROWANY';
for (const f of fs.readdirSync(OUT)) {
  const full = path.join(OUT, f);
  if (!fs.statSync(full).isFile()) continue;
  if (!/\.(md|html|json|graphml)$/.test(f)) continue;
  if (f === 'model.json' || f === 'leeway.graphml') { fs.unlinkSync(full); continue; }
  const head = fs.readFileSync(full, 'utf8').slice(0, 200);
  if (head.includes(STAMP_MARK)) fs.unlinkSync(full);
}
const stamp = () =>
  `<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->\n`;

const slug = k => k.replace(/[^\w]+/g, '-').replace(/^-|-$/g, '').toLowerCase();
const written = [];
function write(name, content) {
  fs.writeFileSync(path.join(OUT, name), content);
  written.push(name);
}

// 1. Graf assembly ------------------------------------------------------------
{
  const asmRefs = new Map();
  const own = new Set(types.map(t => t.assembly));
  for (const t of types) {
    if (!asmRefs.has(t.assembly)) asmRefs.set(t.assembly, new Set());
    for (const r of t.assemblyRefs) asmRefs.get(t.assembly).add(r);
  }
  // Assembly-CSharp nie ma asmdefa, więc jej zależności wyliczamy z użycia typów.
  for (const e of edges.values()) {
    const a = byName.get(e.from)?.assembly, b = byName.get(e.to)?.assembly;
    if (a && b && a !== b) {
      if (!asmRefs.has(a)) asmRefs.set(a, new Set());
      asmRefs.get(a).add(b);
    }
  }

  const lines = ['```mermaid', 'flowchart LR'];
  const id = n => n.replace(/[^\w]/g, '_');
  const externals = new Set();
  for (const [a, refs] of asmRefs) for (const r of refs) if (!own.has(r)) externals.add(r);

  lines.push('    subgraph LEEWAY["Assets/_Project"]');
  for (const a of [...own].sort()) lines.push(`        ${id(a)}["${a}"]`);
  lines.push('    end');
  lines.push('    subgraph EXT["Zewnętrzne (granica grafu)"]');
  for (const x of [...externals].sort()) lines.push(`        ${id(x)}["${x}"]`);
  lines.push('    end');
  for (const [a, refs] of [...asmRefs].sort()) {
    for (const r of [...refs].sort()) lines.push(`    ${id(a)} --> ${id(r)}`);
  }
  lines.push('```');

  const counts = [...own].sort().map(a => `| \`${a}\` | ${types.filter(t => t.assembly === a).length} |`).join('\n');
  write('01-assemblies.md', `${stamp()}
# Graf assembly (asmdef)

Najgrubsza granica w projekcie. Assembly to jedyny poziom, na którym Unity
faktycznie *wymusza* kierunek zależności — czego tu nie ma, tego kod nie
skompiluje. Dlatego ten diagram jest kontraktem, a nie opisem.

${lines.join('\n')}

| Assembly | Typów |
|---|---|
${counts}
`);
}

// 2. Graf komunikatów ---------------------------------------------------------
{
  const lines = ['```mermaid', 'flowchart LR'];
  const id = n => n.replace(/[^\w]/g, '_');
  for (const [name, info] of [...messages].sort()) {
    lines.push(`    ${id(name)}(["${name}"])`);
    for (const p of [...info.publishers].sort()) lines.push(`    ${id(p)}["${p}"] -->|publish| ${id(name)}`);
    for (const s of [...info.subscribers].sort()) lines.push(`    ${id(name)} -->|subscribe| ${id(s)}["${s}"]`);
  }
  lines.push('```');

  const rows = [...messages].sort().map(([n, i]) => {
    const pub = [...i.publishers].join(', ') || '—';
    const sub = [...i.subscribers].join(', ') || '—';
    return `| \`${n}\` | ${i.registered ? 'tak' : '**NIE**'} | ${pub} | ${sub} |`;
  }).join('\n');

  const orphanUse = [...messages].filter(([, i]) => !i.registered && (i.publishers.size || i.subscribers.size)).map(([n]) => n);
  const orphanReg = [...messages].filter(([, i]) => i.registered && !i.publishers.size && !i.subscribers.size).map(([n]) => n);

  let warn = '';
  if (orphanUse.length) warn += `\n> **Używane, ale niezarejestrowane w \`LeewayMessageBrokers\`:** ${orphanUse.map(n => `\`${n}\``).join(', ')}.\n> Pierwsze \`GetPublisher\`/\`GetSubscriber\` rzuci wyjątkiem w runtime.\n`;
  if (orphanReg.length) warn += `\n> **Zarejestrowane, ale nigdzie nieużywane:** ${orphanReg.map(n => `\`${n}\``).join(', ')}.\n`;
  if (!warn) warn = '\n> Każdy komunikat ma rejestrację, nadawcę i odbiorcę.\n';

  write('02-messages.md', `${stamp()}
# Przepływ komunikatów (MessagePipe)

Ta zależność **nie jest widoczna w kodzie ani w grafie assembly** — nadawca i
odbiorca nie znają się nawzajem, łączy ich wyłącznie typ komunikatu. Bez tego
diagramu jest niewidzialna.

${lines.join('\n')}

| Komunikat | Zarejestrowany | Publikuje | Subskrybuje |
|---|---|---|---|
${rows}
${warn}`);
}

// 3. Przegląd grup ------------------------------------------------------------
{
  const gEdges = new Map();
  for (const e of edges.values()) {
    const a = groupOf.get(e.from), b = groupOf.get(e.to);
    if (!a || !b || a === b) continue;
    const k = `${a}|${b}`;
    gEdges.set(k, (gEdges.get(k) || 0) + 1);
  }
  const id = n => 'g_' + n.replace(/[^\w]/g, '_');
  const lines = ['```mermaid', 'flowchart TD'];
  for (const k of [...groups.keys()].sort()) lines.push(`    ${id(k)}["${prettyGroup(k)}<br/>${groups.get(k).length} typów"]`);
  for (const [k, n] of [...gEdges].sort()) {
    const [a, b] = k.split('|');
    lines.push(`    ${id(a)} -->|${n}| ${id(b)}`);
  }
  lines.push('```');

  const index = [...groups.keys()].sort()
    .map(k => `- [${prettyGroup(k)}](./klasy-${slug(k)}.md) — ${groups.get(k).length} typów`).join('\n');

  write('00-overview.md', `${stamp()}
# Przegląd — zależności między obszarami

Węzeł to katalog, liczba na strzałce to ile par typów tworzy tę zależność.
Strzałka w stronę \`Shared/Domain\` jest zdrowa; strzałka **z** \`Shared/Domain\`
w stronę \`Features\` oznaczałaby, że domena zaczęła zależeć od Unity i sieci.

${lines.join('\n')}

## Diagramy klas

${index}
`);
}

// 4. Diagramy klas per obszar -------------------------------------------------
for (const [key, list] of [...groups].sort()) {
  const { body, foreign } = renderGroupDiagram(key, list);
  const legend = foreign.size
    ? `\n**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: ${[...foreign].sort().map(n => `\`${n}\``).join(', ')}.\n`
    : '';
  const filesList = [...new Set(list.map(t => t.filePath))].sort().map(f => `- \`${f}\``).join('\n');

  write(`klasy-${slug(key)}.md`, `${stamp()}
# ${prettyGroup(key)}

${body}
${legend}
<details><summary>Pliki źródłowe (${new Set(list.map(t => t.filePath)).size})</summary>

${filesList}

</details>
`);
}

// 5. Indeks -------------------------------------------------------------------
write('README.md', `${stamp()}
# Architektura Leeway — diagramy

\`\`\`bash
node Tools/DiagramGen/gen.mjs        # przegeneruj wszystko z kodu
node Tools/DiagramGen/validate.mjs   # sprawdź składnię parserem Mermaid
\`\`\`

Walidator wymaga jednorazowego \`cd Tools/DiagramGen && npm install\`.
Ustawienia — poziom szczegółowości i listy pomijanych składowych:
\`Tools/DiagramGen/diagram.config.json\`.

1. [Przegląd zależności między obszarami](./00-overview.md)
2. [Graf assembly (asmdef)](./01-assemblies.md)
3. [Przepływ komunikatów (MessagePipe)](./02-messages.md)
4. Diagramy klas — spis w [przeglądzie](./00-overview.md#diagramy-klas)

## Jak czytać

| Zapis | Znaczenie |
|---|---|
| \`<<NetworkBehaviour>>\` | dziedziczy po typie silnika — hierarchia Unity/FishNet celowo nie jest rozwijana |
| \`[SF]\` | pole \`[SerializeField]\`, czyli wpięte w inspektorze lub prefabie |
| \`[Sync]\` | \`SyncVar\`/\`SyncList\` — stan replikowany przez FishNet |
| \`[Server]\`, \`[ServerRpc]\`, \`[ObserversRpc]\`, \`[TargetRpc]\` | granica autorytetu |
| \`[Replicate]\`, \`[Reconcile]\` | predykcja po stronie klienta (CSP) |
| \`A --> B\` | A trzyma referencję do B (pole/właściwość) |
| \`A ..> B\` | A używa B w implementacji |
| \`A <\\|-- B\` | B dziedziczy po A |

Pokazujemy kontrakt, nie implementację. Ukryte celowo: \`Awake\`/\`Start\`/\`Update\`/\`OnDestroy\`, \`OnStartServer\`/\`OnStartNetwork\`
i pozostały cykl życia, składowe generowane przez codegen FishNet oraz prywatne
metody pomocnicze. Wyjątkiem są składowe prywatne z atrybutem Unity/FishNet
(\`[SerializeField]\`, \`SyncVar\`, \`[Replicate]\`, \`[ServerRpc]\`) — one *są* kontraktem,
tyle że wobec silnika i sieci, nie wobec innych klas C#.
`);

// 6. Model maszynowy — zasila interaktywny eksplorator i eksport do yEd/Gephi.
{
  const model = {
    generatedAt: new Date().toISOString().slice(0, 10),
    sourceRoot: CFG.sourceRoot,
    groups: [...groups.keys()].sort().map(k => ({ key: k, label: prettyGroup(k), count: groups.get(k).length })),
    assemblies: [...new Set(types.map(t => t.assembly))].sort(),
    nodes: [...types]
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(t => ({
        id: t.name,
        kind: t.kind,
        stereotype: t.stereotype,
        group: groupOf.get(t.name),
        assembly: t.assembly,
        file: t.filePath,
        line: t.line,
        members: t.members.map(m => ({
          kind: m.kind,
          text: renderMember(m),
          markers: m.markers,
        })),
      })),
    edges: [...edges.values()]
      .sort((a, b) => `${a.from}${a.to}`.localeCompare(`${b.from}${b.to}`))
      .map(e => ({ from: e.from, to: e.to, kind: e.kind })),
    messages: [...messages].sort().map(([name, i]) => ({
      name,
      registered: i.registered,
      publishers: [...i.publishers].sort(),
      subscribers: [...i.subscribers].sort(),
    })),
  };
  fs.writeFileSync(path.join(OUT, 'model.json'), JSON.stringify(model, null, 2));
  written.push('model.json');

  // GraphML — otwiera się w yEd i Gephi, gdzie graf da się rozłożyć ręcznie
  // i zapisać własny układ, tak jak na tablicy w Miro.
  const xml = v => String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  const gml = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<graphml xmlns="http://graphml.graphdrawing.org/xmlns">',
    '  <key id="label" for="node" attr.name="label" attr.type="string"/>',
    '  <key id="group" for="node" attr.name="group" attr.type="string"/>',
    '  <key id="assembly" for="node" attr.name="assembly" attr.type="string"/>',
    '  <key id="stereotype" for="node" attr.name="stereotype" attr.type="string"/>',
    '  <key id="members" for="node" attr.name="members" attr.type="int"/>',
    '  <key id="kind" for="edge" attr.name="kind" attr.type="string"/>',
    '  <graph id="Leeway" edgedefault="directed">',
  ];
  for (const n of model.nodes) {
    gml.push(`    <node id="${xml(n.id)}">`,
      `      <data key="label">${xml(n.id)}</data>`,
      `      <data key="group">${xml(n.group || '')}</data>`,
      `      <data key="assembly">${xml(n.assembly)}</data>`,
      `      <data key="stereotype">${xml(n.stereotype || '')}</data>`,
      `      <data key="members">${n.members.length}</data>`,
      '    </node>');
  }
  model.edges.forEach((e, idx) => {
    gml.push(`    <edge id="e${idx}" source="${xml(e.from)}" target="${xml(e.to)}">`,
      `      <data key="kind">${xml(e.kind)}</data>`,
      '    </edge>');
  });
  gml.push('  </graph>', '</graphml>');
  fs.writeFileSync(path.join(OUT, 'leeway.graphml'), gml.join('\n'));
  written.push('leeway.graphml');
}

console.log(`Typów: ${types.length}  |  krawędzi: ${edges.size}  |  grup: ${groups.size}  |  komunikatów: ${messages.size}`);
console.log(`Zapisano ${written.length} plików do ${CFG.outDir}/`);
