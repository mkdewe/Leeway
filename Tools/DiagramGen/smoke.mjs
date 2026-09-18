#!/usr/bin/env node
// Smoke test eksploratora: uruchamia stronę pod jsdom z podstawionym kontekstem
// 2D i sprawdza, że skrypt przechodzi bez wyjątku, buduje graf i przelicza układ
// dla każdego z trzech widoków.
//
// Wymaga:  cd Tools/DiagramGen && npm install
// Uruchomienie:  node Tools/DiagramGen/smoke.mjs

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

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, '..', '..');
const CFG = JSON.parse(fs.readFileSync(path.join(HERE, 'diagram.config.json'), 'utf8'));
const file = path.join(ROOT, CFG.outDir, 'explorer.html');

if (!fs.existsSync(file)) {
  console.error('Brak explorer.html — uruchom: node Tools/DiagramGen/explorer.mjs');
  process.exit(1);
}

const CTX_METHODS = [
  'clearRect', 'save', 'restore', 'translate', 'scale', 'setTransform', 'beginPath',
  'arc', 'moveTo', 'lineTo', 'closePath', 'fill', 'stroke', 'setLineDash',
  'fillText', 'strokeText', 'measureText', 'rect', 'quadraticCurveTo', 'bezierCurveTo',
];

const errors = [];
const dom = new JSDOM(fs.readFileSync(file, 'utf8'), {
  runScripts: 'dangerously',
  pretendToBeVisual: true,
  beforeParse(window) {
    // Kontekst nagrywajacy: jsdom nie liczy layoutu, ale mozemy sprawdzic,
    // czy sciezka rysowania w ogole wykonuje sie i emituje geometrie.
    window.__draws = { arc: 0, fill: 0, stroke: 0, fillText: 0, moveTo: 0 };
    window.HTMLCanvasElement.prototype.getContext = function () {
      const ctx = {};
      for (const m of CTX_METHODS) {
        ctx[m] = () => {
          if (window.__draws[m] !== undefined) window.__draws[m]++;
          return m === 'measureText' ? { width: 10 } : undefined;
        };
      }
      return ctx;
    };
    // Bez rozmiaru layout zwraca zera; podajemy realistyczny viewport.
    window.Element.prototype.getBoundingClientRect = function () {
      return { width: 1200, height: 800, top: 0, left: 0, right: 1200, bottom: 800, x: 0, y: 0 };
    };
    window.addEventListener('error', e => errors.push(e.error || e.message));
    window.addEventListener('unhandledrejection', e => errors.push(e.reason));
  },
});

const { window } = dom;
const wait = ms => new Promise(r => setTimeout(r, ms));
const fail = msg => { console.error('FAIL: ' + msg); process.exitCode = 1; };

await wait(300);

if (errors.length) {
  console.error('Wyjątki na stronie:');
  for (const e of errors) console.error('  ' + (e && e.stack ? e.stack.split('\n')[0] : e));
  process.exit(1);
}

const doc = window.document;
const countText = () => doc.getElementById('count').textContent;
const chips = () => doc.querySelectorAll('#groupChips .chip').length;
const kinds = () => doc.querySelectorAll('#kindChips .chip').length;

console.log('widok Typy        :', countText());
if (!/\d+ wezlow \/ \d+ zaleznosci/.test(countText())) fail('licznik nie wypełniony');
if (chips() !== 15) fail('oczekiwano 15 kafelków obszarów, jest ' + chips());
if (kinds() !== 5) fail('oczekiwano 5 rodzajów zależności, jest ' + kinds());

// Rodzaje musza byc rozroznialne wizualnie, nie tylko nazwane. Sygnatura =
// ksztalt grotu + wzor linii + barwa; piec rodzajow ma dac piec sygnatur.
const sigs = [...doc.querySelectorAll('#kindChips .chip')].map(chip => {
  const svg = chip.querySelector('svg');
  if (!svg) return 'brak-svg';
  const line = svg.querySelector('line');
  const shapes = [...svg.querySelectorAll('polygon, polyline')]
    .map(el => el.tagName + ':' + (el.getAttribute('fill') || 'none'));
  return [
    line ? line.getAttribute('stroke') : '?',
    line ? (line.getAttribute('stroke-dasharray') || 'solid') : '?',
    shapes.join('+'),
  ].join(' | ');
});
console.log('sygnatury rodzajów  :');
for (let i = 0; i < sigs.length; i++) {
  const name = doc.querySelectorAll('#kindChips .chip')[i].querySelector('.nm').textContent;
  console.log('  ' + name.padEnd(14) + sigs[i]);
}
const uniq = new Set(sigs).size;
if (uniq !== 5) fail('rodzaje nie są rozróżnialne: ' + uniq + ' unikalnych sygnatur na 5');

// Przełączenie widoków musi przebudować graf bez wyjątku.
for (const view of ['groups', 'messages', 'types']) {
  const btn = [...doc.querySelectorAll('.seg button')].find(b => b.dataset.view === view);
  btn.dispatchEvent(new window.MouseEvent('click', { bubbles: true }));
  await wait(200);
  console.log('widok ' + view.padEnd(12) + ':', countText());
  if (/^0 wezlow/.test(countText())) fail('widok ' + view + ' jest pusty');
}

// Wybór węzła musi wypełnić panel szczegółów.
window.select('CreatureEntity');
await wait(50);
const detail = doc.getElementById('detail');
if (!detail.classList.contains('on')) fail('panel szczegółów się nie otworzył');
const heading = detail.querySelector('h2');
if (!heading || heading.textContent !== 'CreatureEntity') fail('zły nagłówek panelu');
const members = detail.querySelectorAll('ul.members li').length;
console.log('panel CreatureEntity: ' + members + ' składowych kontraktu');
if (members < 5) fail('panel nie pokazał składowych');

// Filtr obszarów musi realnie zmniejszać graf.
const before = countText();
doc.getElementById('noGroups').dispatchEvent(new window.MouseEvent('click', { bubbles: true }));
await wait(80);
const after = countText();
if (before === after) fail('wyłączenie wszystkich obszarów nic nie zmieniło');
console.log('filtr obszarów      :', before, '->', after);

if (errors.length) {
  console.error('Wyjątki w trakcie interakcji:');
  for (const e of errors) console.error('  ' + (e && e.stack ? e.stack.split('\n')[0] : e));
  process.exit(1);
}

// Sciezka rysowania musi realnie emitowac geometrie, a nie tylko nie wybuchac.
const draws = window.__draws;
console.log('operacje rysowania  :', 'arc=' + draws.arc, 'stroke=' + draws.stroke, 'fill=' + draws.fill, 'etykiety=' + draws.fillText);
if (draws.arc < 50) fail('narysowano za malo wezlow (arc=' + draws.arc + ')');
if (draws.moveTo < 50) fail('narysowano za malo krawedzi (moveTo=' + draws.moveTo + ')');
if (draws.fillText < 10) fail('nie narysowano etykiet');

console.log(process.exitCode ? '\nSmoke test: BŁĘDY' : '\nSmoke test: OK');
window.close();
