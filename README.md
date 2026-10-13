# Job Application Agent v2

C#/.NET-10-Webanwendung für Job-Aggregation, Matching, Bewerbungsunterlagen und Bewerbungs-Tracking.

## Enthalten
- SwissDevJobs RSS als echte Quelle; Provider-Architektur für weitere erlaubte APIs/RSS-Feeds
- SQLite-Persistenz und Deduplizierung per ExternalId
- Skill-/Gap-Matching und Filter
- Status-Pipeline: Discovered → Shortlisted → Prepared → Approved → Applied → Interview → Offer/Rejected
- Master-CV als strukturierte Faktenbasis
- DOCX-CV und DOCX-Anschreiben
- optionale OpenAI-Responses-API für Jobanalyse/Anschreiben
- Hintergrundscanner
- Web-Dashboard

## Start
```bash
cd src/JobApplicationAgent.Web
dotnet restore
dotnet run --urls http://0.0.0.0:5286
```
Dann http://localhost:5286 öffnen.

## KI optional aktivieren
Ohne KI verwendet das Anschreiben einen Standardtext. Schlüssel nie in Git speichern.

**OpenAI:** `export OPENAI_API_KEY='...'`, in `appsettings.json` `"Ai": { "Enabled": true, "Model": "<modell>" }`.

**Lokal mit Ollama (kostenlos):**
```json
"Ai": { "Enabled": true, "BaseUrl": "http://host.docker.internal:11434/v1", "Api": "chat", "Model": "<ollama-modell>" }
```
`BaseUrl` ist je nach Setup `http://host.docker.internal:11434/v1` (Ollama auf dem Host) oder `http://ollama:11434/v1` (Ollama als Compose-Service). Bei lokalen Servern ist kein Schlüssel nötig. Fällt die KI aus, nutzt das Anschreiben automatisch den Standardtext.

## Weitere Jobplattformen
Neue Quellen implementieren `IJobSource`. Nur offizielle/erlaubte APIs, RSS/Atom-Feeds oder vom Nutzer bereitgestellte Exporte integrieren. Plattformen ohne erlaubten automatisierten Zugriff werden nicht gescraped; die Original-URL bleibt im Dashboard verfügbar.

## Import statt Crawler (Indeed, jobup.ch, jobs.ch, JobScout24)
Für diese Plattformen gibt es keinen erlaubten automatisierten Abruf (Indeed: Publisher API eingestellt; jobup.ch/jobs.ch/JobScout24: keine öffentliche Schnittstelle gefunden). Deshalb Import statt Crawler:
- Datei-Import: JSON-Dateien nach `data/imports/` legen (siehe `data/imports/beispiel.json.sample`). Die Quelle steht im Feld `source` oder ergibt sich aus dem Unterordner (`data/imports/indeed/…`). Der nächste Scan importiert sie.
- Einzelimport: `POST /api/jobs/import` mit `{ source, url, title, company, location, description, publishedAt }`.
Pfad konfigurierbar über `JobSearch:ImportPath`.

## Bewerbung per E-Mail senden
Im Detail-Dialog gibt es «Bewerben». Es sendet CV und Anschreiben (PDF) an eine von dir eingegebene Adresse, erst nach Bestätigungsdialog, und setzt danach den Status auf `Applied`. Standardmässig aus. Einrichten:
- `appsettings.json`: `Smtp:Enabled=true`, `Smtp:Host`, `Smtp:Port`, `Smtp:User`, `Smtp:From`
- Passwort nur per Umgebungsvariable: `export SMTP_PASSWORD='…'` (bei Hotmail/Gmail ein App-Passwort)

## Freigabeprinzip
Die Anwendung kann Unterlagen vorbereiten und den Status verwalten. Ein endgültiges Absenden einer Bewerbung soll erst nach expliziter Nutzerfreigabe erfolgen.

## PDF/DOCX-Export
`/api/job/cv` und `/api/job/cover-letter` liefern mit `&format=pdf` ein PDF (QuestPDF, `dotnet add package QuestPDF`), sonst DOCX. Der CV ist ATS-freundlich aufgebaut (einspaltig, Standard-Überschriften, reiner Text). Kontaktdaten stehen im Profil (`email`, `phone`, `location`, `links`).

## Profil
`Application/CandidateProfileFactory.cs` enthält die strukturierte Master-CV-Basis. Bitte vor produktiver Nutzung gegen den Original-CV prüfen/ergänzen.
# jobcrawler
