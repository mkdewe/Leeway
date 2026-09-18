/** Czyta .docx do czytelnego tekstu z zachowaniem struktury (nagłówki, listy, tabele). */
import JSZip from "./node_modules/jszip/lib/index.js";
import { readFileSync, writeFileSync } from "fs";

const zip = await JSZip.loadAsync(readFileSync(process.argv[2]));
const stylesXml = await zip.file("word/styles.xml")?.async("string") ?? "";
const styleName = {};
for (const m of stylesXml.matchAll(/<w:style [^>]*w:styleId="([^"]+)"[^>]*>([\s\S]*?)<\/w:style>/g)) {
  const n = m[2].match(/<w:name w:val="([^"]+)"/);
  styleName[m[1]] = n ? n[1] : m[1];
}
const xml = await zip.file("word/document.xml").async("string");
const body = (xml.match(/<w:body>([\s\S]*)<\/w:body>/) || [0, xml])[1];

const textOf = (frag) => {
  let t = "";
  for (const m of frag.matchAll(/<w:t(?: [^>]*)?>([\s\S]*?)<\/w:t>|<w:tab\/>|<w:br\/>/g))
    t += m[1] !== undefined ? m[1] : (m[0].includes("tab") ? "\t" : "\n");
  return t.replace(/&amp;/g,"&").replace(/&lt;/g,"<").replace(/&gt;/g,">")
          .replace(/&quot;/g,'"').replace(/&apos;/g,"'");
};

const out = [];
for (const m of body.matchAll(/<w:tbl>[\s\S]*?<\/w:tbl>|<w:p\b[^>]*\/>|<w:p\b[^>]*>[\s\S]*?<\/w:p>/g)) {
  const frag = m[0];
  if (frag.startsWith("<w:tbl>")) {
    const rows = [];
    for (const r of frag.matchAll(/<w:tr\b[^>]*>([\s\S]*?)<\/w:tr>/g)) {
      const cells = [];
      for (const c of r[1].matchAll(/<w:tc>([\s\S]*?)<\/w:tc>/g)) cells.push(textOf(c[1]).trim().replace(/\n+/g," "));
      rows.push(cells);
    }
    out.push(`[TABELA ${rows.length}x${rows[0]?.length ?? 0}]`);
    rows.forEach(r => out.push("  | " + r.join(" | ")));
    out.push("[/TABELA]");
  } else {
    const st = frag.match(/<w:pStyle w:val="([^"]+)"/);
    const isList = /<w:numPr>/.test(frag);
    const hasImg = /<(w:drawing|w:pict|a:blip)/.test(frag);
    const t = textOf(frag).trim();
    const label = st ? (styleName[st[1]] || st[1]) : "";
    if (!t && !hasImg) { out.push(""); continue; }
    let p = "";
    if (/^heading ?(\d)/i.test(label)) p = "#".repeat(+label.match(/(\d)/)[1]) + " ";
    else if (/^title$/i.test(label)) p = "#TYTUŁ ";
    else if (/^subtitle$/i.test(label)) p = "#PODTYTUŁ ";
    else if (isList) p = "- ";
    else if (label && !/^normal$/i.test(label)) p = `[${label}] `;
    out.push(p + t + (hasImg ? "  [OBRAZ]" : ""));
  }
}
const txt = out.join("\n").replace(/\n{3,}/g, "\n\n");
writeFileSync(process.argv[3], txt);
console.error("ZNAKÓW:", txt.length, "| LINII:", txt.split("\n").length,
  "| MEDIA:", Object.keys(zip.files).filter(f=>f.startsWith("word/media")).length);
