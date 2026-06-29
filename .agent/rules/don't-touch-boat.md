# Vehicle, Helicopter, and Watercraft Physics Integrity
DO NOT modify, refactor, or adjust the core physics, steering, orientation, suspension, or flight logic for vehicles, helicopters, or boats in the following files unless explicitly requested with detailed specifications:
- `Assets/Scripts/VehicleController.cs`
- `Assets/Scripts/VehicleSpawner.cs`
- `Assets/Scripts/PropellerBlock.cs`
- `Assets/Scripts/HelicopterController.cs`
- `Assets/Scripts/WheelBlock.cs`

## Key Rules & Architectural Decisions to Preserve:
1. **Dynamic Forward Axis**: The vehicle's forward axis (`_localForward`) is dynamically determined based on the average thrust vector of longitudinal propellers or forward facing directions. Do not switch this back to centroid-based geometry calculation.
2. **Propeller/Helicopter Separation**: Only attach `HelicopterController` to a vehicle if it has a vertical/lift propeller (determined by neighbor block offsets). Horizontal propellers must not trigger the helicopter controller to avoid boats/cars floating in the air.
3. **Rigid Level-Locking for Watercraft**: Boats in the water must have their X and Z rotations reset to 0 in both `FixedUpdate` and `Update` loops to prevent rolling/tilting during high-speed turns.
4. **Helicopter Flight Mechanics**: The `HelicopterController` must manage vertical lift, flight stabilization, yaw steering, and clamp X and Z rotation/angular velocity to zero to guarantee stable flight behavior.
5. **Instant Parking Freeze**: A stationary vehicle at rest (either grounded or floating in water) must instantly freeze (`isKinematic = true`) to prevent sliding/drifting, and only unfreeze when actively driven, pushed, or airborne.
6. **Suspension and Wheel Friction**: SphereColliders on wheels use zero friction to glide smoothly over voxel seams, while vertical raycast springs simulate physical suspension. Do not alter wheel friction settings.

