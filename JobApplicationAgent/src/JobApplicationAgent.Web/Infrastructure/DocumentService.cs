using System.IO.Compression;
using System.Security;
using System.Text;
using JobApplicationAgent.Application;
using JobApplicationAgent.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JobApplicationAgent.Infrastructure;

public sealed class DocumentService(IAiService ai) : IDocumentService
{
    public Task<(string FileName, byte[] Bytes, string ContentType)> CreateCvAsync(
        JobView view,
        CandidateProfile profile,
        string format = "docx")
    {
        var lines = new List<DocLine>
        {
            new(profile.Name, true, 18),
            new(profile.Headline),
            new(""),
            new($"Zielstelle: {view.Job.Title} – {view.Job.Company}"),
            new(""),
            new("RELEVANTE SKILLS", true),
            new(string.Join(" · ", view.Match.MatchingSkills)),
            new(""),
            new("BERUFSERFAHRUNG", true)
        };

        foreach (var experience in profile.Experience)
        {
            lines.Add(new(
                $"{experience.Period} | {experience.Role} | {experience.Employer}",
                true,
                11));

            lines.Add(new(experience.Summary));
            lines.Add(new(string.Join(", ", experience.Skills)));
            lines.Add(new(""));
        }

        lines.Add(new("AUSBILDUNG", true));

        foreach (var education in profile.Education)
        {
            lines.Add(new(
                $"{education.Period} | {education.Program} | {education.School}"));
        }

        return Task.FromResult(
            Render(
                lines,
                $"CV-{Safe(view.Job.Company)}-{Safe(view.Job.Title)}",
                format));
    }

    public async Task<(string FileName, byte[] Bytes, string ContentType)>
        CreateCoverLetterAsync(
            JobView view,
            CandidateProfile profile,
            string format = "docx")
    {
        string body;

        if (ai.IsEnabled)
        {
            body = await ai.GenerateAsync(
                """
                Schreibe ein prägnantes deutsches Bewerbungsanschreiben.
                Verwende ausschließlich belegte Profilfakten.
                Erfinde keine Fähigkeiten oder Berufserfahrung.
                """,
                $"""
                Kandidat: {profile.Headline}
                Skills: {string.Join(", ", profile.Skills)}
                Stelle: {view.Job.Title} bei {view.Job.Company}
                Beschreibung: {view.Job.Description}
                """
            );
        }
        else
        {
            body =
                $"""
                Sehr geehrte Damen und Herren,

                mit Interesse bewerbe ich mich als {view.Job.Title} bei {view.Job.Company}.

                Meine Erfahrung umfasst insbesondere {string.Join(", ", view.Match.MatchingSkills.Take(8))}.
                In meinen bisherigen Rollen habe ich Full-Stack- und .NET-Anwendungen entwickelt und arbeite aktuell zusätzlich in der Ausbildung von Applikationsentwicklern.

                Gerne erläutere ich meine Erfahrung in einem persönlichen Gespräch.

                Freundliche Grüsse
                {profile.Name}
                """;
        }

        var lines = body
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => new DocLine(line))
            .ToList();

        return Render(
            lines,
            $"Anschreiben-{Safe(view.Job.Company)}",
            format);
    }

    private sealed record DocLine(string Text, bool Heading = false, int Size = 12);

    private static (string FileName, byte[] Bytes, string ContentType) Render(
        IReadOnlyList<DocLine> lines,
        string baseName,
        string? format)
    {
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (
                $"{baseName}.pdf",
                MakePdf(lines),
                "application/pdf");
        }

        return (
            $"{baseName}.docx",
            MakeDocx(lines.Select(l => l.Text)),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    private static byte[] MakePdf(IReadOnlyList<DocLine> lines)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(3);

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line.Text))
                        {
                            column.Item().Height(8);
                            continue;
                        }

                        var text = column.Item().Text(line.Text);

                        if (line.Heading)
                            text.Bold().FontSize(line.Size);
                    }
                });
            });
        }).GeneratePdf();
    }

    private static string Safe(string value)
    {
        return string.Concat(
            value.Where(
                character =>
                    char.IsLetterOrDigit(character) ||
                    character is '-' or '_'
            )
        ).Trim();
    }

    private static byte[] MakeDocx(IEnumerable<string> lines)
    {
        using var memoryStream = new MemoryStream();

        using (
            var archive = new ZipArchive(
                memoryStream,
                ZipArchiveMode.Create,
                true
            )
        )
        {
            Write(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                    <Default
                        Extension="rels"
                        ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                    <Default
                        Extension="xml"
                        ContentType="application/xml" />
                    <Override
                        PartName="/word/document.xml"
                        ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
                </Types>
                """
            );

            Write(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                    <Relationship
                        Id="rId1"
                        Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"
                        Target="word/document.xml" />
                </Relationships>
                """
            );

            var paragraphs = string.Join(
                "",
                lines.Select(
                    line =>
                        $"""
                        <w:p>
                            <w:r>
                                <w:t xml:space="preserve">{SecurityElement.Escape(line)}</w:t>
                            </w:r>
                        </w:p>
                        """
                )
            );

            var document =
                $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                    <w:body>
                        {paragraphs}
                        <w:sectPr />
                    </w:body>
                </w:document>
                """;

            Write(
                archive,
                "word/document.xml",
                document
            );
        }

        return memoryStream.ToArray();
    }

    private static void Write(
        ZipArchive archive,
        string path,
        string text)
    {
        var entry = archive.CreateEntry(path);

        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(false)
        );

        writer.Write(text);
    }
}