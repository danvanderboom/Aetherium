using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aetherium.Model;

namespace Aetherium.Server.Agents.Tools.Economy
{
    /// <summary>
    /// The player's window on the economy: quote the settlement market you're standing in, then buy or
    /// sell goods against it (economy Item 2b). Buy/sell route through the mutation gateway so they hit
    /// the canonical world (grain-bound sessions) or the session world (headless), exactly like move and
    /// pickup. Quote is a read of the market under the player.
    /// </summary>
    [AgentTool("market", "Quote/buy/sell goods at the settlement market you're standing in",
        Categories = new[] { "economy", "interaction" },
        RequiredCapabilities = new[] { "interaction" })]
    public class MarketTool : IAgentTool
    {
        public string ToolId => "market";
        public string Description => "Trade at the settlement market under you (action: quote | buy | sell; buy/sell take good + quantity)";
        public IEnumerable<string> Categories => new[] { "economy", "interaction" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };

        public ToolParameterSchema GetParameterSchema() => new()
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["action"] = new() { Type = "string", Description = "quote | buy | sell", AllowedValues = new() { "quote", "buy", "sell" } },
                ["good"] = new() { Type = "string", Description = "Good name (required for buy/sell)" },
                ["quantity"] = new() { Type = "number", Description = "Units to trade (buy/sell; default 1)", DefaultValue = 1 },
            },
            Required = new() { "action" },
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            var action = (args.TryGetValue("action", out var a) ? a?.ToString() : "quote")?.Trim().ToLowerInvariant() ?? "quote";

            if (action == "quote")
                return Quote(context);

            if (action != "buy" && action != "sell")
                return ToolExecutionResult.Error($"unknown action '{action}' (use quote, buy, or sell)");

            if (!args.TryGetValue("good", out var goodObj) || string.IsNullOrWhiteSpace(goodObj?.ToString()))
                return ToolExecutionResult.Error("buy/sell require a 'good'");
            var good = goodObj!.ToString()!;

            double qty = 1;
            if (args.TryGetValue("quantity", out var qObj) &&
                double.TryParse(qObj?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                qty = parsed;

            if (context.MutationGateway is null)
                return ToolExecutionResult.Error("No execution context available");

            var r = await context.MutationGateway.TradeAsync(action, good, qty);
            if (!r.Success)
                return ToolExecutionResult.Error(r.Reason ?? "trade failed");

            var verb = action == "buy" ? "Bought" : "Sold";
            return ToolExecutionResult.Ok(
                $"{verb} {r.Quantity:0.##} {r.Good} @ {r.UnitPrice:0.##} = {r.Total:0.##}; wallet {r.WalletAfter:0.##}");
        }

        private static ToolExecutionResult Quote(ToolExecutionContext context)
        {
            var session = context.Session;
            if (session?.Player is null)
                return ToolExecutionResult.Error("no player in session");

            var loc = session.Player.Get<Aetherium.Components.WorldLocation>();
            var resolved = Aetherium.Server.Economy.MarketTrade.ResolveMarketAt(session.World, loc);
            if (resolved is not { } r)
                return ToolExecutionResult.Error("no market here — stand in a settlement to trade");

            var sb = new StringBuilder();
            sb.Append($"Market at {r.Settlement.Name} ({r.Settlement.Tier}): ");
            sb.Append(string.Join(", ", r.Market.Goods.OrderBy(g => g.Key)
                .Select(g => $"{g.Key} {g.Value.Price:0.##}")));

            double wallet = session.Player.Has<Aetherium.Components.Wallet>()
                ? session.Player.Get<Aetherium.Components.Wallet>().Currency : 0;
            sb.Append($"; wallet {wallet:0.##}");

            if (session.Player.Has<Aetherium.Components.GoodsHold>())
            {
                var carried = session.Player.Get<Aetherium.Components.GoodsHold>().Units
                    .Where(u => u.Value > 0).ToList();
                if (carried.Count > 0)
                    sb.Append("; carrying " + string.Join(", ", carried.Select(u => $"{u.Value:0.##} {u.Key}")));
            }
            return ToolExecutionResult.Ok(sb.ToString());
        }
    }
}
