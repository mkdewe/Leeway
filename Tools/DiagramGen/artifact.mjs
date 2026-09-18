#!/usr/bin/env node
// Składa wszystkie wygenerowane diagramy w jedną samodzielną stronę HTML.
// Uruchomienie:  node Tools/DiagramGen/artifact.mjs [plik-wyjsciowy.html]

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, '..', '..');
const CFG = JSON.parse(fs.readFileSync(path.join(HERE, 'diagram.config.json'), 'utf8'));
const DOCS = path.join(ROOT, CFG.outDir);
const OUTFILE = process.argv[2] || path.join(DOCS, 'atlas.html');

const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const inline = s =>
  esc(s)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/(^|\s)\*([^*]+)\*/g, '$1<em>$2</em>')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, t, u) => `<a href="${u.startsWith('./') ? '#' + u.slice(2).replace(/\.md.*$/, '') : u}">${t}</a>`);

/** Renderer podzbioru Markdowna, który faktycznie produkuje gen.mjs. */
function render(md) {
  const lines = md.split('\n');
  const out = [];
  let i = 0;
  const flushPara = buf => { if (buf.length) out.push(`<p>${inline(buf.join(' '))}</p>`); buf.length = 0; };
  const para = [];

  while (i < lines.length) {
    const line = lines[i];

    if (line.startsWith('<!--')) { i++; continue; }

    if (line.startsWith('```')) {
      flushPara(para);
      const lang = line.slice(3).trim();
      const body = [];
      i++;
      while (i < lines.length && !lines[i].startsWith('```')) body.push(lines[i++]);
      i++;
      const text = esc(body.join('\n'));
      out.push(lang === 'mermaid'
        ? `<figure class="plate"><pre class="mermaid">${text}</pre></figure>`
        : `<pre class="code"><code>${text}</code></pre>`);
      continue;
    }

    if (/^#{1,6}\s/.test(line)) {
      flushPara(para);
      const level = line.match(/^#+/)[0].length;
      out.push(`<h${Math.min(level + 1, 6)}>${inline(line.replace(/^#+\s/, ''))}</h${Math.min(level + 1, 6)}>`);
      i++;
      continue;
    }

    if (line.startsWith('|')) {
      flushPara(para);
      const rows = [];
      while (i < lines.length && lines[i].startsWith('|')) rows.push(lines[i++]);
      // Podział po niezescapowanych pionowych kreskach — `A <\|-- B` to jedna komórka.
      const cells = r => r.replace(/^\|/, '').replace(/\|$/, '')
        .split(/(?<!\\)\|/).map(c => c.trim().replace(/\\\|/g, '|'));
      const head = cells(rows[0]);
      const body = rows.slice(2).map(cells);
      out.push(`<div class="scroll"><table><thead><tr>${head.map(c => `<th>${inline(c)}</th>`).join('')}</tr></thead><tbody>${
        body.map(r => `<tr>${r.map(c => `<td>${inline(c)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`);
      continue;
    }

    if (line.startsWith('> ')) {
      flushPara(para);
      const body = [];
      while (i < lines.length && lines[i].startsWith('>')) body.push(lines[i++].replace(/^>\s?/, ''));
      out.push(`<aside class="note">${inline(body.join(' '))}</aside>`);
      continue;
    }

    if (line.startsWith('- ')) {
      flushPara(para);
      const items = [];
      while (i < lines.length && lines[i].startsWith('- ')) items.push(lines[i++].slice(2));
      out.push(`<ul>${items.map(x => `<li>${inline(x)}</li>`).join('')}</ul>`);
      continue;
    }

    if (line.startsWith('<details') || line.startsWith('</details') || line.startsWith('<summary')) {
      flushPara(para);
      out.push(line);
      i++;
      continue;
    }

    if (!line.trim()) { flushPara(para); i++; continue; }

    para.push(line);
    i++;
  }
  flushPara(para);
  return out.join('\n');
}

const slugOf = f => f.replace(/\.md$/, '');
const titleOf = md => (md.match(/^#\s+(.+)$/m) || [, 'Bez tytułu'])[1];
const stripH1 = md => md.replace(/^#\s+.+$/m, '');

const all = fs.readdirSync(DOCS).filter(f => f.endsWith('.md'));
const ordered = [
  ...['00-overview.md', '01-assemblies.md', '02-messages.md'].filter(f => all.includes(f)),
  ...all.filter(f => f.startsWith('klasy-')).sort(),
];

const sections = ordered.map(f => {
  const md = fs.readFileSync(path.join(DOCS, f), 'utf8');
  return { id: slugOf(f), title: titleOf(md), html: render(stripH1(md)) };
});

const readme = fs.readFileSync(path.join(DOCS, 'README.md'), 'utf8');
const legendTable = render((readme.match(/\| Zapis \|[\s\S]*?\n\n/) || [''])[0]);

const stats = {
  types: (fs.readFileSync(path.join(DOCS, '01-assemblies.md'), 'utf8').match(/\| (\d+) \|/g) || [])
    .reduce((a, s) => a + Number(s.replace(/\D/g, '')), 0),
  diagrams: sections.length,
  messages: (fs.readFileSync(path.join(DOCS, '02-messages.md'), 'utf8').match(/^\| `\w+` \|/gm) || []).length,
};

const toc = sections.map(s => `<li><a href="#${s.id}">${esc(s.title)}</a></li>`).join('');
const body = sections.map(s => `<section id="${s.id}"><h2>${esc(s.title)}</h2>${s.html}</section>`).join('\n');

const html = `<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/artifact.mjs -->
<title>Atlas architektury Leeway</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,500;12..96,700&family=Source+Serif+4:opsz,wght@8..60,400;8..60,600&family=JetBrains+Mono:wght@400;600&display=swap">
<style>
:root {
  --ground: #eef1ed;
  --surface: #fafbf9;
  --plate: #fbfcfa;
  --ink: #18201c;
  --muted: #5d6b64;
  --line: #d5dbd4;
  --accent: #35674f;
  --accent-soft: #e2ebe4;
  --warn: #9d4f28;
  --shadow: 0 1px 2px rgba(24, 32, 28, .06), 0 8px 24px -12px rgba(24, 32, 28, .18);
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --ground: #10140f;
    --surface: #171d19;
    --plate: #171d19;
    --ink: #e4eae4;
    --muted: #93a099;
    --line: #28302a;
    --accent: #7cbe97;
    --accent-soft: #1c2721;
    --warn: #d8875c;
    --shadow: 0 1px 2px rgba(0, 0, 0, .5), 0 10px 30px -14px rgba(0, 0, 0, .8);
  }
}
:root[data-theme="dark"] {
  --ground: #10140f;
  --surface: #171d19;
  --plate: #171d19;
  --ink: #e4eae4;
  --muted: #93a099;
  --line: #28302a;
  --accent: #7cbe97;
  --accent-soft: #1c2721;
  --warn: #d8875c;
  --shadow: 0 1px 2px rgba(0, 0, 0, .5), 0 10px 30px -14px rgba(0, 0, 0, .8);
}

* { box-sizing: border-box; }
body {
  margin: 0;
  background: var(--ground);
  color: var(--ink);
  font-family: "Source Serif 4", Georgia, serif;
  font-size: 17px;
  line-height: 1.65;
  -webkit-font-smoothing: antialiased;
}
code, pre, .mono { font-family: "JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, monospace; }
h1, h2, h3, h4, .display {
  font-family: "Bricolage Grotesque", "Helvetica Neue", Arial, sans-serif;
  text-wrap: balance;
  letter-spacing: -.015em;
  line-height: 1.15;
}

/* ── nagłówek ─────────────────────────────────────────── */
.hero {
  border-bottom: 1px solid var(--line);
  background: var(--surface);
  padding: clamp(2.5rem, 6vw, 4.5rem) clamp(1.25rem, 4vw, 3rem) clamp(2rem, 4vw, 3rem);
}
.hero-inner { max-width: 1180px; margin: 0 auto; display: grid; gap: 1.5rem; }
.eyebrow {
  font-family: "JetBrains Mono", monospace;
  font-size: .7rem;
  text-transform: uppercase;
  letter-spacing: .18em;
  color: var(--accent);
}
.hero h1 { margin: 0; font-size: clamp(2.1rem, 5.5vw, 3.6rem); font-weight: 700; }
.lede { margin: 0; max-width: 62ch; font-size: clamp(1.02rem, 1.6vw, 1.18rem); color: var(--muted); }
.rules { list-style: none; margin: .5rem 0 0; padding: 0; display: grid; gap: .8rem; max-width: 68ch; counter-reset: rule; }
.rules li {
  counter-increment: rule;
  display: grid;
  grid-template-columns: 1.6rem 1fr;
  gap: .75rem;
  font-size: .96rem;
  color: var(--muted);
}
.rules li::before {
  content: counter(rule);
  font-family: "JetBrains Mono", monospace;
  font-size: .72rem;
  color: var(--accent);
  border: 1px solid var(--line);
  border-radius: 999px;
  width: 1.6rem; height: 1.6rem;
  display: grid; place-items: center;
  margin-top: .12rem;
}
.rules b { color: var(--ink); font-weight: 600; }
.stats { display: flex; flex-wrap: wrap; gap: 2rem; margin-top: .5rem; }
.stat .n { font-family: "Bricolage Grotesque", sans-serif; font-size: 1.9rem; font-weight: 700; font-variant-numeric: tabular-nums; display: block; }
.stat .k { font-family: "JetBrains Mono", monospace; font-size: .68rem; text-transform: uppercase; letter-spacing: .14em; color: var(--muted); }

/* ── układ ────────────────────────────────────────────── */
.shell { max-width: 1180px; margin: 0 auto; padding: clamp(1.5rem, 4vw, 3rem) clamp(1.25rem, 4vw, 3rem) 5rem; display: grid; gap: clamp(1.5rem, 4vw, 3rem); }
@media (min-width: 1000px) { .shell { grid-template-columns: 15rem 1fr; align-items: start; } }
nav.toc { position: sticky; top: 1.5rem; font-size: .88rem; }
nav.toc h4 { font-size: .68rem; text-transform: uppercase; letter-spacing: .14em; color: var(--muted); margin: 0 0 .75rem; font-weight: 500; }
nav.toc ol { list-style: none; margin: 0; padding: 0; display: grid; gap: .32rem; }
nav.toc a { color: var(--muted); text-decoration: none; border-left: 2px solid var(--line); padding: .12rem 0 .12rem .7rem; display: block; }
nav.toc a:hover, nav.toc a:focus-visible { color: var(--accent); border-left-color: var(--accent); }
@media (max-width: 999px) { nav.toc { position: static; } }

main { min-width: 0; display: grid; gap: 3.5rem; }
section { min-width: 0; scroll-margin-top: 1.5rem; }
section > h2 {
  font-size: clamp(1.45rem, 3vw, 2rem);
  margin: 0 0 1rem;
  padding-bottom: .6rem;
  border-bottom: 2px solid var(--accent-soft);
}
h3 { font-size: 1.12rem; margin: 2rem 0 .6rem; }
p { max-width: 68ch; }
a { color: var(--accent); text-underline-offset: 2px; }
strong { font-weight: 600; }
ul { max-width: 68ch; }

code {
  font-size: .86em;
  background: var(--accent-soft);
  padding: .1em .34em;
  border-radius: 3px;
}
pre.code {
  background: var(--surface);
  border: 1px solid var(--line);
  border-radius: 6px;
  padding: 1rem 1.1rem;
  overflow-x: auto;
  font-size: .84rem;
  line-height: 1.6;
}
pre.code code { background: none; padding: 0; }

/* Plansza podąża za motywem widza — Mermaid renderuje tekst w kolorystyce
   motywu, więc podkładka musi być z tego samego zestawu tokenów. */
figure.plate {
  margin: 1.5rem 0;
  background: var(--plate);
  border: 1px solid var(--line);
  border-radius: 8px;
  padding: 1.25rem;
  overflow-x: auto;
  box-shadow: var(--shadow);
}
figure.plate .mermaid { min-width: min-content; }

.scroll { overflow-x: auto; margin: 1.25rem 0; border: 1px solid var(--line); border-radius: 6px; }
table { border-collapse: collapse; width: 100%; font-size: .9rem; background: var(--surface); }
th, td { text-align: left; padding: .6rem .85rem; border-bottom: 1px solid var(--line); vertical-align: top; }
th { font-family: "Bricolage Grotesque", sans-serif; font-size: .74rem; text-transform: uppercase; letter-spacing: .1em; color: var(--muted); font-weight: 500; }
tbody tr:last-child td { border-bottom: none; }
td:first-child { white-space: nowrap; }

aside.note {
  border-left: 3px solid var(--warn);
  background: var(--surface);
  padding: .9rem 1.1rem;
  margin: 1.25rem 0;
  border-radius: 0 6px 6px 0;
  font-size: .93rem;
  max-width: 68ch;
}
details { margin: 1rem 0; font-size: .9rem; }
summary { cursor: pointer; color: var(--muted); font-family: "Bricolage Grotesque", sans-serif; font-size: .82rem; }
details ul { margin: .75rem 0 0; padding-left: 1.1rem; }
details li { color: var(--muted); }

:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; border-radius: 3px; }
@media (prefers-reduced-motion: reduce) { * { animation: none !important; transition: none !important; } }
</style>

<header class="hero">
  <div class="hero-inner">
    <span class="eyebrow">Leeway — dokumentacja architektury</span>
    <h1>Atlas architektury</h1>
    <p class="lede">Diagramy generowane bezpośrednio z <code>Assets/_Project</code>. Nie są rysowane ręcznie i nie da się ich zapomnieć zaktualizować — powstają z tego samego kodu, który się kompiluje.</p>
    <ol class="rules">
      <li>Krawędź powstaje tylko wtedy, gdy <b>oba jej końce są typem z projektu</b>. UnityEngine, FishNet, VContainer i MessagePipe są granicą grafu, nie węzłem.</li>
      <li>Dziedziczenie po typie silnika <b>nie jest krawędzią, tylko stereotypem</b> węzła. Diagram nie ma jak wejść w hierarchię <code>MonoBehaviour</code>.</li>
      <li>Cykl życia i składowe generowane przez codegen FishNet <b>są szumem</b> i wypadają — zostaje kontrakt.</li>
    </ol>
    <div class="stats">
      <div class="stat"><span class="n">${stats.types}</span><span class="k">typów</span></div>
      <div class="stat"><span class="n">${stats.diagrams}</span><span class="k">diagramów</span></div>
      <div class="stat"><span class="n">${stats.messages}</span><span class="k">komunikatów</span></div>
    </div>
  </div>
</header>

<div class="shell">
  <nav class="toc">
    <h4>Spis</h4>
    <ol>${toc}<li><a href="#legenda">Jak czytać</a></li></ol>
  </nav>
  <main>
    ${body}
    <section id="legenda">
      <h2>Jak czytać</h2>
      ${legendTable}
      <p>Ukryte celowo: <code>Awake</code>/<code>Start</code>/<code>Update</code>/<code>OnDestroy</code>, <code>OnStartServer</code>/<code>OnStartNetwork</code> i pozostały cykl życia, składowe generowane przez codegen FishNet oraz prywatne metody pomocnicze. Wyjątkiem są składowe prywatne z atrybutem Unity/FishNet — one <em>są</em> kontraktem, tyle że wobec silnika i sieci, nie wobec innych klas C#.</p>
      <h3>Przegenerowanie</h3>
      <pre class="code"><code>node Tools/DiagramGen/gen.mjs        # z kodu → Docs/Architecture/
node Tools/DiagramGen/validate.mjs   # kontrola składni Mermaid
node Tools/DiagramGen/artifact.mjs   # ta strona</code></pre>
      <p>Poziom szczegółowości i listy pomijanych składowych: <code>Tools/DiagramGen/diagram.config.json</code>.</p>
    </section>
  </main>
</div>
`;

fs.writeFileSync(OUTFILE, html);
console.log(`Zapisano ${path.relative(ROOT, OUTFILE)} (${(html.length / 1024).toFixed(0)} kB, ${sections.length} sekcji)`);
