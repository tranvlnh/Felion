import { describe, expect, it } from 'vitest';
import { parseCsv, toCsv } from '#app/domain/csv.js';

describe('parseCsv', () => {
  it('handles quotes, escaped quotes, commas and newlines inside cells', () => {
    expect(parseCsv('a,b\n"x, y","say ""hi""\nthere"')).toEqual([['a', 'b'], ['x, y', 'say "hi"\nthere']]);
  });

  it('strips the BOM, accepts CRLF and skips blank lines', () => {
    expect(parseCsv('﻿a,b\r\n1,2\r\n\r\n,\r\n3,4\r\n')).toEqual([['a', 'b'], ['1', '2'], ['3', '4']]);
  });

  it('keeps empty cells', () => {
    expect(parseCsv('a,,c')).toEqual([['a', '', 'c']]);
  });
});

describe('toCsv', () => {
  it('quotes cells that need it and round-trips through parseCsv', () => {
    const rows = [['Nguyễn Văn A', 'x, "y"', 'line\nbreak', '']];
    expect(parseCsv(toCsv(rows))).toEqual(rows);
  });

  it('neutralises spreadsheet formulas', () => {
    expect(toCsv([['=1+1', '+84 912', '-x', '@a', 'ok', null]])).toBe("'=1+1,'+84 912,'-x,'@a,ok,");
  });
});
