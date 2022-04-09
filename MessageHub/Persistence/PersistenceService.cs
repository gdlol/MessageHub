using System.Collections.Concurrent;

namespace MessageHub.Persistence;

internal class MatrixPersistenceService : IMatrixPersistenceService
{
    private readonly ConcurrentDictionary<(string, string), string> filters = new();

    public Task<string> SaveFilterAsync(string userId, string filter)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(filter);

        string filterId = Guid.NewGuid().ToString();
        if (!filters.TryAdd((userId, filterId), filter))
        {
            throw new InvalidOperationException();
        }
        return Task.FromResult(filterId);
    }

    public Task<string?> LoadFilterAsync(string userId, string filterId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(filterId);

        filters.TryGetValue((userId, filterId), out string? filter);
        return Task.FromResult(filter);
    }
}
