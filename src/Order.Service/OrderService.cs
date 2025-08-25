using Order.Data;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Order.Service
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;

        public OrderService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync()
        {
            var orders = await _orderRepository.GetOrdersAsync();
            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            return order;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string status)
        {
            var orders = await _orderRepository.GetOrdersByStatusAsync(status); // Get all orders by their status

            return orders.Select(x => new OrderSummary // Map to OrderSummary (DAL to BLL)
            {
                Id = new Guid(x.Id),
                ResellerId = new Guid(x.ResellerId),
                CustomerId = new Guid(x.CustomerId),
                StatusId = new Guid(x.StatusId),
                StatusName = x.Status.Name,
                ItemCount = x.Items.Count,
                TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                CreatedDate = x.CreatedDate
            });
        }


        public async Task UpdateOrderStatusAsync(Guid orderId, string newStatus)
        {
            await _orderRepository.UpdateOrderStatusAsync(orderId, newStatus);
        }

        public async Task<Guid> AddOrderAsync(OrderDetail orderDetail)
        {
            var newOrderId = await _orderRepository.AddOrderAsync(orderDetail);
            return newOrderId;
        }

        public async Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitsAsync()
        {
            var rawProfits = await _orderRepository.GetMonthlyProfitsAsync();

            return rawProfits.Select(x => new MonthlyProfit
            {
                Year = x.Year,
                Month = x.Month,
                TotalProfit = x.TotalProfit
            });
        }

    }
}
