using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Order.Model
{
    public class OrderDetail : IValidatableObject
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public Guid ResellerId { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        public Guid StatusId { get; set; }

        [Required, StringLength(100)]
        public string StatusName { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        public decimal TotalCost { get; set; }

        public decimal TotalPrice { get; set; }

        public IEnumerable<OrderItem> Items { get; set; }


        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Items == null || !Items.Any()) // Ensure there is at least one item in the order
            {
                yield return new ValidationResult(
                    "Order must have at least one item.",
                    new[] { nameof(Items) });
            }

            // Run validation for each item
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    foreach (var result in item.Validate(validationContext))
                    {
                        yield return result;
                    }
                }
            }

            if (TotalCost < 0)
                yield return new ValidationResult("TotalCost cannot be negative.", new[] { nameof(TotalCost) });

            if (TotalPrice < 0)
                yield return new ValidationResult("TotalPrice cannot be negative.", new[] { nameof(TotalPrice) });
        }
    }
}
