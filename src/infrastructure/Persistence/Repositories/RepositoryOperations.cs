using System.Data;
using Microsoft.Extensions.Logging;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal static class RepositoryOperations
{
    public static Task<Result<T>> ExecuteAsync<T>(
        IDbConnectionFactory connectionFactory,
        ILogger logger,
        Func<IDbConnection, Task<T>> operation) =>
        Try(async () =>
        {
            using var connection = connectionFactory.CreateConnection();
            return await operation(connection);
        }, ex => DatabaseExceptionMapper.Map(ex, logger));
}