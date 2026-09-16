using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Felion.Infrastructure.MemberImports;

namespace Felion.IntegrationTests;

public sealed class MemberImportReaderTests
{
    [Fact]
    public async Task CsvReaderSupportsAliasesAndQuotedValues()
    {
        const string content = "StudentId,Full Name,Club Email,Department Slug,Generation Code,Position\n"
            + "SV001,\"Student, One\",student@example.org,technical,G1,Member\n";
        var reader = new MemberImportReader();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await reader.ReadAsync(stream, "members.csv", CancellationToken.None);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Student, One", row.FullName);
        Assert.Equal("technical", row.Department);
        Assert.Equal("G1", row.Generation);
    }

    [Fact]
    public async Task ExcelReaderReadsTheFirstWorksheet()
    {
        await using var stream = CreateWorkbook();
        var reader = new MemberImportReader();

        var result = await reader.ReadAsync(stream, "members.xlsx", CancellationToken.None);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("SV001", row.StudentId);
        Assert.Equal("student@example.org", row.ClubEmail);
    }

    private static MemoryStream CreateWorkbook()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var relationshipId = workbookPart.GetIdOfPart(worksheetPart);
            workbookPart.Workbook = new Workbook(
                new Sheets(new Sheet
                {
                    Name = "Members",
                    SheetId = 1,
                    Id = relationshipId
                }));
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                CreateRow("StudentId", "FullName", "ClubEmail", "Department", "Generation", "Position"),
                CreateRow("SV001", "Student One", "student@example.org", "technical", "G1", "Member")));
            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static Row CreateRow(params string[] values)
    {
        return new Row(values.Select((value, index) => new Cell
        {
            CellReference = $"{(char)('A' + index)}1",
            DataType = CellValues.String,
            CellValue = new CellValue(value)
        }));
    }
}
