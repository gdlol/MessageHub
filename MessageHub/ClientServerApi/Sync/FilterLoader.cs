using System.Text.Json;
using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;

namespace MessageHub.ClientServerApi.Sync;

public class FilterLoader
{
    private readonly IPersistenceService persistenceService;

    public FilterLoader(IPersistenceService persistenceService)
    {
        ArgumentNullException.ThrowIfNull(persistenceService);

        this.persistenceService = persistenceService;
    }

    public async Task<(Filter?, MatrixError?)> LoadFilterAsync(string? filter)
    {
        if (filter is null)
        {
            return (null, null);
        }
        Filter? result = null;
        MatrixError? error = null;
        if (filter.StartsWith('{'))
        {
            try
            {
                var element = JsonSerializer.Deserialize<JsonElement>(filter);
                try
                {
                    result = element.Deserialize<Filter>();
                }
                catch (Exception)
                {
                    error = MatrixError.Create(MatrixErrorCode.BadJson);
                }
            }
            catch (Exception)
            {
                error = MatrixError.Create(MatrixErrorCode.NotJson);
            }
        }
        else
        {
            string? filterJson = await persistenceService.LoadFilterAsync(filter);
            if (filterJson is null)
            {
                error = MatrixError.Create(MatrixErrorCode.NotFound);
            }
            else
            {
                result = JsonSerializer.Deserialize<Filter>(filterJson);
            }
        }
        return (result, error);
    }
}
