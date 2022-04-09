using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.ClientServerProtocol;

public class MatrixError
{
    [JsonPropertyName("errcode")]
    public string ErrorCode { get; }

    [JsonPropertyName("error")]
    public string Error { get; }

    private MatrixError(string errorCode, string error)
    {
        ErrorCode = errorCode;
        Error = error;
    }

    public static MatrixError Create(MatrixErrorCode errorCode, string? error = null)
    {
        error ??= string.Empty;

        string errorCodeString = errorCode switch
        {
            MatrixErrorCode.Forbidden => "M_FORBIDDEN",
            MatrixErrorCode.UnknownToken => "M_UNKNOWN_TOKEN",
            MatrixErrorCode.MissingToken => "M_MISSING_TOKEN ",
            MatrixErrorCode.BadJson => "M_BAD_JSON",
            MatrixErrorCode.NotJson => "M_NOT_JSON",
            MatrixErrorCode.NotFound => "M_NOT_FOUND",
            MatrixErrorCode.LimitExceeded => "M_LIMIT_EXCEEDED",
            MatrixErrorCode.Unknown => "M_UNKNOWN",
            MatrixErrorCode.Unrecognized => "M_UNRECOGNIZED",
            MatrixErrorCode.Unauthorized => "M_UNAUTHORIZED",
            MatrixErrorCode.UserDeactivated => "M_USER_DEACTIVATED",
            MatrixErrorCode.UserInUse => "M_USER_IN_USE",
            MatrixErrorCode.InvalidUserName => "M_INVALID_USERNAME",
            MatrixErrorCode.RoomInUse => "M_ROOM_IN_USE",
            MatrixErrorCode.InvalidRoomState => "M_INVALID_ROOM_STATE",
            MatrixErrorCode.UnsupportedRoomVersion => "M_UNSUPPORTED_ROOM_VERSION",
            MatrixErrorCode.BadState => "M_BAD_STATE",
            MatrixErrorCode.GuestAccessForbiden => "M_GUEST_ACCESS_FORBIDDEN",
            MatrixErrorCode.MissingParameter => "M_MISSING_PARAM",
            MatrixErrorCode.InvalidParameter => "M_INVALID_PARAM",
            MatrixErrorCode.TooLarge => "M_TOO_LARGE",
            MatrixErrorCode.Exclusive => "M_EXCLUSIVE",
            MatrixErrorCode.ResourceLimitExceeded => "M_RESOURCE_LIMIT_EXCEEDED",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode))
        };
        return new MatrixError(errorCodeString, error);
    }

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            ["errcode"] = ErrorCode,
            ["error"] = Error
        };
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(ToDictionary());
    }
}
