using System;
using System.ComponentModel.DataAnnotations;

namespace Order.Model
{
    public class OrderItem
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public Guid OrderId { get; set; }

        [Required]
        public Guid ServiceId { get; set; }

        [Required, StringLength(100)]
        public string ServiceName { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [Required, StringLength(100)]
        public string ProductName { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UnitCost { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalCost { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalPrice { get; set; }
    }
}
