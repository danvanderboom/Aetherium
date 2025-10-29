## 1. Implementation
- [x] 1.1 Assign `ObstructsView` defaults for terrain in world builders
- [x] 1.2 Create `FovCalculator` with Bresenham rays and cumulative opacity
- [x] 1.3 Create `VisionSystem` to build `VisionFrame` from FOV results
- [x] 1.4 Update `ConsoleMapView` to render only `VisionFrame` contents
- [x] 1.5 Honor `OpensAndCloses.IsOpen` overriding sight opacity
- [x] 1.6 Add unit tests for corridor blocking, forest attenuation, and doors

## 2. Validation
- [x] 2.1 Hidden cells not drawn; draw priority preserved on visible cells
- [x] 2.2 Mountains/Walls block; Water transparent; Forest attenuates range
- [x] 2.3 Opening doors restores LoS; closing doors blocks


