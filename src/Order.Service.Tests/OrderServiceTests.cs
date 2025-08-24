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

            // Ensure "Failed" status exists in context without duplicates
            if (!_orderContext.OrderStatus.Any(s => s.Id.SequenceEqual(failedStatusId)))
            {
                _orderContext.OrderStatus.Add(new OrderStatus
                {
                    Id = failedStatusId,
                    Name = "Failed"
                });
                await _orderContext.SaveChangesAsync();
            }

            // Add orders
            var failedOrderId = Guid.NewGuid();
            var createdOrderId = Guid.NewGuid();

            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = failedOrderId.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.UtcNow,
                StatusId = failedStatusId,
                Status = await _orderContext.OrderStatus.FindAsync(failedStatusId) // attach existing status
            });

            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = createdOrderId.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.UtcNow,
                StatusId = _orderStatusCreatedId,
                Status = await _orderContext.OrderStatus.FindAsync(_orderStatusCreatedId) // attach existing status
            });

            await _orderContext.SaveChangesAsync();

            // Act
            var failedOrders = await _orderService.GetOrdersByStatusAsync("Failed");

            // Assert
            Assert.AreEqual(1, failedOrders.Count(), "Should return only one Failed order");

            var returnedOrder = failedOrders.Single();
            Assert.AreEqual(failedOrderId, returnedOrder.Id, "Returned order ID should match the failed order");
            Assert.AreEqual("Failed", returnedOrder.StatusName, "Returned order should have status 'Failed'");
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

        [Test]
        public async Task GetMonthlyProfitsAsync_ReturnsCorrectProfits()
        {

            // Arrange
            var completedStatusId = Guid.NewGuid().ToByteArray();

            // Add "Completed" status to reference data
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = completedStatusId,
                Name = "Completed"
            });
            await _orderContext.SaveChangesAsync();

            // Add completed orders in different months
            var orderId1 = Guid.NewGuid();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderId1.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = new DateTime(2023, 1, 15),
                StatusId = completedStatusId
            });
            _orderContext.OrderItem.Add(new Data.Entities.OrderItem()
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderId1.ToByteArray(),
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = 2,

            });

            var orderId2 = Guid.NewGuid();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderId2.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = new DateTime(2023, 1, 20),
                StatusId = completedStatusId
            });
            _orderContext.OrderItem.Add(new Data.Entities.OrderItem()
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderId2.ToByteArray(),
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = 1
            });
            var orderId3 = Guid.NewGuid();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderId3.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = new DateTime(2023, 2, 5),
                StatusId = completedStatusId
            });
            _orderContext.OrderItem.Add(new Data.Entities.OrderItem()
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderId3.ToByteArray(),
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = 3
            });
            await _orderContext.SaveChangesAsync();
            // Act

            var profits = await _orderService.GetMonthlyProfitsAsync();

            // Assert
            Assert.AreEqual(2, profits.Count(), "Should return profits for 2 months.");
            var januaryProfit = profits.SingleOrDefault(p => p.Year == 2023 && p.Month == 1);
            var februaryProfit = profits.SingleOrDefault(p => p.Year == 2023 && p.Month == 2);
            Assert.NotNull(januaryProfit, "January profit should be present.");
            Assert.NotNull(februaryProfit, "February profit should be present.");
            Assert.AreEqual(0.3m, januaryProfit.TotalProfit, "January profit should be calculated correctly.");
            Assert.AreEqual(0.3m, februaryProfit.TotalProfit, "February profit should be calculated correctly.");
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
