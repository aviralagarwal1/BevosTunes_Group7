using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public enum OrderStatus { InCart, Ordered, Refunded }

public class Order
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int OrderID { get; set; }

    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;

    public int? CreditCardID { get; set; }
    public CreditCard? CreditCard { get; set; }

    public DateTime? OrderDate { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.InCart;

    public bool IsGift { get; set; } = false;

    [StringLength(100)]
    public string? GiftRecipientEmail { get; set; }

    public bool IsRefunded { get; set; } = false;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
