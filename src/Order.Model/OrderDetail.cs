using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Order.Model
{
    public class OrderDetail
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

        [Range(0, double.MaxValue)]
        public decimal TotalCost { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalPrice { get; set; }

        [MinLength(1)]
        public IEnumerable<OrderItem> Items { get; set; }

    }
}
