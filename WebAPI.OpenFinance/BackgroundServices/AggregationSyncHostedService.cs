using WebAPI.OpenFinance.Services;

namespace WebAPI.OpenFinance.BackgroundServices
{
    // Periodically re-syncs every active connection. This is the $0, in-process stand-in for a
    // queue + worker (SQS + ECS) — the sync logic is identical, only the trigger changes later.
    public class AggregationSyncHostedService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AggregationSyncHostedService> _logger;

        public AggregationSyncHostedService(IServiceScopeFactory scopeFactory, ILogger<AggregationSyncHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Delay once so startup (and migrations) settle before the first run.
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // BackgroundService is a singleton; scoped services (DbContext, etc.) need their own scope.
                    using var scope = _scopeFactory.CreateScope();
                    var aggregation = scope.ServiceProvider.GetRequiredService<IAggregationService>();
                    var count = await aggregation.SyncAllActiveAsync(stoppingToken);
                    _logger.LogInformation("Background sync completed for {Count} connection(s)", count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Background sync pass failed");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
