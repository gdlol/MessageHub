namespace MessageHub.Persistence;

public interface IMatrixPersistenceService
{
    Task<string> SaveFilterAsync(string userId, string filter);
    Task<string?> LoadFilterAsync(string userId, string filter_id);
}
