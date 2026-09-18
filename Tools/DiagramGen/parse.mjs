// Lekki parser C# — wystarczający do diagramów, celowo nie jest kompilatorem.
// Kluczowa własność: czyta WYŁĄCZNIE źródła z Assets/_Project, więc typy Unity,
// FishNet czy VContainer są dla niego nierozwiązywalne i z definicji stają się
// granicą grafu. Diagram nie ma jak wejść w bebechy MonoBehaviour.

/** Usuwa komentarze i literały, zachowując numerację linii. */
export function stripNoise(src) {
  let out = '';
  let i = 0;
  const n = src.length;
  const keepNewlines = (from, to) => {
    for (let k = from; k < to; k++) if (src[k] === '\n') out += '\n';
  };

  while (i < n) {
    const c = src[i];
    const d = src[i + 1];

    if (c === '/' && d === '/') {
      while (i < n && src[i] !== '\n') i++;
      continue;
    }
    if (c === '/' && d === '*') {
      const start = i;
      i += 2;
      while (i < n && !(src[i] === '*' && src[i + 1] === '/')) i++;
      i = Math.min(i + 2, n);
      keepNewlines(start, i);
      continue;
    }
    if (c === '@' && d === '"') {
      const start = i;
      i += 2;
      while (i < n) {
        if (src[i] === '"' && src[i + 1] === '"') { i += 2; continue; }
        if (src[i] === '"') { i++; break; }
        i++;
      }
      out += '""';
      keepNewlines(start, i);
      continue;
    }
    if (c === '"') {
      const start = i;
      i++;
      while (i < n) {
        if (src[i] === '\\') { i += 2; continue; }
        if (src[i] === '"') { i++; break; }
        if (src[i] === '\n') break;
        i++;
      }
      out += '""';
      keepNewlines(start, i);
      continue;
    }
    if (c === "'") {
      i++;
      while (i < n) {
        if (src[i] === '\\') { i += 2; continue; }
        if (src[i] === "'") { i++; break; }
        i++;
      }
      out += "''";
      continue;
    }
    out += c;
    i++;
  }
  return out;
}

const lineAt = (src, idx) => src.slice(0, idx).split('\n').length;

/** Znajduje indeks domykającego nawiasu dla otwierającego na `open`. */
function matchBrace(src, open) {
  let depth = 0;
  for (let i = open; i < src.length; i++) {
    if (src[i] === '{') depth++;
    else if (src[i] === '}') {
      depth--;
      if (depth === 0) return i;
    }
  }
  return -1;
}

