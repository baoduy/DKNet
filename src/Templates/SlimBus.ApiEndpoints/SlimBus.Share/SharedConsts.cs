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

    public static string DbConnectionString
    {
        get => "AppDb";
    }

    public static string AzureBusConnectionString
    {
        get => "AzureBus";
    }

    public static string RedisConnectionString
    {
        get => "Redis";
    }

    public static string SystemAccount
    {
        get => "System";
    }
}