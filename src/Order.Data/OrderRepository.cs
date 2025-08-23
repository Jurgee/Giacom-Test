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

        /// <summary>
        /// Get orders by status name (e.g., "Pending", "Completed", etc.)
        /// </summary>
        /// <param name="status">
        /// The status of the orders to retrieve.
        /// </param>
        /// <returns>
        /// A list of orders with the specified status.
        /// </returns>
        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string status)
        {
            return await _orderContext.Order
                .Include(x => x.Items)
                .Include(x => x.Status)
                .Where(x => x.Status.Name == status) // Filter orders by status name
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
                .OrderByDescending(x => x.CreatedDate) // Order by created date descending for better understanding
                .ToListAsync();
        }
        /// <summary>
        /// Update the status of an order by its ID.
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="new_status"></param>
        /// <returns>
        /// Updates the status of the order if both the order and the new status exist.
        /// </returns>
        /// <exception cref="ArgumentException"></exception>
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
                    order.StatusId = status.Id;
                    await _orderContext.SaveChangesAsync();
                }
                else // if the status does not exist
                {
                    throw new ArgumentException($"Status '{newStatus}' does not exist.");
                }
            }
            else // if something deleted the order
            {
                throw new ArgumentException($"Order with ID '{orderId}' does not exist.");
            }
        }

        /// <summary>
        /// Add a new order to the database with default status "Created".
        /// </summary>
        /// <param name="order"></param>
        /// <returns>
        /// A new order ID (Guid) of the created order.
        /// </returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<Guid> AddOrderAsync(OrderDetail order)
        {
            var createdStatus = await _orderContext.OrderStatus // Create a new order with default status "Created"
                .SingleOrDefaultAsync(s => s.Name == "Created");
            if (createdStatus == null)
                throw new InvalidOperationException("Default status 'Created' not found.");

            var newOrder = new Entities.Order // Map OrderDetail to Order entity
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
                newOrder.Items.Add(new Entities.OrderItem // Map OrderDetail.Item to OrderItem entity
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    ProductId = item.ProductId.ToByteArray(),
                    ServiceId = item.ServiceId.ToByteArray(),
                    Quantity = item.Quantity
                });
            }

            _orderContext.Order.Add(newOrder); // Add the new order to the context
            await _orderContext.SaveChangesAsync();

            return new Guid(newOrder.Id);
        }


    }
}
