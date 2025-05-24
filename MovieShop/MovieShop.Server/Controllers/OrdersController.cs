using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.Constants;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = UserRoles.Admin)]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders()
        {
            var orders = await _orderService.GetAllOrdersAsync();
            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrder(int id)
        {
            var order = await _orderService.GetOrderByIdAdminAsync(id);
            if (order == null)
                return NotFound();

            return Ok(order);
        }

        [HttpGet("status/{status}")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrdersByStatus(string status)
        {
            var orders = await _orderService.GetOrdersByStatusAsync(status);
            return Ok(orders);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _orderService.UpdateOrderStatusAsync(id, dto.Status);
            if (!success)
                return NotFound();

            return NoContent();
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<OrderStatisticsDto>> GetOrderStatistics()
        {
            var statistics = await _orderService.GetOrderStatisticsAsync();
            return Ok(statistics);
        }
    }
}
