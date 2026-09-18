import {
  Paragraph, TextRun, HeadingLevel, AlignmentType,
  Table, TableRow, TableCell, WidthType, BorderStyle, ShadingType,
} from "docx";

/* ============================ POMOCNIKI DOCX ============================ */

export const NAVY = "1F3864", ACCENT = "2E7D5B", GREY = "5A5A5A", LIGHT = "EFEFEF", HEADBG = "1F3864";

export const H1 = (t) => new Paragraph({ text: t, heading: HeadingLevel.HEADING_1, spacing: { before: 400, after: 180 } });
export const H2 = (t) => new Paragraph({ text: t, heading: HeadingLevel.HEADING_2, spacing: { before: 280, after: 120 } });
export const H3 = (t) => new Paragraph({ text: t, heading: HeadingLevel.HEADING_3, spacing: { before: 200, after: 100 } });

export const P = (t, opts = {}) => new Paragraph({
  spacing: { after: opts.after ?? 120, line: 276 },
  alignment: opts.align,
  children: [new TextRun({ text: t, italics: opts.i, bold: opts.b, color: opts.color, size: opts.size })],
});

// Akapit z mieszanym formatowaniem: rich([["Bold:", true], [" reszta", false]])
export const rich = (parts, opts = {}) => new Paragraph({
  spacing: { after: opts.after ?? 120, line: 276 },
  children: parts.map(([t, b]) => new TextRun({ text: t, bold: !!b })),
});

export const BUL = (t, level = 0) => new Paragraph({
  bullet: { level }, spacing: { after: 60, line: 276 },
  children: [new TextRun(t)],
});

export const BULB = (head, rest, level = 0) => new Paragraph({
  bullet: { level }, spacing: { after: 60, line: 276 },
  children: [new TextRun({ text: head, bold: true }), new TextRun({ text: rest })],
});

export const NUM = (t, ref = "steps") => new Paragraph({
  numbering: { reference: ref, level: 0 }, spacing: { after: 60, line: 276 },
  children: [new TextRun(t)],
});

export const cell = (text, { bold, bg, align, size } = {}) => new TableCell({
  shading: bg ? { type: ShadingType.CLEAR, fill: bg, color: "auto" } : undefined,
  margins: { top: 70, bottom: 70, left: 110, right: 110 },
  children: [new Paragraph({
    alignment: align,
    spacing: { after: 0, line: 240 },
    children: [new TextRun({ text: String(text), bold, size: size ?? 19, color: bg === HEADBG ? "FFFFFF" : undefined })],
  })],
});

export const thinBorder = {
  top: { style: BorderStyle.SINGLE, size: 2, color: "BFBFBF" },
  bottom: { style: BorderStyle.SINGLE, size: 2, color: "BFBFBF" },
  left: { style: BorderStyle.SINGLE, size: 2, color: "BFBFBF" },
  right: { style: BorderStyle.SINGLE, size: 2, color: "BFBFBF" },
  insideHorizontal: { style: BorderStyle.SINGLE, size: 2, color: "D9D9D9" },
  insideVertical: { style: BorderStyle.SINGLE, size: 2, color: "D9D9D9" },
};

// tbl(headers, rows, widths[%], opts) — rows: string[] lub {cells, bold, bg}
export const tbl = (headers, rows, widths, opts = {}) => new Table({
  width: { size: 100, type: WidthType.PERCENTAGE },
  borders: thinBorder,
  columnWidths: widths,
  rows: [
    new TableRow({
      tableHeader: true,
      children: headers.map((h, i) => cell(h, { bold: true, bg: HEADBG, align: (i >= (opts.numFrom ?? 99) && i <= (opts.numTo ?? 99)) ? AlignmentType.RIGHT : undefined })),
    }),
    ...rows.map((r) => {
      const cells = Array.isArray(r) ? r : r.cells;
      const bold = Array.isArray(r) ? false : r.bold;
      const bg = Array.isArray(r) ? undefined : r.bg;
      return new TableRow({
        children: cells.map((c, i) => cell(c, { bold, bg, align: (i >= (opts.numFrom ?? 99) && i <= (opts.numTo ?? 99)) ? AlignmentType.RIGHT : undefined })),
      });
    }),
  ],
});

export const SPACER = (after = 200) => new Paragraph({ spacing: { after }, children: [] });
export const CAPTION = (t) => new Paragraph({
  spacing: { before: 80, after: 240 },
  children: [new TextRun({ text: t, italics: true, size: 17, color: GREY })],
});
