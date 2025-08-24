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
        Task<IEnumerable<Data.Entities.Order>> GetOrdersByStatusAsync(string status); // Get all orders by their status

        Task UpdateOrderStatusAsync(Guid orderId, string newStatus); // Update status of the order

        Task<Guid> AddOrderAsync(OrderDetail orderDetail); // Add a new order and return its ID

        Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitsAsync(); // Get monthly profits

    }
}
