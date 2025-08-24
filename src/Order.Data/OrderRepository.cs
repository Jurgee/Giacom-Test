using Microsoft.EntityFrameworkCore;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Order.Data
{
    public class OrderRepository : IOrderRepository
    {
        private readonly OrderContext _orderContext;

        public OrderRepository(OrderContext orderContext)
        {
            _orderContext = orderContext;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync()
        {
            var orders = await _orderContext.Order
                .Include(x => x.Items)
                .Include(x => x.Status)
                .Select(x => new OrderSummary
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
                })
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
        {
            var orderIdBytes = orderId.ToByteArray();

            var order = await _orderContext.Order
                .Where(x => _orderContext.Database.IsInMemory() ? x.Id.SequenceEqual(orderIdBytes) : x.Id == orderIdBytes)
                .Select(x => new OrderDetail
                {
                    Id = new Guid(x.Id),
                    ResellerId = new Guid(x.ResellerId),
                    CustomerId = new Guid(x.CustomerId),
                    StatusId = new Guid(x.StatusId),
                    StatusName = x.Status.Name,
                    CreatedDate = x.CreatedDate,
                    TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                    TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                    Items = x.Items.Select(i => new Model.OrderItem
                    {
                        Id = new Guid(i.Id),
                        OrderId = new Guid(i.OrderId),
                        ServiceId = new Guid(i.ServiceId),
                        ServiceName = i.Service.Name,
                        ProductId = new Guid(i.ProductId),
                        ProductName = i.Product.Name,
                        UnitCost = i.Product.UnitCost,
                        UnitPrice = i.Product.UnitPrice,
                        TotalCost = i.Product.UnitCost * i.Quantity.Value,
                        TotalPrice = i.Product.UnitPrice * i.Quantity.Value,
                        Quantity = i.Quantity.Value
                    })
                }).SingleOrDefaultAsync();

            return order;
        }


        public async Task<IEnumerable<Data.Entities.Order>> GetOrdersByStatusAsync(string status)
        {
            return await _orderContext.Order
                .Include(x => x.Status)
                .Where(x => EF.Functions.Like(x.Status.Name, status)) // Filter orders by status name using case-insensitive comparison
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task UpdateOrderStatusAsync(Guid orderId, string newStatus)
        {
            var orderIdBytes = orderId.ToByteArray();
            var order = await _orderContext.Order // Find the order by its ID
                .Where(x => x.Id == orderIdBytes)
                .SingleOrDefaultAsync();
            if (order != null)
            {
                var status = await _orderContext.OrderStatus // Find the new status by its name
                    .Where(s => s.Name == newStatus)
                    .SingleOrDefaultAsync();
                if (status != null)
                {
                    order.StatusId = status.Id; // Update the order's status ID
                    await _orderContext.SaveChangesAsync();
                }
                else
                {
                    throw new ArgumentException($"Status '{newStatus}' does not exist."); // If the new status does not exist
                }
            }
            else
            {
                throw new ArgumentException($"Order with ID '{orderId}' does not exist."); // If the order does not exist
            }
        }

        public async Task<Guid> AddOrderAsync(OrderDetail order)
        {
            var createdStatus = await _orderContext.OrderStatus // Create a new order with default status "Created"
                .SingleOrDefaultAsync(s => s.Name == "Created");
            if (createdStatus == null)
                throw new InvalidOperationException("Default status 'Created' not found."); // Ensure the "Created" status exists

            var newOrder = new Entities.Order // Create a new Order entity
            {
                Id = Guid.NewGuid().ToByteArray(),
                CustomerId = order.CustomerId.ToByteArray(),
                ResellerId = order.ResellerId.ToByteArray(),
                StatusId = createdStatus.Id,
                CreatedDate = DateTime.UtcNow,
                Status = createdStatus
            };

            foreach (var item in order.Items)
            {
                newOrder.Items.Add(new Entities.OrderItem
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    ProductId = item.ProductId.ToByteArray(),
                    ServiceId = item.ServiceId.ToByteArray(),
                    Quantity = item.Quantity,
                    Product = await _orderContext.OrderProduct.FindAsync(item.ProductId.ToByteArray()),
                    Service = await _orderContext.OrderService.FindAsync(item.ServiceId.ToByteArray())
                });

            }

            _orderContext.Order.Add(newOrder); // Add the new order to the context
            await _orderContext.SaveChangesAsync();

            return new Guid(newOrder.Id);
        }


        public async Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitsAsync()
        {
            var orders = await _orderContext.Order // Load completed orders with their items and products
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.Status.Name == "Completed") // Only consider completed orders
                .ToListAsync();

            var profits = orders // Calculate profits grouped by year and month
                .GroupBy(o => new { o.CreatedDate.Year, o.CreatedDate.Month })
                .Select(g => new MonthlyProfit // Project to MonthlyProfit model
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalProfit = g.Sum(o => o.Items.Sum(i => (i.Product.UnitPrice - i.Product.UnitCost) * (i.Quantity ?? 0))) // Profit calculation
                })
                .OrderByDescending(mp => mp.Year)
                .ThenByDescending(mp => mp.Month);

            return profits;
        }

    }
}
