using System.Text.Json;

namespace SearchContracts;

public static class ApiRoutes
{
    public const string Health = "/api/health";

    public const string HealthStatusOk = "ok";

    /// <summary>JSON options shared by server and CLI: snake_case property names.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
