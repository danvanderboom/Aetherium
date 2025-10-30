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

        public GameHub(GameSessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionId}");

            // Create a new game session for this client
            // Using FovDiagnosticWorldBuilder as default (matches current Program.cs)
            var session = sessionManager.CreateSession(Context.ConnectionId, new FovDiagnosticWorldBuilder("open_space"));

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
    }
}

