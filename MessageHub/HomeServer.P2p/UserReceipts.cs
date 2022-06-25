using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;
using RoomReceipts = ImmutableDictionary<(string userId, string receiptType), UserReadReceipt>;

public class UserReceipts : IUserReceipts
{
    private const string storeName = nameof(UserReceipts);

    private static string GetReceiptKey(string roomId, string userId, string receiptType)
    {
        return JsonSerializer.Serialize(new[] { roomId, userId, receiptType });
    }

    private static (string roomId, string userId, string receiptType) DecodeReceiptKey(string key)
    {
        var array = JsonSerializer.Deserialize<string[]>(key)!;
        return (array[0], array[1], array[2]);
    }

    private readonly ILogger logger;
    private readonly IStorageProvider storageProvider;
    private readonly ConcurrentDictionary<string, RoomReceipts> receipts = new();
    private int initializer = 0;

    public UserReceipts(ILogger<UserReceipts> logger, IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.logger = logger;
        this.storageProvider = storageProvider;
    }

    public async ValueTask PutReceiptAsync(string roomId, string userId, string receiptType, UserReadReceipt readReceipt)
    {
        if (receiptType != ReceiptTypes.Read)
        {
            logger.LogWarning("Unknown receipt type: {}", receiptType);

            return;
        }

        receipts.AddOrUpdate(
            roomId,
            _ => RoomReceipts.Empty.Add((userId, receiptType), readReceipt),
            (_, roomReceipts) => roomReceipts.SetItem((userId, receiptType), readReceipt));

        // Save to storage.
        using var store = storageProvider.GetKeyValueStore(storeName);
        await store.PutSerializedValueAsync(GetReceiptKey(roomId, userId, receiptType), readReceipt);
        await store.CommitAsync();
    }

    public async ValueTask<RoomReceipts> TakeReceiptsAsync(string roomId)
    {
        // Initialize from storage.
        if (Interlocked.CompareExchange(ref initializer, 1, 0) == 0)
        {
            using var store = storageProvider.GetKeyValueStore(storeName);
            if (!store.IsEmpty)
            {
                using var iterator = store.Iterate();
                do
                {
                    var (key, value) = iterator.CurrentValue;
                    var decodedKey = DecodeReceiptKey(key);
                    var readReceipt = JsonSerializer.Deserialize<UserReadReceipt>(value.Span)!;
                    var receiptKey = (decodedKey.userId, decodedKey.receiptType);
                    receipts.AddOrUpdate(
                        decodedKey.roomId,
                        _ => RoomReceipts.Empty.Add(receiptKey, readReceipt),
                        (_, roomReceipts) =>
                        {
                            if (!roomReceipts.ContainsKey(receiptKey))
                            {
                                roomReceipts = roomReceipts.Add(receiptKey, readReceipt);
                            }
                            return roomReceipts;
                        });
                } while (await iterator.TryMoveAsync());
            }
        }

        if (receipts.TryRemove(roomId, out var roomReceipts))
        {
            return roomReceipts;
        }
        return RoomReceipts.Empty;
    }
}
