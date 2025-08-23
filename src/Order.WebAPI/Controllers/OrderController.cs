using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.Model;
using Order.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderService.WebAPI.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var orders = await _orderService.GetOrdersAsync();
            return Ok(orders);
        }

        [HttpGet("{orderId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderById(Guid orderId)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order != null)
            {
                return Ok(order);
            }
            else
            {
                return NotFound();
            }
        }

        // Get all orders by their status (e.g., "Created", "Processing", "Completed", "Failed")
        [HttpGet("status/{status}")]
        [ProducesResponseType(StatusCodes.Status200OK)]      // Success
        [ProducesResponseType(StatusCodes.Status404NotFound)] // No orders found
        public async Task<ActionResult<IEnumerable<OrderSummary>>> GetOrdersByStatus(string status)
        {
            var orders = await _orderService.GetOrdersByStatusAsync(status);

            if (!orders.Any())
                return NotFound(new { message = $"No orders found with status '{status}'." }); // Return 404 if no orders found

            return Ok(orders);
        }

        // Update the status of an order
        [HttpPatch("{orderId}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] string newStatus)
        {
            try
            {
                await _orderService.UpdateOrderStatusAsync(orderId, newStatus);
                return Ok(new { message = $"Order '{orderId}' updated to status '{newStatus}'." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Add a new order
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddOrder([FromBody] OrderDetail orderDetail)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState); // Automatically includes all validation errors

            var newOrderId = await _orderService.AddOrderAsync(orderDetail);
            return CreatedAtAction(nameof(GetOrderById), new { orderId = newOrderId }, new { orderId = newOrderId });
        }


    }
}
