using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using MessageHub.Serialization;

namespace MessageHub.HomeServer.Services;

public class ProfileUpdateService : ScheduledService
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IUserProfile userProfile;
    private readonly IRooms rooms;
    private readonly ITimelineLoader timelineLoader;
    private readonly IEventSaver eventSaver;
    private readonly IEventPublisher eventPublisher;

    public ProfileUpdateService(
        ILogger<ProfileUpdateService> logger,
        IIdentityService identityService,
        IUserProfile userProfile,
        IRooms rooms,
        ITimelineLoader timelineLoader,
        IEventSaver eventSaver,
        IEventPublisher eventPublisher)
        : base(initialDelay: TimeSpan.FromSeconds(3), interval: TimeSpan.FromSeconds(30))
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.logger = logger;
        this.identityService = identityService;
        this.userProfile = userProfile;
        this.rooms = rooms;
        this.timelineLoader = timelineLoader;
        this.eventSaver = eventSaver;
        this.eventPublisher = eventPublisher;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running profile update service.");
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        if (!identityService.HasSelfIdentity)
        {
            return;
        }
        logger.LogDebug("Checking user profile updates...");

        var identity = identityService.GetSelfIdentity();
        var sender = UserIdentifier.FromId(identity.Id);
        var userId = sender.ToString();
        string? avatarUrl = await userProfile.GetAvatarUrlAsync(userId);
        string? displayName = await userProfile.GetDisplayNameAsync(userId);
        var batchStates = await timelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
        foreach (string roomId in batchStates.JoinedRoomIds)
        {
            try
            {
                var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
                using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
                if (!snapshot.States.TryGetValue(new RoomStateKey(EventTypes.Member, userId), out var joinEventId))
                {
                    logger.LogWarning("Join event not found in room {}.", roomId);
                    continue;
                }
                var joinEvent = await roomEventStore.LoadEventAsync(joinEventId);
                var content = joinEvent.Content.Deserialize<MemberEvent>()!;
                if (content.DisplayName == displayName && content.AvatarUrl == avatarUrl)
                {
                    continue;
                }
                logger.LogInformation("Updating user profile in room {}...", roomId);

                content = content with
                {
                    DisplayName = displayName,
                    AvatarUrl = avatarUrl
                };
                var contentElement = DefaultJsonSerializer.SerializeToElement(content);

                var (newSnapshot, pdu) = EventCreation.CreateEvent(
                    roomId,
                    snapshot,
                    EventTypes.Member,
                    userId,
                    identity.GetServerKeys(),
                    sender,
                    contentElement,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                var authorizer = new EventAuthorizer(snapshot.StateContents);
                if (!authorizer.Authorize(pdu.EventType, pdu.StateKey, sender, contentElement))
                {
                    logger.LogWarning(
                        "Event not authorized at current state: {}, {}",
                        pdu.ToJsonElement(),
                        JsonSerializer.SerializeToElement(snapshot.StateContents));
                    continue;
                }
                pdu = identity.SignEvent(pdu);
                string eventId = EventHash.GetEventId(pdu);
                await eventSaver.SaveAsync(roomId, eventId, pdu, newSnapshot.States);
                await eventPublisher.PublishAsync(pdu);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error updating user profile for room {}", roomId);
            }
        }
    }
}

public class HostedProfileUpdateService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly ProfileUpdateService profileUpdateService;

    public HostedProfileUpdateService(ProfileUpdateService profileUpdateService)
    {
        ArgumentNullException.ThrowIfNull(profileUpdateService);

        this.profileUpdateService = profileUpdateService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcs = new TaskCompletionSource();
        using var _ = stoppingToken.Register(tcs.SetResult);
        profileUpdateService.Start();
        return tcs.Task;
    }
}