/** Zbiera atrybuty stojące bezpośrednio przed pozycją `idx`. */
function attributesBefore(src, idx) {
  const attrs = [];
  let i = idx - 1;
  for (;;) {
    while (i >= 0 && /\s/.test(src[i])) i--;
    if (i < 0 || src[i] !== ']') break;
    let depth = 0;
    let j = i;
    for (; j >= 0; j--) {
      if (src[j] === ']') depth++;
      else if (src[j] === '[') {
        depth--;
        if (depth === 0) break;
      }
    }
    if (j < 0) break;
    attrs.unshift(src.slice(j + 1, i));
    i = j - 1;
  }
  return attrs.flatMap(a => a.split(',')).map(a => a.trim().replace(/\(.*$/s, '').trim()).filter(Boolean);
}

const TYPE_DECL =
  /(?:^|[\s;}])(?:(?:public|private|protected|internal|static|sealed|abstract|partial|unsafe|readonly|ref|new)\s+)*(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)\s*(<[^>{;]*>)?\s*(?:\([^)]*\))?\s*(:\s*[^{;]+?)?\s*(?:where\s[^{;]+?)?\s*([{;])/g;

/** Wydobywa deklaracje typów wraz z ciałami. */
export function parseTypes(cleanSrc, filePath) {
  const nsMatch = /\bnamespace\s+([\w.]+)/.exec(cleanSrc);
  const namespace = nsMatch ? nsMatch[1] : '';
  const types = [];

  TYPE_DECL.lastIndex = 0;
  let m;
  while ((m = TYPE_DECL.exec(cleanSrc)) !== null) {
    const [, kind, name, generics, baseList, terminator] = m;
    const declStart = m.index + m[0].indexOf(kind);

    let body = '';
    if (terminator === '{') {
      const open = cleanSrc.indexOf('{', m.index + m[0].length - 1);
      const close = matchBrace(cleanSrc, open);
      if (close === -1) continue;
      body = cleanSrc.slice(open + 1, close);
      TYPE_DECL.lastIndex = close;
    }

    const bases = (baseList || '')
      .replace(/^:\s*/, '')
      .split(/,(?![^<>()]*[>)])/)
      .map(s => s.trim())
      .filter(Boolean);

    // Modyfikatory czytamy z tekstu poprzedzającego słowo kluczowe.
    const head = cleanSrc.slice(Math.max(0, declStart - 120), declStart);
    const modsOnly = /((?:(?:public|private|protected|internal|static|sealed|abstract|partial|unsafe|readonly|ref|new)\s+)*)$/.exec(head);
    const modStart = declStart - (modsOnly ? modsOnly[1].length : 0);
    types.push({
      kind,
      name,
      generics: (generics || '').trim(),
      bases,
      body,
      namespace,
      filePath,
      line: lineAt(cleanSrc, declStart),
      isStatic: /\bstatic\s+$/.test(head) || /\bstatic\s+(?:partial\s+)?$/.test(head),
      isAbstract: /\babstract\s+/.test(head),
      attributes: attributesBefore(cleanSrc, modStart),
    });
  }
  return types;
}

/** Tnie ciało typu na deklaracje składowych (tylko najwyższy poziom). */
export function splitMembers(body) {
  const chunks = [];
  let buf = '';
  let depth = 0;
  let parens = 0;

  for (let i = 0; i < body.length; i++) {
    const c = body[i];
    if (c === '(') parens++;
    else if (c === ')') parens--;

    if (c === '{' && parens === 0) {
      const close = matchBrace(body, i);
      if (close === -1) break;
      const inner = body.slice(i + 1, close);
      chunks.push({ header: buf.trim(), inner });
      buf = '';
      i = close;
      while (i + 1 < body.length && /[\s;]/.test(body[i + 1])) i++;
      continue;
    }
    if (c === ';' && depth === 0 && parens === 0) {
      if (buf.trim()) chunks.push({ header: buf.trim(), inner: null });
      buf = '';
      continue;
    }
    buf += c;
  }
  if (buf.trim()) chunks.push({ header: buf.trim(), inner: null });
  return chunks;
}

const VISIBILITY = { public: '+', protected: '#', internal: '-', private: '-' };

/** Parsuje pojedynczą deklarację składowej. Zwraca null dla rzeczy nieistotnych. */
export function parseMember(chunk, ownerName) {
  let text = chunk.header;
  const attrs = [];

  // Atrybuty prowadzące.
  for (;;) {
    const t = text.trimStart();
    if (!t.startsWith('[')) break;
    let depth = 0;
    let j = 0;
    for (; j < t.length; j++) {
      if (t[j] === '[') depth++;
      else if (t[j] === ']') { depth--; if (depth === 0) break; }
    }
    if (j >= t.length) break;
    attrs.push(...t.slice(1, j).split(',').map(a => a.trim().replace(/\(.*$/s, '').trim()).filter(Boolean));
    text = t.slice(j + 1);
  }

  text = text.trim();
  if (!text) return null;
  // Zagnieżdżone typy obsługuje parseTypes — tutaj pomijamy.
  if (/\b(class|struct|interface|enum|record)\s+[A-Za-z_]/.test(text)) return null;
  if (/\bthis\s*\[/.test(text)) return null;      // indeksery
  if (/\boperator\b/.test(text)) return null;
  if (/^\s*(if|for|foreach|while|switch|return|else|try|catch|finally|lock|using)\b/.test(text)) return null;

  const mods = [];
  const modRe = /^(public|private|protected|internal|static|readonly|const|virtual|override|abstract|sealed|async|extern|partial|unsafe|event|new|volatile|ref)\b\s*/;
  let mm;
  while ((mm = modRe.exec(text)) !== null) {
    mods.push(mm[1]);
    text = text.slice(mm[0].length);
  }

  let visibility = 'private';
  for (const v of ['public', 'protected', 'internal', 'private']) if (mods.includes(v)) { visibility = v; break; }

  const isEvent = mods.includes('event');
  const isStatic = mods.includes('static') || mods.includes('const');

  // Konstruktor: Nazwa(...)
  const ctor = new RegExp(`^${ownerName}\\s*\\(([^)]*)\\)`).exec(text);
  if (ctor) {
    return { kind: 'ctor', name: ownerName, type: '', params: ctor[1].trim(), visibility, attrs, isStatic, symbol: VISIBILITY[visibility] };
  }

  // Metoda: Typ Nazwa<T>(params)
  const method = /^([\w.<>,\[\]\?]+(?:\s*\[\s*\])?)\s+([A-Za-z_]\w*)\s*(<[^>(]*>)?\s*\(([^)]*)\)/s.exec(text);
  if (method) {
    return { kind: 'method', name: method[2], type: method[1], params: method[4].replace(/\s+/g, ' ').trim(), visibility, attrs, isStatic, symbol: VISIBILITY[visibility] };
  }

  // Pole / właściwość: Typ Nazwa
  const field = /^([\w.<>,\[\]\?]+(?:\s*\[\s*\])?)\s+([A-Za-z_]\w*)/s.exec(text);
  if (field) {
    const isProperty = chunk.inner !== null || /=>/.test(chunk.header);
    return {
      kind: isEvent ? 'event' : isProperty ? 'property' : 'field',
      name: field[2],
      type: field[1].trim(),
      params: null,
      visibility,
      attrs,
      isStatic,
      symbol: VISIBILITY[visibility],
    };
  }
  return null;
}
