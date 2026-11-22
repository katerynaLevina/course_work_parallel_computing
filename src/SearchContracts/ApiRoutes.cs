using System.Text.Json;

namespace SearchContracts;

public static class ApiRoutes
{
    public const string Health = "/api/health";

    public const string Index = "/api/index";

    public const string Stats = "/api/stats";

    public const string Search = "/api/search";

    public const string DocPrefix = "/api/doc/";

    public const string Doc = DocPrefix + "{**docId}";

    public const bool DefaultExpand = true;

    public const string HealthStatusOk = "ok";

    public const int MinThreads = 1;

    public const int MaxThreads = 256;

    public const string SearchModeKeyword = "keyword";

    public const int MaxQueryLength = 1000;

    public const int MinTopK = 1;

    public const int MaxTopK = 100;

    public const string ErrorIndexNotBuilt = "index not built";

    public const string ErrorQueryRequired = "query is required";

    public const string ErrorQueryTooLong = "query is too long";

    public const string ErrorUnknownMode = "mode must be 'keyword'";

    public const string ErrorDocNotFound = "document not found";

    public const string LockModeStriped = "striped";

    public const string LockModeGlobal = "global";

    /// <summary>JSON options shared by server and CLI: snake_case property names.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
