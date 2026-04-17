# Terrain Carving - Brainstorm and Plan

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

Lost Spawns is DayZ-style survival with **hybrid terrain**: smooth SDF natural terrain + blocky player-built structures. Terrain carving must work coherently across both representations. The world is a persistent record of what players and events have done to it - craters, trenches, mines, and scars stay until someone rebuilds.

**Design goals:**

1. **Persistent scars.** Every dent a player makes should be there when they come back. No auto-repair of natural terrain.
2. **Dual-representation carving.** An explosion that hits a hillside (SDF) and a wooden shack (blocks) damages both naturally. One API, two backends.
3. **Material-aware.** Dirt carves fast, stone slow, concrete slower, bedrock not at all. Tools and explosives matter.
4. **Emergent combat use.** Dig foxholes, blast breach-holes in walls, collapse tunnels. Carving is a core combat verb, not a sandbox feature.
5. **Performance first.** Re-meshing cost after a carve must fit Quest 3S frame budget. Batch modifications, bound dispatches.

---

## Foundation (what exists today)

From the VoxelEngine library:

- **`Destruction/ExplosionKernels.cs`** - sphere-based block destruction, blast resistance from BlockRegistry, structural integrity hook. CPU reference done, GPU kernel planned.
- **`Physics/StructuralIntegrity`** - unsupported-block detection (collapse after dig).
- **Phase 3 plan: "SDF terrain modification kernel"** - modify SDF values in sphere (dig/fill). ExplosionKernels is the blocky half; this is the smooth half.
- **`DamageOverlay`** - cracks before full destruction (tested, pixel-readback verified).
- **`PackedBlock`** - 12-bit type + 4-bit damage per block. Damage bits exist but aren't driven by carving yet.

From Lost Spawns today:
- **No runtime carving.** `HeightmapLoader` and `TerrainGenerator` only run at world-gen. `VoxelEngineService` and `WorldService` load/mesh chunks. No player input produces terrain change.

**The gap:** input → tool model → damage/destroy API → library kernels → re-mesh → persist.

---

## Tool-based carving (intentional player input)

### [COMMIT] Shovel - dig soft materials

- Carves dirt, sand, gravel, loose rock
- One swing = small SDF deformation (smooth) OR one block removal (blocky)
- Tool durability decreases with use
- Stamina cost per swing
- Noise level: low (attracts zombies only at short range)
- Speed: fast on dirt, blocked by stone

### [COMMIT] Pickaxe - mine hard materials

- Carves stone, ore veins, concrete
- Higher stamina cost than shovel, slower swings
- Exposes ore blocks buried in terrain
- Noise level: medium
- Quality tiers (iron < steel < hardened steel) affect speed and allowable material

### [LIKELY] Axe - chop trees, not terrain

- Fell trees into usable logs
- Breaks wooden walls/structures faster than other tools
- Not for stone/earth
- Relevant here because tree felling leaves stumps and affects terrain shape slightly (stump becomes a ~1-block obstacle)

### [LIKELY] Drill - powered mining

- Requires fuel/battery
- Fast material removal
- High noise, attracts infected aggressively
- Larger carve radius per tick
- Industrial tool - rare loot, not crafted

### [UNDECIDED] Excavator / vehicle-mounted tools

- Vehicles with mounted equipment (bulldozer blade, mining drill)
- Could trivialize terrain modification, undermine survival pacing
- Decision hinges on whether vehicles exist in v1.0 at all
- Revisit after vehicle decision

### [UNDECIDED] Demolition charges (crafted)

- Player-crafted explosives from scavenged materials
- Different from military grenades (scrap C4 from car batteries + fertilizer)
- Survival progression: early game = shovel, mid game = scavenged grenades, late game = crafted charges
- Question: how much crafting depth do we want in v1.0?

---

## Combat/weapon carving

### [COMMIT] Grenades and military explosives

- Standard blast radius with falloff
- Fragmentation (small radius, high damage to entities, minimal terrain)
- High explosive (medium radius, notable crater)
- Thermobaric (large radius, deep SDF deformation)
- Already have `ExplosionKernels.DestroyInSphere` as the foundation

### [COMMIT] Rocket/RPG

- Direct-hit point explosion with larger blast than grenade
- Building breaching tool - can open wall sections
- Rare ammunition to prevent spam

### [LIKELY] Tank / vehicle weapons (artillery shells)

- Large craters, deep penetration
- Requires vehicle crew (multi-seat)
- May be DEFERRED if vehicles slip to v1.1

### [LIKELY] Bullet damage accumulation

