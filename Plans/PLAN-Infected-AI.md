# Infected AI - Brainstorm and Plan

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

**The infected are the world's metronome.** Not event-bosses. Not rare cryptids. Just the constant, hungry, ticking pressure that follows sound. One infected is a problem. Six are a fight. Thirty are a siege.

Lost Spawns infected are closer to **Days Gone hordes** + **State of Decay migration** + **DayZ's opportunistic stragglers** than to slow Romero zombies. They react to sound. They hunt in packs. At night they come alive. A loud firefight in town summons them from a kilometer around.

Cryptids (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md)) are the rare, named bosses. Infected are the daily weather.

**Design goals:**

1. **Sound is the nervous system.** Gunshots aggregate. Silent kills stay silent. Suppressors are gold.
2. **Packs matter more than singles.** One shambler is trivial. A migrating pack is existential.
3. **Variety, not just HP tiers.** Each type has a role: aggro, stealth, ranged, summoner, tank.
4. **Night is infected time.** Faster, more aggressive, higher density. Day belongs to players.
5. **Hives + migrations = persistent world.** Clearing a nest clears the area for hours. Heat builds up. Herd migrates.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Entity + AI system** (VoxelEngine Phase 12) - state machines, pathing, perception
- **Audio propagation** (cross-ref [PLAN-Audio-Design.md](PLAN-Audio-Design.md)) - noise radius drives aggro
- **Chunk streaming** (Phase 16) - load AI density per region, scale spawns to player proximity
- **Navigation mesh** - AI pathing over hybrid terrain (SDF + voxel) (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))
- **Combat system** (cross-ref [PLAN-Combat.md](PLAN-Combat.md)) - damage model, hit locations

---

## Infected archetypes

### [COMMIT] Shambler (baseline)

- Slow, single-minded, common
- Low HP, weak melee, no ranged
- Dangerous in groups (swarm + flanking + noise chain)
- Spawns everywhere, day and night
- Signature: 1-2 always near ruins, 5-10 in towns, 30+ at event sites

### [COMMIT] Runner

- Fast sprint, weak-to-medium HP
- Single-file chase (fast but mediocre in packs)
- Alarms other infected by screaming during chase
- Spawns at night, event zones, after heat buildup
- Signature: alone or in pairs, 3-5 in urban

### [LIKELY] Tank / Brute

- Slow, huge HP, heavy armor plates on head/chest
- Melee strikes break blocks (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md) raid physics)
- Requires AP rounds, head-shots, explosives
- Rare solo spawn, focal point of horde pushes
- Signature: lone bull in ruins, 1-3 max in horde

### [LIKELY] Screamer

- Medium speed + HP
- Special: shrieks on aggro, aggregates every infected within large radius
- Kill quietly FIRST if spotted
- Rare, signature spawn in hives, migration centers
- Signature: 1 per nest or large pack

### [LIKELY] Spitter / Ranged

- Low speed, medium HP
- Ranged acid/bile projectile (low damage, area denial)
- Forces cover, disrupts sniper lines
- Spawns in cryptid-adjacent zones + biohazard areas (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Signature: perched on ruins, back ranks of herds

### [LIKELY] Stalker

- Stealth approach, high damage backstab
- Low-medium HP but hard to spot (crouch, shadow, low ambient)
- Single-target ambush predator
- Spawns at night, forests, dark interiors
- Signature: solo predator, rare encounter

### [LIKELY] Crawler

- Legless or leg-broken infected
- Low profile, hard to spot in tall grass or rubble
- Low damage but bites = high infection chance (cross-ref [PLAN-Medical.md](PLAN-Medical.md))
- Common in hospital/event-scar zones
- Signature: ambush from dead bodies

### [UNDECIDED] Spawner / Mother

- Stationary, huge HP, spawns lesser infected at periodic intervals
- Anchor of a hive
- Similar to Mother Mutation cryptid but weaker
- Lean: overlap with cryptid, maybe [DEFER] as common type

### [UNDECIDED] Exploder / Bomber

- Walks toward player, detonates on contact (damage + gore)
- Classic zombie trope, well-known mechanic
- Could overlap with Spitter role - lean [LIKELY] for variety if Spitter is cut

### [LIKELY] Child infected

- Small, fast, low HP
- Moral ambiguity (genre standard)
- Appear at specific locations (schools, playgrounds) for atmospheric weight
- Same damage as Runner, eerie audio

### [REJECT] Boss infected dressed as cryptid

- Named bosses = cryptids (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))
- Infected archetypes stay type-based, not named

---

## Pack behavior

### [COMMIT] Sound alert chain

- One infected sees/hears player → shrieks/groans → all infected within radius converge
- Screamer infected extends radius massively
- Chain propagates: infected A alerts infected B, who alerts infected C (limited radius reaction)

### [LIKELY] Leader-follower

- In packs, one "leader" (highest-HP or spawn-seed) drives pathfinding
- Followers lag behind in formation
- Kill leader = pack scatters briefly before re-forming

