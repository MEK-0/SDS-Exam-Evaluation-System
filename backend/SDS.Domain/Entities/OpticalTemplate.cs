// Optik form şablonunu (ad, varsayılanlık ve sürümleri) tanımlar.
namespace SDS.Domain.Entities;

public sealed class OpticalTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<OpticalTemplateVersion> Versions { get; set; } = new List<OpticalTemplateVersion>();
}
