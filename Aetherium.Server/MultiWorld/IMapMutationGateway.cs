using System.Threading.Tasks;
using Aetherium.Model;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Contract for applying gameplay mutations to a session's world.
    ///
    /// <para>
    /// Phase 2a (this change) introduces the abstraction so tools no longer reach
    /// directly into <c>session.World</c> or call <c>InteractionSystem</c>
    /// methods. The only implementation today is <see cref="LocalMutationGateway"/>,
    /// which delegates to the existing in-process logic and changes nothing about
    /// runtime behavior.
    /// </para>
    ///
    /// <para>
    /// Phase 2b+c will add <c>GrainMutationGateway</c> which routes each call to
    /// <c>IGameMapGrain</c> methods so mutations apply to the grain's authoritative
    /// world and propagate to all sessions in the same map via SignalR group
    /// fan-out. Tool code stays unchanged across both phases — the gateway hides
    /// the choice.
    /// </para>
    /// </summary>
    public interface IMapMutationGateway
    {
        Task<MoveResult> MoveAsync(Aetherium.Model.RelativeDirection direction, int distance);

        Task<RotateResult> RotateAsync(int degrees);

        Task<ChangeLevelResult> ChangeLevelAsync(int deltaZ);

        Task<InteractionResultDto> PickupAsync(string targetEntityId);

        Task<InteractionResultDto> DropAsync(string itemEntityId);

        Task<InteractionResultDto> UseAsync(string itemEntityId, string onEntityId, string? usageId = null);

        Task<InteractionResultDto> OpenAsync(string targetEntityId);

        Task<InteractionResultDto> CloseAsync(string targetEntityId);

        Task<AttackResultDto> AttackAsync(string targetEntityId);

        /// <summary>Buy or sell a good against the settlement market the player is standing in
        /// (<paramref name="side"/> = "buy"/"sell"). Economy Item 2b.</summary>
        Task<TradeResultDto> TradeAsync(string side, string good, double quantity);
    }

    /// <summary>Result of <see cref="IMapMutationGateway.TradeAsync"/>: whether the trade filled, why not,
    /// and — when it did — how much moved at what price, plus the trader's wallet balance after.</summary>
    [Orleans.GenerateSerializer]
    public class TradeResultDto
    {
        [Orleans.Id(0)] public bool Success { get; set; }
        [Orleans.Id(1)] public string? Reason { get; set; }
        [Orleans.Id(2)] public string Side { get; set; } = string.Empty;
        [Orleans.Id(3)] public string Good { get; set; } = string.Empty;
        [Orleans.Id(4)] public double Quantity { get; set; }
        [Orleans.Id(5)] public double UnitPrice { get; set; }
        [Orleans.Id(6)] public double Total { get; set; }
        [Orleans.Id(7)] public double WalletAfter { get; set; }

        public static TradeResultDto Fail(string reason) => new() { Success = false, Reason = reason };
    }

    /// <summary>
    /// Result of <see cref="IMapMutationGateway.MoveAsync"/>. Movement is best-effort
    /// in the current engine — the gateway returns success unless the session has
    /// no view location. Future iterations may surface "blocked by wall" or
    /// "blocked by other character" failure reasons.
    /// </summary>
    [Orleans.GenerateSerializer]
    public class MoveResult
    {
        [Orleans.Id(0)] public bool Success { get; set; }
        [Orleans.Id(1)] public string? Reason { get; set; }

        public static MoveResult Ok() => new() { Success = true };
        public static MoveResult Fail(string reason) => new() { Success = false, Reason = reason };
    }

    /// <summary>Result of <see cref="IMapMutationGateway.RotateAsync"/>.</summary>
    [Orleans.GenerateSerializer]
    public class RotateResult
    {
        [Orleans.Id(0)] public bool Success { get; set; }
        [Orleans.Id(1)] public string? Reason { get; set; }
        [Orleans.Id(2)] public int HeadingDegrees { get; set; }

        public static RotateResult Ok(int headingDegrees) => new() { Success = true, HeadingDegrees = headingDegrees };
        public static RotateResult Fail(string reason) => new() { Success = false, Reason = reason };
    }

    /// <summary>Result of <see cref="IMapMutationGateway.ChangeLevelAsync"/>.</summary>
    [Orleans.GenerateSerializer]
    public class ChangeLevelResult
    {
        [Orleans.Id(0)] public bool Success { get; set; }
        [Orleans.Id(1)] public string? Reason { get; set; }
        [Orleans.Id(2)] public int NewZ { get; set; }

        public static ChangeLevelResult Ok(int newZ) => new() { Success = true, NewZ = newZ };
        public static ChangeLevelResult Fail(string reason) => new() { Success = false, Reason = reason };
    }
}
