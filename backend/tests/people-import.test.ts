import { describe, expect, it } from 'vitest';
import { parseImportRows } from '#app/api/routes/people-csv.js';

const it1 = { id: '00000000-0000-4000-8000-000000000001', name: 'Tech', active: true };
const old = { id: '00000000-0000-4000-8000-000000000002', name: 'Old', active: false };
const departments = [it1, old];
const header = 'studentId,fullName,department,generation,phone';

describe('parseImportRows', () => {
  it('maps department names case-insensitively and normalises the StudentId', () => {
    const { inputs, errors } = parseImportRows(`${header}\n b2201 ,Nguyễn  Văn A,tech,5,`, 'Candidate', departments);
    expect(errors).toEqual([]);
    expect(inputs).toEqual([{
      row: 2,
      input: { kind: 'Candidate', studentId: 'B2201', fullName: 'Nguyễn  Văn A', departmentId: it1.id, generation: 5 },
    }]);
  });

  it('reports bad rows by spreadsheet row number', () => {
    const csv = [header, 'B1,An Le,Tech,5,', 'B2,Binh,Old,5,', 'B3,Chi,Tech,abc,', 'b1,Dup,Tech,5,', 'B4,Dung,Tech,5,12'].join('\n');
    const { inputs, errors } = parseImportRows(csv, 'Member', departments);
    expect(inputs.map((entry) => entry.row)).toEqual([2]);
    expect(errors.map((entry) => entry.row)).toEqual([3, 4, 5, 6]);
    expect(errors[0]?.error).toContain('department');
    expect(errors[1]?.error).toContain('generation');
    expect(errors[2]?.error).toContain('row 2');
    expect(errors[3]?.error).toContain('phone');
  });

  it('keeps spreadsheet row numbers across blank rows', () => {
    const csv = [header, 'B1,An Le,Tech,5,', ',,,,', '', 'B2,Binh,Old,5,'].join('\r\n');
    const { inputs, errors } = parseImportRows(csv, 'Member', departments);
    expect(inputs.map((entry) => entry.row)).toEqual([2]);
    expect(errors.map((entry) => entry.row)).toEqual([5]);
  });

  it('reports a club email repeated in the file', () => {
    const csv = ['studentId,fullName,department,generation,clubEmail', 'B1,An Le,Tech,5,a@gdg.vn', 'B2,Binh,Tech,5, A@GDG.vn '].join('\n');
    const { inputs, errors } = parseImportRows(csv, 'Member', departments);
    expect(inputs[0]?.input.clubEmail).toBe('a@gdg.vn');
    expect(errors).toEqual([{ row: 3, error: 'clubEmail a@gdg.vn is also on row 2.' }]);
  });

  it('rejects unknown or missing headers and empty files up front', () => {
    expect(() => parseImportRows('studentId,fullName,department,generation,isAdmin\nB1,A B,Tech,1,true', 'Member', departments)).toThrow(/Unknown columns: isAdmin/);
    expect(() => parseImportRows('studentId,fullName\nB1,A B', 'Member', departments)).toThrow(/Missing columns/);
    expect(() => parseImportRows(header, 'Member', departments)).toThrow(/no data rows/);
  });
});
