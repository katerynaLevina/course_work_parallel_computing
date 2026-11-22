using System.Text.Json;

namespace SearchContracts;

public static class ApiRoutes
{
    public const string Health = "/api/health";

    public const string Index = "/api/index";

    public const string Stats = "/api/stats";

    public const string HealthStatusOk = "ok";

    public const int MinThreads = 1;

    public const int MaxThreads = 256;

    public const string LockModeStriped = "striped";

    public const string LockModeGlobal = "global";

    /// <summary>JSON options shared by server and CLI: snake_case property names.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
