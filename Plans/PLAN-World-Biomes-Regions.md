# World, Biomes, and Regions - Brainstorm and Plan

**Status:** Living brainstorm. Decisions get locked as features mature.
**Owner:** Captain (TJ)
**Consulted:** Tuvok (research/planning)
**Last updated:** 2026-04-17

---

## Status markers

- **[COMMIT]** - committed to v1.0, active work or next in queue
- **[LIKELY]** - strong fit, assumed yes unless something knocks it out
- **[UNDECIDED]** - interesting, uncertain value/cost tradeoff, revisit before touching
- **[DEFER]** - post-v1.0 or beyond scope
- **[REJECT]** - considered and ruled out (with reason)

---

## Vision

The world of Lost Spawns is **a place**, not a procedural noise field. It has names, weather patterns, history, and reasons why a player would head somewhere over somewhere else. Every biome is a survival challenge of its own; every region is a story.

Inspired by DayZ's Chernarus (named villages, real-feeling geography), STALKER's Zone (each region with distinct hazards and rumors), Subnautica's biome layering (depth and danger correlate), and Skyrim's Holds (faction-coded regions).

**Design goals:**

1. **Biomes drive gameplay, not just visuals.** A swamp is not "green forest with water" - it has its own threats, loot, fauna, sounds, tactical cover, and survival demands.
2. **Regions are knowable.** Players can describe a region to each other ("the burn zone east of Mill Creek") because regions have names, landmarks, and identity.
3. **Geographic difficulty curve.** The starting region is survivable; remote regions punish unprepared travelers.
4. **Faction-coded territory.** Each region has dominant or contested faction control (cross-ref [PLAN-Factions-Squads.md]).
5. **Persistent named locations.** Towns, ruins, military bases, named landmarks survive across server restarts and across player generations.
6. **Procedural with intent.** Some content is hand-authored set-piece (named towns, story locations); the rest is procedurally varied within biome rules.

---

## Foundation (what exists today)

**Voxel terrain engine partially exists** (VoxelEngine SDF/DMC compute path active 2026-04-16). Depends on:

- **Terrain heightmap + biome layer** (VoxelEngine generation phase)
- **Voxel material palette** (cross-ref AubsCraft palette compression research)
- **Vegetation system** (foliage placement, density per biome)
- **Weather system** (climate per biome - cross-ref [PLAN-Day-Night-Cycle.md])
- **Persistent OPFS region storage** (Phase 8)
- **Hand-authored prefab system** (drop building/landmark templates into procedural terrain)
- **Audio ambient layer** (cross-ref [PLAN-Audio-Design.md])

---

## World scale and structure

### [LIKELY] World scale

- **Target playable area: ~50 km^2** (similar to DayZ Chernarus 225 km^2 reduced for browser performance)
- Voxels are 0.5m per cube, so ~14M voxels per square km on the surface
- Players can walk corner to corner in ~3-4 real hours, run in ~1.5 hours
- Vehicles cut travel time significantly (cross-ref [PLAN-Vehicles.md])

### [UNDECIDED] World shape

- **Bounded** (island, peninsula, walled-off zone) - simpler streaming, clear edges, narrative justification
- **Open with falloff** (procedurally degraded terrain at distant edges, unsupported gameplay) - more "endless" feeling
- **Tile-based seamless** (chunks load endlessly in any direction) - DayZ-style, but harder to scope content

**Lean: Bounded.** Set in the wreckage of a former military quarantine peninsula. Walls / collapsed bridges / radiation barriers explain the bounds in-fiction.

### [LIKELY] World seed system

- One seed = one canonical world
- Seed determines biome layout, town placement, faction territory
- Custom servers can pick a seed; default servers use canon seed
- Cross-ref [PLAN-Vision.md] world generation section

### [COMMIT] Persistent named locations

- Hand-authored: ~30-50 named landmarks (towns, military bases, broadcast towers, lighthouses, hospitals, prisons)
- Procedural: smaller residential clusters, rural homesteads, gas stations, isolated cabins
- Named landmarks have unique architecture and lore (cross-ref [PLAN-Quests-Storyline.md])

