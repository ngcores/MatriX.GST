using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MatriX.GST.Services;

public class StatsService
{
    readonly ConcurrentDictionary<DateTime, ConcurrentDictionary<string, UserHourStats>> hours = new();

    static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    public StatsScope TrackRequest(string userId, string ip, string infohash)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        var hour = CurrentHour();
        var stats = GetStats(hour, userId);
        stats.Add(ip, infohash);
        Prune();

        return new StatsScope(hour, userId);
    }

    public void AddBytes(StatsScope scope, ulong bytes)
    {
        if (scope == null || bytes == 0)
            return;

        var stats = GetStats(scope.hour, scope.userid);
        stats.AddBytes(bytes);
        Prune();
    }

    public string GetJson()
    {
        Prune();

        var snapshot = hours
            .OrderByDescending(x => x.Key)
            .Select(x => new StatsHour
            {
                hour = x.Key,
                users = x.Value.Values
                    .Select(y => y.Snapshot())
                    .OrderBy(y => y.userid)
                    .ToArray()
            })
            .ToArray();

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    UserHourStats GetStats(DateTime hour, string userId)
    {
        var users = hours.GetOrAdd(hour, _ => new ConcurrentDictionary<string, UserHourStats>());
        return users.GetOrAdd(userId, _ => new UserHourStats(userId));
    }

    static DateTime CurrentHour()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
    }

    void Prune()
    {
        var cutoff = CurrentHour().AddHours(-24);

        foreach (var key in hours.Keys)
        {
            if (key < cutoff)
                hours.TryRemove(key, out _);
        }
    }
}

public class StatsScope
{
    public StatsScope(DateTime hour, string userid)
    {
        this.hour = hour;
        this.userid = userid;
    }

    public DateTime hour { get; }

    public string userid { get; }
}

public class StatsHour
{
    public DateTime hour { get; set; }

    public StatsUser[] users { get; set; }
}

public class StatsUser
{
    public string userid { get; set; }

    public string[] ips { get; set; }

    public ulong bytes { get; set; }

    public string[] hashs { get; set; }
}

class UserHourStats
{
    readonly object sync = new();
    readonly HashSet<string> ips = new();
    readonly HashSet<string> hashs = new();

    public UserHourStats(string userId)
    {
        userid = userId;
    }

    public string userid { get; }

    public ulong bytes { get; private set; }

    public void Add(string ip, string infohash)
    {
        lock (sync)
        {
            if (!string.IsNullOrEmpty(ip))
                ips.Add(ip);

            if (!string.IsNullOrEmpty(infohash))
                hashs.Add(infohash);
        }
    }

    public void AddBytes(ulong value)
    {
        lock (sync)
        {
            bytes += value;
        }
    }

    public StatsUser Snapshot()
    {
        lock (sync)
        {
            return new StatsUser
            {
                userid = userid,
                ips = ips.OrderBy(x => x).ToArray(),
                bytes = bytes,
                hashs = hashs.OrderBy(x => x).ToArray()
            };
        }
    }
}
