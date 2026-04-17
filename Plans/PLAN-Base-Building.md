# Base Building - Brainstorm and Plan

**Status:** Living brainstorm. Decisions get locked as features mature.
**Owner:** Captain (TJ)
**Consulted:** Tuvok (research/planning), Data (VoxelEngine editor)
**Last updated:** 2026-04-16

---

## Status markers

- **[COMMIT]** - committed to v1.0, active work or next in queue
- **[LIKELY]** - strong fit, assumed yes unless something knocks it out
- **[UNDECIDED]** - interesting, uncertain value/cost tradeoff, revisit before touching
- **[DEFER]** - post-v1.0 or beyond scope
- **[REJECT]** - considered and ruled out (with reason)

---

## Vision

Your base is **yours** - and you can **pack it up and move**. Not a rigid land grab. Not a DayZ-style permabase-or-nothing. Lost Spawns borrows Fallout 76's C.A.M.P. philosophy: design a base, save it as a blueprint, drop it wherever claim rules let you. Redeploy next session. Trade blueprints with friends. Test a layout in the woods, move it to the mountains, rebuild richer.

Built on the hybrid terrain system: **smooth SDF ground + clean voxel blocks stacked on top** (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md)). Dig a foundation pit in a hillside, lay a concrete floor, stack walls, roof it, move in. Same world, two representations, unified build API.

**Design goals:**

1. **Build anywhere land rules permit.** No fixed plots. Hillside base, rooftop base, cave base, floating-platform base all valid.
2. **Blueprint-based portability.** Save, pack, move, redeploy. Your design is your signature.
3. **Modular block-based construction.** Snap-to-grid walls, floors, roofs, doors, stairs. Clean builds fast, complex builds slowly.
4. **Structural integrity matters.** Remove a support pillar, the roof falls. Raid tactics emerge from physics (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md)).
5. **Bases are tactical, not decorative.** Defenses, power, storage, crafting, smuggling, all expressed via placed blocks and furniture.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Hybrid terrain** - smooth + blocky coexistence, chunk-level block placement API (VoxelEngine Phases 3 + core voxel)
- **Structural integrity simulation** - stress propagation through blocks (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))
- **OPFS persistence** (Phase 8) - base blueprints saved locally, redeployable
- **Crafting system** - block recipes, building tools (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))
- **Entity system** (Phase 12) - claim markers, doors, turrets as entities

---

## Construction system

### [COMMIT] Block-based modular building

- Snap-to-grid 1m³ voxel blocks for constructed structures
- Place via in-hand building tool + build-menu UI
- Block categories: floors, walls, roofs, doors, windows, stairs, pillars, beams, fences
- Live preview (ghost block) before commit, shows snap point + structural validity

### [COMMIT] Material tiers

Lower tier = cheap but weak. Higher tier = expensive but durable.

| Tier | Materials | HP | Raid time |
|------|-----------|----|-----------| 
| 1 | Wood planks, scrap metal sheets | Low | Seconds with axe |
| 2 | Stone bricks, sandbags, reinforced wood | Medium | Minutes with hammer/pickaxe |
| 3 | Concrete, steel plate | High | Explosives or drills |
| 4 | Reinforced concrete, armor plate | Very High | Shaped charges, C4 |

### [LIKELY] Foundation requirement

- Structures need a foundation block on terrain / prior foundation
- Foundation anchors structural integrity calc
- Floating blocks (no support path to foundation) are flagged and collapse after a tolerance threshold
- Cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) for the simulation

### [LIKELY] Hybrid: blocks on carved terrain

- Carve a pit into a hillside with the terrain tool, lay concrete floor blocks at the bottom, stack walls
- Smooth terrain + clean blocks coexist in the same world
- Walls can tunnel into hillsides, making half-underground bases natural

### [UNDECIDED] Curved walls / custom shapes

- Players want curved walls, angled roofs, non-90-degree stairs
- F76 does limited angles. Too much freedom = trolly art, too little = boxy sameness.
- Lean: cardinal axis + 45° diagonals at launch, expand post-v1.0

### [REJECT] Full freeform block rotation

- Rotating every block arbitrarily = massive collision/physics complexity + troll builds
- Fixed cardinal + diagonals is enough for expressive builds

