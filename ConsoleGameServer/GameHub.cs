using System;
using System.Threading.Tasks;
using ConsoleGame.WorldBuilders;
using ConsoleGameModel;
using Microsoft.AspNetCore.SignalR;

namespace ConsoleGameServer
{
    public class GameHub : Hub
    {
        private readonly GameSessionManager sessionManager;
        private readonly InteractionSystem interactionSystem = new InteractionSystem();

        public GameHub(GameSessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionId}");

            // Select world builder (env-flag gated)
            WorldBuilder builder;
            var audioTest = Environment.GetEnvironmentVariable("AUDIO_TEST");
            if (string.Equals(audioTest, "1", StringComparison.OrdinalIgnoreCase))
                builder = new AudioTestWorldBuilder();
            else
                builder = new FovDiagnosticWorldBuilder("open_space");

            // Create a new game session for this client
            var session = sessionManager.CreateSession(Context.ConnectionId, builder);

            // Send initial game state (without world coordinates)
            var initialState = new GameStateDto
            {
                PlayerId = session.SessionId,
                // DO NOT send PlayerLocation - client should not know absolute world coordinates
                PlayerHeading = session.Heading.ToDto()
            };

            await Clients.Caller.SendAsync("ReceiveGameState", initialState);

            // Send initial perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
            sessionManager.RemoveSession(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task MovePlayer(ConsoleGameModel.RelativeDirection direction, int distance)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.MoveView(direction, distance);

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        public async Task RotatePlayer(bool clockwise)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.RotateView(clockwise);

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        public async Task ChangeLevel(int deltaZ)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.ChangeLevel(deltaZ);

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        public async Task JumpToRandomLocation()
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.JumpToRandomLocation();

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        public async Task<InteractionResultDto> Pickup(string targetEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryPickup(session, targetEntityId);
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        public async Task<InteractionResultDto> Drop(string itemEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryDrop(session, itemEntityId);
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        public async Task<InteractionResultDto> Use(string itemEntityId, string onEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryUse(session, itemEntityId, onEntityId);
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        public async Task<InteractionResultDto> Open(string targetEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryOpen(session, targetEntityId);
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        public async Task<InteractionResultDto> Close(string targetEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryClose(session, targetEntityId);
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }
    }
}

