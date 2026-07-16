namespace Aetherium.Topology
{
    /// <summary>
    /// One outgoing edge of a cell. <see cref="DirectionIndex"/> is stable per
    /// (topology, cell) in-process but is <b>never persisted or sent on the wire</b> —
    /// persist headings (degrees) or locations instead (docs/grid-topologies.md,
    /// invariant 6). <see cref="HeadingDegrees"/> is the compass heading (0 = north,
    /// clockwise; engine convention north = -Y) of crossing this edge in the local
    /// embedding.
    /// </summary>
    public readonly record struct EdgeStep(int DirectionIndex, GridCoord Target, int HeadingDegrees);

    /// <summary>
    /// Result of resolving a relative move (<see cref="Aetherium.Model.RelativeDirection"/>)
    /// against a cell's outgoing edges. <see cref="NewHeadingDegrees"/> is the chosen
    /// edge's heading — the caller decides whether the actor's heading updates (the
    /// square engine today never updates heading on movement, only on rotate).
    /// </summary>
    public readonly record struct RelativeMoveResolution(
        bool Success, EdgeStep Step, int NewHeadingDegrees, string? FailReason);
}
