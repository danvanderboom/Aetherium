using System.Collections.Generic;

namespace Aetherium.Model.Accessibility
{
    /// <summary>
    /// An abstract, input-device-agnostic game action (engine gap-analysis §4.13): "every game
    /// action has an abstract ActionIntent id; renderers may bind it to keyboard, gamepad, touch,
    /// gaze, sip-and-puff — none of this leaks into the server."
    /// </summary>
    public class ActionIntent
    {
        public string Id { get; }
        public string Description { get; }

        public ActionIntent(string id, string description)
        {
            Id = id;
            Description = description;
        }
    }

    /// <summary>Registry of <see cref="ActionIntent"/>s by id.</summary>
    public class ActionIntentCatalog
    {
        private readonly Dictionary<string, ActionIntent> _intents = new();

        public bool Add(ActionIntent intent) => _intents.TryAdd(intent.Id, intent);

        public bool TryGet(string id, out ActionIntent? intent) => _intents.TryGetValue(id, out intent);
    }

    /// <summary>
    /// A seed catalog covering the game actions that already exist today (move, attack, the
    /// open/close/pickup/drop/use interaction set) — grounded in real capabilities, per this
    /// project's established seed-from-real-code convention (see <c>DefaultContentAtlas</c>).
    /// </summary>
    public static class DefaultActionIntents
    {
        public static ActionIntentCatalog Build()
        {
            var catalog = new ActionIntentCatalog();

            catalog.Add(new ActionIntent("move", "Move one step in a direction"));
            catalog.Add(new ActionIntent("attack", "Attack an adjacent target"));
            catalog.Add(new ActionIntent("interact_open", "Open a door or container"));
            catalog.Add(new ActionIntent("interact_close", "Close a door or container"));
            catalog.Add(new ActionIntent("pickup", "Pick up an item"));
            catalog.Add(new ActionIntent("drop", "Drop a carried item"));
            catalog.Add(new ActionIntent("use_item", "Use a carried item"));
            catalog.Add(new ActionIntent("toggle_inventory", "Open or close the inventory view"));

            return catalog;
        }
    }
}
