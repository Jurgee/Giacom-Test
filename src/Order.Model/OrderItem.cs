using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Order.Model
{
    public class OrderItem : IValidatableObject
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

        public int Quantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UnitCost { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalCost { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalPrice { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Ensure Quantity is at least 1
            if (Quantity < 1)
            {
                yield return new ValidationResult(
                    "Quantity must be at least 1.",
                    new[] { nameof(Quantity) });
            }
        }

    }
}
