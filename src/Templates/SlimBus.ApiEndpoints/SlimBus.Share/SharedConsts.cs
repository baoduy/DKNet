using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlimBus.Share;

public static class SharedConsts
{
    public const string ApiName = "SlimBus.Api";

    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string DbConnectionString => "AppDb";

    public static string AzureBusConnectionString => "AzureBus";

    public static string RedisConnectionString => "Redis";

    public static string SystemAccount => "System";
}