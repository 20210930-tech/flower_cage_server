namespace FlowerCageServer.Common.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string CameraImageBasePath { get; set; } = "placeholders/camera-images";
    public string PublicBaseUrl { get; set; } = string.Empty;
}
