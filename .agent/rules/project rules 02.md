# Project Summary: Unity Voxel Engine & Vehicle System

A voxel-based, block-building game built in Unity, featuring survival/creative inventory management, dynamic crafting grids, procedural item icons, a structure-to-vehicle physics conversion system, and realistic foliage/world generation.

---

## 1. Core Systems & Architecture

### **Voxel World Generation (`Chunk.cs`, `VoxelWorld.cs`, `VoxelData.cs`)**
* **Chunk Scale:** Chunks are `16 (Width) × 64 (Height) × 16 (Depth)` voxels.
* **Meshing:** Dynamically generated procedural meshes using vertex colors, face culling, and transparency handling for glass (`neighborIsTransparent`) and foliage/crossed-quad billboards (`neighborIsFlower`).
* **Noise Generation:** Perlin noise layers generate terrain, sand shores, and natural flora patches.

### **Voxel-Vehicle Conversion System (`VehicleController.cs`, `VehicleSpawner.cs`, `BlueprintGenerator.cs`, `StructureScanner.cs`)**
* **The Loop:** Static structures containing a **Control Block** (ID 50) can be converted into active, physics-enabled vehicles using a **Wrench** (Item ID 99). Players can de-convert the vehicle back to static voxels aligned to the world grid.
* **Blueprints:** Bounding box calculations, mass accumulation (`BlockMassTable`), and durability (`BlockDurabilityTable`) determine the vehicle's weight and robustness.
* **Physics & Controls:** Hull-relative propeller thrust, wheel steering/suspension, and climbing steps. Movement and character controller inputs are frozen during transitions to prevent jitter.

### **Inventory & Crafting System (`Inventory.cs`, `InventoryUI.cs`, `DragDropManager.cs`, `StarterItems.cs`)**
* **Modes:** Survival mode (starts empty, items collected through drops) and Creative mode (limitless blocks, organized into category tabs: BLOCKS, TOOLS, VEHICLES, FOLIAGE).
* **Crafting:** Supports a 2x2 pocket crafting grid and a 3x3 table crafting grid (activated by right-clicking Crafting Table block ID 36). Crafting result slots are safely consumed and synced with UI states.
* **Procedural Icons:** Starter blocks and tools render custom 3D isometric icons dynamically (like `MakeIsometricBlock()` or tool icon shaders) to avoid stale serialized sprite assets.

### **Foliage & Environment (`PlayerInteraction.cs`, `DroppedItem.cs`)**
* **Billboards:** Flowers and grasses render as crossed-quad billboards (X-billboards) both when placed and when dropped as floating items in the world.
* **Placement Restrictions:** Foliage can only be placed on Grass, Dirt, or Sand.
* **Break Propagation:** Breaking a block immediately breaks/drops any foliage block sitting directly on top of it.
* **Mining Hardness:** Foliage has a hardness value of `0.05f`, allowing players to clear foliage efficiently by holding down left-click (gradual mining).

---

## 2. Key Block IDs Reference

| Block ID | Block Name | Geometry Type | Category | Key Properties |
|---|---|---|---|---|
| **0** | Air / Empty | - | - | - |
| **1** | Wood | 3D Cube | BLOCKS | Wood material |
| **2** | Plank | 3D Cube | BLOCKS | Building block |
| **3** | Stone | 3D Cube | BLOCKS | High durability block |
| **4** | Grass Block | 3D Cube | BLOCKS | Standard soil block |
| **5** | Dirt | 3D Cube | BLOCKS | Soil block |
| **8 / 34**| Sand | 3D Cube | BLOCKS | Shoreline block |
| **9** | Rose Flower | X-Billboard | FOLIAGE | Placeable on soil |
| **10** | Dandelion | X-Billboard | FOLIAGE | Placeable on soil |
| **11** | Iris | X-Billboard | FOLIAGE | Placeable on soil |
| **12** | Leaves | 3D Cube | BLOCKS | Tree canopy block |
| **13** | Short Grass | X-Billboard | FOLIAGE | Common organic block |
| **14** | Tall Grass | X-Billboard | FOLIAGE | Common organic block |
| **20** | Small Wheel | Custom Mesh | VEHICLES | Rotational vehicle wheel |
| **21** | Large Wheel | Custom Mesh | VEHICLES | 2x2 structural wheel (anchor) |
| **22** | Propeller | Custom Mesh | VEHICLES | Propulsion |
| **23** | Wheel Helper | Invisible Cube | VEHICLES | Reserved 3 blocks around ID 21 |
| **30** | Coal Ore | 3D Cube | BLOCKS | Ore block |
| **31** | Iron Ore | 3D Cube | BLOCKS | Ore block |
| **32** | Gold Block | 3D Cube | BLOCKS | Premium heavy block |
| **33** | Iron Block | 3D Cube | BLOCKS | High durability building block |
| **35** | Glass | 3D Cube | BLOCKS | Semi-transparent block |
| **36** | Crafting Table| 3D Cube | BLOCKS | Opens 3x3 crafting screen |
| **37** | Furnace | 3D Cube | BLOCKS | Smelting block |
| **46** | Wooden Slab | Half-Height | BLOCKS | Bottom slab geometry |
| **47** | Stone Slab | Half-Height | BLOCKS | Bottom slab geometry |
| **50** | Control Block | 3D Cube | VEHICLES | Initiates structure conversion |

---

## 3. Important Item IDs (Non-Blocks)
* **Item ID 99:** Wrench (used to convert structures to vehicles and vice versa)
* **Tool Items:** Pickaxes, axes, shovels, and swords parsed by tier (Wood, Stone, Iron, Diamond).
