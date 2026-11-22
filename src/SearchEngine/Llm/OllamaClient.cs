using System.Net.Http.Json;
using System.Text.Json;
using SearchEngine.Text;

namespace SearchEngine.Llm;

public sealed class OllamaClient(HttpClient http, OllamaOptions options) : IOllamaHealth, IQueryExpander
{
    private sealed record TagsResponse(List<ModelInfo> Models);

    private sealed record ModelInfo(string Name);

    private sealed record GenerateOptions(double Temperature);

    private sealed record GenerateRequest(string Model, string Prompt, bool Stream, string Format, GenerateOptions Options);

    private sealed record GenerateResponse(string Response);

    private sealed record TermsPayload(List<string> Terms);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var tags = await http.GetFromJsonAsync<TagsResponse>(options.Url + Defaults.OllamaTagsPath, ct);
            return tags is not null && tags.Models.Any(m =>
                m.Name == options.Model || m.Name == options.Model + Defaults.OllamaLatestTag);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<ExpansionResult> ExpandAsync(string query, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.TimeoutMs);
        try
        {
            var request = new GenerateRequest(options.Model, Defaults.ExpansionPrompt + query, false,
                Defaults.OllamaJsonFormat, new GenerateOptions(Defaults.OllamaTemperature));
            using var reply = await http.PostAsJsonAsync(options.Url + Defaults.OllamaGeneratePath, request, Json, timeout.Token);
            reply.EnsureSuccessStatusCode();
            var generated = await reply.Content.ReadFromJsonAsync<GenerateResponse>(Json, timeout.Token);
            var payload = generated is null ? null : JsonSerializer.Deserialize<TermsPayload>(generated.Response, Json);
            return payload is null
                ? new ExpansionResult([], Defaults.ExpansionBadResponseError)
                : new ExpansionResult(Sanitize(payload.Terms, query), null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ExpansionResult([], Defaults.ExpansionTimeoutError);
        }
        catch (HttpRequestException)
        {
            return new ExpansionResult([], Defaults.ExpansionHttpError);
        }
        catch (JsonException)
        {
            return new ExpansionResult([], Defaults.ExpansionBadResponseError);
        }
    }

    private static List<string> Sanitize(IEnumerable<string> terms, string query)
    {
        var own = Tokenizer.Tokenize(query).ToHashSet();
        return terms.SelectMany(Tokenizer.Tokenize).Distinct().Where(t => !own.Contains(t))
            .Take(Defaults.MaxExpandedTerms).ToList();
    }
}
