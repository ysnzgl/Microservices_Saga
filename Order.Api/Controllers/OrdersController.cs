using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.Api.DTOs;
using Order.Api.Models;
using Shared;
using System.Linq;
using System.Threading.Tasks;

namespace Order.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        public OrdersController(AppDbContext context, IPublishEndpoint publishEntpoint)
        {
            _context = context;
            _publishEndpoint = publishEntpoint;
        }
        [HttpPost]
        public async Task<IActionResult> Create(OrderCreateDto orderCreate)
        {
            var newOrder = new Models.Order()
            {
                BuyerId = orderCreate.BuyerId,
                Status = OrderStatus.Suspend,
                Address = new Address
                {
                    Line = orderCreate.Address.Line,
                    Province = orderCreate.Address.Province,
                    District = orderCreate.Address.District
                },
                CreatedDate = System.DateTime.Now
            };

            orderCreate.OrderItems.ForEach(item =>
            {
                newOrder.Items.Add(new OrderItem()
                {
                    Count = item.Count,
                    ProductID = item.ProductId,
                    Price = item.Price

                });
            });

            await _context.AddAsync(newOrder);
            await _context.SaveChangesAsync();

            var orderCreatedEvent = new OrderCreatedEvent()
            {
                BuyerId = orderCreate.BuyerId,
                OrderId = newOrder.Id,
                PaymentMessage = new PaymentMessage
                {
                    CardName = orderCreate.Payment.CardName,
                    CardNumber = orderCreate.Payment.CardNumber,
                    CVV = orderCreate.Payment.CVV,
                    TotalPrice = orderCreate.OrderItems.Sum(X => X.Price * X.Count),
                }

            };
            orderCreate.OrderItems.ForEach(_ =>
            {
                orderCreatedEvent.OrderItems.Add(new OrderItemMessage
                {
                    Count = _.Count,
                    ProductId = _.ProductId

                });
            });
            await _publishEndpoint.Publish(orderCreatedEvent);

            return Ok();
        }
    }
}