### [LIKELY] Region naming and discovery

- Player learns region names from NPCs, found maps, holotapes, signs
- Map UI shows region names only after discovery (no all-knowing minimap)
- Cross-ref [PLAN-Quests-Storyline.md] navigation philosophy

---

## Biome catalog

Each biome listed with: climate, hazards, dominant flora/fauna, common loot, faction presence, atmosphere, gameplay tilt.

### [COMMIT] Boreal Forest

- **Climate:** cold-temperate, frequent rain, snow at higher elevations. Cold drops fast at night.
- **Hazards:** wolves and bears (cross-ref Animal Wildlife plan when written), hypothermia risk (cross-ref [PLAN-Survival-Needs.md]), tree falls
- **Flora/fauna:** dense conifers, ferns, mushrooms, deer, rabbits, wolves, bears, eagles
- **Cover/visibility:** high cover from trees + brush; medium visibility on game trails
- **Loot:** hunting cabins, rangers' stations, lumberjack camps, abandoned campsites
- **Faction presence:** Survivors (subsistence), occasional Bandits hideouts
- **Atmosphere:** sound is muffled by trees; bird calls; rain on canopy; constant moisture
- **Gameplay tilt:** stealth, hunting, slow careful movement, ambush combat

### [COMMIT] Plains and farmland

- **Climate:** temperate, open sky, wind exposed
- **Hazards:** low cover (sniper bait), summer heatstroke, lightning in storms, lone cryptids hunt across open ground
- **Flora/fauna:** tall grass, scattered trees, wildflowers, deer, rabbits, cattle, scavenger birds, livestock turned feral
- **Cover/visibility:** low natural cover; high long-range visibility; trenches/fences for cover
- **Loot:** farmhouses, barns, silos (rare grain stores), tractors, abandoned vehicles on roads
- **Faction presence:** Settlement Builders (best farmland), Survivors, scattered homesteaders
- **Atmosphere:** wind in grass, distant crows, silence on still days
- **Gameplay tilt:** long-range combat, base building (clear sight lines), farming for food

### [COMMIT] Urban ruins

- **Climate:** local microclimate (heat island in summer, wind tunnels), heavily polluted air in some districts
- **Hazards:** infected dense, structural collapse, cryptids that nest in apartments, ambush points everywhere
- **Flora/fauna:** weeds through cracks, urban trees overgrown, rats, feral dogs, cats, infected, urban-mutated cryptids
- **Cover/visibility:** maze-like; very high cover but constant blind corners; rooftops give snipe positions
- **Loot:** stores, restaurants, apartments, hospitals, police stations, schools, offices (computers / terminals)
- **Faction presence:** Bandits (high), Aether Group patrols (selectively), no settler factions
- **Atmosphere:** echoing footsteps, broken glass underfoot, distant horns of long-dead cars, infected groans through walls
- **Gameplay tilt:** close-quarters combat, scavenging, vertical movement, infected combat priority

### [COMMIT] Industrial zone

- **Climate:** locally polluted, often overcast from factory haze
- **Hazards:** chemical spills (cross-ref [PLAN-Environment-Hazards.md]), radioactive sites, structural hazards, heavy infected
- **Flora/fauna:** sparse weeds, rats, cryptids drawn to chemical mutation
- **Cover/visibility:** mid - large structures + open lots between them
- **Loot:** machine parts, fuel, chemicals, vehicles, tools, fabrication equipment, raw materials
- **Faction presence:** Aether Group (tech salvage), Bandits (raid factory loot)
- **Atmosphere:** rusted metal in wind, distant industrial drone (where some power still flows), chemical reek
- **Gameplay tilt:** crafting/recipe loot, chemical combat (Molotov fuel), heavy gear scavenging

### [LIKELY] Swamp / wetland

