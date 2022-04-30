using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;

namespace MessageHub.ClientServer.Sync;

public class FilterLoader
{
    private readonly IAccountData accountData;

    public FilterLoader(IAccountData accountData)
    {
        ArgumentNullException.ThrowIfNull(accountData);

        this.accountData = accountData;
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
            string? filterJson = await accountData.LoadFilterAsync(filter);
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