- Each bullet hit adds small damage to a block
- Tracked via `PackedBlock` damage bits (4 bits = 16 damage stages)
- Enough hits → block breaks
- Enables "shooting through walls" gameplay with realistic wear
- Question: track damage per block or per chunk? Per-block is more granular but more state

### [UNDECIDED] Molotov / fire spread

- Burning terrain and structures
- Requires fire-spread simulation (Phase 9 fluid sim companion?)
- Charred blocks become structurally weaker
- Smoke affects visibility
- Heavy sim cost - may not be worth it for v1.0

### [LIKELY] Traps and snares (DayZ-style arsenal)

Signature DayZ gear, direct fit for survival paranoia. Great synergy with moldable terrain - most of these get buried, concealed under leaves, or tucked into carved nooks.

- **[COMMIT] Tripwires** - strung between two anchor points (trees, posts, stakes). Thin, hard to spot at distance. Rigged to:
  - Grenade (kill trap)
  - Flare (alarm, reveals intruder's position at night)
  - Noise can / tin-can rattle (alarm, reusable, pulls zombies toward the intruder)
- **[COMMIT] Bear traps** - placed on ground, leg-snare, slows victim to crawl until freed, damage over time. Reusable after reset. Moldable terrain lets you half-bury them to hide the jaw.
- **[COMMIT] Land mines** - anti-personnel proximity charge, blast radius similar to frag grenade. Buried with moldable brush - invisible from above unless player has detection tool (see Buried-stash detection below).
- **[COMMIT] Claymores** - directional fragmentation charge, wired detonator (tripwire or remote). Kill-cone in front, safe behind. Classic choke-point weapon: narrow trail, doorway, staircase.
- **[LIKELY] Punji pit** - moldable-terrain-native trap. Dig pit, line with sharpened stakes, cover with thin dirt layer and leaf overlay. Pure survival-horror vibe, zero explosives needed.
- **[LIKELY] Spike board** - plank with nails, placed flat on ground or tucked into snow/grass. Cheap, ubiquitous, forces attention to footing.
- **[UNDECIDED] Snares** (wildlife-focused) - passive animal trap for food income, different vibe from combat traps.
- **[UNDECIDED] Remote-detonated IEDs** - player-wired explosive with clacker. More agency than proxy mine, less "set and forget."

**Placement and persistence:**

- All traps persist across logins. Wire a cabin doorway, log out for a week, it's still armed.
- Trap ownership not tracked - step on your OWN bear trap and it bites. Survival horror, no friendly-fire immunity.
- Moldable-terrain integration: dig shallow depression, place mine, smooth dirt over it - invisible without detector.

**Detection and disarming:**

- Keen observation (slow movement, crouch) helps spot visible traps.
- Metal detector reveals buried mines and claymores (see Buried-stash detection below).
- Wire cutters + skill check to disarm visible tripwires. Failed disarm = detonation.
- Shooting a mine triggers it from safe distance (loud, wastes ammo, no loot recovery).

**Gameplay verbs this enables:**

- Wire a cabin doorway with a flare - wake up if anyone approaches at night.
- Lay a bear-trap gauntlet on the only path up to your base.
- Buried minefield along a treeline, forcing opponents onto the exposed road.
- Claymore at the top of a staircase, detonator taped under your pillow.
- Bait-and-trap: drop visible loot next to a punji pit, shoot anyone who reaches for it.
- Rig your OWN corpse with a grenade - the looter triggers it while searching you.
- Wire-cut a rival's trap line to deny their ambush setup without triggering it.

---

## Environmental / dynamic carving

### [LIKELY] Crashed aircraft / vehicles

- Dynamic world events spawn wrecks
- Impact crater + debris field on spawn
- Carves terrain on arrival (one-shot SDF modification + block destruction)
- Valuable loot inside, visible from distance

### [UNDECIDED] Artillery strikes as world events

- Scheduled random events in some zones (combat zones)
- Series of blasts, gameplay danger and terrain modification
- Pacing tool: forces players to move, carves new craters continuously
- Question: does this fit DayZ vibe or cross into warzone sim?

### [UNDECIDED] Water erosion over time

- Rivers slowly carve channels deeper
- Rain creates gullies on steep terrain
- Time-based, runs at very slow cadence
- Big realism win, questionable cost
- Alternative: bake erosion into world-gen once, skip runtime sim

### [DEFER] Earthquakes / seismic events

- Zone-wide terrain deformation
- Collapses buildings in affected area
- Spectacular but expensive (every chunk in zone re-meshes)
- Post-v1.0

### [DEFER] Meteor impacts

- One-shot large crater + fire spread
- Cool, niche, not core to survival
- Post-v1.0

### [REJECT] Weather damage to terrain (tornadoes flattening trees)

- Tornado particle effects fine, but terrain deformation from wind is expensive and rarely noticed
- Trees as separate entities can be felled by wind if we want that effect, no SDF modification needed

---

## Structural physics

### [COMMIT] Unsupported block collapse

- Already have `StructuralIntegrity` hook
- After dig/blast, check affected columns for blocks with no supporting neighbors
- Unsupported blocks fall (become falling-block entities or just drop to first solid)
- Critical for tunnel collapse gameplay

### [LIKELY] Falling blocks (sand/gravel)

- Sand and gravel fall into any void below them immediately
- Enables "sand pour" effects, collapsing sand walls in deserts
- Straightforward compared to full structural integrity

### [LIKELY] Beam / pillar structural requirements

- Large structures need support pillars every N blocks
- Remove the pillar, large section collapses
- Enables "take out the support beam" tactic against player bases
- Question: simulate full structural mesh (expensive) or use heuristic (cheap but fakey)?

### [UNDECIDED] Real-time debris physics

- Blocks falling from collapse tumble as rigid bodies
- Dust clouds, debris piles
- Visually satisfying, physics-simulation cost unclear
- Simpler alternative: debris just disappears after a few seconds, dust particle effect covers the transition

### [DEFER] Cliff erosion from overhang

- Overhangs too large become unstable, slough off
- Cool realism, niche value
- Post-v1.0

---

## Material and damage model

### [COMMIT] Per-material hardness

- `BlockRegistry` already has `Hardness` field
- Tool effectiveness × material hardness → time per carve tick
- Wrong tool (shovel on stone) = very slow or impossible
- Right tool (pickaxe on stone) = normal speed

### [COMMIT] Damage stages before destruction

- 4 damage bits in `PackedBlock` = 16 stages
- `DamageOverlay` renders cracks matching stage
- Multiple hits required to destroy - fast blocks (1 hit for dirt with shovel) vs slow blocks (~16 hits for stone with pickaxe)
- Damage decays very slowly when unhit (natural repair? or permanent?) - see next entry

### [UNDECIDED] Damage decay (natural repair)

- Slightly damaged blocks slowly heal over long real-world time
- Realistic but makes combat less impactful
- Alternative: damage is permanent, encourages rebuilding
- Lean REJECT for survival feel; LIKELY for structures-owned-by-server

### [LIKELY] SDF density as implicit "hardness"

- SDF terrain has no discrete blocks - "hardness" = density field + material-at-voxel lookup
- Carving subtracts from density field proportional to tool power and material
- Multiple passes needed for hard materials (stone base of mountain vs dirt topsoil)

### [UNDECIDED] Heat / fire material state

- Burned blocks change appearance and become structurally weaker
- Ties into [UNDECIDED] Molotov/fire spread - stands or falls together

### [REJECT] Block type transitions via damage

- "Damaged stone becomes gravel" style mechanics
- Added complexity for minor gameplay benefit
- Rebuild from inventory is cleaner model

---

## Bedrock / unbreakable floor

### [COMMIT] Bedrock layer

- Unbreakable layer at world bottom (-256 or wherever)
- Prevents infinite digging, world falling apart
- Distinct block type with max hardness and special damage=immune flag
- Visual: dark grey or unique texture

### [LIKELY] Biome-varied bedrock depth

- Mountainous terrain has bedrock closer to surface
- Swamps have deep bedrock allowing deep wells
- World-gen parameter

---

## Persistence and serialization

### [COMMIT] All player carves are permanent

- Once dug, stays dug until refilled
- Persisted via Phase 8 (VoxelEngine OPFS region files)
- Dirty-chunk tracking marks modified chunks for save

### [COMMIT] Modification log per chunk

- Each modified chunk gets flagged dirty
- On save, only dirty chunks write
- Proven OPFS region-file pattern (310 MB/s benchmarked)

### [UNDECIDED] Modification granularity

- **Option A:** save full modified chunk (simple, slightly wasteful for small carves)
- **Option B:** save a delta list per chunk (fewer bytes, requires diff computation)
- Phase 15 palette compression makes Option A cheap enough that B probably isn't worth the code complexity
- Revisit after palette compression lands

### [LIKELY] Session-scoped batch saves

- Don't save every carve immediately
- Buffer modifications, flush on quit / periodically / on chunk-leave
- Matches AubsCraft's proven pattern

---

## Performance and re-meshing

### [COMMIT] Bounded re-mesh per frame

- Cap at N chunks re-meshed per frame (tie to StreamingBudget)
- Over-cap queues to next frame
- Blast affecting 30 chunks doesn't stall the frame

### [COMMIT] Asymmetric update cost

- Small carve (1-2 blocks) → update only affected chunks
- Large carve (explosion, radius > chunk) → batch all affected, compute once
- Avoid N small remesh calls when one big one would work

### [LIKELY] Predictive pre-mesh

- When player raises pickaxe (aiming), pre-compute "what would this hit look like"
- Makes the actual hit feel instant
- Cost: one extra chunk remesh per aim-start, discarded if they move away

### [UNDECIDED] Sub-chunk re-meshing

- Only re-mesh affected 8x8x8 sub-region within a chunk
- Bigger savings for small carves on big chunks
- Complicates neighbor-padding logic - interacts with Data's task #8 fix
- Revisit after Phase 3 stabilizes

---

## Multiplayer sync (scope decision)

### [DEFER] Networked carving for multiplayer

- Lost Spawns v1.0 is single-player
- Multiplayer adds network sync cost, authority question (client-side prediction vs server-authoritative), anti-cheat
- Post-v1.0, possibly v1.1 or v2.0
- Architecture note: keep carve API server-ready (all modifications pass through a single entry point) so multiplayer can intercept later

---

## Aesthetic polish

### [LIKELY] Dust/debris particle burst on hit

- Every pickaxe/shovel strike emits small particle burst
- Color matches material (brown for dirt, grey for stone, white for snow)
- Ties into Phase 5 post-processing

### [LIKELY] Impact crater shaping

- Explosions leave realistic crater shape (lip, scorch, debris skirt)
- Not just a sphere - use noise-perturbed SDF modification
- Small code cost, big visual impact

### [UNDECIDED] Tool animation and first-person swing

- First-person weapon/tool model with swing animation
- Entity rendering (Phase 12) territory
- Could start with placeholder and iterate

### [UNDECIDED] Sound-per-material

- Different dig/break sounds for each material
- Audio integration (Phase 16) provides material-at-position query
- Art/audio asset scope question

### [LIKELY] Scorch marks on terrain near explosions

- Dark decal around blast sites
- Fades over very long time (or permanent)
- Uses DamageOverlay infrastructure at a different abstraction level

---

## Unique-to-hybrid (SDF + blocky) considerations

### [COMMIT] Unified carve API

- `ITerrainCarve.ApplySphere(center, radius, tool, material)`
- Internally routes: intersects SDF region → SDF modification; intersects block region → block damage
- Single entry point for explosions, tools, events
- Returns list of modified chunks for re-mesh

### [UNDECIDED] SDF → block transition zones

- What happens when player carves INTO the SDF/block boundary?
- Example: natural cliff face (SDF) with built wall (blocks) on top. Blast below carves both.
- Requires clean contract between SDF and block storage at overlap regions
- Pushes on Phase 3's SDF storage decisions

### [UNDECIDED] Building blocks INSIDE SDF-carved caves

- Can a player excavate a cave (SDF mod) and build inside it (place blocks)?
- DayZ-style basement scenarios
- Depends on whether block space and SDF space can coexist in the same voxel
- Likely yes for v1.0 but worth confirming

### [UNDECIDED] SDF "healing" for natural erosion

- Natural terrain slowly smooths back toward original form
- Player digs cave, walks away for 100 real-time hours, cave partially fills with sediment
- Realistic but undermines "permanent scars" design goal
- REJECT by default; flag here because it's a natural thought to raise and reject cleanly

---

## Open questions

1. **Tool durability** - yes/no for v1.0? If yes, tracks on item or consumes materials?
2. **Damage per block vs per chunk** - granular state per block (state explosion) or coarser chunk-level?
3. **Material hardness tuning** - static data or procedural from geology biome layer?
4. **Carving affects neighbor blocks** - does pickaxing stone expose surrounding ore, or is ore placement pre-computed?
5. **Tool progression gate** - which materials unlock which tools? Fixed tech tree or player-defined?
6. **World bottom** - hard bedrock or infinite scroll (teleport back to top)?
7. **Modification persistence limit** - cap on total modifications in save file? (a million dug blocks at 3 bytes each = 3 MB delta, trivial)

---

## Dependencies on VoxelEngine phases

| Feature | Depends on Phase |
|---------|------------------|
| Damage stages | existing DamageOverlay + PackedBlock damage bits (done) |
| Unsupported collapse | existing StructuralIntegrity (partial) |
| SDF carving | Phase 3 SDF terrain modification kernel |
| Block carving | existing ExplosionKernels (partial - needs GPU kernel) |
| Re-mesh budget | Phase 13 StreamingBudget + Phase 16 streaming service |
| Persistence | Phase 8 OPFS region files |
| Bulk modification compression | Phase 15 palette compression |
| Lighting recalc after carve | Phase 14 flood fill lighting (dig into mountain = dark interior) |
| Sound per material | Phase 16 audio integration points |

---

---

## MOLDABLE TERRAIN (Captain's focus - deep dive)

Moldable terrain is the headline feature - **not just destructive carving, but sculpting**. Push, pull, smooth, pile, flatten. The player can shape natural terrain like clay.

This is possible because Phase 3 makes natural terrain an SDF (signed distance field), not a grid of blocks. SDFs are continuous - you can add or subtract density at any point, at any resolution, and the surface re-meshes smoothly via DMC. This is the same substrate Astroneer, Dreams, and No Man's Sky's Terrain Manipulator sit on.

### Reference point: Astroneer

Astroneer's Terrain Tool is the gold standard for moldable survival gameplay. Verified features worth porting:

- **Dual-mode brush** - hold trigger to deform, modifier key flips dig→fill
- **Circular brush with falloff** - center strongest, edges feather
- **Material conservation** - dug material fills tool's "canister," can be redeposited elsewhere
- **Material types visible in terrain** - dirt / clay / resin / compound, each with its own color band
- **Base-building foundation plates** - flatten a platform for structures
- **Grouping** - contiguous material regions highlight when aimed at (shows what you're about to dig)
- **Finite undo** - can reverse recent modifications (tool has a memory buffer)

### [COMMIT] Moldable core: bidirectional SDF brush

- Tool/verb: **push** (add density, raises terrain) + **pull** (subtract density, lowers terrain)
- Brush positioned at surface point under crosshair, radius configurable
- Held input = continuous modification, bounded by re-mesh budget
- Matching surface re-meshes every frame as density updates
- **Hook:** extends Phase 3's SDF modification kernel to support additive ops, not just subtractive

### [COMMIT] Brush profiles

- **Sphere** - default, round deform, Astroneer-style
- **Flatten** - targets a height plane, pushes down anything above, fills anything below, until surface matches the plane. Best feature for base-building terraces.
- **Smooth** - averages density within radius toward local mean, removes bumps and spikes
- **Ramp** - creates graded slopes between two points (dig a road, raise a berm)
- Brush selection is a radial menu or hotkey

### [COMMIT] Material awareness

- Every voxel has a material ID (sand, dirt, clay, stone, iron-bearing stone, etc.)
- Brush can only mold materials softer than tool power
  - Bare hands (if implemented): nothing
  - Shovel: dirt, sand, clay
  - Pickaxe: all of the above + stone
  - Powered drill: all + hard stone, iron, ore
  - Explosive charge: all, ignores power rating
- Brush reports "tool insufficient" when aimed at material it can't mold

### [COMMIT] Material economy (conservation)

- **Dig:** material added to player inventory (or tool canister)
- **Fill:** material drawn from inventory, deposited at target
- Finite resource - can't just level mountains infinitely without somewhere to put the dirt
- Exceptions: vehicle-mounted excavator (Phase N+1) has huge canister, mining drills compact material
- Question: simplify to "all dirt is one category" or keep per-material granularity?

### [LIKELY] Material layering at deposit sites

- Deposited dirt goes on top of existing terrain as a new layer
- Visible color/texture shift where player has built up terrain
- Over time (days?) deposited material "settles" and blends with underlying
- Enables visible evidence of player activity in the world

### [LIKELY] Snap-to-grid and flat-plane modes

- Hold modifier: brush snaps modifications to a grid (1-block increments)
- Useful for building approaches where blocky construction meets moldable terrain
- Another modifier: locks Y axis, only modifies horizontally (dig sideways into a hillside without going down)

### [LIKELY] Finite undo buffer

- Tool stores last ~30 seconds of modifications
- Undo key reverses them in reverse order
- Stored as SDF diff per step (compressed, small)
- Only applies to modifications made by THAT player's tool - not world-wide undo

### [UNDECIDED] Terrain paint (reskinning without reshaping)

- Paint material color/type onto existing terrain without changing shape
- Useful for decoration (grass → bare earth path markers)
- Orthogonal to moldability - considered separate feature

### [UNDECIDED] Terraforming speed tiers

- Base moldability is slow (fits survival pacing)
- Advanced tools mold faster (progression reward)
- Vehicles/base-attached machines mold fastest (late game)
- Without this: moldability is fun for 5 minutes then tedious for serious earthworks

### [UNDECIDED] Noise-perturbed brushes (not just smooth sphere)

- Brush adds dirt in a noisy organic blob (looks natural)
- vs. clinical smooth sphere (looks artificial)
- Can mix: smooth for construction, noisy for natural-looking earthworks
- Small shader cost, big aesthetic win

### [UNDECIDED] Multiplayer concurrent molding

- Two players mold same chunk simultaneously
- Requires last-writer-wins or operational transform
- DEFERRED with rest of multiplayer

### [REJECT] Grid-snapped "voxel sculpt"

- Making moldable terrain actually grid-snap to voxels defeats the whole point of SDF
- Use the blocky building system (separate) for that use case

### Buried stash detection and counter-play (hide vs. seek arms race)

Moldable terrain lets players bury anything - supply caches, bodies, mines. That only creates REAL tension if there are plausible ways to FIND buried things. This subsection is the "seeker" half of the dynamic - the arms race that makes burying meaningful instead of invincible.

**Detection tools (seeker side):**

- **[COMMIT] Metal detector** - ground-scan tool. Beeps faster/louder nearer to buried metal. Design hooks:
  - Battery powered (finite scan time, forces prioritization)
  - Material discrimination - ferrous vs non-ferrous (distinguishes "buried gun" from "buried iron ore node")
  - Depth falloff - shallow targets ping clearly, deep targets give faint signal
  - Tiers: VLF (cheap, common), pulse-induction (rare, military-grade, deeper + better discrimination)
  - Headphones accessory: beeping is silent to others nearby (stealth scanning)
- **[LIKELY] Ground disturbance visual** - recent modifications leave a "fresh dirt" texture overlay. Fades over in-game days. Careful attacker can cover tracks with grass/leaf decoration brush.
- **[LIKELY] Scent-tracking dogs** - AI companion or NPC. Leads player along scent trails. Finds buried corpses and recently-handled items. Countered by scent masking kit.
- **[LIKELY] Ground-penetrating radar (GPR)** - backpack unit with wheeled antenna cart. Sweeping the antenna over suspect ground produces a "B-scan" slice on a mini-display - hyperbolic returns mark buried objects (metal OR non-metal, including wood, plastic, cloth - defeats the non-metal container trick). Design hooks:
  - **Slow sweep** - walk the antenna at ~2 m/s, no sprint-scanning
  - **Visible operation** - the cart is obvious, anyone watching knows you're searching
  - **Skill check on readout** - hyperbola interpretation is non-trivial; novices miss faint returns, experts read soil layers
  - **Very battery hungry** - scanning a single 10x10 plot drains a full cell
  - **Rare tier** - military/industrial loot only, not craftable
  - Defeats non-metal containers. Still defeated by: very-deep burial (below antenna range), burying adjacent to ambient voids (rock/root/existing cave) that muddy the signature, or layered decoy stash sitting on top of the real one
  - Late-game "arms escalation" tool - emerges once metal detectors saturate at mid-game, restoring tension for players who thought they'd solved the hide problem
  - Real-world reference: archaeology, utility locating, forensics - finding bodies and caches is its literal IRL use case, perfect survival-horror fit

  **Raid and recon use cases (the big one - not just buried stashes):**

  The same B-scan that reads buried objects reads bodies behind thin walls (wood, drywall, shack walls). Shows silhouettes moving in real time. This turns GPR from "stash-finder" into the signature **raid-planning and raid-support tool** of the game.

  - **[LIKELY] Through-wall silhouette readout** - passive scan shows body-shaped returns behind wooden/drywall/thin-stone walls, moving in real time. Denser materials block or degrade the signal.
  - **Pre-raid recon** - scan a target base from adjacent cover. Map defender positions, count occupants, learn patrol routes before your squad commits.
  - **Live raid intel** - one teammate stays outside on scanner, calls movement to the breach team: "Two in the east wing, one moving toward the basement stairs, one stationary in the loft - probably asleep."
  - **Find the panic room** - a still silhouette among moving ones is someone hiding, almost always near valuables.
  - **Post-breach sweep** - scan each room through the doorway before entering, avoid ambushes.
  - **Defender awareness** - if defenders own a scanner too, they see the raid stack forming outside and can prepare. Asymmetric info becomes mutual info.

  **Defender counter-play (material-driven base progression):**

  - **Dense wall materials** block or degrade the scan - concrete, metal plating, reinforced stone walls. Gives players a concrete reason to upgrade from wood → stone → reinforced through the progression curve.
  - **Faraday/panic rooms** - interior rooms lined with RF-opaque material (foil, mesh). High-cost construction, guarantees privacy.
  - **Move silently / crouch** - reduces silhouette intensity in some tunings (skill-based counter).
  - **Decoy movement** - animated dummies, AI pets patrolling, rotating traps that simulate a walking person. Makes the scanner's readout noisy.
  - **Active RF jammers** - placed appliance draws power, blankets a zone with scan interference. Raiders know "they have a jammer" the moment they step into it (their display noises up).
  - **Base layout design** - long halls, offset doorways, dense interior walls limit scanner line-of-sight even through thin exteriors.

  **Tactical role: radar operator (real-time voice callouts):**

  The scanner operator becomes a legitimate team role - a position, not just a tool-holder.

  - Trades combat readiness for real-time team intel (scanner is slow + visible, you're exposed while operating)
  - Standard callout vocabulary over proximity/team voice:
    - "Two tangos east wing, moving north toward stairs"
    - "One stationary, basement, behind the vault wall - asleep or looting"
    - "Runner coming up stairs, third floor, contact in 4 seconds"
    - "Lost signal on target 3, check for panic room"
  - High-value target: enemies who spot the operator and take them out blind the assault team
  - Defensive inverse: the base can have its OWN operator inside, watching the raid stack form up, calling its own team ("Six outside the north wall, stacking for breach - flip the jammer")
  - Pure emergent tactical moment: squad coordinates breach, operator feeds intel, breacher charges in knowing exactly what waits on the other side

  **[LIKELY] AR tabletop mode (Quest 3S passthrough - headline VR feature):**

  When the scanner operator wears a VR headset in passthrough/AR mode, the 2D mini-display upgrades to a **holographic 3D tabletop**. You see the real world through the headset PLUS a miniature voxel reconstruction of the scanned area floating in front of you - like a tactical hologram from a sci-fi film, but built from your own scan data.

  - **Full underground topology** - base walls, rooms, corridors, buried caches, tunnels - all rendered as a semi-transparent x-ray voxel model
  - **Real-time defender silhouettes** - animated figures walking the corridors, pausing at doorways, descending stairs - updates live as the scan sweeps
  - **Walk around the hologram** - physically move around the projection, view from any angle, kneel to see basement levels, lean in to inspect the vault room
  - **Scale and zoom** - pinch-to-zoom, scale from floor-plan size up to dollhouse, or shrink to palm-sized for discretion
  - **Shared tactical markers** - drop "stack here," "breach point," "hostage" markers that appear in teammates' HUDs and in THEIR own AR tabletop if they share the view
  - **Pre-raid war table** - whole squad gathers in a safehouse, plants the hologram on an actual physical table (AR passthrough blends it into the real environment), rehearses the breach
  - **Frame budget** - scanner overlay is a new render pass, needs to fit Quest 3S foveated budget. Voxel model is already resident in memory - the overlay is projection + silhouette raster, cheap.
  - **Defender equivalent** - defender operator sees the raid stack forming OUTSIDE their base on their own tabletop. Time to flip the jammer, wake the sleeping teammate, arm the claymore.

  **Why this matters strategically:** AR tabletop is the signature Quest 3S showcase for survival gaming. No competing game has tactical-hologram base recon. This is the demo moment - player pulls up the underground base hologram on their dining room table, watches tiny defenders walk the corridors below, realizes what's possible, tells everyone they know.

  **AR-tabletop gameplay verbs:**

  - Plant the hologram on your actual kitchen table, gather the squad around it, mark the breach point
  - Kneel to eye level with the miniature basement, spot the guard leaning against the vault wall
  - Walk a full loop around the hologram searching for the weak point in the east wall
  - Pinch-zoom into the war room, watch the enemy commander pacing
  - Drop a "frag through this window" marker, watch it ghost into your breacher's HUD
  - Shrink the tabletop to palm-size when a stranger walks in, hide the intel
  - Scan AND watch - move the antenna to reveal more of the base, see the hologram fill in room by room

  **Companion plan note:** base raiding is a huge topic that this doc only touches. Breach charges, alarm systems, holdout rooms, defender reinforcement waves, loot vault design all belong in a dedicated `PLAN-Base-Raiding.md` - flagged for a future brainstorm session.
- **[COMMIT] Player surveillance** - watch someone from a hillside, note where they dug, come back later. No tool required, just attention and patience. Pure emergent gameplay.
- **[LIKELY] Corpse clues** - a dead body near disturbed ground is itself a tell. Lets you scout without HUD markers.
- **[UNDECIDED] Dowsing rods** (unreliable folk tool) - cheap crafted item, noisy hints. "Almost a metal detector" for low-tier players, fun flavor.

**Defender counter-counter (hider side):**

- **[COMMIT] Non-metal containers** - wooden crates, plastic tubs, burlap sacks. Metal detector ignores them. Trade: less protection from moisture/damage.
- **[LIKELY] Deep burial** - dig 3-5 voxels down before placing stash, backfill. Outside cheap-detector range.
- **[LIKELY] Cover tracks** - after digging, use moldable brush to smooth surface and apply decoration (leaves, grass, branches). Removes the visual disturbance tell.
- **[LIKELY] Decoy caches** - bury worthless junk in obvious spots, hide the real stash elsewhere. Wastes enemy scan battery and attention.
- **[LIKELY] Booby-trapped caches** - bury a grenade or land mine beneath the stash (direct tie-in to Traps and snares). Enemy digs it up, goes boom.
- **[UNDECIDED] Hidden in plain sight** - stash inside hollow tree stump, under loose floorboard. Out of terrain-carving scope but worth flagging as alternative.
- **[COMMIT] Scent masking** - tarp wrap, coffee grounds, commercial scent-blocker. Foils tracking dogs.
- **[LIKELY] Vary routes** - don't walk the same path to your stash every trip. A well-worn trail is itself a tell to observant rivals.

**Meta-layer: stash maps**

- Craftable physical map item (paper + pencil), players hand-mark their own cache locations.
- If killed, map drops as loot - finder now knows where your caches are. Terror moment.
- Without a map, remembering stash locations is pure mental load - genuine survival skill.

**Concrete player stories this enables:**

- Spend dusk burying your spare kit in the woods, noting position relative to two forked trees. Sleep in an abandoned shed nearby.
- Morning: watch a squad sweep the treeline with a metal detector. Heart in throat. They pass 10 feet from your stash because you wrapped it in a tarp inside a wooden box.
- Follow one of them when they break off solo. Watch them dig up THEIR stash. Wait until they're loaded up. Headshot. Now you have both hauls.
- Counter-play: they left a booby-trap decoy above their real cache. You blow yourself up.
- Next iteration: always probe with a stick before reaching into unfamiliar dirt.

**Gameplay verbs this adds:**

- Sweep a suspicious patch of fresh dirt with a detector before stepping on it.
- Bury a rifle wrapped in a non-metal tarp to defeat metal detectors.
- Cover your tracks with the smoothing brush after every dig.
- Plant decoy caches along obvious paths to waste a rival's scan battery.
- Booby-trap your own buried cache - murder-looter gets the "reward."
- Draw a mental-map route through landmark trees to find your own stash later.
- Train a dog to lead you to buried treasure (and buried bodies).
- Steal a dying player's stash map before they bleed out.

### Gameplay verbs moldable terrain enables

- **Shape a base plot** - flatten a hillside terrace for buildings
- **Dig a moat** - defensive trench around a settlement
- **Build a berm** - raised defensive earthwork for cover
- **Sculpt a road** - graded path up a mountainside
- **Excavate a cellar** - dig down, build block walls inside the hole
- **Fox-hole combat** - mid-firefight, dig shallow cover from the ground
- **Hide a cache** - bury supplies, mark the spot mentally, excavate later
- **Terrain signage** - carve words/arrows into hillsides visible from distance
- **Dam a river** - fill terrain to block water flow, flood areas
- **Tunnel into a mountain** - mining with intent, not just random digging

### Technical enablers (depends on)

- **Phase 3 SDF modification kernel** - MUST support both add and subtract ops
- **Phase 3 DMC re-mesh** - must handle continuous per-frame updates cleanly
- **Phase 13 StreamingBudget** - bounds re-mesh cost per frame so molding doesn't tank framerate
- **Phase 14 lighting flood fill** - dug-out caves must re-propagate light correctly (moldable interiors)
- **Phase 16 streaming service** - modified chunks trigger save-dirty flags correctly
- **Unified ITerrainCarve API** - moldable brush is one client of it, explosions are another

### Performance targets (draft)

- Single brush tick: modify ~1000 voxels, re-mesh affected DMC cells, under 2ms on Quest 3S
- Continuous hold: 60 ticks/sec at brush tick cost → frame budget of ~33ms for everything else
- Brush radius in terms of SDF voxels: adjust to fit, probably max 8-voxel radius at full power

### Unique selling point

Lost Spawns is the first **browser-native** moldable-terrain survival game at this scale. Astroneer, No Man's Sky, etc. are desktop/console native. Browser + WebGPU + SDF + DMC is novel and demo-worthy.

---

## Next actions

1. Review this plan with Data - note any library surface additions needed
2. Lock in the `ITerrainCarve` unified API shape (even if implementation is phased)
3. When Phase 3 SDF lands, wire `ExplosionKernels.DestroyInSphere` + SDF modification through the unified API
4. Lost Spawns consumer: add `CarveService` in `Services/` that takes input and calls the library API
5. Add pickaxe/shovel tool models and input bindings
6. Iterate on feel before committing to advanced features

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
