using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Application;

namespace JobApplicationAgent.Infrastructure;

/// <summary>
/// KI-Anbindung für OpenAI und OpenAI-kompatible Server (z. B. Ollama).
///
/// Konfiguration (Abschnitt "Ai", Rückfall auf den alten Abschnitt "OpenAI"):
///   Enabled  true/false
///   BaseUrl  Standard https://api.openai.com/v1; Ollama: http://host.docker.internal:11434/v1
///   Model    Modellname
///   Api      "responses" (OpenAI, Standard) oder "chat" (Ollama u. a. = /chat/completions)
///
/// Schlüssel nur per Umgebungsvariable AI_API_KEY oder OPENAI_API_KEY.
/// Für lokale Server (BaseUrl nicht api.openai.com) ist kein Schlüssel nötig.
/// </summary>
public sealed class OpenAiService(HttpClient http, IConfiguration cfg) : IAiService
{
    string? Setting(string name) => cfg[$"Ai:{name}"] ?? cfg[$"OpenAI:{name}"];

    string BaseUrl => (Setting("BaseUrl") ?? "https://api.openai.com/v1").TrimEnd('/');

    bool IsOpenAi => BaseUrl.StartsWith("https://api.openai.com", StringComparison.OrdinalIgnoreCase);

    string? Key =>
        Environment.GetEnvironmentVariable("AI_API_KEY") ??
        Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    string Api => (Setting("Api") ?? (IsOpenAi ? "responses" : "chat")).ToLowerInvariant();

    string Model => Setting("Model") ?? "gpt-6-luna";

    public bool IsEnabled =>
        bool.TryParse(Setting("Enabled"), out var enabled) && enabled &&
        (!IsOpenAi || !string.IsNullOrWhiteSpace(Key));

    public async Task<string> GenerateAsync(string instruction, string input, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return "KI ist deaktiviert. Setze Ai:Enabled=true (bei OpenAI zusätzlich OPENAI_API_KEY).";

        var chat = Api == "chat";
        var url = BaseUrl + (chat ? "/chat/completions" : "/responses");

        using var req = new HttpRequestMessage(HttpMethod.Post, url);

        // Bei lokalen Servern ist der Schlüssel Platzhalter; Header trotzdem setzen, falls vorhanden.
        if (!string.IsNullOrWhiteSpace(Key))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);

        object payload = chat
            ? new
            {
                model = Model,
                stream = false,
                messages = new object[]
                {
                    new { role = "system", content = instruction },
                    new { role = "user", content = input }
                }
            }
            : new { model = Model, instructions = instruction, input };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"KI-Fehler {(int)res.StatusCode} von {url}: {json}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (chat)
        {
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
                return content.GetString() ?? "";

            return "Keine Textantwort erhalten.";
        }

        if (root.TryGetProperty("output_text", out var direct))
            return direct.GetString() ?? "";

        if (root.TryGetProperty("output", out var output))
            foreach (var o in output.EnumerateArray())
                if (o.TryGetProperty("content", out var parts))
                    foreach (var part in parts.EnumerateArray())
                        if (part.TryGetProperty("text", out var text))
                            return text.GetString() ?? "";

        return "Keine Textantwort erhalten.";
    }
}
