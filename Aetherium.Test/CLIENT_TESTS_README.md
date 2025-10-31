# Client Tests

The client-side tests (compass widget, themes, audio) have been separated to avoid assembly conflicts with the server tests.

## Future: Create Aetherium.Client.Test Project

To add comprehensive tests for the new client features:

1. Create a new test project:
```bash
dotnet new xunit -n Aetherium.Client.Test
```

2. Add reference to Aetherium client project:
```xml
<ProjectReference Include="..\Aetherium\Aetherium.Console.csproj" />
<ProjectReference Include="..\Aetherium.Model\Aetherium.Model.csproj" />
```

3. Add the test files (templates provided below)

## Test Templates

### CompassWidgetTests.cs
```csharp
using Xunit;
using Aetherium.Rendering.Widgets;
using Aetherium.Rendering.Themes;
using Aetherium.Model;

public class CompassWidgetTests
{
    [Fact]
    public void CompassWidget_HidesWhenNoCompass()
    {
        var widget = new CompassWidget(BuiltInThemes.Zen);
        widget.UpdateNavigationData(null);
        Assert.False(widget.IsVisible);
    }
    
    [Fact]
    public void CompassWidget_ShowsWhenHasCompass()
    {
        var widget = new CompassWidget(BuiltInThemes.Zen);
        var navData = new NavigationDataDto
        {
            HasCompass = true,
            HeadingDegrees = 0,
            CardinalDirection = WorldDirection.North
        };
        widget.UpdateNavigationData(navData);
        Assert.True(widget.IsVisible);
    }
    
    [Fact]
    public void CompassWidget_TogglesMode()
    {
        var widget = new CompassWidget(BuiltInThemes.Zen);
        var initialMode = widget.Mode;
        widget.ToggleMode();
        Assert.NotEqual(initialMode, widget.Mode);
    }
}
```

### ThemeSystemTests.cs
```csharp
using Xunit;
using Aetherium.Rendering.Themes;

public class ThemeSystemTests
{
    [Theory]
    [InlineData("zen")]
    [InlineData("cyberpunk")]
    [InlineData("halloween")]
    [InlineData("winter")]
    [InlineData("classic")]
    public void BuiltInThemes_ContainsAllThemes(string themeName)
    {
        var theme = BuiltInThemes.GetByName(themeName);
        Assert.NotNull(theme);
    }
    
    [Fact]
    public void ThemeConfig_GetSymbol_ReturnsSymbolOrFallback()
    {
        var theme = BuiltInThemes.Zen;
        var northSymbol = theme.GetSymbol("compass_n", "?");
        Assert.Equal("↑", northSymbol);
    }
}
```

###Audio System Tests
```csharp
using Xunit;
using Aetherium.Audio;

public class AudioSystemTests
{
    [Fact]
    public void NullAudioSystem_DoesNotThrow()
    {
        var audio = new NullAudioSystem();
        audio.PlayBackgroundMusic("test");
        audio.StopBackgroundMusic();
        audio.PlaySoundEffect("test");
        // Should not throw
        Assert.False(audio.IsEnabled);
    }
    
    [Fact]
    public void NAudioSystem_CreatesWithConfig()
    {
        var config = new AudioConfig { Enabled = true };
        using var audio = new NAudioSystem(config);
        Assert.True(audio.IsEnabled);
    }
}
```

## Running Client Tests

Once the separate test project is created:

```bash
dotnet test Aetherium.Client.Test
```

## Current Test Status

- ✅ Server tests: Aetherium.Test (existing, passing)
- ⚠️ Client tests: Need separate project due to assembly conflicts
- 📝 Test templates documented above for future implementation


