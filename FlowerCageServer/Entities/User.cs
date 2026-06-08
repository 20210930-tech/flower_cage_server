namespace FlowerCageServer.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<CageDevice> CageDevices { get; set; } = new List<CageDevice>();
    public ICollection<PlantProfile> PlantProfiles { get; set; } = new List<PlantProfile>();
}
