using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TicketFlow.BuildingBlocks.Persistence;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync<TDbContext>(this IServiceProvider services, CancellationToken cancellationToken = default)
        where TDbContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