- **Climate:** humid, hot in day, bug-infested
- **Hazards:** disease (cross-ref [PLAN-Medical.md] infection from contaminated water), gators, leeches, quicksand-like mud, foggy nights
- **Flora/fauna:** cypress, mangrove, reeds, frogs, gators, snakes, swamp cryptids
- **Cover/visibility:** mid; reeds give knee-high cover, fog drops visibility hard
- **Loot:** swamp shacks, illegal stills, fishing camps, smuggler stashes, drowned vehicles
- **Faction presence:** Marsh Folk (UNDECIDED faction - swamp natives, avoidant), Bandits use swamps as hideouts
- **Atmosphere:** insect buzz, frog croaks, splashing in distance, mosquito ambient, stagnant water smell
- **Gameplay tilt:** slow movement, water hazards, disease management, ambush by cryptids

### [LIKELY] Coastal / shoreline

- **Climate:** salt air, wind, milder than inland; storms hit hardest here
- **Hazards:** drowning, undertow, salt corrosion of equipment, exposed positions, cryptid emergence from water (UNDECIDED)
- **Flora/fauna:** dune grass, gulls, crabs, fish, occasional sea-creature mutations
- **Cover/visibility:** very low cover on beach; high in coastal forests inland; cliffs for elevated positions
- **Loot:** docks, marinas, beach houses, lighthouses, lifeguard stations, shipwrecks, washed-up containers (rare loot drops)
- **Faction presence:** Survivors (fishing villages), Smugglers (UNDECIDED faction - boat-based)
- **Atmosphere:** crashing waves, gull cries, foghorns at distance, salt spray, distant ship rust groan
- **Gameplay tilt:** boat exploration, fishing, beachcombing for rare loot, naval combat (cross-ref [PLAN-Vehicles.md])

### [LIKELY] Mountain / highland

- **Climate:** very cold (deadly at high elevation), snow at peaks, thin air
- **Hazards:** falls, avalanches (UNDECIDED procedural events), hypothermia, low oxygen affecting stamina, predator cats, isolated locations
- **Flora/fauna:** stunted pines at altitude, alpine flowers, mountain goats, big cats, eagles, bears, mountain cryptids
- **Cover/visibility:** very high visibility from peaks; low cover above tree line
- **Loot:** ski resorts, observatories, ranger huts, military lookouts, mining tunnels, downed aircraft
- **Faction presence:** Mountain Hermits (UNDECIDED faction), Aether Group (high-altitude installations)
- **Atmosphere:** wind howl, distant rockslides, eagle cries, oppressive silence at altitude
- **Gameplay tilt:** elevation challenges, sniper paradise, base building advantage (chokepoints), dangerous travel

### [UNDECIDED] Desert / arid

- **Climate:** brutal day heat, freezing night, no water
- **Hazards:** dehydration, heatstroke, sandstorms blocking visibility, scorpions, snakes, sun-mutated cryptids
- **Flora/fauna:** cacti, dry grass, lizards, vultures, coyotes, scorpions, rattlesnakes
- **Cover/visibility:** low cover in flats; high in canyons / rock formations
- **Loot:** abandoned mines, desert outposts, gas stations, ghost towns, military test sites
- **Faction presence:** sparse; few outposts
- **Atmosphere:** wind across sand, distant thunder, dry creak of parched wood, vulture cries
- **Gameplay tilt:** water management, vehicle travel essential, exposure as primary threat
- **Concern:** does the canon world have desert? Maybe not in a coastal peninsula. **Lean DEFER** unless world geography includes a rain-shadow inland zone.

### [LIKELY] Burn / wildfire scar zone

- **Climate:** mostly clear; ash on wind in summer
- **Hazards:** unstable burned trees, unstable terrain, low cover, ambient ash exposure, "The Scorched One" cryptid (cross-ref [PLAN-Dynamic-World-Events.md])
- **Flora/fauna:** charred husks of trees, scrub regrowth, deer (returning), opportunistic predators, fire-mutated cryptids
- **Cover/visibility:** medium-low; mostly clear sight lines with tree-husk obstacles
- **Loot:** burned-out vehicles, melted-but-salvageable equipment, what remains of old camps
- **Faction presence:** mostly empty; loners passing through
- **Atmosphere:** wind through burnt branches, ash in throat, unsettling quiet (no birds), creak of dying trees
- **Gameplay tilt:** rare cryptid hunts, salvage, low resistance, post-event scar zones from dynamic events

