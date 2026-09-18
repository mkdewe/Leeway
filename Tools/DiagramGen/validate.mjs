#!/usr/bin/env node
// Waliduje składnię wszystkich wygenerowanych diagramów prawdziwym parserem
// Mermaid — bez przeglądarki, pod jsdom.
//
// Wymaga:  cd Tools/DiagramGen && npm install
// Uruchomienie:  node Tools/DiagramGen/validate.mjs

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

let JSDOM;
try {
  ({ JSDOM } = await import('jsdom'));
} catch {
  console.error('Brak zależności. Uruchom: cd Tools/DiagramGen && npm install');
  process.exit(2);
}

// Kolejność ma znaczenie: dompurify (zależność mermaida) łapie `window` przy
// inicjalizacji modułu, więc globale muszą stać ZANIM zaimportujemy mermaida.
const dom = new JSDOM('<!doctype html><html><body></body></html>', { pretendToBeVisual: true });
for (const key of [
  'window', 'document', 'navigator', 'Element', 'HTMLElement', 'SVGElement',
  'Node', 'getComputedStyle', 'requestAnimationFrame', 'MutationObserver', 'DOMParser',
]) {
  if (globalThis[key] === undefined && dom.window[key] !== undefined) globalThis[key] = dom.window[key];
}

const mermaid = (await import('mermaid')).default;
mermaid.initialize({ startOnLoad: false });

const HERE = path.dirname(fileURLToPath(import.meta.url));
const CFG = JSON.parse(fs.readFileSync(path.join(HERE, 'diagram.config.json'), 'utf8'));
const dir = process.argv[2] || path.resolve(HERE, '..', '..', CFG.outDir);

let blocks = 0;
let bad = 0;
for (const file of fs.readdirSync(dir).filter(f => f.endsWith('.md')).sort()) {
  const md = fs.readFileSync(path.join(dir, file), 'utf8');
  const re = /```mermaid\n([\s\S]*?)```/g;
  let m;
  let idx = 0;
  while ((m = re.exec(md)) !== null) {
    blocks++;
    idx++;
    try {
      await mermaid.parse(m[1]);
    } catch (e) {
      bad++;
      console.log(`\n✗ ${file} (blok ${idx}):`);
      console.log(String(e?.message || e).split('\n').slice(0, 8).join('\n'));
    }
  }
}

console.log(`\nSprawdzono ${blocks} diagramów w ${path.relative(process.cwd(), dir) || dir}, błędnych: ${bad}`);
process.exit(bad ? 1 : 0);
