// Notlandırma aralıkları ve sınav ağırlıklarını tanımlar.
namespace SDS.Domain.Entities;

public sealed class GradingPolicy
{
    public Guid Id { get; set; }
    public decimal AaMinimum { get; set; }
    public decimal BaMinimum { get; set; }
    public decimal BbMinimum { get; set; }
    public decimal CbMinimum { get; set; }
    public decimal CcMinimum { get; set; }
    public decimal DcMinimum { get; set; }
    public decimal MidtermWeight { get; set; }
    public decimal FinalWeight { get; set; }
    public decimal MakeUpWeight { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
