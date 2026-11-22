namespace SearchEngine.Llm;

public interface IOllamaHealth
{
    Task<bool> IsAvailableAsync(CancellationToken ct);
}
