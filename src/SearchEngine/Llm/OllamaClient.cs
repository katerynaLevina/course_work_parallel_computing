using System.Net.Http.Json;

namespace SearchEngine.Llm;

public sealed class OllamaClient(HttpClient http, OllamaOptions options) : IOllamaHealth
{
    private sealed record TagsResponse(List<ModelInfo> Models);

    private sealed record ModelInfo(string Name);

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
}
