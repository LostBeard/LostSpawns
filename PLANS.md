# Lost Spawns - Development Plans

**A voxel-based DayZ.** Post-apocalyptic survival in the browser, built on Blazor WASM + SpawnDev.ILGPU + WebGPU.

DayZ is THE inspiration. Not Minecraft. We want the DayZ feel, vibe, tension, base building, economy, hunting, exploration. Minecraft has some neat features to draw from but Lost Spawns IS DayZ in voxel form.

---

## Play Styles

### First-Person VR (immersive-vr)
Full-scale 1:1 world. Walk the ruins, loot buildings, hunt deer, defend your base. WebXR on Quest 3S or PCVR via streaming. This is the ultimate way to play.

### First-Person Desktop
Traditional WASD + mouse. Same game, flat screen. Keyboard/mouse or gamepad.

### Third-Person Top-Down / Tabletop AR (immersive-ar)
Diorama view on a real table via Quest passthrough. God-mode overview. See the whole area, manage your base, plan routes, watch other players move through the miniature world. Editor mode uses this view.

### Mobile Touch
PWA on phone/tablet. Touch controls. Simplified UI. Same world, same servers.

---

## Core Systems (DayZ-Inspired)

### Survival Mechanics
- [ ] **Health system** - blood, health, shock (DayZ's three-tier system)
- [ ] **Hunger + thirst** - food and water meters that drain over time
- [ ] **Temperature** - hypothermia in rain/cold, heatstroke in sun. Clothing matters.
- [ ] **Disease** - cholera from bad water, infection from wounds, cold/flu
- [ ] **Blood types** - blood bags, saline, transfusions (compatible types only, like DayZ)
- [ ] **Stamina** - sprinting drains stamina, recovery affected by health/hunger
- [ ] **Broken bones** - fall damage, splints to heal
- [ ] **Bleeding** - bandages, rags to stop bleeding. Blood trails.
- [ ] **Unconsciousness** - shock damage knocks you out, others can loot/help you

### Weapons + Combat
- [ ] **Melee weapons** - axe, machete, baseball bat, knife, shovel, crowbar
- [ ] **Firearms** - pistols, rifles, shotguns, SMGs. Realistic reloading. Magazine system.
- [ ] **Crossbow** - silent ranged weapon, craftable bolts
- [ ] **Bow** - craftable, improvised
- [ ] **Throwing** - grenades, smoke grenades, flashbangs
- [ ] **Ammunition types** - different calibers, tracer rounds
- [ ] **Weapon attachments** - scopes, suppressors, flashlights, bipods
- [ ] **Weapon condition** - degrades with use, can be cleaned/repaired
- [ ] **Ballistics** - bullet drop, travel time, penetration through materials
- [ ] **NVGs (Night Vision Goggles)** - essential for nighttime gameplay, battery powered
- [ ] **Binoculars** - zoom without a weapon

### Base Building
- [ ] **Freeform voxel building** - place blocks, walls, doors, gates
- [ ] **Base components** - walls, floors, roofs, stairs, watchtowers, fences
- [ ] **Lock system** - combination locks on doors/gates, keys
- [ ] **Raiding** - tools to break into bases (axes, explosives). Building materials have HP.
- [ ] **Base decay** - structures deteriorate if not maintained
- [ ] **Electricity** - generators, lights, alarms, powered gates
- [ ] **Storage** - crates, barrels, tents, buried stashes
- [ ] **Territory flags** - claim area, prevent building by others

### Vehicles
- [ ] **Cars** - sedans, trucks, off-road. Find parts (engine, wheels, battery, fuel)
- [ ] **Boats** - rowboat, motorboat. River and coastal travel.
- [ ] **Bicycles** - quiet, no fuel needed
- [ ] **Vehicle damage** - tires can be shot out, engine damage, radiator leaks
- [ ] **Fuel system** - jerry cans, fuel pumps at gas stations
- [ ] **Vehicle storage** - trunk/bed inventory

### Hunting + Fishing + Animals
- [ ] **Wildlife** - deer, rabbits, bears, wolves, chickens, cows, pigs, fish
- [ ] **AI behavior** - flee on approach (deer), attack when threatened (bears/wolves), ambient (birds)
- [ ] **Hunting** - track, stalk, kill, skin, cook
- [ ] **Fishing** - rod + bait at rivers/coast, different fish types
- [ ] **Animal husbandry** - pen animals, breed for food
- [ ] **Predator danger** - wolves hunt in packs at night, bears are territorial

### Weather + Environment
- [ ] **Rain** - particle effects, gets you wet (temperature penalty), sound
- [ ] **Snow** - accumulation on terrain, cold environment
- [ ] **Fog** - reduced visibility, tactical advantage
- [ ] **Wind** - affects ballistics, sound propagation
- [ ] **Day/night cycle** - full cycle with dawn/dusk. Night is DARK (like DayZ)
- [ ] **Seasons** - visual changes, different weather patterns
- [ ] **Lightning** - visual + sound during thunderstorms, fire risk
- [ ] **Dynamic sky** - clouds, sun position, moon phases, stars

### Swimming + Water
- [ ] **Swimming** - surface and underwater. Stamina drain while swimming.
- [ ] **Drowning** - oxygen meter underwater
- [ ] **Water crossings** - rivers as natural obstacles
- [ ] **Wet clothing** - takes time to dry, temperature penalty
- [ ] **Underwater loot** - wrecks, crates in rivers/coast

### Crafting
- [ ] **DayZ-style crafting** - combine items in inventory (not crafting table)
- [ ] **Recipes** - discovered by having the right ingredients
- [ ] **Tool requirements** - some crafting needs specific tools (knife, saw, etc.)
- [ ] **Improvised items** - rag + stick = torch, stones + sticks = stone knife
- [ ] **Cooking** - raw meat on fire/stove, different foods have different nutrition
- [ ] **Medical crafting** - splints, bandages, blood test kits

### Economy + Loot
- [ ] **Loot spawns** - buildings, military zones, industrial areas, hospitals
- [ ] **Loot tiers** - civilian (common) < industrial < military (rare)
- [ ] **Loot cycling** - items despawn and respawn over time
- [ ] **Rarity system** - common, uncommon, rare, very rare
- [ ] **Trader NPCs** - safe zones with AI traders (optional per server)
- [ ] **Player trading** - direct item exchange, drop trading

### Voice Chat + Communication
- [ ] **Proximity voice chat** - hear nearby players, distance falloff
- [ ] **Megaphone** - increased voice range item
- [ ] **Radio** - long-range communication on specific frequencies
- [ ] **Text chat** - global, local, group channels
- [ ] **Hand signals** - emotes/gestures visible to others
- [ ] **Notes** - write and leave paper notes in the world

### NVGs + Optics
- [ ] **Night vision** - green/white phosphor modes, battery drain
- [ ] **Thermal optics** - see heat signatures (players, animals, vehicles)
- [ ] **Rangefinder** - measure distance to targets
- [ ] **Scopes** - variable zoom, different reticle styles
- [ ] **Camera** - take screenshots, leave photos

---

## World Generation

### Terrain
- [ ] **Biomes** - forest, plains, coast, mountain, swamp, desert, urban, industrial
- [ ] **Elevation** - hills, valleys, cliffs, mountain passes
- [ ] **Rivers + lakes** - procedural water systems
- [ ] **Roads** - connecting towns, dirt roads, highways
- [ ] **Coastline** - beaches, docks, lighthouses

### Structures (Pre-Built)
- [ ] **Houses** - residential, different styles per biome
- [ ] **Apartments** - multi-story buildings
- [ ] **Military** - bases, checkpoints, tents, barracks
- [ ] **Industrial** - factories, warehouses, power plants
- [ ] **Medical** - hospitals, clinics
- [ ] **Commercial** - stores, gas stations, restaurants
- [ ] **Infrastructure** - bridges, dams, radio towers, water towers
- [ ] **Ruins** - destroyed buildings, crash sites, abandoned vehicles
- [ ] **Underground** - bunkers, sewers, mine shafts

### Seeds + Procedural
- [ ] **World seeds** - same seed = same world, shareable
- [ ] **Biome distribution** - weighted random based on seed
- [ ] **Structure placement** - procedural but sensible (towns at intersections, farms in plains)
- [ ] **Ore/resource distribution** - mineable resources underground

---

## Editor Mode

- [ ] **God mode camera** - fly freely, no collision
- [ ] **Block palette** - select and place any block type
- [ ] **Copy/paste** - select region, copy, place elsewhere
- [ ] **Structure templates** - save/load pre-built structures
- [ ] **Terrain sculpting** - raise/lower terrain, smooth, flatten
- [ ] **Biome painting** - assign biomes to regions
- [ ] **Entity placement** - place animals, loot, NPCs
- [ ] **AR tabletop editor** - edit the world as a diorama on your real table (Quest passthrough)
- [ ] **Collaborative editing** - multiple editors connected via WebRTC

---

## Multiplayer (Phase 2)

### Server Types
- [ ] **Official servers** - persistent, always online, hosted by us
- [ ] **Community servers** - player-hosted, customizable rules
- [ ] **Private servers** - invite-only, password protected
- [ ] **Solo/co-op** - offline or LAN with friends

### Networking
- [ ] **P2P via WebRTC** - SpawnDev.WebTorrent / PeerJS infrastructure
- [ ] **Dedicated servers** - ASP.NET server for persistent worlds
- [ ] **Lobby/matchmaking** - browse servers, ping display, player count
- [ ] **Anti-cheat** - server-authoritative for critical game state
- [ ] **Cross-platform** - PC, Quest VR, mobile - all on the same servers

### Social
- [ ] **Clans/groups** - shared base ownership, group chat
- [ ] **Friend list** - see friends online, join their server
- [ ] **Player profiles** - stats, kills, time survived, bases built
- [ ] **Events** - server-wide events (airdrops, hordes, weather events)
- [ ] **Mod support** - community-created content (weapons, vehicles, maps, modes)

---

## VR-Specific Features

### First Person VR
- [ ] **Physical interactions** - open doors by reaching, pick up items by grabbing
- [ ] **Weapon handling** - two-handed weapons, physical reloading
- [ ] **Inventory** - backpack on back (reach behind to access), vest pockets
- [ ] **Map** - physical folding map item, hold up to read
- [ ] **Compass** - physical compass item, hold to check bearing
- [ ] **Crafting** - physically combine items in hands
- [ ] **Vehicle driving** - grab steering wheel, shift gears
- [ ] **Melee combat** - physics-based swing detection
- [ ] **Archery** - nock arrow, draw bow physically

### AR Tabletop
- [ ] **Diorama view** - miniature world on real table
- [ ] **Pinch to zoom** - scale the world up/down
- [ ] **Editor tools** - place blocks, structures, entities from above
- [ ] **Player tracking** - see tiny player avatars moving in the world
- [ ] **Strategic planning** - mark routes, set waypoints from overview
- [ ] **Base management** - manage your base from the overview

### PC-Streamed VR
- [ ] **Desktop renders, Quest displays** - full quality VR via WebRTC streaming
- [ ] **QR code pairing** - scan to connect Quest to PC
- [ ] **Adaptive quality** - bitrate adjusts to network conditions
- [ ] **Latency mitigation** - predictive pose, wider FOV render, ATW

---

## Technical Foundation (From AubsCraft R&D)

All performance optimizations developed in AubsCraft feed directly into Lost Spawns:

- [ ] **LOD system** - multi-tier with adaptive vertex budget
- [ ] **Compact vertex format** - 8 bytes/vertex for massive worlds
- [ ] **Greedy/binary greedy meshing** - 80-95% polygon reduction
- [ ] **Cave culling** - 15-bit flood fill, 50-99% underground cull
- [ ] **Indirect draw** - single draw call for all chunks
- [ ] **GPU frustum culling** - compute shader visibility
- [ ] **Texture arrays** - correct mipmapping, no atlas bleeding
- [ ] **GPU-rendered UI** - SpawnScene UI system (no HTML overlay)
- [ ] **Buffer compaction** - defragment vertex buffer on demand
- [ ] **Adaptive vertex budget** - FPS-driven LOD selection
- [ ] **WebXR integration** - VR + AR modes via XRService

---

## Related Projects

**AubsCraft** (`D:\users\tj\Projects\AubsCraft`) - Minecraft server admin panel + 3D world viewer. ALL voxel rendering R&D, performance optimization, VR integration, and GPU-rendered UI work is developed and proven in AubsCraft first, then applied to Lost Spawns. Research docs at `AubsCraft/Research/` cover:
- Renderer optimization audit
- Voxel LOD and greedy meshing
- WebGPU rendering optimizations
- Minecraft renderer analysis (Sodium, Distant Horizons)
- GitHub voxel engine reference
- VR UI design (QuestCraft/Vivecraft patterns)
- SpawnScene UI architecture (GPU-rendered UI system)
- PC-streamed VR architecture
- AR tabletop editor vision

**SpawnScene** (`D:\users\tj\Projects\SpawnScene`) - Gaussian splatting viewer. Source of the GPU-rendered UI system (UIElement, UIRenderer, FontAtlas) and WebXR integration (XRService, WebGLXRBlit).

---

## Art Direction

**This is a big kids' voxel survival game. NOT Minecraft. NO kid vibe.**

### Visual Identity
- Gritty, mature, post-apocalyptic. DayZ feel.
- Muted desaturated palette (olive, gray-brown, rust, dirty beige)
- No bright primary colors, no cartoon aesthetic, no clean surfaces
- Everything weathered, cracked, overgrown, abandoned

### Voxel Quality
- 0.5m voxels (half-meter cubes) for smoother terrain than Minecraft
- 32x32 textures minimum (photorealistic-inspired, not pixel art)
- Multiple texture variants per block type (visual variety)
- Normal maps for depth without extra geometry

### Lighting (Critical)
- Ambient occlusion - corners darken, crevices are dark
- Sun shadows - directional shadow mapping
- Smooth lighting - interpolated between blocks
- Interior darkness - buildings dark without light sources
- NVG post-process - green/white phosphor, noise grain

### Post-Processing
- Desaturated color grading (cold blue outdoors, warm near fire)
- Subtle film grain, vignette
- SSAO for depth
- Very subtle bloom on light sources only

### Color Palette
- Grass: muted olive/dark forest green
- Dirt: dark brown, muddy
- Stone: cold gray, weathered
- Metal: rusted orange-brown, oxidized
- Concrete: cracked, water stains, moss
- Asphalt: dark, cracked, weeds through cracks
- Sky: overcast grays, muted sunsets through haze
- Night: DARK. Flashlight shows 20m. NVGs essential.

### Reference Games
- DayZ (tone, atmosphere, weather)
- Teardown (small voxel realism, destruction, lighting)
- Vintage Story (mature voxel aesthetic)
- S.T.A.L.K.E.R. (atmosphere, abandoned environments)
- The Long Dark (isolation, weather, color grading)

Full art direction details: `Research/art-direction.md`

---

## Architecture Rules

Same as all SpawnDev projects:
- **SpawnDev.BlazorJS** for all JS interop. NEVER raw JS. NEVER eval. NEVER IJSRuntime.
- **SpawnDev.ILGPU** for ALL GPU compute. No CPU fallbacks.
- **Performance IS the feature.** Every decision through "does this make it faster?"
- **Zero-copy pipelines.** Data enters GPU and stays there.
- **Fix libraries first.** If ILGPU/BlazorJS has a bug, fix it there.
- **When in doubt, ask TJ.**