### [LIKELY] Flanking behavior

- In groups of 5+, some AI seeks flank routes
- Prevents static-position cheese
- Requires navmesh + peer awareness (expensive but impactful)

### [LIKELY] Swarm mechanics at scale

- 20+ infected = "horde" state
- Horde moves as a mass (particle-like flow) toward noise or food
- Horde pressure on base = raid state (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md))

### [UNDECIDED] Infighting

- Rarely, infected turn on weaker/wounded infected (visible feeding frenzy)
- Flavor detail, low gameplay impact
- Lean [LIKELY] as emergent behavior

---

## Sound-driven AI

### [COMMIT] Noise heat map

- World maintains a noise heat field (per zone / chunk)
- Gunshots add heat. Silence decays it.
- Infected density + aggressiveness scale with heat
- Cross-ref [PLAN-Audio-Design.md](PLAN-Audio-Design.md)

### [COMMIT] Gunshot aggregation

- Unsuppressed shot: heard within ~200-400m (caliber + environment dependent)
- Every infected in radius turns, investigates
- Suppressor: massive reduction in alert radius
- Subsonic + suppressor = near-silent

### [LIKELY] Noise bait

- Intentional noise as tactical tool: thrown rock, car alarm, boom box
- Pull infected away from objective
- Skilled tactic, rewards preparation

### [LIKELY] Smell attraction

- Blood (from wounds, kills, corpses) draws infected to area
- Wash / bandage = reduced trail
- Cross-ref [PLAN-Medical.md](PLAN-Medical.md) blood trail visuals

### [UNDECIDED] Light attraction

- Flashlight, torches, campfires draw infected?
- Nights-especially design question
- Lean [LIKELY] at night only - creates tension for base lighting

---

## Migration and dynamic density

### [LIKELY] Regional heat accumulation

- Over real-time, each region builds up heat from player activity
- High heat = migration trigger → horde state (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))
- Heat cools with time away from region (days)

### [LIKELY] Inter-zone migration paths

- Hordes move between zones along roads, valleys, natural routes
- Visible via distance - smoke columns of dust, birds fleeing
- Intercept / avoid as strategic choice

### [LIKELY] Hive anchors

- Specific locations (abandoned subway, sewer, dense urban ruin) act as hives
- Infected respawn here if not cleared
- Clear a hive = region quieter for N hours
- Destroying nest central = longer reprieve

### [UNDECIDED] Seasonal migration

- Winter: infected migrate to warmer zones (valleys, urban)
- Summer: spread out
- Cool simulation detail, scope-heavy - lean [DEFER]

---

## Day/Night shift

### [COMMIT] Night aggro boost

- Infected faster + more aggressive at night
- Density doubles at night
- Perception range increases (vision + smell)
- Cross-ref [PLAN-Day-Night-Cycle.md](PLAN-Day-Night-Cycle.md)

### [LIKELY] Dawn dispersal

- Infected retreat to shade (buildings, tree cover, subways) at sunrise
- Dawn is the safe moving hour
- Exception: hive-anchored infected stay put

### [LIKELY] Nocturnal specialists

- Stalker and Spitter spawn only at night
- Shambler and Runner day+night
- Gives "time-of-day determines threat mix" flavor

---

## Infected vs player combat

### [COMMIT] Hit locations (cross-ref [PLAN-Combat.md](PLAN-Combat.md))

- Head-shot = instant kill (most types)
- Body shots bleed but don't kill fast
- Tank head = armored (requires AP rounds or explosives)
- Crawler low profile harder to headshot

### [LIKELY] Melee risks

- Infected melee attacks risk infection on wound (cross-ref [PLAN-Medical.md](PLAN-Medical.md))
- Bite = high infection chance
- Claw = bleed + cut
- Push/grab = stagger state, cannot attack back until recovered

### [LIKELY] Infected carrying disease

- Wounds from infected carry infection risk
- Disinfect immediately or face sepsis trajectory
- Medic skill mitigates

### [LIKELY] Body recovery

- Infected corpses still carry infection
- Don't loot bare-handed (gloves + disinfect)
- Drag bodies away from camp (attract packs)

---

## Player counter-play

### [COMMIT] Stealth is viable

- Avoid detection through quiet movement
- Night vision / thermal + silent movement = ghost playstyle
- Cross-ref [PLAN-Combat.md](PLAN-Combat.md) stealth

### [COMMIT] Suppressors + subsonic = god-tier

- Kill infected quietly, never trigger chain alerts
- Premium gear, premium gameplay
- Balanced by scarcity + condition drain

### [LIKELY] Distraction tools

- Thrown rocks, car alarms, timed charges
- Lead hordes away from objectives
- Skilled-play tactic

### [LIKELY] Fire as AoE

- Molotovs burn groups
- Flamethrowers (rare military loot)
- Gasoline trails (strategic zone denial)
- Wildfires (natural + accidental)

### [LIKELY] Environmental kills