### [LIKELY] Quarantine zone (military lockdown)

- **Climate:** local; varies based on geographic placement, often near urban
- **Hazards:** automated turrets active, locked checkpoints, radiation pockets, military-tier infected, AI guard drones (UNDECIDED)
- **Flora/fauna:** sparse; what survived the lockdown is mutated
- **Cover/visibility:** mid; barricades, concrete walls, vehicles
- **Loot:** military gear, ammo, medical supplies, classified terminals (lore + quest hooks), high-tier loot generally
- **Faction presence:** Aether Group (heavy interest), Bandits (raids), no civilians
- **Atmosphere:** distant turret servos, automated PA loops in dead languages of military code, chain-link fence in wind, claxon alarms still sounding
- **Gameplay tilt:** late-game endgame zone, group recommended, lore epicenter, top-tier loot

### [UNDECIDED] Underground

- **Climate:** cool stable temperature, no weather, no day/night
- **Hazards:** dark (NVGs essential), hard to escape, cryptids that nest there, structural collapse, gas pockets
- **Flora/fauna:** rats, bats, fungus, tunnel cryptids, blind cave creatures
- **Cover/visibility:** total dark without light source
- **Loot:** sewers (city escape routes + lore), bunkers (military caches + stories), mines (raw materials), subway (vehicles + passages)
- **Faction presence:** depends on tunnel - bunkers have Aether traces, sewers have Bandits, mines mostly empty
- **Atmosphere:** dripping water, distant scratching, echo of own footsteps, bat wings
- **Gameplay tilt:** lighting dependency, claustrophobia, ambush combat, treasure hunt
- **Concern:** voxel underground is performance-heavy. **Lean LIKELY** but plan for limited extent in v1.0 (sewers + 3-5 named bunkers, not vast cave networks).

### [DEFER] Snow / arctic biome

- DayZ vibe doesn't need an arctic zone in v1.0
- Mountain peaks cover snow gameplay sufficiently
- Defer to post-v1.0 expansion

---

## Region structure

### [LIKELY] Region as gameplay unit

- World divided into ~15-25 named regions
- Each region has dominant biome + secondary biome blend at edges
- Each region has 1-3 named settlements / landmarks
- Each region has a "threat tier" (1=safe starter, 5=quarantine endgame) determining infected density, cryptid presence, faction patrol strength, loot tier

### [LIKELY] Threat tier examples

- **Tier 1:** Mill Creek Valley, Coastal Fishing Village, the Cryo Shelter Zone (starting)
- **Tier 2:** Westwood Forest, Hagley Plains, Shoreline Industrial
- **Tier 3:** Old Quarry, Burn Scar East, Lake Halloran (cryptid hotbed)
- **Tier 4:** Downtown Ruins, Reaper Highway, The Marsh
- **Tier 5:** Quarantine Zone Charlie, Aether Hilltop Station, Mount Vernon (cryptid alpha territory)

(Names placeholder; Captain to canon)

### [LIKELY] Region transitions

- Geographic transitions: rivers (need bridge or boat), cliffs (need climb skill), forests blending into plains
- Faction transitions: hostile-faction territory has signs / NPC patrols / barricades
- Threat transitions: gradient (Tier 1 to Tier 2 over a 500m walk), not instant

### [LIKELY] Persistent regional state

- Settlement health (population, structural damage)
- Faction control of region
- Recent player events (graves, ruined buildings from raids)
- Loot respawn timer per region
- Cross-ref [PLAN-P2P-Reputation-System.md] persistence

### [UNDECIDED] Server-customizable region difficulty

- Server admins can crank up / down threat tier per region
- Could enable hardcore mode (entire world Tier 5) or peaceful exploration mode (mostly Tier 1)
- Lean LIKELY for v1.0 community server support

---

## Map and navigation

### [COMMIT] No GPS / mini-map

- Hard rule (cross-ref [PLAN-Vision.md] and [PLAN-Quests-Storyline.md])
- Players use compass, sun position, terrain landmarks, paper map item

