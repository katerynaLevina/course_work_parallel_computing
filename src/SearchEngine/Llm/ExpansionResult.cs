namespace SearchEngine.Llm;

public record ExpansionResult(IReadOnlyList<string> Terms, string? Error);
