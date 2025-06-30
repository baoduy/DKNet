using System.Text.Json.Serialization;

namespace SlimBus.AppServices.Share;

public record BaseCommand
{
    [JsonIgnore] public string? ByUser { get; set; }
}