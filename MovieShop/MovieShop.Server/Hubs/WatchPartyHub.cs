using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace MovieShop.Server.Hubs
{
    [Authorize]
    public class WatchPartyHub : Hub
    {
        private static readonly ConcurrentDictionary<string, WatchPartyRoom> _rooms = new();

        // Create a new room as host, returns the 6-char room code
        public async Task<string> CreateRoom(int movieId)
        {
            var code = GenerateRoomCode();
            var room = new WatchPartyRoom
            {
                Code = code,
                MovieId = movieId,
                HostConnectionId = Context.ConnectionId,
            };
            room.Members[Context.ConnectionId] = Context.User?.Identity?.Name ?? "Host";
            _rooms[code] = room;

            await Groups.AddToGroupAsync(Context.ConnectionId, code);
            return code;
        }

        // Join an existing room, receive current room state
        public async Task JoinRoom(string roomCode, string displayName)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                await Clients.Caller.SendAsync("Error", "Room not found");
                return;
            }

            room.Members[Context.ConnectionId] = displayName;
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            // Notify others
            await Clients.OthersInGroup(roomCode).SendAsync("UserJoined", displayName, room.Members.Count);

            // Send current playback state to the new joiner
            // Estimate current position if video is playing
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

        // Get current room state (used for re-sync button)
        // Estimates current position based on elapsed time since last update
        public object? GetRoomState(string roomCode)
        {
            if (!_rooms.TryGetValue(roomCode, out var room)) return null;
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

        // Host broadcasts play event with latency compensation timestamp
        public async Task Play(string roomCode, double position)
        {
            if (!_rooms.TryGetValue(roomCode, out var room)) return;
            if (room.HostConnectionId != Context.ConnectionId) return;

            room.IsPlaying = true;
            room.Position = position;
            room.LastPositionUpdate = DateTime.UtcNow;

            await Clients.OthersInGroup(roomCode).SendAsync("Play", position,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        // Host broadcasts pause event
        public async Task Pause(string roomCode, double position)
        {
            if (!_rooms.TryGetValue(roomCode, out var room)) return;
            if (room.HostConnectionId != Context.ConnectionId) return;

            room.IsPlaying = false;
            room.Position = position;

            await Clients.OthersInGroup(roomCode).SendAsync("Pause", position);
        }

        // Host broadcasts seek event with latency compensation timestamp
        public async Task Seek(string roomCode, double position)
        {
            if (!_rooms.TryGetValue(roomCode, out var room)) return;
            if (room.HostConnectionId != Context.ConnectionId) return;

            room.Position = position;

            await Clients.OthersInGroup(roomCode).SendAsync("Seek", position,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        // Send chat message to all room members
        public async Task SendMessage(string roomCode, string message)
        {
            if (!_rooms.TryGetValue(roomCode, out var room)) return;
            if (!room.Members.ContainsKey(Context.ConnectionId)) return;

            var userName = room.Members[Context.ConnectionId];
            await Clients.Group(roomCode).SendAsync("ChatMessage", userName, message);
        }

        // Handle disconnection: remove from room, transfer host if needed
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var (code, room) in _rooms)
            {
                if (!room.Members.TryRemove(Context.ConnectionId, out var userName))
                    continue;

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, code);

                if (room.Members.IsEmpty)
                {
                    _rooms.TryRemove(code, out _);
                }
                else
                {
                    // Transfer host if the host disconnected
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
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars
            var random = new Random();
            string code;
            do
            {
                code = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            } while (_rooms.ContainsKey(code));
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
}
