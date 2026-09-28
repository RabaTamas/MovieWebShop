using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MovieShop.CatalogService.Hubs;

/// <summary>
/// Szinkronizált közös filmnézés — a monolit WatchPartyHub-jával azonos működés.
///
/// A hub a Catalog Service-ben él, mert a Watch Party a lejátszáshoz tartozik:
/// ugyanez a service adja a HLS streamet. A szobaállapot a példány memóriájában
/// van, ezért több példányra skálázva Redis backplane kellene.
/// </summary>
[Authorize]
public class WatchPartyHub : Hub
{
    private static readonly ConcurrentDictionary<string, WatchPartyRoom> Rooms = new();

    // Új szoba létrehozása gazdaként, visszaadja a 6 karakteres szobakódot
    public async Task<string> CreateRoom(int movieId)
    {
        var code = GenerateRoomCode();
        var room = new WatchPartyRoom
        {
            Code = code,
            MovieId = movieId,
            HostConnectionId = Context.ConnectionId
        };

        room.Members[Context.ConnectionId] = Context.User?.Identity?.Name ?? "Host";
        Rooms[code] = room;

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return code;
    }

    // Csatlakozás meglévő szobához, a csatlakozó megkapja az aktuális állapotot
    public async Task JoinRoom(string roomCode, string displayName)
    {
        if (!Rooms.TryGetValue(roomCode, out var room))
        {
            await Clients.Caller.SendAsync("Error", "Room not found");
            return;
        }

        room.Members[Context.ConnectionId] = displayName;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

        await Clients.OthersInGroup(roomCode).SendAsync("UserJoined", displayName, room.Members.Count);

        // Ha a videó megy, a pozíciót az utolsó frissítés óta eltelt idővel becsüljük
        var estimatedPosition = room.IsPlaying
            ? room.Position + (DateTime.UtcNow - room.LastPositionUpdate).TotalSeconds
            : room.Position;

        await Clients.Caller.SendAsync("RoomState", new
        {
            room.MovieId,
            room.IsPlaying,
            Position = estimatedPosition,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Members = room.Members.Values.ToList(),
            IsHost = room.HostConnectionId == Context.ConnectionId
        });
    }

    // Aktuális szobaállapot (újraszinkronizáló gombhoz)
    public object? GetRoomState(string roomCode)
    {
        if (!Rooms.TryGetValue(roomCode, out var room)) return null;

        var estimatedPosition = room.IsPlaying
            ? room.Position + (DateTime.UtcNow - room.LastPositionUpdate).TotalSeconds
            : room.Position;

        return new
        {
            room.IsPlaying,
            Position = estimatedPosition,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    // A gazda lejátszást indít, időbélyeggel a késleltetés-kompenzációhoz
    public async Task Play(string roomCode, double position)
    {
        if (!Rooms.TryGetValue(roomCode, out var room)) return;
        if (room.HostConnectionId != Context.ConnectionId) return;

        room.IsPlaying = true;
        room.Position = position;
        room.LastPositionUpdate = DateTime.UtcNow;

        await Clients.OthersInGroup(roomCode).SendAsync("Play", position,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public async Task Pause(string roomCode, double position)
    {
        if (!Rooms.TryGetValue(roomCode, out var room)) return;
        if (room.HostConnectionId != Context.ConnectionId) return;

        room.IsPlaying = false;
        room.Position = position;

        await Clients.OthersInGroup(roomCode).SendAsync("Pause", position);
    }

    public async Task Seek(string roomCode, double position)
    {
        if (!Rooms.TryGetValue(roomCode, out var room)) return;
        if (room.HostConnectionId != Context.ConnectionId) return;

        room.Position = position;

        await Clients.OthersInGroup(roomCode).SendAsync("Seek", position,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    // Csevegőüzenet a szoba minden tagjának
    public async Task SendMessage(string roomCode, string message)
    {
        if (!Rooms.TryGetValue(roomCode, out var room)) return;
        if (!room.Members.ContainsKey(Context.ConnectionId)) return;

        var userName = room.Members[Context.ConnectionId];
        await Clients.Group(roomCode).SendAsync("ChatMessage", userName, message);
    }

    // Lecsatlakozás: kilépés a szobából, szükség esetén gazdaátadás
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (code, room) in Rooms)
        {
            if (!room.Members.TryRemove(Context.ConnectionId, out var userName))
                continue;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, code);

            if (room.Members.IsEmpty)
            {
                Rooms.TryRemove(code, out _);
            }
            else
            {
                if (room.HostConnectionId == Context.ConnectionId)
                {
                    room.HostConnectionId = room.Members.Keys.First();
                    var newHostName = room.Members[room.HostConnectionId];
                    await Clients.Group(code).SendAsync("HostChanged", newHostName);
                }

                await Clients.Group(code).SendAsync("UserLeft", userName, room.Members.Count);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // félreolvasható karakterek nélkül
        string code;
        do
        {
            code = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        } while (Rooms.ContainsKey(code));

        return code;
    }
}

public class WatchPartyRoom
{
    public string Code { get; set; } = "";
    public int MovieId { get; set; }
    public string HostConnectionId { get; set; } = "";
    public ConcurrentDictionary<string, string> Members { get; set; } = new();
    public bool IsPlaying { get; set; }
    public double Position { get; set; }
    public DateTime LastPositionUpdate { get; set; } = DateTime.UtcNow;
}
