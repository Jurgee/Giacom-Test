using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.Service
{
    public interface IOrderService
    {
        Task<IEnumerable<OrderSummary>> GetOrdersAsync();

        Task<OrderDetail> GetOrderByIdAsync(Guid orderId);

        Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string status); // Get all orders by their status

        Task UpdateOrderStatusAsync(Guid orderId, string newStatus); // Update status of the order

        Task<Guid> AddOrderAsync(OrderDetail orderDetail); // Add a new order and return its ID

        Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitsAsync(); // Get monthly profits

    }
}