---

## C.A.M.P. blueprint system (the headliner)

### [COMMIT] Blueprint save + redeploy

- Select area (box or auto-detect your structure), save as blueprint file (.lsbp)
- Blueprint captures all blocks + furniture + defenses (not contents of containers)
- Pack current base into a camp device (returns components to inventory)
- Redeploy elsewhere: the device consumes materials, places the blueprint, ghost-preview first

### [LIKELY] Blueprint library

- Named blueprint collection stored in OPFS per character
- Multiple designs saved (mountain cabin, urban pillbox, medic station, trade stall)
- Quick-swap between blueprints at new claim site

### [LIKELY] Blueprint sharing

- Export blueprint as file (copy / share like Minecraft schematics)
- Import a friend's blueprint, redeploy at your claim
- Community blueprint sharing long-term - web portal post-v1.0

### [UNDECIDED] Material persistence on pack

- When you pack, do you get 100% materials back? 80%? Degraded by condition?
- Lean: 80-90% refund (small cost for reconfigurable convenience)

### [LIKELY] Claim marker device

- Physical placed item that defines your camp location
- Build radius extends from marker (50-100m)
- Only one active marker at a time per character
- Marker has HP - destroyed by raiders = claim lost, blueprint recoverable but must redeploy

### [LIKELY] Camp decay

- Inactive camps decay after N days of character absence
- Decay means HP drain on blocks - someone returns after 2 weeks offline and finds wood walls gone but concrete intact
- Protects against base-squatting while absent

### [REJECT] Fixed settlement plots

- No "you can only build in these approved locations"
- Go build on that mountaintop. Go build in that parking garage. Free-placement is core.

---

## Functional base components

### [COMMIT] Storage containers

- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) storage chapter
- Wooden crates (L, T1), ammo cans (S, T2, ruggedized), safes (M, T3, locked), vaults (L, T3, codeable)
- Capacity scales with size + material tier
- Lockable with pin code / picklock resistance tier

### [COMMIT] Crafting stations placement

- Cross-ref [PLAN-Crafting.md](PLAN-Crafting.md) stations
- Workbench, forge, sewing, chemistry, gunsmith, armor bench, generator each placed as furniture
- Station rank visible (Rank 1-3), drives what player can craft here
- Destroyed station = lose rank investment (repair or rebuild)

### [LIKELY] Power grid

- Generators (diesel, solar, wind, waterwheel, battery bank) produce power
- Wire nodes connect generator → consumers
- Consumers: lighting, alarms, cameras, auto-turrets, electric doors, refrigerators (food spoilage), workshops
- Power-down mode: manual operations still work, powered gear goes offline
- Sabotage vector: cut a wire, the base goes dark mid-raid

### [LIKELY] Defensive systems

- **Walls + gates** - tiered material, block placement
- **Guard towers** - elevated platforms with shooting slits
- **Auto-turrets** - require power, ammo magazine, skill to build (cross-ref Marksman/Engineer in [PLAN-Player-Progression.md](PLAN-Player-Progression.md))
- **Alarm systems** - trip wires + siren triggers (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) traps)
- **Cameras** - monitor angles from inside, power-dependent
- **Searchlights** - sweeping night lighting

### [LIKELY] Anti-recon measures

- **Thick walls** - GPR defeated by reinforced-concrete walls (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) GPR)
- **Faraday rooms** - lead-lined rooms block GPR + jam radios
- **RF jammer** - active device, blocks enemy radios inside claim radius, power-hungry
- **Dummy rooms** - decoy containers, fake interior layouts to confuse tabletop AR scans (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) AR tabletop)

### [UNDECIDED] Water and plumbing

