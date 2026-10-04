namespace PaymentAPI.Services
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(long orderId, int amount, string ipAddress);

        Task<string> HandleVNPayReturn(IQueryCollection query, string rawQuery);
    }
}
