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

        public decimal UnitCost { get; set; }

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
            if (UnitCost < 0)
            {
                yield return new ValidationResult(
                    "UnitCost cannot be negative.",
                    new[] { nameof(UnitCost) });
            }
            if (UnitPrice < 0)
            {
                yield return new ValidationResult(
                    "UnitPrice cannot be negative.",
                    new[] { nameof(UnitPrice) });
            }
            if (TotalCost != UnitCost * Quantity)
            {
                yield return new ValidationResult(
                    $"TotalCost ({TotalCost}) does not equal UnitCost ({UnitCost}) × Quantity ({Quantity}).",
                    new[] { nameof(TotalCost), nameof(UnitCost), nameof(Quantity) });
            }

            if (TotalPrice != UnitPrice * Quantity)
            {
                yield return new ValidationResult(
                    $"TotalPrice ({TotalPrice}) does not equal UnitPrice ({UnitPrice}) × Quantity ({Quantity}).",
                    new[] { nameof(TotalPrice), nameof(UnitPrice), nameof(Quantity) });
            }
            if (UnitPrice < UnitCost)
            {
                yield return new ValidationResult(
                    "UnitPrice cannot be less than UnitCost.",
                    new[] { nameof(UnitPrice), nameof(UnitCost) });
            }
        }

    }
}