- Collect rainwater into barrels → filter → purify
- Hot water for decontamination showers (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Could be fun detail or scope creep - revisit during environment hazards impl

### [LIKELY] Medical station

- Bed + medical kit inventory + surgery trays
- Heal passively when resting, faster with medical skill
- Decontam shower cleanses rads (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

### [LIKELY] Vending machines / trade stall

- Cross-ref [PLAN-Economy.md](PLAN-Economy.md)
- Place in claim, set prices, other players can browse + buy while you're offline
- Your cash/scrap accumulates for pickup
- Must be physically visited by buyers (tactical friction keeps them from being infinite-ATM-grindy)

### [UNDECIDED] Farming / agriculture

- Planters + seeds → crops over time
- Renewable food source for sedentary base-players
- Maybe [DEFER] - food scavenging is core loop for v1.0, farming competes with that

---

## Structural integrity (raid physics)

### [COMMIT] Stress propagation

- Blocks transmit load through the structure
- Support path to foundation required (beam/pillar/wall chain)
- Overextended cantilevers sag, then collapse
- Destroy a pillar, the roof it supports falls (and everything above)
- Cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) for shared simulation

### [LIKELY] Raid tactics that emerge

- Shaped charge at base support pillar > hours of wall-chipping
- Tunnel under a base (smooth SDF dig) and collapse it from below
- Grenade through a window bypasses wall HP entirely
- Scale walls with ladders, bypass external defenses, clear from inside

### [LIKELY] Structural feedback

- Visual stress indicators (cracks, sagging mesh deformation)
- Audio cues (creaks, groaning wood) before collapse
- HUD warning if you're standing under a failing structure

### [LIKELY] Damage persists until repaired

- Bullet holes, blast craters, burn marks stay
- Repair requires materials + time + station access (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) repair system)

---

## Claim rules and PvP

### [COMMIT] Claim marker + radius

- Claim defines where you can build + where others can't build on top of your footprint
- Claim radius ~ 50m initially, extensible with higher-tier markers
- Overlapping claims prohibited - pick unclaimed land

### [LIKELY] Raid windows

- Raid timer / safe hours: bases are raidable only during designated windows (DayZ experimental servers have done this)
- OR: full-time raidable with offline damage cap (F76-style)
- Lean: server-config switch, default to F76-style (reduced offline damage, always-on PvP)

### [LIKELY] Offline damage cap

- When owner is logged off, block HP takes reduced damage
- Prevents 3am raid cheese, rewards active defense
- Tradeoff: raiders have to catch owners home

### [UNDECIDED] Base PvP flag zones

- Should certain zones (safe zones, trader towns) prohibit base building?
- Lean: yes, defined NPC-controlled safe zones reject claims

### [LIKELY] Infamy-based raid risk

- High-infamy players (cross-ref [PLAN-Economy.md](PLAN-Economy.md) reputation) draw NPC-contracted raid parties
- Your bad rep comes home to hit your base

---

## Decorative / personalization

### [LIKELY] Signs + banners

- Custom text signs, placed on walls
- Banners with faction insignia
- Graffiti spray can for terrain walls (lightweight, no geometry cost)

### [LIKELY] Furniture

- Tables, chairs, beds, lockers, shelves (some functional, some decorative)
- Bedroom, kitchen, armory, war room - base feels lived-in

### [LIKELY] Radio / music player

- Tune to in-world radio stations (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))
- Play your faction's claimed broadcast tower content
- Atmospheric but also signals "this camp is active" to passing players (scent trail trade-off)

### [UNDECIDED] Trophy display

- Mount cryptid heads, rare weapons, flags captured from enemy bases
- Reputation boost with fellow survivors who visit

### [LIKELY] Lighting

- Torches, lanterns, electric floodlights, moody wall sconces
- Cross-ref flood-fill lighting (VoxelEngine Phase 14)

---

## Base types (natural archetypes)

### [LIKELY] Defensive fortress

- Thick walls, auto-turrets, layered defenses, minimal windows
- Sacrifice storage for armor tier
- Common choice for high-infamy players

### [LIKELY] Trade stall

- Vending machines front, small living quarters back
- High-traffic location preferred (near safe zones or roads)
- Low defensive investment, high economic throughput

### [LIKELY] Medic post

- Bed + medical station + decontam shower
- Welcome sign for passing survivors (rep gain by healing others)
- Cross-ref field medic loadout in [PLAN-Player-Progression.md](PLAN-Player-Progression.md)

### [LIKELY] Hidden bunker

- Entry concealed by carved terrain, decoy surface shack
- Underground storage + crafting, surface looks innocuous
- Defends via obscurity, not firepower

