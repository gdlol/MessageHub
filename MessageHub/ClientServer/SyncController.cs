using MessageHub.ClientServer.Sync;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;
using MessageHub.HomeServer.Rooms.Timeline;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Authorization;
using MessageHub.Authentication;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Events.General;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class SyncController : ControllerBase
{
    private readonly TimelineUpdateNotifier notifier;
    private readonly FilterLoader filterLoader;
    private readonly AccountDataLoader accountDataLoader;
    private readonly RoomsLoader roomLoader;
    private readonly PresenceLoader presenceLoader;

    public SyncController(
        TimelineUpdateNotifier notifier,
        IAccountData accountData,
        ITimelineLoader timelineLoader,
        IRooms rooms,
        IUserPresence userPresence,
        IUserReceipts userReceipts)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(accountData);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(userPresence);
        ArgumentNullException.ThrowIfNull(userReceipts);

        this.notifier = notifier;
        filterLoader = new FilterLoader(accountData);
        accountDataLoader = new AccountDataLoader(accountData);
        var ephemeralLoader = new EphemeralLoader(userReceipts);
        roomLoader = new RoomsLoader(timelineLoader, rooms, accountDataLoader, ephemeralLoader);
        presenceLoader = new PresenceLoader(userPresence);
    }

    [Route("sync")]
    [HttpGet]
    public async Task<IActionResult> Sync(
        [FromQuery(Name = "filter")] string? filterString,
        [FromQuery(Name = "full_state")] bool? fullState,
        [FromQuery(Name = "set_presence")] string? setPresence,
        [FromQuery(Name = "since")] string? since,
        [FromQuery(Name = "timeout")] long? timeout)
    {
        string? userId = HttpContext.User.Identity?.Name ?? throw new InvalidOperationException();
        var request = new SyncRequest
        {
            Filter = filterString,
            FullState = fullState ?? false,
            SetPresence = setPresence ?? PresenceValues.Online,
            Since = since,
            Timeout = timeout ?? 0
        };

        var (filter, error) = await filterLoader.LoadFilterAsync(request.Filter);
        if (error is not null)
        {
            return BadRequest(error);
        }
        if (request.Timeout > 0)
        {
            var tcs = new TaskCompletionSource();
            using var timer = new Timer(
                _ => tcs.TrySetResult(),
                null,
                dueTime: Math.Min(
                    request.Timeout,
                    (long)TimeSpan.FromSeconds(30).TotalMilliseconds),
                period: Timeout.Infinite);
            using var _ = notifier.Register(() => tcs.TrySetResult());
            if (roomLoader.IsEmpty || roomLoader.CurrentBatchId == request.Since)
            {
                await tcs.Task;
            }
        }
        var accountData = await accountDataLoader.LoadAccountDataAsync(userId, filter?.AccountData);
        var (nextBatch, rooms) = await roomLoader.LoadRoomsAsync(
            userId,
            request.FullState,
            request.Since,
            filter?.Room);
        var presence = presenceLoader.LoadPresenceUpdates(filter?.Presence);
        return new JsonResult(new SyncResponse
        {
            AccountData = accountData,
            NextBatch = nextBatch,
            Rooms = rooms,
            Presence = presence
        });
    }
}
