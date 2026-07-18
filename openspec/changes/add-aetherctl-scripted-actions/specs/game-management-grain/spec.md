## ADDED Requirements

### Requirement: Batch Action Execution
The grain SHALL execute an ordered sequence of tool invocations against a single session in one grain call and return a result for each attempted step, so that callers can drive a character with a deterministic, reproducible action script.

#### Scenario: Execute an ordered batch
- **WHEN** `ExecuteToolBatchAsync` is called with a valid `sessionId` and a list of actions
- **THEN** the grain SHALL execute the actions in the given order against that session
- **AND** SHALL return one result per action containing its index, tool id, success flag, and message
- **AND** the results SHALL be in the same order as the input actions

#### Scenario: Stop on first error
- **WHEN** `ExecuteToolBatchAsync` is called with `stopOnError` = true and a step fails
- **THEN** the grain SHALL stop after the failing step
- **AND** SHALL return the results for the steps attempted so far, ending with the failed step

#### Scenario: Continue past errors
- **WHEN** `ExecuteToolBatchAsync` is called with `stopOnError` = false and a step fails
- **THEN** the grain SHALL continue executing the remaining steps
- **AND** SHALL return a result for every action, each reporting its own success or failure

#### Scenario: Unknown session
- **WHEN** `ExecuteToolBatchAsync` is called with a session id that is not registered
- **THEN** the grain SHALL NOT throw
- **AND** SHALL return a single failure result indicating the session was not found

#### Scenario: Empty and oversized batches
- **WHEN** `ExecuteToolBatchAsync` is called with an empty action list
- **THEN** the grain SHALL return an empty result list
- **WHEN** the action list exceeds the maximum batch size
- **THEN** the grain SHALL reject the batch with a clear error rather than executing a partial sequence
