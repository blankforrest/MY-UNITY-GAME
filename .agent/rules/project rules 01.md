# Unity Voxel Game - Project Context & Rules

This project is a 3D procedural voxel game built in Unity (targeting URP) featuring player-built structures, dynamic physics-based vehicles, buoyancy, a dry interior cabin system, procedural rendering, and a grid-based crafting/inventory system.

---

## 🎮 Core Systems Architecture

- **Voxel & Chunk Rendering System**: 
  - Chunks generated dynamically (16x80x16 dimensions) via multi-octave 2D Perlin noise and river-carving.
  - Mesh geometry split into four children to optimize transparency and depth sorting: Opaque/Cutout solid mesh, transparent `waterMaterial` mesh (`"Water"`), double-sided `foliageMaterial` mesh (`"Foliage"`), and transparent ZWrite On/Cull Back `glassMaterial` mesh (`"Glass"`).
- **Vehicle Conversion & Physics**:
  - `StructureScanner.cs` scans player-built structures starting at the Control Block (ID 50) using flood fill.
  - `VehicleSpawner.cs` converts scanned structures into a single `Rigidbody` entity, erasing original voxels.
  - `VehicleController.cs` handles driving physics, suspensions, anti-flip gyros, and step-climbing to glide over 1-block steps.
  - Submerged hull blocks generate buoyancy. Water rendering is dynamically hidden inside the cabin boundaries using `IsWorldPosInsideVehicle` to keep cabins dry.
- **Inventory & Crafting**:
  - Handles item collection via ScriptableObjects, ensuring items are slotted in top-left to bottom-right order.
  - Supports toggling between 2x2 (inventory) and 3x3 (placed Crafting Table, ID 36) crafting grids.

---

## 🗂 Voxel Block & Item ID Catalog

- **0**: Air (Ignored)
- **1**: Wood (Solid / Main Mesh - Tree trunks)
- **2**: Plank (Solid / Main Mesh - Base construction block)
- **3**: Stone (Solid / Main Mesh - Deep underground terrain)
- **4**: Grass (Solid / Main Mesh - Terrain surface; uses unified procedural isometric sprite)
- **5**: Dirt / Iron (Solid / Main Mesh - Used as Dirt in terrain; marked as Iron in blueprint tables)
- **7**: Water (Water Mesh / Child Object - Semi-transparent; subject to vehicle dry cabin suppression)
- **8**: Sand (Solid / Main Mesh - Beach terrain block)
- **9**: Rose (Foliage / Crossed Quads - Spawns on grass; crossed-quad rendering)
- **10**: Dandelion (Foliage / Crossed Quads - Yellow flower; crossed-quad rendering)
- **11**: Iris (Foliage / Crossed Quads - Violet flower; crossed-quad rendering)
- **12**: Leaves (Foliage / Main Mesh style - Tree canopy; culled against adjacent leaves)
- **20**: Small Wheel (Special / Sphere Collider - Zero-friction glide physics material)
- **21**: Large Wheel (Special / Sphere Collider - 2x2 voxel footprint; offset to visual center during spawn)
- **22**: Propeller (Special / Box Collider - Auto-oriented relative to the nearest hull block)
- **23**: Large Helper (Special / Placeholder - Voxel placeholder for 2x2 footprint; deleted on spawn)
- **30**: Coal Ore (Solid / Main Mesh)
- **31**: Iron Ore (Solid / Main Mesh)
- **32**: Gold Block (Solid / Main Mesh)
- **33**: Iron Block (Solid / Main Mesh)
- **34**: Sand (Alternative Sand index)
- **35**: Glass (Glass Mesh / Child - ZWrite On + Cull Back; shatters on break - no drop)
- **36**: Crafting Table (Solid / Main Mesh - Opens 3x3 crafting grid on right-click)
- **50**: Control Block (Solid / Main Mesh - Pilot interaction seat; yellow stripes)

---

## ⚠️ Critical Development & Coding Guidelines

1. **Ignore Foliage Colliders**: Always apply `Physics.IgnoreCollision` between player/vehicle colliders and the `Foliage` mesh collider so standard block-breaking raycasts can hit foliage without triggers firing warnings.
2. **Vehicle Coordinate Offsets**: Spawned block colliders must align with the voxel grid using a half-unit offset (`new Vector3(0.5f, 0.5f, 0.5f)`). Large Wheels (ID 21) must be offset by their visual center (1 unit up, 0.5 units to the side) to center their physics colliders.
3. **Player Controller Dimensions**: Keep the player's CharacterController height at `1.8`, vertical center at `0.9`, stepOffset at `0.4`, and radius at `0.3` to ensure standard block-gap navigation.
4. **No Player Parenting**: Do not parent the player to a vehicle Rigidbody when driving. Instead, cache their local position in yaw-only space and manually sync their position relative to the vehicle's yaw to avoid camera tilt jitter.
5. **Preserve Procedural Icons**: Retain the procedural isometric sprite generation methods (`StarterItems.MakeBlockIcon`, etc.) rather than hardcoding static texture references.