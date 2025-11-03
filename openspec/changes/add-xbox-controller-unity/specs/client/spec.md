## MODIFIED Requirements
### Requirement: Unity 2D Client
The system SHALL provide a Unity 2D client for PC and iOS platforms that renders game state as a tilemap with Z-level support.

#### Scenario: Unity client renders tilemap
- **WHEN** Unity client receives PerceptionDto data
- **THEN** it SHALL render visible tiles to a Unity 2D Tilemap
- **AND** SHALL display a player marker sprite at PlayerLocation
- **AND** SHALL render only tiles on the current Z-level
- **AND** SHALL allow cycling through Z-levels via keyboard or gamepad input

#### Scenario: Unity client handles input
- **WHEN** player presses WASD or Arrow keys, OR moves Gamepad Left Stick
- **THEN** client SHALL execute "move" tool with appropriate direction
- **WHEN** player presses Z/X or Gamepad LB/RB
- **THEN** client SHALL execute "rotate" tool (clockwise/counter-clockwise) via axis-based input
- **WHEN** player presses PageUp/PageDown or U/D, OR presses Gamepad RT/LT
- **THEN** client SHALL execute "changelevel" tool to move up/down Z-levels via axis-based input
- **WHEN** player presses Gamepad A Button
- **THEN** client SHALL execute context tool use or confirm option selection
- **WHEN** player presses Gamepad B Button
- **THEN** client SHALL cancel option selection if in selection mode

#### Scenario: Unity client displays HUD
- **WHEN** Unity client is running
- **THEN** it SHALL display current Z-level, player heading, and connection status
- **AND** SHALL show FPS or status message on screen
- **WHEN** client enters option selection mode
- **THEN** HUD SHALL display available options with selection indicator (>>)
- **AND** SHALL allow navigation and selection of options

## ADDED Requirements
### Requirement: Unity Client Gamepad Support
The Unity client SHALL support Xbox controller (Gamepad) input on Windows platform for all game actions.

#### Scenario: Gamepad movement input
- **WHEN** player moves Gamepad Left Stick in any direction
- **THEN** client SHALL execute "move" tool with cardinalized direction (North, East, South, West)
- **AND** SHALL prevent diagonal movement by selecting dominant axis

#### Scenario: Gamepad rotation input
- **WHEN** player presses Gamepad LB (Left Bumper)
- **THEN** client SHALL execute "rotate" tool with clockwise=false (counter-clockwise)
- **WHEN** player presses Gamepad RB (Right Bumper)
- **THEN** client SHALL execute "rotate" tool with clockwise=true (clockwise)
- **AND** rotation SHALL be handled as axis-based input (negative=left, positive=right)

#### Scenario: Gamepad level change input
- **WHEN** player presses Gamepad LT (Left Trigger)
- **THEN** client SHALL execute "changelevel" tool with up=false (move down)
- **WHEN** player presses Gamepad RT (Right Trigger)
- **THEN** client SHALL execute "changelevel" tool with up=true (move up)
- **AND** level change SHALL be handled as axis-based input (negative=down, positive=up)

### Requirement: Unity Client Multi-Option Selection
The Unity client SHALL support interactive selection of usage options when tools return multiple choices (e.g., multi-use items).

#### Scenario: Tool returns multiple options
- **WHEN** player executes a tool (e.g., "use") that returns multiple usage options in ToolExecutionResultDto.Data["options"]
- **THEN** client SHALL enter option selection mode
- **AND** SHALL disable movement and other game actions until selection is confirmed or cancelled
- **AND** SHALL display available options in HUD with selection indicator (>>) on currently selected option
- **AND** SHALL initialize selection index to 0 (first option)

#### Scenario: Navigate options with Gamepad
- **WHEN** player is in option selection mode
- **AND** player presses Gamepad D-Pad Up
- **THEN** client SHALL decrement selection index (wrapping to last option if at 0)
- **WHEN** player presses Gamepad D-Pad Down
- **THEN** client SHALL increment selection index (wrapping to first option if at last)
- **AND** HUD SHALL update to show new selection indicator position

#### Scenario: Confirm option selection
- **WHEN** player is in option selection mode
- **AND** player presses Gamepad A Button
- **THEN** client SHALL re-execute tool with selected option's usageId in arguments
- **AND** SHALL exit option selection mode
- **AND** SHALL restore normal HUD display
- **WHEN** re-executed tool succeeds
- **THEN** client SHALL continue normal gameplay
- **WHEN** re-executed tool returns options again
- **THEN** client SHALL remain in option selection mode with updated options

#### Scenario: Cancel option selection
- **WHEN** player is in option selection mode
- **AND** player presses Gamepad B Button
- **THEN** client SHALL exit option selection mode without executing tool
- **AND** SHALL restore normal HUD display
- **AND** SHALL re-enable movement and game actions

#### Scenario: Async tool execution returns options
- **WHEN** client calls ExecuteToolAsync and receives ToolExecutionResultDto with Success=true and Data["options"] containing list of option dictionaries
- **THEN** each option dictionary SHALL contain "usageId", "label", and "description" keys
- **AND** client SHALL parse options into UsageOptionDto objects
- **AND** SHALL enter option selection mode with parsed options

#### Scenario: Tool execution without options
- **WHEN** player executes a tool that returns ToolExecutionResultDto with Success=true and no options
- **THEN** client SHALL NOT enter option selection mode
- **AND** SHALL continue normal gameplay immediately after tool execution

### Requirement: Unity Client Async Tool Execution
The Unity client SHALL support asynchronous tool execution that returns ToolExecutionResultDto for handling tool results including multi-option scenarios.

#### Scenario: ExecuteToolAsync returns result
- **WHEN** client calls ExecuteToolAsync(toolId, args)
- **THEN** method SHALL return Task<ToolExecutionResultDto> that completes when tool execution finishes
- **AND** result SHALL contain Success (bool), Message (string), and optional Data (Dictionary<string, object>)
- **WHEN** tool execution succeeds
- **THEN** result.Success SHALL be true
- **WHEN** tool execution fails
- **THEN** result.Success SHALL be false
- **AND** result.Message SHALL contain error description

#### Scenario: Offline mode async tool execution
- **WHEN** client is in Offline Mock mode
- **AND** ExecuteToolAsync is called
- **THEN** method SHALL execute tool against local mock state synchronously
- **AND** SHALL return Task<ToolExecutionResultDto> with success result immediately
- **AND** SHALL update local PerceptionLite state
- **AND** SHALL trigger perception update event

#### Scenario: Live mode async tool execution
- **WHEN** client is in Live mode
- **AND** ExecuteToolAsync is called
- **THEN** method SHALL send tool execution request to server via SignalR
- **AND** SHALL wait for server response containing ToolExecutionResultDto
- **AND** SHALL return completed Task with server response
- **WHEN** server returns perception update
- **THEN** client SHALL update local perception state and render

