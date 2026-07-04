extern alias Console;

using System.Collections.Generic;
using System.Linq;
using Aetherium.Model;
using NUnit.Framework;
using CompassMode = Console::Aetherium.Rendering.CompassMode;
using CompassRenderData = Console::Aetherium.Rendering.CompassRenderData;
using CompassWidget = Console::Aetherium.Rendering.Widgets.CompassWidget;
using InventoryRenderData = Console::Aetherium.Rendering.InventoryRenderData;
using InventoryWidget = Console::Aetherium.Rendering.Widgets.InventoryWidget;
using SpectreConsoleRenderer = Console::Aetherium.Rendering.SpectreConsoleRenderer;
using ThemeConfig = Console::Aetherium.Rendering.Themes.ThemeConfig;

namespace Aetherium.Test.Client
{
    /// <summary>
    /// Client-side unit tests (P2-6) for the console client's widgets and the
    /// renderer's markup builders, via the Console extern alias — the same
    /// approach AudioDirectorTests uses. Covers the P1-11 fixes: inventory
    /// labels/key-ids are escaped before being fed to Spectre Markup, and the
    /// status message has a render path.
    /// </summary>
    [TestFixture]
    public class ClientWidgetAndMarkupTests
    {
        private static InventoryDto Inventory(params ItemDto[] items) => new InventoryDto
        {
            Capacity = 10,
            Items = items.ToList(),
        };

        // ---------- InventoryWidget render data ----------

        [Test]
        public void InventoryWidget_Formats_Items_And_Appends_KeyId()
        {
            var widget = new InventoryWidget(ThemeConfig.Default);
            widget.UpdateInventoryData(Inventory(
                new ItemDto { Label = "Torch" },
                new ItemDto { Label = "Brass Key", KeyId = "gold-key" }));

            var data = (InventoryRenderData)widget.GetRenderData();

            Assert.That(widget.IsVisible, Is.True);
            Assert.That(data.Count, Is.EqualTo(2));
            Assert.That(data.Capacity, Is.EqualTo(10));
            Assert.That(data.Items, Is.EqualTo(new[] { "Torch", "Brass Key [gold-key]" }));
        }

        [Test]
        public void InventoryWidget_Is_Hidden_When_Empty_Or_Null()
        {
            var widget = new InventoryWidget(ThemeConfig.Default);

            widget.UpdateInventoryData(null);
            Assert.That(widget.IsVisible, Is.False);

            widget.UpdateInventoryData(Inventory());
            Assert.That(widget.IsVisible, Is.False);
        }

        // ---------- Markup builders (P1-11 injection fix) ----------

        [Test]
        public void Inventory_Markup_Escapes_Bracketed_KeyIds_So_Markup_Parses()
        {
            // "[gold-key]" is not a Spectre style tag; unescaped it throws at parse
            // time and drops the frame. The builder must escape it.
            var data = new InventoryRenderData
            {
                Count = 1,
                Capacity = 10,
                Items = new[] { "Brass Key [gold-key]" },
            };

            var markup = SpectreConsoleRenderer.BuildInventoryItemsMarkup(data);

            Assert.That(markup, Does.Contain("[[gold-key]]"),
                "brackets must be escaped, not passed through");
            Assert.DoesNotThrow(() => _ = new Spectre.Console.Markup(markup),
                "escaped inventory markup must be parseable by Spectre");
        }

        [Test]
        public void Inventory_Markup_Neutralizes_Style_Tag_Injection()
        {
            // "[red]" IS a valid style tag — unescaped it silently restyles the
            // line instead of showing the item's actual name.
            var data = new InventoryRenderData
            {
                Count = 1,
                Capacity = 10,
                Items = new[] { "[red]Potion of Fire[/]" },
            };

            var markup = SpectreConsoleRenderer.BuildInventoryItemsMarkup(data);

            // "[[" is Spectre's escape for a literal "[" — the tag must arrive
            // escaped so it renders as text instead of restyling the line.
            Assert.That(markup, Does.Contain("[[red]]"),
                "style-tag brackets must be escaped to literals");
            var parsed = new Spectre.Console.Markup(markup);
            Assert.That(parsed, Is.Not.Null);
        }

        [Test]
        public void Inventory_Markup_Shows_Dim_Empty_Placeholder_For_No_Items()
        {
            var markup = SpectreConsoleRenderer.BuildInventoryItemsMarkup(new InventoryRenderData());
            Assert.That(markup, Is.EqualTo("[dim]Empty[/]"));
        }

        [Test]
        public void Status_Markup_Escapes_Server_Supplied_Text()
        {
            var markup = SpectreConsoleRenderer.BuildStatusMarkup("Picked up Brass Key [gold-key]!");

            Assert.That(markup, Does.Contain("[[gold-key]]"));
            Assert.DoesNotThrow(() => _ = new Spectre.Console.Markup(markup));
        }

        // ---------- CompassWidget ----------

        [Test]
        public void CompassWidget_Is_Hidden_And_Unknown_Without_Compass()
        {
            var widget = new CompassWidget(ThemeConfig.Default);
            widget.UpdateNavigationData(null, directionalVision: false, fieldOfView: 360);

            var data = (CompassRenderData)widget.GetRenderData();

            Assert.That(widget.IsVisible, Is.False);
            Assert.That(data.DirectionName, Is.EqualTo("Unknown"));
            Assert.That(data.DirectionSymbol, Is.EqualTo("?"));
        }

        [Test]
        public void CompassWidget_Reports_Heading_And_Direction_From_Navigation_Data()
        {
            var widget = new CompassWidget(ThemeConfig.Default);
            widget.UpdateNavigationData(new NavigationDataDto
            {
                HasCompass = true,
                HeadingDegrees = 90,
                CardinalDirection = Aetherium.Model.WorldDirection.East,
            }, directionalVision: true, fieldOfView: 120);

            var data = (CompassRenderData)widget.GetRenderData();

            Assert.That(widget.IsVisible, Is.True);
            Assert.That(data.Heading, Is.EqualTo(90));
            Assert.That(data.DirectionName, Is.EqualTo("East"));
            Assert.That(data.IsDirectionalVision, Is.True);
            Assert.That(data.FieldOfViewDegrees, Is.EqualTo(120));
        }

        [Test]
        public void CompassWidget_ToggleMode_Cycles_Arrow_And_Degree()
        {
            var widget = new CompassWidget(ThemeConfig.Default);

            Assert.That(widget.Mode, Is.EqualTo(CompassMode.Arrow));
            widget.ToggleMode();
            Assert.That(widget.Mode, Is.EqualTo(CompassMode.Degree));
            widget.ToggleMode();
            Assert.That(widget.Mode, Is.EqualTo(CompassMode.Arrow));
        }
    }
}