- Lure into traps (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) traps)
- Push off cliffs
- Collapse buildings on top of groups (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md) structural)
- Industrial machines + crushers at zone sites

### [UNDECIDED] Decoys / mannequins

- Placeables that attract infected for limited duration
- Cool tactical layer
- Lean [LIKELY] as craftable

---

## Performance and scale

### [COMMIT] Density scales with player proximity

- Only simulate full AI near players
- Dormant AI ticks very slowly, pathing skipped
- Chunk load/unload drives AI lifecycle

### [LIKELY] Spatial partitioning

- AI grouped per chunk / region
- Pack logic runs on group, not per-member
- Individual AI pathing cached and reused

### [LIKELY] LOD AI

- Close = full brain (perception, pathing, combat)
- Medium = behavior state only (idle wander, pack cohesion)
- Far = position tick, no simulation
- Very far = despawned or dormant

### [LIKELY] Horde streaming

- Large groups rendered via instanced meshes + simplified physics
- Individual detail only for nearest 8-16
- Supports 100+ visible infected without tanking framerate

---

## Infected AI interactions with other plans

### Combat (see [PLAN-Combat.md](PLAN-Combat.md))

- Hit locations, stealth kills, caliber choice
- Suppressor meta

### Audio (see [PLAN-Audio-Design.md](PLAN-Audio-Design.md))

- Noise aggro is the nervous system
- Suppressor physics

### Medical (see [PLAN-Medical.md](PLAN-Medical.md))

- Bite wound infection chain
- Blood trail visibility

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Horde sieges pressure walls
- Tank infected can break lower-tier blocks
- Alarms and lights draw/distract

### Dynamic events (see [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))

- Horde sieges = event
- Crashed transports spawn event-local swarms
- Hive Mother cryptid is infected-adjacent

### Environment hazards (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Bio zones = higher infected density
- Mutation cure affects player vs infection risk

### Day/Night cycle (see [PLAN-Day-Night-Cycle.md](PLAN-Day-Night-Cycle.md))

- Night aggression boost
- Dawn dispersal

### Terrain carving (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

- Traps for infected groups
- Carved moats, spike pits
- Collapse-on-them structural tactics

---

## Gameplay verbs infected AI enables

- Fire a single unsuppressed round in a town, hear the groans cascade from three blocks, sprint to the rooftop for the high ground
- Clear a hive-anchored subway system with four squadmates and one Molotov, earn hours of regional quiet
- Spot a Screamer in a pack, take it out first with a crossbow bolt before the alert chain triggers
- Lure a wandering horde away from your base by driving a noisy truck past, park it, walk home quiet
- Ghost a full nighttime scavenge run with a suppressed subsonic .22, never fire it, walk past three Stalkers who never smell you
- Brace your base gates for a horde siege event after a day of heavy fighting spiked regional heat
- Kite a Tank into a structural-weakness wall with explosives, drop a building on it
- Lead Crawlers into your spike-pit traps at a choke point, farm their rare gland drops for Medic recipes
- Realize the kids at the school playground are child infected, hold position, shake off the dissonance, engage
- Reset your squad's heat meter by skipping town for three real-world days, return to find the streets nearly empty
- Use a flashlight as bait in a dark warehouse, draw a pack into a narrow hallway, machine-gun them down
- Drag a bleeding squadmate silently past a Stalker zone by crawling through grass with the bandaging done first

---

## Open questions

1. **Density cap per server** - how many infected total at once? Scale question.
2. **Infected reproduction / world repopulation** - hives respawn or does world slowly empty over months?
3. **Player infection becoming infected** - permadeath of character vs cure-available? Genre choice.
4. **AI "cheating" perception** - are infected allowed wall-hacks for aggro? Realism vs frustration.
5. **Cross-faction targeting** - will infected attack NPC factions too? Visible wandering NPCs fighting infected = immersion.
6. **Corpse despawn vs persistence** - do corpses stay forever (atmosphere) or despawn (perf)? Lean time-based.
7. **Mutation crossover** - can a mutated player gain infected-flavor traits (visible infection look)? Lean [DEFER].

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| AI behavior | Entity + state machine + perception |
| Pathfinding | Navmesh over hybrid terrain |
| Pack logic | Group AI + squad coordination |
| Noise aggro | Audio propagation (Audio Design plan) |
| Horde rendering | Instanced mesh + LOD pipeline |
| Hive respawn | Region persistence + timer |
| Migration | Inter-zone state + heat map |
| Performance scaling | Chunk streaming + LOD AI |

---

## Next actions

1. Define infected archetype registry (schema: HP, speed, aggro radius, attacks, drops)
2. Prototype baseline Shambler (spawn, perceive, chase, attack, die)
3. Noise aggro integration with audio plan
4. Pack behavior test (6 shamblers around a target - leader/flank/swarm)
5. Hive anchor + respawn loop (region persistence integration)
6. Horde rendering performance test (100 visible at 60 FPS target)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