### [COMMIT] Paper map item

- Found in most cars, gas stations, ranger stations
- Shows static map of the world with named regions and major roads
- Player position NOT marked - player triangulates from landmarks
- Player can mark custom waypoints with pencil item

### [LIKELY] Region discovery

- Region names appear on map only after entering region (or hearing it named in dialog)
- Discovered regions show name + dominant biome icon

### [LIKELY] Compass + sun

- Compass item shows cardinal directions (electronic + magnetic versions)
- Sun position used for time-of-day estimation (cross-ref [PLAN-Day-Night-Cycle.md])
- Stars at night for navigation (basic Survivalist skill check)

### [UNDECIDED] Topographic detail

- Paper map shows elevation contours (yes/no)
- Lean: yes, simple contour lines

### [REJECT] Player position blip on map

- No "you are here" arrow. Triangulate or get lost.

---

## Resource and loot distribution

### [COMMIT] Loot tier matches region tier

- Tier 1 regions: civilian common loot, basic medical, low-tier weapons
- Tier 5 regions: military hardware, advanced medical, named weapons, holotapes with rare lore
- Cross-ref [PLAN-Economy.md] for loot economy

### [LIKELY] Resource biomes

- Mountain: stone, ore (iron, copper)
- Forest: wood, herbs, mushrooms (food + medical)
- Plains: cloth (cotton), grain, livestock
- Coastal: fish, salt, washed-up containers
- Industrial: machine parts, fuel, chemicals
- Swamp: rare plants, gator hide
- Burn zone: charcoal, scrap, salvage

### [LIKELY] Cryptid territory

- Each cryptid has a preferred biome (cross-ref [PLAN-Dynamic-World-Events.md])
- The Scorched One: burn zones
- The Howler: forests, foggy nights
- The Doctor: urban hospitals
- The Broadcaster: anywhere with active radio infrastructure
- The Warden: prison ruins (specific named location)
- Mother Mutation: industrial / quarantine zones

### [UNDECIDED] Region-locked rare loot

