namespace TicketAPI.Services
{
    public sealed class ExpiredOrderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredOrderWorker> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();

                    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                    await orderService.ExpirePendingOrdersAsync();
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to expire pending orders.");
                }
            }
        }
    }
}
