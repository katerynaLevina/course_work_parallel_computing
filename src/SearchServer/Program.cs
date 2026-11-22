using SearchContracts;
using SearchEngine;
using SearchEngine.Concurrency;
using SearchEngine.Llm;
using SearchServer;
using SearchServer.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var options = ServerOptions.FromEnvironment();
builder.Services.AddSingleton(options);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = ApiRoutes.Json.PropertyNamingPolicy);
builder.Services.AddSingleton(options.Ollama);
builder.Services.AddHttpClient<OllamaClient>(c => c.Timeout = TimeSpan.FromMilliseconds(options.Ollama.TimeoutMs));
builder.Services.AddTransient<IOllamaHealth>(sp => sp.GetRequiredService<OllamaClient>());
builder.Services.AddTransient<IQueryExpander>(sp => sp.GetRequiredService<OllamaClient>());

builder.Services.AddSingleton<EngineState>();
builder.Services.AddKeyedSingleton(Defaults.SearchPoolKey, (_, _) => new WorkerPool(options.SearchThreads, name: Defaults.SearchPoolKey));
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<IndexService>();

var app = builder.Build();
app.UseWebSockets();
HealthEndpoints.Map(app);
IndexEndpoints.Map(app);
SearchEndpoints.Map(app);
WebSocketEndpoints.Map(app);
app.Run();

public partial class Program;
