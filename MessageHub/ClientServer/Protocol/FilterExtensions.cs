using System.Text.Json;

namespace MessageHub.ClientServer.Protocol;

public static class FilterExtensions
{
    public static bool ShouldIncludeEvent(this EventFilter? filter, string sender, string type)
    {
        if (filter is null)
        {
            return true;
        }
        if (filter.NotSenders is not null && filter.NotSenders.Contains(sender))
        {
            return false;
        }
        if (filter.NotTypes is not null && filter.NotTypes.Any(pattern => Filter.StringMatch(type, pattern)))
        {
            return false;
        }
        if (filter.Senders is not null && !filter.Senders.Contains(sender))
        {
            return false;
        }
        if (filter.Types is not null && !filter.Types.Any(pattern => Filter.StringMatch(type, pattern)))
        {
            return false;
        }
        return true;
    }

    public static bool ShouldIncludeEvent(this RoomEventFilter? filter, string sender, string type)
    {
        var eventFilter = filter is null ? null : new EventFilter
        {
            NotSenders = filter?.NotSenders,
            NotTypes = filter?.NotTypes,
            Senders = filter?.Senders,
            Types = filter?.Types
        };
        return eventFilter.ShouldIncludeEvent(sender, type);
    }

    public static bool ShouldIncludeEvent(
        this RoomEventFilter? filter,
        string sender,
        string type,
        JsonElement content)
    {
        if (!ShouldIncludeEvent(filter, sender, type))
        {
            return false;
        }
        if (filter?.ContainsUrl is bool containsUrl)
        {
            return content.TryGetProperty("url", out var _) == containsUrl;
        }
        return true;
    }

    public static bool ShouldIncludeEvent(this StateFilter? filter, string sender, string type)
    {
        var eventFilter = filter is null ? null : new EventFilter
        {
            NotSenders = filter?.NotSenders,
            NotTypes = filter?.NotTypes,
            Senders = filter?.Senders,
            Types = filter?.Types
        };
        return eventFilter.ShouldIncludeEvent(sender, type);
    }

    public static bool ShouldIncludeEvent(
        this StateFilter? filter,
        string sender,
        string type,
        JsonElement content)
    {
        if (!ShouldIncludeEvent(filter, sender, type))
        {
            return false;
        }
        if (filter?.ContainsUrl is bool containsUrl)
        {
            return content.TryGetProperty("url", out var _) == containsUrl;
        }
        return true;
    }

    public static IEnumerable<T> ApplyLimit<T>(this IEnumerable<T> source, EventFilter? filter)
    {
        if (filter?.Limit is int limit)
        {
            return source.Take(limit);
        }
        return source;
    }

    public static IAsyncEnumerable<T> ApplyLimit<T>(this IAsyncEnumerable<T> source, EventFilter? filter)
    {
        if (filter?.Limit is int limit)
        {
            return source.Take(limit);
        }
        return source;
    }

    public static IEnumerable<T> ApplyLimit<T>(this IEnumerable<T> source, RoomEventFilter? filter)
    {
        if (filter?.Limit is int limit)
        {
            return source.Take(limit);
        }
        return source;
    }

    public static IAsyncEnumerable<T> ApplyLimit<T>(this IAsyncEnumerable<T> source, RoomEventFilter? filter)
    {
        if (filter?.Limit is int limit)
        {
            return source.Take(limit);
        }
        return source;
    }

    public static IEnumerable<T> ApplyLimit<T>(this IEnumerable<T> source, StateFilter? filter)
    {
        if (filter?.Limit is int limit)
        {
            return source.Take(limit);
        }
        return source;
    }

    public static IAsyncEnumerable<T> ApplyLimit<T>(this IAsyncEnumerable<T> source, StateFilter? filter)
    {
        if (filter?.Limit is int limit)
        {
            return source.Take(limit);
        }
        return source;
    }

    public static bool ShouldIncludeRoomId(this RoomEventFilter? filter, string roomId)
    {
        if (filter is null)
        {
            return true;
        }
        if (filter.NotRooms is not null && filter.NotRooms.Contains(roomId))
        {
            return false;
        }
        if (filter.Rooms is not null && !filter.Rooms.Contains(roomId))
        {
            return false;
        }
        return true;
    }

    public static bool ShouldIncludeRoomId(this StateFilter? filter, string roomId)
    {
        if (filter is null)
        {
            return true;
        }
        if (filter.NotRooms is not null && filter.NotRooms.Contains(roomId))
        {
            return false;
        }
        if (filter.Rooms is not null && !filter.Rooms.Contains(roomId))
        {
            return false;
        }
        return true;
    }

    public static bool ShouldIncludeRoomId(this RoomFilter? filter, string roomId)
    {
        if (filter is null)
        {
            return true;
        }
        if (filter.NotRooms is not null && filter.NotRooms.Contains(roomId))
        {
            return false;
        }
        if (filter.Rooms is not null && !filter.Rooms.Contains(roomId))
        {
            return false;
        }
        return true;
    }
}
