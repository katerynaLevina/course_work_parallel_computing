namespace SearchEngine.Llm;

public interface IQueryExpander
{
    Task<ExpansionResult> ExpandAsync(string query, CancellationToken ct);
}
