namespace SlimBus.AppServices.Share;

public record BaseCommand
{
    #region Properties

    [JsonIgnore] public string? ByUser { get; set; }

    #endregion
}