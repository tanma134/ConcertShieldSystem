using TicketAPI.Models;

namespace TicketAPI.Services
{
    public interface IEmailService
    {
        Task SendPaymentSuccessAsync(Order order, List<Ticket> tickets);
    }
}
