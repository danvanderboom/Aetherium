using System;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    /// <summary>
    /// Entity representing a lore fragment (book, inscription, etc.) with historical text.
    /// </summary>
    public class LoreFragment : Entity
    {
        public LoreFragment() : base()
        {
            Set(new Inscription());
            Set(new Carriable());
            
            // Default visual representation
            var tile = Get<Tile>();
            if (tile != null)
            {
                tile.Character = '?'; // Will be customized by lore generator
            }
        }

        /// <summary>
        /// Sets the inscription content for this lore fragment.
        /// </summary>
        public void SetInscription(string title, string text, string topic, string author = "", string era = "")
        {
            var inscription = Get<Inscription>();
            if (inscription != null)
            {
                inscription.Title = title;
                inscription.Text = text;
                inscription.Topic = topic;
                inscription.Author = author;
                inscription.Era = era;
            }

            // Update visual based on type
            var tile = Get<Tile>();
            if (tile != null)
            {
                // Different symbols for different lore types
                tile.Character = topic switch
                {
                    "history" => 'H',
                    "legend" => 'L',
                    "journal" => 'J',
                    "prophecy" => 'P',
                    _ => '?'
                };
            }
        }
    }
}

