using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Order.Data;
using Order.Data.Entities;
using Order.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

        // Get all orders by their status (e.g., "Created", "InProgress", "Completed", "Failed") --------------------
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
        public async Task GetOrdersByStatusAsync_IsCaseInsensitive()
        {
            // Arrange
            var failedStatusId = Guid.NewGuid().ToByteArray();

            // Ensure "Failed" status exists
            if (!_orderContext.OrderStatus.Any(s => s.Id.SequenceEqual(failedStatusId)))
            {
                _orderContext.OrderStatus.Add(new OrderStatus
                {
                    Id = failedStatusId,
                    Name = "Failed"
                });
                await _orderContext.SaveChangesAsync();
            }

            // Add a single failed order
            var failedOrderId = Guid.NewGuid();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = failedOrderId.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.UtcNow,
                StatusId = failedStatusId,
                Status = await _orderContext.OrderStatus.FindAsync(failedStatusId)
            });
            await _orderContext.SaveChangesAsync();

            // Act
            var failedOrders = await _orderService.GetOrdersByStatusAsync("fAiLeD");

            // Assert
            Assert.AreEqual(1, failedOrders.Count(), "Should return orders regardless of case");
            Assert.AreEqual("Failed", failedOrders.Single().StatusName);
        }


        [Test]
        public async Task GetOrdersByStatusAsync_WhenNoOrdersExist_ReturnsEmptyList()
        {
            // Act
            var orders = await _orderService.GetOrdersByStatusAsync("NonExistentStatus");

            // Assert
            Assert.NotNull(orders, "Should return a non-null collection");
            Assert.IsEmpty(orders, "No orders should be returned for a status that doesn't exist");
        }

        [Test]
        public async Task GetOrdersByStatusAsync_ReturnsAllOrdersWithGivenStatus()
        {
            // Arrange
            var inProgressStatusId = Guid.NewGuid().ToByteArray();
            if (!_orderContext.OrderStatus.Any(s => s.Id.SequenceEqual(inProgressStatusId)))
            {
                _orderContext.OrderStatus.Add(new OrderStatus { Id = inProgressStatusId, Name = "InProgress" });
                await _orderContext.SaveChangesAsync();
            }

            var orderIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

            foreach (var id in orderIds)
            {
                _orderContext.Order.Add(new Data.Entities.Order
                {
                    Id = id.ToByteArray(),
                    ResellerId = Guid.NewGuid().ToByteArray(),
                    CustomerId = Guid.NewGuid().ToByteArray(),
                    CreatedDate = DateTime.UtcNow,
                    StatusId = inProgressStatusId,
                    Status = await _orderContext.OrderStatus.FindAsync(inProgressStatusId)
                });
            }
            await _orderContext.SaveChangesAsync();

            // Act
            var orders = await _orderService.GetOrdersByStatusAsync("InProgress");

            // Assert
            Assert.AreEqual(2, orders.Count(), "Should return all orders with 'InProgress' status");
            CollectionAssert.AreEquivalent(orderIds, orders.Select(o => o.Id), "Returned order IDs should match the added ones");
        }

        // Update the status of an order by its ID --------------------------------------------------------------
        [Test]
        public async Task UpdateOrderStatus_ChangesOrderStatusSuccessfully()
        {
            // Arrange
            var processingStatusId = Guid.NewGuid().ToByteArray();

            _orderContext.OrderStatus.Add(new OrderStatus // Add "InProgress" status to reference data
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
        public void UpdateOrderStatus_ThrowsWhenOrderDoesNotExist()
        {
            // Arrange
            var nonExistentOrderId = Guid.NewGuid();

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _orderService.UpdateOrderStatusAsync(nonExistentOrderId, "Completed"));

            Assert.That(ex.Message, Does.Contain("does not exist"));
        }

        [Test]
        public async Task UpdateOrderStatus_ThrowsWhenStatusDoesNotExist()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            await AddOrder(orderId, 1); // create a default order

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _orderService.UpdateOrderStatusAsync(orderId, "NonExistingStatus"));

            Assert.That(ex.Message, Does.Contain("does not exist"));
        }

        [Test]
        public async Task UpdateOrderStatus_DoesNotChangeOtherOrders()
        {
            // Arrange
            var processingStatusId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = processingStatusId,
                Name = "InProgress"
            });
            await _orderContext.SaveChangesAsync();

            var order1Id = Guid.NewGuid();
            var order2Id = Guid.NewGuid();
            await AddOrder(order1Id, 1);
            await AddOrder(order2Id, 1);

            // Act
            await _orderService.UpdateOrderStatusAsync(order1Id, "InProgress");

            // Assert
            var order1 = await _orderService.GetOrderByIdAsync(order1Id);
            var order2 = await _orderService.GetOrderByIdAsync(order2Id);

            Assert.AreEqual("InProgress", order1.StatusName);
            Assert.AreNotEqual("InProgress", order2.StatusName);
        }

        // Add a new order ----------------------------------------------------------------------------------
        [Test]
        public void AddOrderAsync_ThrowsException_WhenCreatedDateIsInFuture()
        {
            // Arrange
            var orderDetail = new OrderDetail
            {
                CustomerId = Guid.NewGuid(),
                ResellerId = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow.AddDays(1), // invalid future date
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = Guid.NewGuid(), ServiceId = Guid.NewGuid(), Quantity = 1 }
                }
            };

            // Act: validate using IValidatableObject
            var validationResults = orderDetail.Validate(new ValidationContext(orderDetail)).ToList();

            // Assert
            Assert.IsTrue(validationResults.Any(vr => vr.MemberNames.Contains(nameof(OrderDetail.CreatedDate))),
                "CreatedDate cannot be in the future.");
        }

        [Test]
        public async Task AddOrderAsync_ThrowsException_WhenNoItems()
        {
            // Arrange
            var orderDetail = new OrderDetail
            {
                CustomerId = Guid.NewGuid(),
                ResellerId = Guid.NewGuid(),
                StatusName = "Created",
                CreatedDate = DateTime.UtcNow,
                Items = new List<OrderItem>() // Empty
            };

            // Manually validate using IValidatableObject
            var validationResults = orderDetail.Validate(new ValidationContext(orderDetail)).ToList();

            // Assert validation fails
            Assert.IsTrue(validationResults.Any(vr => vr.MemberNames.Contains(nameof(OrderDetail.Items))),
                "Order must contain at least one item.");
        }


        [Test]
        public async Task AddOrderAsync_ThrowsException_WhenItemQuantityIsNegative()
        {
            // Arrange: create a product and service in the context
            var orderItem = new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ServiceId = Guid.NewGuid(),
                Quantity = -1
            };

            // Act: validate using IValidatableObject
            var validationResults = orderItem.Validate(new ValidationContext(orderItem)).ToList();

            // Assert
            Assert.IsTrue(validationResults.Any(vr => vr.MemberNames.Contains(nameof(OrderItem.Quantity))),
                "Quantity must be at least 1.");
        }


        [Test]
        public async Task AddOrderAsync_CalculatesTotalsCorrectly_ForMultipleItems()
        {
            // Arrange
            var serviceId = _orderServiceEmailId;
            var productId1 = Guid.NewGuid().ToByteArray();
            var productId2 = Guid.NewGuid().ToByteArray();

            // Ensure service exists
            if (!await _orderContext.OrderService.AnyAsync(s => s.Id == serviceId))
            {
                _orderContext.OrderService.Add(new Data.Entities.OrderService
                {
                    Id = serviceId,
                    Name = "Email Service"
                });
            }

            // Create two products with specific UnitCost/UnitPrice
            _orderContext.OrderProduct.Add(new Data.Entities.OrderProduct
            {
                Id = productId1,
                Name = "Product 1",
                ServiceId = serviceId,
                UnitCost = 0.8m,
                UnitPrice = 0.9m
            });

            _orderContext.OrderProduct.Add(new Data.Entities.OrderProduct
            {
                Id = productId2,
                Name = "Product 2",
                ServiceId = serviceId,
                UnitCost = 0.5m,
                UnitPrice = 0.7m
            });

            await _orderContext.SaveChangesAsync();

            var orderDetail = new OrderDetail
            {
                CustomerId = Guid.NewGuid(),
                ResellerId = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow,
                Items = new List<OrderItem>
        {
            new OrderItem { ProductId = new Guid(productId1), ServiceId = new Guid(serviceId), Quantity = 2 },
            new OrderItem { ProductId = new Guid(productId2), ServiceId = new Guid(serviceId), Quantity = 3 }
        }
            };

            // Act
            var newOrderId = await _orderService.AddOrderAsync(orderDetail);
            var createdOrder = await _orderService.GetOrderByIdAsync(newOrderId);

            // Assert totals
            var totalCost = createdOrder.Items.Sum(i => i.TotalCost);
            var totalPrice = createdOrder.Items.Sum(i => i.TotalPrice);

            Assert.AreEqual(2 * 0.8m + 3 * 0.5m, totalCost, "Total cost should sum all items correctly");
            Assert.AreEqual(2 * 0.9m + 3 * 0.7m, totalPrice, "Total price should sum all items correctly");
        }


        // Get monthly profits from completed orders --------------------------------------------------------
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

        [Test]
        public async Task GetMonthlyProfitsAsync_IgnoresNonCompletedOrders()
        {
            // Arrange
            var completedStatus = await _orderContext.OrderStatus
                .SingleOrDefaultAsync(s => s.Name == "Completed");

            if (completedStatus == null)
            {
                completedStatus = new OrderStatus
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    Name = "Completed"
                };
                _orderContext.OrderStatus.Add(completedStatus);
                await _orderContext.SaveChangesAsync();
            }

            var completedStatusId = completedStatus.Id;

            var inProgressStatusId = Guid.NewGuid().ToByteArray();

            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = inProgressStatusId,
                Name = "InProgress"
            });
            await _orderContext.SaveChangesAsync();

            // Add an order with non-completed status
            var orderId = Guid.NewGuid();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderId.ToByteArray(),
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = new DateTime(2023, 1, 10),
                StatusId = inProgressStatusId
            });
            _orderContext.OrderItem.Add(new Data.Entities.OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderId.ToByteArray(),
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = 5
            });
            await _orderContext.SaveChangesAsync();

            // Act
            var profits = await _orderService.GetMonthlyProfitsAsync();

            // Assert
            Assert.False(profits.Any(p => p.Month == 1 && p.Year == 2023 && p.TotalProfit > 0),
                "Non-completed orders should not be included in profits.");
        }


        [Test]
        public async Task GetMonthlyProfitsAsync_ReturnsZeroForMonthsWithoutCompletedOrders()
        {
            // Arrange
            var completedStatus = await _orderContext.OrderStatus
                .SingleOrDefaultAsync(s => s.Name == "Completed");

            if (completedStatus == null)
            {
                completedStatus = new OrderStatus
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    Name = "Completed"
                };
                _orderContext.OrderStatus.Add(completedStatus);
                await _orderContext.SaveChangesAsync();
            }

            // No orders in April
            var aprilMonth = 4;

            // Act
            var profits = await _orderService.GetMonthlyProfitsAsync();

            // Assert
            Assert.False(profits.Any(p => p.Month == aprilMonth && p.Year == 2023),
                "Months without completed orders should not appear in the results.");
        }

        // -----------------------------------------------------------------------------------------------
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
