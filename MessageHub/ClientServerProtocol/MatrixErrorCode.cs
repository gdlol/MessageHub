namespace MessageHub.ClientServerProtocol;

public enum MatrixErrorCode
{
    Forbidden,
    UnknownToken,
    MissingToken,
    BadJson,
    NotJson,
    NotFound,
    LimitExceeded,
    Unknown,
    Unrecognized,
    Unauthorized,
    UserDeactivated,
    UserInUse,
    InvalidUserName,
    RoomInUse,
    InvalidRoomState,
    UnsupportedRoomVersion,
    BadState,
    GuestAccessForbiden,
    MissingParameter,
    InvalidParameter,
    TooLarge,
    Exclusive,
    ResourceLimitExceeded,
}