### [LIKELY] Nomad wagon

- Minimal base, vehicle-mounted storage, portable workbench (Rank 1)
- Pack up and move weekly - reconnaissance-heavy playstyle
- Low investment, low loss on raid

### [UNDECIDED] Squad compound

- Multiple players share a single claim
- Aggregate contributions to material cost + defense
- Opens group-politics surface area - interesting but complex

---

## Base building interactions with other plans

### Terrain carving (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

- Dig foundations + pits before building
- Tunnel between buried rooms
- Structural integrity is shared simulation

### Traps and defenses (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) traps)

- Bear traps at doorways, tripwires across corridors, claymores on rooftops
- Buried stash bags beneath floor blocks

### Crafting (see [PLAN-Crafting.md](PLAN-Crafting.md))

- Station placement = base function
- Rank upgrades invested into your base - ties you to the spot until you pack-and-move

### Hazards (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Sealed rooms for decontamination
- Rad-shielded vaults (lead-lined room = safe zone inside hazard map)

### Economy (see [PLAN-Economy.md](PLAN-Economy.md))

- Vending machine + trade stall integration
- Base advertising (signs, radio broadcast)

### GPR / AR tabletop (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

- Reinforced walls defeat GPR scans
- Faraday rooms defeat radio tracking
- Dummy rooms mislead enemy AR tabletop scouts

---

## Gameplay verbs base building enables

- Spend an afternoon surveying a hillside, carve a foundation pit into the slope, lay concrete, stack walls into the earth for a half-buried bunker
- Pack your mountain cabin blueprint into a camp device, hike three days to a coastal cove, redeploy the same build with ocean view
- Trade a rare "trade stall" blueprint with another survivor in exchange for their "fortress" blueprint
- Rig an auto-turret on the roof, wire it to a power line, hope the generator doesn't die mid-raid
- Test a new defense layout in a low-population zone before committing to the real location
- Carve a hidden entrance beneath a collapsed building, build a full workshop 30m underground, never be found by casual raiders
- Install a Faraday room inside your vault, store rare loot there, watch enemy GPR scans return nothing but noise
- Lose a camp to a raid, recover the blueprint file, rebuild at a safer location within an hour, smarter this time
- Host a trade stall on a highway, stand by the window with a shotgun while customers browse your vending machines
- Watch a rival squad tunnel under your base with shaped charges, collapse your kitchen floor, turn the defense into a lethal multi-story killbox
- Deploy a temporary "field forward" nomad cart near an active event (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md)), pack it and move when the event resolves
- Swap from defense-heavy layout to medic-post layout between sessions - same location, different claimed blueprint

---

## Open questions

1. **Blueprint deployment cost** - free or material-based? Balancing reconfigure-at-will vs build-once-commit.
2. **Claim marker cooldown** - how often can a player move? Instant = zero commitment, weekly = heavy commitment.
3. **Block HP model** - flat HP or material-vs-tool matrix (axe chops wood fast but struggles with concrete)?
4. **Raid loot rules** - raider takes containers + mods only, or all placed blocks become loot?
5. **Server block budget** - how many blocks per server? Streaming is fine but total count matters for persistence.
6. **Multi-claim characters** - one claim per character, or limited number (premium feature)?
7. **Decay tuning** - how many days before an inactive camp degrades? Too fast = punitive, too slow = ghost towns.

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Block placement | Voxel API + snap-to-grid + in-hand tool |
| Structural integrity | Stress simulation (cross-ref Terrain Carving) |
| Blueprint save/load | OPFS + block-region serialization |
| Power grid | Wire graph + consumer/producer registry |
| Auto-turret | Entity AI + ammo system + perception |
| Claim marker | Entity + world-state per region file |
| Raid physics | Explosives + structural simulation + persistence |

---

## Next actions

1. Define block data schema (type, material tier, HP, orientation, metadata)
2. Prototype blueprint save/load round-trip (serialize → OPFS → deserialize → redeploy)
3. Structural integrity spike - 2-room structure, destroy one pillar, watch the roof fall
4. Crafting station placement flow (place → rank read → UI opens at station)
5. Raid balance pass (block HP values, explosive damage, offline damage cap) before wide playtest

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
