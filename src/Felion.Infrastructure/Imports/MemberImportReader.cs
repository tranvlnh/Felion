using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Felion.Application.Members;

namespace Felion.Infrastructure.MemberImports;

public sealed class MemberImportReader : IMemberImportReader
{
    private static readonly Dictionary<string, string> HeaderAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["studentid"] = nameof(MemberImportRow.StudentId),
            ["fullname"] = nameof(MemberImportRow.FullName),
            ["clubemail"] = nameof(MemberImportRow.ClubEmail),
            ["department"] = nameof(MemberImportRow.Department),
            ["departmentid"] = nameof(MemberImportRow.Department),
            ["departmentslug"] = nameof(MemberImportRow.Department),
            ["generation"] = nameof(MemberImportRow.Generation),
            ["generationid"] = nameof(MemberImportRow.Generation),
            ["generationcode"] = nameof(MemberImportRow.Generation),
            ["position"] = nameof(MemberImportRow.Position)
        };

    private static readonly string[] RequiredHeaders =
    [
        nameof(MemberImportRow.StudentId),
        nameof(MemberImportRow.FullName),
        nameof(MemberImportRow.ClubEmail),
        nameof(MemberImportRow.Department),
        nameof(MemberImportRow.Generation),
        nameof(MemberImportRow.Position)
    ];

    public async Task<MemberImportReadResult> ReadAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return ParseRows(ParseCsv(text));
        }

        if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await using var memory = new MemoryStream();
                await content.CopyToAsync(memory, cancellationToken);
                memory.Position = 0;
                return ParseRows(ReadWorkbook(memory));
            }
            catch (InvalidDataException exception)
            {
                return new MemberImportReadResult(
                    [],
                    [new MemberImportError(0, "File", exception.Message)]);
            }
            catch (OpenXmlPackageException exception)
            {
                return new MemberImportReadResult(
                    [],
                    [new MemberImportError(0, "File", $"The .xlsx file is invalid: {exception.Message}")]);
            }
        }

        return new MemberImportReadResult(
            [],
            [new MemberImportError(0, "File", "Only .csv and .xlsx files are supported.")]);
    }

    private static MemberImportReadResult ParseRows(IReadOnlyList<IReadOnlyList<string>> records)
    {
        if (records.Count == 0)
        {
            return new MemberImportReadResult([], [new MemberImportError(1, "File", "The file is empty.")]);
        }

        var errors = new List<MemberImportError>();
        var headerIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < records[0].Count; index++)
        {
            var normalizedHeader = NormalizeHeader(records[0][index]);
            if (!HeaderAliases.TryGetValue(normalizedHeader, out var canonicalHeader))
            {
                continue;
            }

            if (!headerIndexes.TryAdd(canonicalHeader, index))
            {
                errors.Add(new MemberImportError(1, canonicalHeader, "The header is duplicated."));
            }
        }

        foreach (var requiredHeader in RequiredHeaders)
        {
            if (!headerIndexes.ContainsKey(requiredHeader))
            {
                errors.Add(new MemberImportError(1, requiredHeader, "The required header is missing."));
            }
        }

        if (errors.Count > 0)
        {
            return new MemberImportReadResult([], errors);
        }

        var rows = new List<MemberImportRow>();
        for (var recordIndex = 1; recordIndex < records.Count; recordIndex++)
        {
            var record = records[recordIndex];
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new MemberImportRow(
                recordIndex + 1,
                GetValue(record, headerIndexes[nameof(MemberImportRow.StudentId)]),
                GetValue(record, headerIndexes[nameof(MemberImportRow.FullName)]),
                GetValue(record, headerIndexes[nameof(MemberImportRow.ClubEmail)]),
                GetValue(record, headerIndexes[nameof(MemberImportRow.Department)]),
                GetValue(record, headerIndexes[nameof(MemberImportRow.Generation)]),
                GetValue(record, headerIndexes[nameof(MemberImportRow.Position)])));
        }

        if (rows.Count == 0)
        {
            errors.Add(new MemberImportError(1, "File", "The file contains no member rows."));
        }

        return new MemberImportReadResult(rows, errors);
    }

    private static List<IReadOnlyList<string>> ParseCsv(string text)
    {
        var records = new List<IReadOnlyList<string>>();
        var record = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && character == ',')
            {
                record.Add(value.ToString());
                value.Clear();
                continue;
            }

            if (!inQuotes && (character == '\r' || character == '\n'))
            {
                record.Add(value.ToString());
                value.Clear();
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                records.Add(record.ToArray());
                record = [];
                continue;
            }

            value.Append(character);
        }

        if (value.Length > 0 || record.Count > 0)
        {
            record.Add(value.ToString());
            records.Add(record.ToArray());
        }

        return records;
    }

    private static IReadOnlyList<string>[] ReadWorkbook(Stream content)
    {
        using var document = SpreadsheetDocument.Open(content, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The workbook does not contain a workbook part.");
        var workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("The workbook does not contain workbook metadata.");
        var firstSheet = workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException("The workbook does not contain a worksheet.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id!.Value!);
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException("The worksheet is invalid.");
        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidDataException("The worksheet does not contain rows.");

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        return sheetData.Elements<Row>()
            .Select(row => ReadWorkbookRow(row, sharedStrings))
            .ToArray();
    }

    private static string[] ReadWorkbookRow(Row row, SharedStringTable? sharedStrings)
    {
        var values = new Dictionary<int, string>();
        foreach (var cell in row.Elements<Cell>())
        {
            var column = GetColumnIndex(cell.CellReference?.Value);
            if (column < 0)
            {
                continue;
            }

            values[column] = GetCellValue(cell, sharedStrings);
        }

        var width = values.Count == 0 ? 0 : values.Keys.Max() + 1;
        return Enumerable.Range(0, width).Select(index => values.GetValueOrDefault(index, string.Empty)).ToArray();
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var rawValue = cell.CellValue?.Text ?? cell.InlineString?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex)
            && sharedStrings is not null)
        {
            return sharedStrings.Elements<SharedStringItem>().ElementAtOrDefault(sharedIndex)?.InnerText ?? string.Empty;
        }

        return rawValue;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return -1;
        }

        var index = 0;
        foreach (var character in cellReference.ToUpperInvariant())
        {
            if (character is < 'A' or > 'Z')
            {
                break;
            }

            index = (index * 26) + character - 'A' + 1;
        }

        return index - 1;
    }

    private static string NormalizeHeader(string value)
    {
        return new string(value
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray())
            .ToLowerInvariant();
    }

    private static string GetValue(IReadOnlyList<string> record, int index)
    {
        return index < record.Count ? record[index].Trim() : string.Empty;
    }
}
