using SearchEngine;
using SearchEngine.Llm;

namespace SearchServer;

public record ServerOptions(string DataDir, string DataGlob, int SearchThreads, OllamaOptions Ollama)
{
    public static ServerOptions FromEnvironment() => new(
        Env(Defaults.DataDirEnv, Defaults.DataDir),
        Env(Defaults.DataGlobEnv, Defaults.DataGlob),
        int.Parse(Env(Defaults.SearchThreadsEnv, Environment.ProcessorCount.ToString())),
        new OllamaOptions(
            Env(Defaults.OllamaUrlEnv, Defaults.OllamaUrl),
            Env(Defaults.OllamaModelEnv, Defaults.OllamaModel),
            int.Parse(Env(Defaults.OllamaTimeoutMsEnv, Defaults.OllamaTimeoutMs.ToString()))));

    private static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}
