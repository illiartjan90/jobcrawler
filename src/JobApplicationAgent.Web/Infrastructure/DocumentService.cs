using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
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
        // ATS-freundlich: einspaltig, Standard-Überschriften, reiner Text.
        var lines = new List<DocLine>
        {
            new(profile.Name, true, 18),
            new(profile.Headline)
        };

        var contact = Join(
            " | ",
            profile.Email,
            profile.Phone,
            profile.Location);

        if (contact.Length > 0)
            lines.Add(new(contact));

        // Links je auf eigener Zeile, damit URLs nie mitten im Link umbrechen.
        foreach (var link in profile.Links ?? [])
        {
            if (!string.IsNullOrWhiteSpace(link))
                lines.Add(new(link.Trim()));
        }

        // Alle belegten Skills aus dem Profil; die zur Stelle passenden zuerst.
        var matching = new HashSet<string>(
            view.Match.MatchingSkills,
            StringComparer.OrdinalIgnoreCase);

        var skills = profile.Skills
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(skill => matching.Contains(skill))
            .ToList();

        lines.Add(new(""));
        lines.Add(new("TECHNISCHE KENNTNISSE", true, 12, 1));
        lines.Add(new(string.Join(", ", skills)));

        lines.Add(new(""));
        lines.Add(new("BERUFSERFAHRUNG", true, 12, 1));

        foreach (var experience in profile.Experience)
        {
            lines.Add(new(
                Join(" | ", experience.Role, experience.Employer, experience.Period),
                true,
                11,
                2));

            if (!string.IsNullOrWhiteSpace(experience.Summary))
                lines.Add(new(experience.Summary));

            if (experience.Skills.Count > 0)
                lines.Add(new("Technologien: " + string.Join(", ", experience.Skills)));

            lines.Add(new(""));
        }

        lines.Add(new("AUSBILDUNG", true, 12, 1));

        foreach (var education in profile.Education)
        {
            lines.Add(new(
                Join(" | ", education.Program, education.School, education.Period)));
        }

        if (profile.Languages.Count > 0)
        {
            lines.Add(new(""));
            lines.Add(new("SPRACHEN", true, 12, 1));
            lines.Add(new(string.Join(", ", profile.Languages)));
        }

        return Task.FromResult(
            Render(
                lines,
                $"CV-{Safe(view.Job.Company)}-{Safe(view.Job.Title)}",
                format,
                $"Lebenslauf {profile.Name}",
                profile.Name));
    }

    private static string Shorten(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max] + " …";

    private static string Join(string separator, params string?[] parts)
    {
        return string.Join(
            separator,
            parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
    }

    public async Task<(string FileName, byte[] Bytes, string ContentType, string Source)>
        CreateCoverLetterAsync(
            JobView view,
            CandidateProfile profile,
            string format = "docx")
    {
        string? body = null;

        // ai = KI-Text, template-off = KI aus, template-error = KI hat nicht geantwortet
        var source = ai.IsEnabled ? "template-error" : "template-off";

        if (ai.IsEnabled)
        {
            try
            {
                var text = await ai.GenerateAsync(
                    LetterInstruction,
                    BuildLetterInput(view, profile));

                body = FinishAiLetter(text, profile);

                if (body is not null)
                    source = "ai";
            }
            catch
            {
                // KI nicht erreichbar (z. B. kein Guthaben): Vorlage verwenden.
                body = null;
            }
        }

        if (string.IsNullOrWhiteSpace(body))
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

        var document = Render(
            lines,
            $"Anschreiben-{Safe(view.Job.Company)}",
            format,
            $"Anschreiben {profile.Name}",
            profile.Name);

        return (document.FileName, document.Bytes, document.ContentType, source);
    }

    private const string LetterInstruction =
        """
        Schreibe den Textkörper eines deutschen Bewerbungsanschreibens in Schweizer Rechtschreibung (immer «ss», nie «ß»).
        Verwende ausschliesslich Fakten aus dem Kandidatenprofil. Erfinde keine Fähigkeiten, Erfahrung, Verantwortung oder Projekte.
        Behaupte keine Tiefe, die nicht im Profil steht (kein «tiefgehende Kenntnisse», «routiniert», «verantworte»).
        Nenne mindestens zwei konkrete Stationen mit Arbeitgeber.
        Verlangt die Stelle eine Technologie, die nicht im Profil steht, erwähne sie nicht als vorhanden.
        Gib nur 2 bis 3 Absätze aus: ohne Anrede, ohne Grussformel, ohne Namen, ohne Platzhalter in eckigen Klammern, ohne Kommentar.
        Höchstens 200 Wörter.
        """;

    private static string BuildLetterInput(JobView view, CandidateProfile profile)
    {
        var experience = string.Join(
            "\n",
            profile.Experience.Select(e =>
                $"- {e.Role}, {e.Employer} ({e.Period}): {e.Summary} [{string.Join(", ", e.Skills)}]"));

        var education = string.Join(
            "\n",
            profile.Education.Select(e => $"- {e.Program}, {e.School} ({e.Period})"));

        return
            $"""
            STELLE
            Titel: {view.Job.Title}
            Firma: {view.Job.Company}
            Beschreibung: {Shorten(view.Job.Description, 2500)}

            KANDIDATENPROFIL (nur diese Fakten verwenden)
            Headline: {profile.Headline}
            Skills: {string.Join(", ", profile.Skills)}
            Sprachen: {string.Join(", ", profile.Languages)}
            Stationen:
            {experience}
            Ausbildung:
            {education}
            """;
    }

    /// <summary>
    /// Bereinigt den KI-Text: Schweizer Schreibweise, keine eigene Anrede/Grussformel,
    /// keine Platzhalter, dann Anrede und Grussformel mit Namen vom Programm.
    /// </summary>
    private static string? FinishAiLetter(string? text, CandidateProfile profile)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text
            .Replace("ß", "ss")
            .Replace("**", "")
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        // eigene Anrede entfernen
        while (lines.Count > 0 &&
               (string.IsNullOrWhiteSpace(lines[0]) ||
                lines[0].TrimStart().StartsWith("Sehr geehrte", StringComparison.OrdinalIgnoreCase)))
        {
            lines.RemoveAt(0);
        }

        // eigene Grussformel samt allem danach entfernen (nur in den letzten Zeilen suchen)
        var closing = new Regex(@"^\s*(mit\s+)?freundliche[nr]?\s+gr(ü|ue|u)ss", RegexOptions.IgnoreCase);
        var from = Math.Max(0, lines.Count - 6);

        for (var i = from; i < lines.Count; i++)
        {
            if (closing.IsMatch(lines[i]))
            {
                lines.RemoveRange(i, lines.Count - i);
                break;
            }
        }

        // Platzhalter wie [Ihr Name] entfernen
        lines = lines.Where(line => !Regex.IsMatch(line, @"\[[^\]]+\]")).ToList();

        var content = string.Join("\n", lines).Trim();

        if (content.Length == 0)
            return null;

        return $"Sehr geehrte Damen und Herren,\n\n{content}\n\nFreundliche Grüsse\n\n{profile.Name}";
    }

    private sealed record DocLine(string Text, bool Heading = false, int Size = 12, int KeepNext = 0);

    private static (string FileName, byte[] Bytes, string ContentType) Render(
        IReadOnlyList<DocLine> lines,
        string baseName,
        string? format,
        string title,
        string author)
    {
        if (string.Equals(format, "txt", StringComparison.OrdinalIgnoreCase))
        {
            return (
                $"{baseName}.txt",
                Encoding.UTF8.GetBytes(string.Join("\n", lines.Select(l => l.Text))),
                "text/plain; charset=utf-8");
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (
                $"{baseName}.pdf",
                MakePdf(lines, title, author),
                "application/pdf");
        }

        return (
            $"{baseName}.docx",
            MakeDocx(lines.Select(l => l.Text)),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    private static byte[] MakePdf(IReadOnlyList<DocLine> lines, string title, string author)
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

                    for (var i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];

                        if (string.IsNullOrWhiteSpace(line.Text))
                        {
                            column.Item().Height(8);
                            continue;
                        }

                        // Zeile + die folgenden Zeilen, die dazugehören
                        // (Überschrift/Station bleibt mit ihrem Text auf einer Seite).
                        var end = i;
                        var need = line.KeepNext;

                        while (need > 0 && end + 1 < lines.Count)
                        {
                            end++;
                            need = Math.Max(need - 1, lines[end].KeepNext);
                        }

                        var group = lines.Skip(i).Take(end - i + 1).ToList();
                        i = end;

                        var holder = column.Item();

                        // Nur echte Gruppen zusammenhalten; einzelne Zeilen (z. B. lange
                        // Absätze im Anschreiben) dürfen normal umbrechen.
                        if (group.Count > 1)
                            holder = holder.ShowEntire();

                        holder.Column(inner =>
                        {
                            inner.Spacing(3);

                            foreach (var item in group)
                            {
                                if (string.IsNullOrWhiteSpace(item.Text))
                                {
                                    inner.Item().Height(8);
                                    continue;
                                }

                                var text = inner.Item().Text(item.Text);

                                if (item.Heading)
                                    text.Bold().FontSize(item.Size);
                            }
                        });
                    }
                });
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = title,
            Author = author
        })
        .GeneratePdf();
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