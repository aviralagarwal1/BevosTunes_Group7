using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class CreditCard
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int CreditCardID { get; set; }

    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;

    [Required]
    [StringLength(16)]
    public string CardNumber { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string CardType { get; set; } = "";

    [NotMapped]
    public string DisplayCardType =>
        string.Equals(CardType, "MasterCard", StringComparison.OrdinalIgnoreCase)
            ? "Mastercard"
            : CardType;

    public bool IsActive { get; set; } = true;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
