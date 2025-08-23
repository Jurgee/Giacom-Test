using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NUnit.Framework;
using Order.Data;
using Order.Data.Entities;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using OrderItem = Order.Model.OrderItem;


namespace Order.Service.Tests
{
    public class OrderServiceTests
    {
        private IOrderService _orderService;
        private IOrderRepository _orderRepository;
        private OrderContext _orderContext;
        private DbConnection _connection;

        private readonly byte[] _orderStatusCreatedId = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderServiceEmailId = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderProductEmailId = Guid.NewGuid().ToByteArray();


        [SetUp]
        public async Task Setup()
        {
            var options = new DbContextOptionsBuilder<OrderContext>()
                .UseSqlite(CreateInMemoryDatabase())
                .EnableDetailedErrors(true)
                .EnableSensitiveDataLogging(true)
                .Options;

            _connection = RelationalOptionsExtension.Extract(options).Connection;

            _orderContext = new OrderContext(options);
            _orderContext.Database.EnsureDeleted();
            _orderContext.Database.EnsureCreated();

            _orderRepository = new OrderRepository(_orderContext);
            _orderService = new OrderService(_orderRepository);

            await AddReferenceDataAsync(_orderContext);
        }

        [TearDown]
        public void TearDown()
        {
            _connection.Dispose();
            _orderContext.Dispose();
        }


        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            return connection;
        }

        [Test]
        public async Task GetOrdersAsync_ReturnsCorrectNumberOfOrders()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            var orderId2 = Guid.NewGuid();
            await AddOrder(orderId2, 2);

            var orderId3 = Guid.NewGuid();
            await AddOrder(orderId3, 3);

            // Act
            var orders = await _orderService.GetOrdersAsync();

            // Assert
            Assert.AreEqual(3, orders.Count());
        }

        [Test]
        public async Task GetOrdersAsync_ReturnsOrdersWithCorrectTotals()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            var orderId2 = Guid.NewGuid();
            await AddOrder(orderId2, 2);

            var orderId3 = Guid.NewGuid();
            await AddOrder(orderId3, 3);

            // Act
            var orders = await _orderService.GetOrdersAsync();

            // Assert
            var order1 = orders.SingleOrDefault(x => x.Id == orderId1);
            var order2 = orders.SingleOrDefault(x => x.Id == orderId2);
            var order3 = orders.SingleOrDefault(x => x.Id == orderId3);

            Assert.AreEqual(0.8m, order1.TotalCost);
            Assert.AreEqual(0.9m, order1.TotalPrice);

            Assert.AreEqual(1.6m, order2.TotalCost);
            Assert.AreEqual(1.8m, order2.TotalPrice);

            Assert.AreEqual(2.4m, order3.TotalCost);
            Assert.AreEqual(2.7m, order3.TotalPrice);
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsCorrectOrder()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(orderId1, order.Id);
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsCorrectOrderItemCount()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(1, order.Items.Count());
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsOrderWithCorrectTotals()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 2);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(1.6m, order.TotalCost);
            Assert.AreEqual(1.8m, order.TotalPrice);
        }


        [Test]
        public async Task GetOrdersByStatusAsync_ReturnsOnlyFailedOrders()
        {
            // Arrange
            var failedStatusId = Guid.NewGuid().ToByteArray();

            // Add "Failed" status to reference data
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = failedStatusId,
                Name = "Failed"
            });
            await _orderContext.SaveChangesAsync();

            // Add orders with different statuses
            var failedOrderId = Guid.NewGuid();
            var createdOrderId = Guid.NewGuid();

            // Failed order
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = failedOrderId.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now,
                StatusId = failedStatusId
            });

            // Created order
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = createdOrderId.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now,
                StatusId = _orderStatusCreatedId
            });

            await _orderContext.SaveChangesAsync();

            // Act
            var failedOrders = await _orderService.GetOrdersByStatusAsync("Failed");

            // Assert
            Assert.AreEqual(1, failedOrders.Count(), "Should return only one Failed order");
            Assert.AreEqual(failedOrderId, failedOrders.Single().Id);
        }

        [Test]
        public async Task UpdateOrderStatus_ChangesOrderStatusSuccessfully()
        {
            // Arrange
            var processingStatusId = Guid.NewGuid().ToByteArray(); // generate new status ID

            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = processingStatusId,
                Name = "InProgress"
            });
            await _orderContext.SaveChangesAsync();
            var orderId = Guid.NewGuid();
            await AddOrder(orderId, 1);

            // Act
            await _orderService.UpdateOrderStatusAsync(orderId, "InProgress");

            // Assert
            var updatedOrder = await _orderService.GetOrderByIdAsync(orderId);
            Assert.AreEqual("InProgress", updatedOrder.StatusName, "Order status should be updated to 'InProgress'");
        }

        [Test]
        public async Task AddOrderAsync_CreatesOrderWithItemsSuccessfully()
        {
            // Arrange
            var orderDetail = new OrderDetail
            {
                CustomerId = Guid.NewGuid(),
                ResellerId = Guid.NewGuid(),
                StatusName = "Created", // service will assign default
                CreatedDate = DateTime.UtcNow,
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = new Guid(_orderProductEmailId), // reference data
                        ServiceId = new Guid(_orderServiceEmailId), // reference data
                        Quantity = 2,
                        UnitCost = 0.8m,
                        UnitPrice = 0.9m
                    },
                    new OrderItem
                    {
                        ProductId = new Guid(_orderProductEmailId), // reference data
                        ServiceId = new Guid(_orderServiceEmailId), // reference data
                        Quantity = 1,
                        UnitCost = 0.8m,
                        UnitPrice = 0.9m
                    }
                }

            };

            // Act
            var newOrderId = await _orderService.AddOrderAsync(orderDetail);

            // Assert
            var createdOrder = await _orderService.GetOrderByIdAsync(newOrderId);

            Assert.NotNull(createdOrder, "Order should exist after adding.");
            Assert.AreEqual(2, createdOrder.Items.Count(), "Order should contain 2 items.");

            var firstItem = createdOrder.Items.First();
            Assert.AreEqual(2, firstItem.Quantity, "First item quantity should match.");
            Assert.AreEqual(0.8m * 2, firstItem.TotalCost, "First item total cost should be calculated correctly.");
            Assert.AreEqual(0.9m * 2, firstItem.TotalPrice, "First item total price should be calculated correctly.");
        }



        private async Task AddOrder(Guid orderId, int quantity)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now,
                StatusId = _orderStatusCreatedId,
            });

            _orderContext.OrderItem.Add(new Data.Entities.OrderItem()
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }

        private async Task AddReferenceDataAsync(OrderContext orderContext)
        {
            orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = _orderStatusCreatedId,
                Name = "Created",
            });

            orderContext.OrderService.Add(new Data.Entities.OrderService
            {
                Id = _orderServiceEmailId,
                Name = "Email"
            });

            orderContext.OrderProduct.Add(new OrderProduct
            {
                Id = _orderProductEmailId,
                Name = "100GB Mailbox",
                UnitCost = 0.8m,
                UnitPrice = 0.9m,
                ServiceId = _orderServiceEmailId
            });

            await orderContext.SaveChangesAsync();
        }
    }
}