- Some unique items exist only in specific regions (e.g., the only Surgeon's Kit is dropped by The Doctor in Old Mercy Hospital)
- Lean: yes for ~10-20 unique items, drives cross-region travel

---

## Atmosphere and ambience per region

### [COMMIT] Audio per biome (cross-ref [PLAN-Audio-Design.md])

- Biome-driven ambient layer (forest birds vs city wind vs swamp insects)
- Time-of-day modifies ambient (day birds, night insects)
- Weather modifies ambient (rain, wind layered over biome base)
- Distant sound carries across region (gunshot heard 1km away, cryptid roar heard 2km)

### [LIKELY] Visual signature per biome

- Color grading per biome (cross-ref [PLAN-Vision.md] art direction)
- Forest: greens with cool blue shadow tint
- Plains: warm gold-green daytime, muted blue at night
- Urban: muted gray-brown, occasional sodium-orange in functioning streetlights
- Industrial: brown-yellow haze tint
- Swamp: green-yellow with brown water reflection
- Coastal: blue-gray with salt-haze whites
- Mountain: cool blue-white, stark shadows
- Burn zone: monochrome gray-orange, ash particulate

### [LIKELY] Weather variation per biome (cross-ref [PLAN-Day-Night-Cycle.md] + weather plan when written)

- Coastal: storms more frequent, fog mornings
- Forest: rain regular, fog rare
- Plains: thunderstorms, wind constantly
- Mountain: snow at altitude, wind always
- Swamp: humid, always slightly foggy at dawn/dusk
- Urban: smog haze, rare clear days
- Burn zone: ash on wind in summer, clear in winter

---

## Faction territorial overlay

### [LIKELY] Faction-controlled regions (cross-ref [PLAN-Factions-Squads.md])

- Each major faction has 1-3 regions of strong control
- Some regions are contested between factions (war zones, dynamic state)
- Some regions are neutral / no-faction (true wilderness, abandoned)

### [LIKELY] Faction signage

- Spray-painted markers, flags, broadcast tower colors, NPC patrol uniforms identify faction territory
- Players learn to read signs over time
- Walking into hostile-faction territory without low-profile gear escalates encounters

### [UNDECIDED] Dynamic faction territory

- Region control can shift over time based on quest outcomes / dynamic events
- Faction A loses Region X after a player-driven assassination quest
- Persistent state, server-wide
- Lean LIKELY for v1.0

---

## Special / one-off locations

### [COMMIT] Named landmarks (one of each, hand-authored)

- The Cryo Shelter (player starting location)
- Old Mercy Hospital (The Doctor cryptid lair, holotape archive)
- Aether Hilltop Station (faction HQ, late-game)
- Mount Vernon Observatory (mountain peak, lore/loot)
- Reaper Highway 7 (long stretch of vehicle wrecks, sniper alley)
- The Marsh Cathedral (swamp landmark, cult lore)
- Mill Creek Bridge (faction war flashpoint)
- Coastal Fishing Village (peaceful starter region anchor)
- The Quarry (Tier 3 wreckage hub, hostile)
- Burn Scar East (post-wildfire region with The Scorched One)
- Quarantine Zone Charlie (Tier 5 endgame)
- The Prison (The Warden cryptid only-spawn)
- Broadcasting Tower Alpha (radio quest location)
- The Reservoir Dam (water source, faction-contested)
- Lazarus Plus Corporate HQ (cryo-shelter origin, lore endgame)

### [LIKELY] Procedural locations

- Generic farmhouses, ranger stations, gas stations, residential clusters
- Procedural placement using biome rules + density per region
- Each procedural building uses a hand-authored prefab template (so quality stays high)

### [UNDECIDED] Easter egg locations

- Hidden bunker with developer note holotape
- Reference locations to TJ's other projects (Star Trek nods, AubsCraft callout)
- Lean: yes for ~5-10 careful Easter eggs

---

## Performance considerations

### [COMMIT] Region streaming

- Only nearby regions actively simulate (NPCs, dynamic events)
- Distant regions update on tick (faction state, persistent loot respawn)
- LOD reduces voxel detail at distance (cross-ref AubsCraft LOD R&D)

### [LIKELY] Biome transition blending

- Smooth interpolation between biomes (no hard line)
- Audio cross-fades, color grade lerps, foliage density lerps over ~50m

### [UNDECIDED] Macro vs micro biomes

- Macro: forest occupies a region (~5 sq km)
- Micro: small variations within a region (clearing in a forest, swamp pocket in plains)
- Lean: macro for v1.0, micro added incrementally

---

## Anti-patterns to avoid

### [REJECT] Procedural noise without intent

- A perlin-generated infinite world feels random and forgettable. Hand-author the spine, procedural the variation.

### [REJECT] Identical biomes copy-pasted

- Two forests should feel different (one boreal/cold, one mixed/temperate). Same applies to other biomes.

### [REJECT] "Quest fence" walls

- No invisible walls preventing player from leaving a region. Use natural geography (cliffs, water, radiation, hostile patrols) for soft boundaries.

### [REJECT] Loot piñata regions

- A region whose only purpose is "loot here is good" is shallow. Every region needs a story / atmosphere / threat reason for loot density.

### [REJECT] Unmarked region transitions

- Players should always know which region they entered. UI fade-in of region name on entry (subtle, immersive, not toast-style).

---

## Gameplay verbs world structure enables

- Cross a forest at night with NVGs, hearing distant wolf howls, knowing the next region has a Tier 4 threat tag and you're underleveled
- Walk into Coastal Fishing Village and recognize the smell of fish smoke, hear the distant slap of the dock against waves, know you can rest here
- Stand on Mount Vernon Observatory at dawn and survey four regions visible from one peak
- Trade with a fishing village merchant whose stock is salted cod and shipwreck salvage you can't get inland
- Get lost in The Marsh because fog cut visibility to 5m and your compass is acting weird (cryptid presence)
- Cross Reaper Highway 7 by sprinting between burned-out vehicles, gunfire from a sniper position behind you
- Decide to risk the Burn Zone East to hunt The Scorched One, knowing your fireproof gear is the only barrier
- Travel to Quarantine Zone Charlie with three squadmates, the only Tier 5 zone, knowing you cannot outrun an automated turret
- Find a holotape in Lazarus Plus Corporate HQ that explains your own cryo shelter
- Build a base in Mill Creek Valley because it's Tier 1 (safe), then expand to Hagley Plains as you outgrow it
- Walk a paper map you found in a gas station, triangulate your position from a water tower in the distance and a road bend
- Hear an NPC say "the Old Quarry has been hot lately, three crews fought there yesterday" and decide whether to investigate or avoid
- Push north into mountain territory, run out of warmth, learn that mountain travel needs winter gear (cross-ref [PLAN-Survival-Needs.md])
- Cross from boreal forest into burn scar - color grade shifts, ambient audio changes, dead trees and ash replace pines
- Find a hand-authored Easter egg location (a hidden bunker with developer notes) and recognize it as a love letter from the team

---

## Open questions

1. **World scale** - is 50 km^2 the right target? Smaller (15-20 km^2) for a tighter focused experience? Larger (100+) for endless exploration?
2. **World shape** - peninsula, island, or open border? Bounded recommended; Captain confirms.
3. **Number of regions** - 15-25 named regions in 50 km^2 = ~2-3 km^2 per region. Right granularity?
4. **Procedural vs hand-authored ratio** - 30-50 named landmarks + procedural fill. Right mix?
5. **Tier 5 endgame regions** - one (Quarantine Zone Charlie) or multiple (one per faction late-game)?
6. **Region transitions** - smooth blend (50m gradient) or sharper "you have entered X" cues?
7. **Region-locked content** - how aggressive? 10-20 unique items? More? Less?
8. **Underground extent** - sewers + 3-5 bunkers, or larger cave networks worth the perf cost?
9. **Desert biome** - skip for canon (peninsula geography), or include via inland rain-shadow zone?
10. **Snow biome** - mountain peaks only (lean), or full arctic region (defer)?
11. **Marsh Folk faction** - real faction or just NPC encounters?
12. **Smugglers faction** - real faction (boat-based, coastal) or just bandit subset?
13. **Dynamic faction territory** - regions can flip control based on player actions, or fixed for v1.0?
14. **Server-customizable threat tier** - support for community servers in v1.0?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| World generation | VoxelEngine SDF/DMC + heightmap + biome layer |
| Persistent regions | OPFS region files (Phase 8) |
| Named landmark prefabs | Hand-authored asset pipeline |
| Audio per biome | PLAN-Audio-Design.md ambient layer |
| Weather per biome | PLAN-Day-Night-Cycle.md + dedicated weather plan |
| Faction territorial overlay | PLAN-Factions-Squads.md |
| Cryptid territory | PLAN-Dynamic-World-Events.md |
| Resource distribution | PLAN-Crafting.md + PLAN-Economy.md |
| Region streaming | VoxelEngine LOD + region-aware simulation tick |
| Map / navigation UI | PLAN-UI-HUD.md (when written) |
| Region threat scaling | PLAN-Player-Progression.md + region tier system |

---

## Next actions

1. Lock world shape (Captain: peninsula / island / open?)
2. Lock world scale target (~50 km^2 confirmed?)
3. Sketch a paper map of canon world: 15-25 named regions, dominant biomes, named landmarks
4. Write biome-spec data schema (JSON: biome_id, climate_params, audio_id, color_grade_id, foliage_density, loot_table_id, faction_overlay_id)
5. Author one biome end-to-end as proof of concept (lean: Boreal Forest, since Tier 1 starting region likely)
6. Author one named landmark end-to-end (lean: Cryo Shelter, since players start there)
7. Write dedicated PLAN-Weather.md to formalize weather model
8. Write dedicated PLAN-UI-HUD.md to formalize map/journal/HUD
9. Decide on canon faction list (current sketch: Survivors, Bandits, Aether Group, Settlement Builders + UNDECIDED Marsh Folk, Mountain Hermits, Smugglers)
10. Cross-plan audit: walk through each existing plan, confirm biome references match this catalog

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
