// RFC 4180: quoted cells, "" escapes, newlines inside quotes, CRLF. Blank lines are skipped.
export function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let quoted = false;
  const input = text.replace(/^﻿/, '');

  const endRow = () => {
    row.push(cell);
    if (row.some((value) => value.trim() !== '')) rows.push(row);
    row = [];
    cell = '';
  };

  for (let i = 0; i < input.length; i += 1) {
    const char = input[i];
    if (quoted) {
      if (char === '"' && input[i + 1] === '"') {
        cell += '"';
        i += 1;
      } else if (char === '"') {
        quoted = false;
      } else {
        cell += char;
      }
    } else if (char === '"') {
      quoted = true;
    } else if (char === ',') {
      row.push(cell);
      cell = '';
    } else if (char === '\n' || char === '\r') {
      if (char === '\r' && input[i + 1] === '\n') i += 1;
      endRow();
    } else {
      cell += char;
    }
  }
  endRow();
  return rows;
}

// A leading = + - @ makes Excel run the cell as a formula; the apostrophe keeps it text.
function escapeCell(value: string): string {
  const safe = /^[=+\-@]/.test(value) ? `'${value}` : value;
  return /[",\r\n]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe;
}

export function toCsv(rows: readonly (readonly (string | number | null | undefined)[])[]): string {
  return rows.map((row) => row.map((value) => escapeCell(value == null ? '' : String(value))).join(',')).join('\r\n');
}
