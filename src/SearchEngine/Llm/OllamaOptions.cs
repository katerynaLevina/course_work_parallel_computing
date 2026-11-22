namespace SearchEngine.Llm;

public record OllamaOptions(string Url, string Model, int TimeoutMs, int NumThread);
