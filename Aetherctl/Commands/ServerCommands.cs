using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Auth;
using Aetherctl.Config;
using Aetherctl.SignalR;

namespace Aetherctl.Commands
{
    public static class ServerCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var serverCmd = new Command("server", "Manage server connections");

            // server add
            var addCmd = new Command("add", "Add a new server configuration");
            var addNameArg = new Argument<string>("name", "Server name");
            var addUrlOpt = new Option<string>("--url", "Base URL of the server") { IsRequired = true };
            var addTenantOpt = new Option<string>("--tenant", "Azure AD B2C tenant (e.g., mytenant.onmicrosoft.com)") { IsRequired = true };
            var addPolicyOpt = new Option<string>("--policy", "B2C sign-up sign-in policy ID") { IsRequired = true };
            var addClientIdOpt = new Option<string>("--client-id", "Azure AD B2C client ID") { IsRequired = true };
            var addScopeOpt = new Option<string>("--scope", "API scope (e.g., api://{clientId}/.default)") { IsRequired = true };
            
            addCmd.AddArgument(addNameArg);
            addCmd.AddOption(addUrlOpt);
            addCmd.AddOption(addTenantOpt);
            addCmd.AddOption(addPolicyOpt);
            addCmd.AddOption(addClientIdOpt);
            addCmd.AddOption(addScopeOpt);
            
            addCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var name = parseResult.GetValueForArgument(addNameArg);
                    var url = parseResult.GetValueForOption(addUrlOpt);
                    var tenant = parseResult.GetValueForOption(addTenantOpt);
                    var policy = parseResult.GetValueForOption(addPolicyOpt);
                    var clientId = parseResult.GetValueForOption(addClientIdOpt);
                    var scope = parseResult.GetValueForOption(addScopeOpt);

                    var serverConfig = new ServerConfig
                    {
                        Name = name,
                        BaseUrl = url!,
                        B2C = new B2CConfig
                        {
                            Tenant = tenant!,
                            Policy = policy!,
                            ClientId = clientId!,
                            Scopes = scope!
                        }
                    };

                    ConfigManager.AddServer(serverConfig);
                    Common.WriteSuccess(ctx.ParseResult, $"Server '{name}' added successfully");
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to add server: {ex.Message}");
                }
            });

            // server list
            var listCmd = new Command("list", "List all configured servers");
            listCmd.SetHandler((InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var servers = ConfigManager.ListServers();
                    var current = ConfigManager.Load().CurrentServer;

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            servers = servers.Select(s => new { name = s, isCurrent = s == current })
                        });
                    }
                    else if (!Common.IsQuiet(parseResult))
                    {
                        if (servers.Count == 0)
                        {
                            Console.WriteLine("No servers configured.");
                        }
                        else
                        {
                            Console.WriteLine("Configured servers:");
                            foreach (var server in servers)
                            {
                                var marker = server == current ? " (current)" : "";
                                Console.WriteLine($"  {server}{marker}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to list servers: {ex.Message}");
                }
            });

            // server remove
            var removeCmd = new Command("remove", "Remove a server configuration");
            var removeNameArg = new Argument<string>("name", "Server name to remove");
            removeCmd.AddArgument(removeNameArg);
            removeCmd.SetHandler((InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var name = parseResult.GetValueForArgument(removeNameArg);

                    ConfigManager.RemoveServer(name);
                    Common.WriteSuccess(parseResult, $"Server '{name}' removed successfully");
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to remove server: {ex.Message}");
                }
            });

            // server connect
            var connectCmd = new Command("connect", "Connect to a server (sets as current)");
            var connectNameArg = new Argument<string>("name", "Server name to connect to");
            connectCmd.AddArgument(connectNameArg);
            connectCmd.SetHandler((InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var name = parseResult.GetValueForArgument(connectNameArg);

                    var server = ConfigManager.GetServer(name);
                    if (server == null)
                    {
                        Common.WriteError(parseResult, $"Server '{name}' not found");
                        return;
                    }

                    ConfigManager.SetCurrentServer(name);
                    Common.WriteSuccess(parseResult, $"Connected to server '{name}'");
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to connect to server: {ex.Message}");
                }
            });

            // login
            var loginCmd = new Command("login", "Authenticate with Azure AD B2C");
            var loginUserOpt = new Option<string?>("--username", "Username for ROPC flow (CI/testing only)");
            var loginPassOpt = new Option<string?>("--password", "Password for ROPC flow (CI/testing only)");
            loginCmd.AddOption(loginUserOpt);
            loginCmd.AddOption(loginPassOpt);
            loginCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var server = ConfigManager.GetCurrentServer();
                    if (server == null)
                    {
                        Common.WriteError(parseResult, "No server connected. Use 'server connect <name>' first.");
                        return;
                    }

                    var username = parseResult.GetValueForOption(loginUserOpt);
                    var password = parseResult.GetValueForOption(loginPassOpt);

                    await using var authService = new AuthService(
                        server.B2C.Tenant,
                        server.B2C.Policy,
                        server.B2C.ClientId,
                        server.B2C.Scopes);

                    string token;
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        if (Environment.GetEnvironmentVariable("B2C_ALLOW_ROPC") != "1")
                        {
                            Common.WriteError(parseResult, "ROPC flow requires B2C_ALLOW_ROPC=1 environment variable");
                            return;
                        }
                        token = await authService.AcquireTokenROPCAsync(username!, password!);
                    }
                    else
                    {
                        token = await authService.AcquireTokenDeviceCodeAsync();
                    }

                    // Test connection
                    await using var client = new ManagementClient(server.BaseUrl, async () => token);
                    await client.ConnectAsync();
                    var pingResult = await client.PingAsync();

                    Common.WriteSuccess(parseResult, "Login successful");
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = true, message = "Login successful" });
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Login failed: {ex.Message}");
                }
            });

            // status
            var statusCmd = new Command("status", "Get server status");
            statusCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var server = ConfigManager.GetCurrentServer();
                    if (server == null)
                    {
                        Common.WriteError(parseResult, "No server connected. Use 'server connect <name>' first.");
                        return;
                    }

                    await using var authService = new AuthService(
                        server.B2C.Tenant,
                        server.B2C.Policy,
                        server.B2C.ClientId,
                        server.B2C.Scopes);

                    var token = await authService.AcquireTokenDeviceCodeAsync();
                    await using var client = new ManagementClient(server.BaseUrl, async () => token);
                    await client.ConnectAsync();
                    var info = await client.GetServerInfoAsync();

                    Common.WriteOutput(parseResult, info);
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to get status: {ex.Message}");
                }
            });

            serverCmd.AddCommand(addCmd);
            serverCmd.AddCommand(listCmd);
            serverCmd.AddCommand(removeCmd);
            serverCmd.AddCommand(connectCmd);
            root.AddCommand(loginCmd);
            root.AddCommand(statusCmd);
            root.AddCommand(serverCmd);
        }
    }
}

