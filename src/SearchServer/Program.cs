using SearchContracts;
using SearchEngine.Llm;
using SearchServer;
using SearchServer.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var options = ServerOptions.FromEnvironment();
builder.Services.AddSingleton(options);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = ApiRoutes.Json.PropertyNamingPolicy);
builder.Services.AddSingleton(options.Ollama);
builder.Services.AddHttpClient<IOllamaHealth, OllamaClient>(c => c.Timeout = TimeSpan.FromMilliseconds(options.Ollama.TimeoutMs));

builder.Services.AddSingleton<EngineState>();
builder.Services.AddSingleton<IndexService>();

var app = builder.Build();
HealthEndpoints.Map(app);
IndexEndpoints.Map(app);
app.Run();

public partial class Program;
