using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.Data
{
    public interface IOrderRepository
    {
        Task<IEnumerable<OrderSummary>> GetOrdersAsync();

        Task<OrderDetail> GetOrderByIdAsync(Guid orderId);

        /// <summary>
        /// Retrieves all orders that have the specified status
        /// </summary>
        /// <param name="status">
        /// The status of the orders to retrieve
        /// </param>
        /// <returns>
        /// A list of orders with the specified status
        /// </returns>
        Task<IEnumerable<Data.Entities.Order>> GetOrdersByStatusAsync(string status);

        /// <summary>
        /// Update the status of an order by its ID
        /// </summary>
        /// <param name="orderId">
        /// The ID of the order to update
        /// </param>
        /// <param name="newStatus">
        /// The new status to set
        /// </param>
        /// <returns>
        /// Updates the status of the order if both the order and the new status exist
        /// </returns>
        Task UpdateOrderStatusAsync(Guid orderId, string newStatus);

        /// <summary>
        /// Add a new order to the database with default status "Created"
        /// </summary>
        /// <param name="order">
        /// The order details to create
        /// </param>
        /// <returns>
        /// A new order ID (Guid) of the created order
        /// </returns>
        Task<Guid> AddOrderAsync(OrderDetail order);

        /// <summary>
        /// Get monthly profits calculated from completed orders
        /// </summary>
        /// <returns>
        /// Monthly profits with year, month, and total profit
        /// </returns>
        Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitsAsync();

    }
}
