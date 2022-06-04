namespace MessageHub.HomeServer.Notifiers;

public enum ProfileUpdateType
{
    AvatarUrl,
    DisplayName
}

public record UserProfileUpdate(ProfileUpdateType UpdateType, string? Value);

public class UserProfileUpdateNotifier : Notifier<UserProfileUpdate> { }
