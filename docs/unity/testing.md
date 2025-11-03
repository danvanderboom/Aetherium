# Unity Client Testing Guide

## Overview

The Unity client includes EditMode and PlayMode tests for validating perception parsing, tilemap rendering, and input handling.

## Running Tests

### In Unity Editor

1. Open **Window → General → Test Runner**
2. Select **EditMode** or **PlayMode** tab
3. Click **Run All** or select individual tests
4. View results in the Test Runner window

### From Command Line

Unity provides a CLI test runner:

```bash
Unity.exe -runTests -testPlatform EditMode -testResults results.xml
Unity.exe -runTests -testPlatform PlayMode -testResults results.xml
```

**Note:** Unity must be installed and path must be in your PATH or specified explicitly.

## Test Structure

### EditMode Tests

Located in `Assets/Tests/EditMode/`:

- **PerceptionParsingTests.cs**: Tests JSON deserialization and PerceptionLite structure validation
- **ToolExecutionResultTests.cs**: Tests ToolExecutionResultDto and UsageOptionDto models

### PlayMode Tests

Located in `Assets/Tests/PlayMode/`:

- **TilemapAndInputTests.cs**: Tests scene loading, tilemap rendering, and player marker positioning
- **InputAutomationTests.cs**: Tests input simulation and player movement
- **GamepadInputTests.cs**: Tests Gamepad input handling for movement, rotation, and level changes
- **OptionSelectionTests.cs**: Tests multi-option selection flow and HUD display

## EditMode Tests

### PerceptionParsingTests

**Test: ParsePerceptionJson_ValidFrame_DeserializesCorrectly**
- Validates that sample JSON frame deserializes correctly
- Asserts player location, heading, and bounds are correct
- Verifies visuals dictionary is populated

**Test: ParsePerceptionJson_GridDimensions_MatchVisibleBounds**
- Ensures visual count does not exceed bounds area
- Validates grid coordinate calculations

**Test: PerceptionLite_PlayerLocation_InitializedCorrectly**
- Validates WorldLocationLite constructor and properties

**Test: PerceptionLite_WorldDirection_EnumValuesMatch**
- Ensures WorldDirectionLite enum matches expected values

## PlayMode Tests

### TilemapAndInputTests

**Test: LoadMainScene_TilemapRendersCorrectly**
- Loads Main.unity scene
- Verifies TilemapRenderer2D and GameClientFacade exist
- Asserts that perception is loaded and tiles are rendered

**Test: PlayerMarker_UpdatesOnPerception**
- Verifies player marker GameObject updates position based on perception
- Validates grid-to-world coordinate conversion

### InputAutomationTests

**Test: SimulateMoveInput_PlayerMovesOneCell**
- Simulates keyboard input (W key)
- Verifies player marker moves one cell forward
- Uses Unity Input System test fixtures

### GamepadInputTests

**Test: GamepadMovement_LeftStick_ExecutesMoveTool**
- Tests that Gamepad left stick input executes move tool
- Validates Gamepad input handling

**Test: GamepadRotate_Shoulders_ExecutesRotateTool**
- Tests that Gamepad shoulder buttons execute rotate tool
- Validates axis-based rotation input

**Test: GamepadChangeLevel_Triggers_ExecutesChangeLevelTool**
- Tests that Gamepad triggers execute change level tool
- Validates axis-based level change input

### OptionSelectionTests

**Test: ToolExecutionResult_WithOptions_EntersSelectionMode**
- Tests that tools returning options enter selection mode
- Validates option parsing from ToolExecutionResultDto

**Test: PlayerController_OptionSelection_DisplaysInHUD**
- Tests that option selection displays correctly in HUD
- Validates HUD overlay functionality

### ToolExecutionResultTests (EditMode)

**Test: ToolExecutionResultDto_SuccessResult_InitializesCorrectly**
- Tests successful tool execution result creation

**Test: ToolExecutionResultDto_WithOptionsData_StoresCorrectly**
- Tests tool results with option data for multi-use tools

**Test: UsageOptionDto_AllProperties_SetCorrectly**
- Tests UsageOptionDto model initialization

## UI Automation Testing

The `InputAutomationTests` demonstrate input simulation using Unity's Input System:

```csharp
var keyboard = InputSystem.AddDevice<Keyboard>();
InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
// ... wait for processing ...
InputSystem.RemoveDevice(keyboard);
```

This allows testing player movement without manual input.

## Test Data

Tests rely on sample perception frames in `Assets/StreamingAssets/PerceptionFrames/sample-frame.json`.

If tests fail due to missing JSON:
1. Ensure `sample-frame.json` exists in `Assets/StreamingAssets/PerceptionFrames/`
2. Verify JSON format matches PerceptionLite structure (see [README.md](README.md#perception-json-format))

## Troubleshooting

### Tests Fail: "Scene not found"

- Ensure `Main.unity` scene exists in `Assets/Scenes/`
- Verify scene is added to Build Settings (File → Build Settings → Add Open Scenes)

### Tests Fail: "Component not found"

- Ensure scene is properly set up with required GameObjects:
  - TilemapRenderer2D on Tilemap
  - GameClientFacade on GameManager
  - PlayerController on Player GameObject
- See [Scene Setup Guide](README.md#scene-setup-guide) in main README

### Input Tests Fail

- Ensure Input System package is installed
- Verify `InputActions.inputactions` is imported
- Check that Input System is enabled (Edit → Project Settings → Input System Package)

### JSON Parsing Tests Fail

- Verify `sample-frame.json` exists and is valid JSON
- Check that Unity can access StreamingAssets folder (builds may require explicit copying)
- Note: Unity's JsonUtility may have limitations with Dictionary serialization; see README for workarounds

## Continuous Integration

For CI/CD pipelines:

```bash
# Run all tests and export results
Unity.exe -runTests -testPlatform EditMode -testResults editmode-results.xml -logFile editmode.log
Unity.exe -runTests -testPlatform PlayMode -testResults playmode-results.xml -logFile playmode.log
```

**Note:** Unity test runner requires Unity Editor installation and may not work in headless mode without additional setup.

## Best Practices

1. **Keep tests isolated**: Each test should not depend on other tests
2. **Use WaitForSeconds**: PlayMode tests should wait for async operations (scene loads, perception updates)
3. **Clean up devices**: Remove Input System devices after test completion
4. **Mock where possible**: Use PerceptionMockProvider for predictable test data
5. **Validate assertions**: Check both positive and negative cases

## Adding New Tests

### EditMode Test Example

```csharp
[Test]
public void MyEditModeTest()
{
    // Arrange
    var location = new WorldLocationLite(5, 10, 0);
    
    // Act
    var worldPos = GridHelpers.GridToWorld(location);
    
    // Assert
    Assert.AreEqual(new Vector3(5, 10, 0), worldPos);
}
```

### PlayMode Test Example

```csharp
[UnityTest]
public IEnumerator MyPlayModeTest()
{
    // Arrange
    yield return SceneManager.LoadSceneAsync("Main");
    
    // Act
    var component = Object.FindObjectOfType<MyComponent>();
    
    // Assert
    Assert.IsNotNull(component);
    
    yield return new WaitForSeconds(0.5f);
}
```

## References

- [Unity Test Framework Documentation](https://docs.unity3d.com/Packages/com.unity.test-framework@latest)
- [Input System Testing](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest/manual/Testing.html)
- Main Unity Client [README.md](README.md)

