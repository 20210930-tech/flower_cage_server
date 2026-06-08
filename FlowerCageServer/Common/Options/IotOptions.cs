namespace FlowerCageServer.Common.Options;

public class IotOptions
{
    public const string SectionName = "ExternalServices:Iot";

    public string BaseUrl { get; set; } = string.Empty;
    public string CommandPath { get; set; } = "/commands";
    public bool Enabled { get; set; }
}
