## 1. Shared Model Implementation
- [x] 1.1 Create Aetherium.Model project with shared DTOs
- [x] 1.2 Define PerceptionDto with player location, heading, visuals
- [x] 1.3 Define VisualDto with location, terrain, light level
- [x] 1.4 Define WorldLocationDto, TileTypeDto, RectangleDto
- [x] 1.5 Define shared enums (WorldDirection, RelativeDirection)
- [x] 1.6 Define GameStateDto for initial state

## 2. Server Implementation
- [x] 2.1 Convert Aetherium.Server to ASP.NET Core Web app
- [x] 2.2 Add SignalR package reference
- [x] 2.3 Copy game engine (World, Entities, Components, Systems) to server
- [x] 2.4 Implement GameHub with command methods
- [x] 2.5 Implement GameSession for per-client state
- [x] 2.6 Implement GameSessionManager for session tracking
- [x] 2.7 Implement PerceptionService to compute and serialize perception
- [x] 2.8 Create MappingExtensions for DTO conversions
- [x] 2.9 Configure ASP.NET Core host with SignalR

## 3. Client Implementation
- [x] 3.1 Add SignalR client package to Aetherium.Console
- [x] 3.2 Create GameClient for SignalR connection management
- [x] 3.3 Create ClientConsoleDungeonGame for perception-based game loop
- [x] 3.4 Create ClientConsoleMapView for rendering from PerceptionDto
- [x] 3.5 Create ClientMappingExtensions for enum conversions
- [x] 3.6 Update Program.cs to connect to server
- [x] 3.7 Implement input handling to send commands

## 4. Integration
- [x] 4.1 Wire client perception updates to rendering
- [x] 4.2 Wire client input to server commands
- [x] 4.3 Test server can start and listen on port 5000
- [x] 4.4 Test client can connect to server
- [x] 4.5 Test perception updates flow correctly

## 5. Testing
- [x] 5.1 Add projects to solution
- [x] 5.2 Build all projects
- [x] 5.3 Run existing tests (verify no regression)
- [x] 5.4 Create ClientServerCommunicationTests
- [x] 5.5 Test perception computation and serialization
- [x] 5.6 Test session management
- [x] 5.7 Test command processing
- [x] 5.8 Test FOV and lighting integration
- [x] 5.9 Test client rendering from perception
- [x] 5.10 Manual end-to-end test

## 6. Documentation
- [x] 6.1 Create CLIENT_SERVER_README.md
- [x] 6.2 Document how to run server and client
- [x] 6.3 Document architecture overview
- [x] 6.4 Update main README with architecture information


