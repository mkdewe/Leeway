#!/usr/bin/env node
// Skleja interaktywny eksplorator: szablon + model wygenerowany z kodu.
// Uruchomienie:  node Tools/DiagramGen/explorer.mjs [plik-wyjsciowy.html]

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, '..', '..');
const CFG = JSON.parse(fs.readFileSync(path.join(HERE, 'diagram.config.json'), 'utf8'));
const DOCS = path.join(ROOT, CFG.outDir);

const modelPath = path.join(DOCS, 'model.json');
if (!fs.existsSync(modelPath)) {
  console.error('Brak model.json — uruchom najpierw: node Tools/DiagramGen/gen.mjs');
  process.exit(1);
}

const template = fs.readFileSync(path.join(HERE, 'explorer.template.html'), 'utf8');
const model = fs.readFileSync(modelPath, 'utf8');

// </script> w danych zamknęłoby blok skryptu w przeglądarce.
const safe = model.replace(/<\/script/gi, '<\\/script');
const out = process.argv[2] || path.join(DOCS, 'explorer.html');

if (!template.includes('/*__MODEL__*/')) {
  console.error('Szablon nie zawiera znacznika /*__MODEL__*/');
  process.exit(1);
}

const html = template.replace('/*__MODEL__*/ null', safe);
fs.writeFileSync(out, html);

const m = JSON.parse(model);
console.log(`Zapisano ${path.relative(ROOT, out)} (${(html.length / 1024).toFixed(0)} kB) — ${m.nodes.length} typów, ${m.edges.length} zależności, ${m.groups.length} obszarów`);
