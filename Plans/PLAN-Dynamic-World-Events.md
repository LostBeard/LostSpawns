# Dynamic World Events - Brainstorm and Plan

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

The world of Lost Spawns is **always moving**. At any hour a distant explosion, a radio broadcast, or a swarm of birds overhead says *something is happening somewhere*. Players chase events for loot, reputation, and story - and discover they aren't the only ones chasing. Events are the engine that stops the world from feeling like a static loot-sandbox.

Inspired by Fallout 76 public events + DayZ dynamic loot drops + STALKER emissions, but molded into the Lost Spawns survival loop.

**Design goals:**

1. **World tells its own story.** Events appear, peak, resolve - whether or not players show up.
2. **Risk matches reward.** Bigger loot, louder signal, bigger fight - often with other players racing to beat you.
3. **Multi-party events are best.** Some events encourage cooperation even between rival factions (a three-crew mess over a crashed transport).
4. **Cryptids are boss-tier theater.** Named creatures with lore, appearance tells, and rare drops. Fighting one is a story told later.
5. **Radio is the town crier.** Broadcasts, distress calls, and emissions sell the event world to every player within range, not just a UI popup.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Entity + AI system** (VoxelEngine Phase 12) - spawns, behavior, boss statemachines
- **Audio broadcast layer** - radio stations, distance attenuation, directional cues
- **World persistence** (Phase 8 OPFS) - event state, post-event map scars
- **Weather/atmosphere** - emissions, fog-of-event signaling
- **Chunk streaming** (Phase 16) - load AI density at event site on demand

---

## Scripted vs procedural events

### [COMMIT] Two event pipelines

- **Scripted events** - hand-authored set pieces with fixed narrative. Slower cadence, higher polish. Examples: convoy ambush, broadcast tower heist.
- **Procedural events** - generated from templates (pick zone, pick density, pick reward pool, roll modifiers). Fast cadence, high replay. Examples: random crashed transport, wandering horde.

### [LIKELY] Event lifecycle phases

1. **Telegraphed** - radio chatter, distant sound, sky effect (smoke column, flare)
2. **Window open** - players can arrive; event is active; loot spawning / bosses awake
3. **Climax** - final wave, final objective, last-chance loot window
4. **Resolved** - area remains briefly for looting, then despawns AI and clears non-persistent props
5. **Scar** - carved terrain, wreckage, burn marks stay permanently (world remembers)

---

## Event catalog

### [COMMIT] Crashed transport

- Scheduled (timed) or triggered (pilot AI failure). Military plane, helicopter, supply drone.
- Active smoke column visible for kilometers
- Loot tier scales with aircraft class (drone = common, transport plane = rare, blackbox = unique)
- **Radiation cloud** around crash from leaking reactor / classified payload (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Attracts infected, cryptid scavengers, and rival crews
- First crew to secure the site can hold it - but holding draws waves

### [COMMIT] Convoy ambush / escort

- Procedural convoy of AI vehicles moves along road between zones on a schedule
- Player can ambush (loot shipment, invite bandit NPC reinforcements on timer)
- Alternatively: player accepts escort contract from faction, defends convoy to destination for rep + pay
- Rival crews may intercept mid-route
- Hijacked vehicle itself is loot (see [PLAN-Base-Building.md](PLAN-Base-Building.md) for vehicle storage)

### [LIKELY] Horde siege

- Infected migration triggers when region "heat" accumulates (shot counts, fires, noise)
- Zone enters siege state for N minutes
- Bases in zone take horde pressure (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md) defense systems)
- Reward: hordes drop aggregated rare loot + event clear bonus
- Narrative hook: bells start ringing on church roofs in affected town

### [LIKELY] Refugee column

- AI NPCs walk a road toward shelter, under-equipped
- Protect them = rep with survivor faction, cash reward on arrival
- Rob them = infamy, faction bounty, loot now
- Ignore = they reach safety, small positive event outcome, no player reward
- Ambusher NPCs (bandits) will attack the column if no player is present - players can intervene late

### [LIKELY] Black-market pop-up

- Rare traveling merchant appears at unlisted coordinates for 15-30 min
- Announced via encrypted radio channel (players must have radio, be scanning)
- Stocks cycling rare items (mods, schematics, perk cards - cross-ref [PLAN-Economy.md](PLAN-Economy.md))
- Protected by mercenary NPCs - hostile behavior drives them off and voids the visit

### [LIKELY] Broadcast tower heist

- Capturable tower claims zone influence for owning faction
- Active tower broadcasts your faction's chosen music/propaganda
- Other crews can contest - siege of the tower is its own event
- Holding tower grants passive loot tick + zone-wide radio reach

### [UNDECIDED] Contamination spill

- Procedural: tanker crashes / factory ruptures / cryptid bile releases a new toxic zone
- Zone persists until players clear the source (dangerous, requires NBC gear - cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Spreads over time if ignored
- Could be too punishing for solos - maybe lean [DEFER] to post-v1.0 until hazard system matures

### [LIKELY] Emission / pulse event

- STALKER-style world-wide emission: every N hours the sky changes, anomalies surge, radios scream
- 5-minute warning to reach shelter
- Players caught outside take damage / mutation risk
- Rewards: post-emission anomaly fields yield rare crafting drops

### [DEFER] Weather-triggered events

- Lightning strikes start wildfires in forests (smoke column event)
- Blizzards trap players in zones (cabin fever gameplay)
- Nice to have, defer until weather + fire simulation is solid

---

## Cryptid boss encounters

### [COMMIT] Cryptid design philosophy

- Each cryptid is a named, distinct boss with appearance tells, signature attack, unique drop
- Rare spawn - encountering one is a memorable moment, not a grind target
- Announced via environmental cues (tracks, corpses, radio reports) before you see it
- Drops rare perk cards (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)) + unique crafting materials + cosmetic trophies

### [LIKELY] Cryptid roster

- **The Scorched One** - mutated wildfire survivor, immune to fire, leaves burn trail. Spawns in old burn-scar zones. Drop: fireproof hide, Pyromancer perk card.
- **Mother Mutation** - matriarch infected, spawns lesser infected while alive. Kill fast or get swarmed. Drop: mutagen vial, Swarm Leader perk.
- **The Howler** - stealth predator, hunts by sound, near-invisible in fog. Best countered with suppressed weapons + silent movement. Drop: cloaking gland, Silent Step perk.
- **The Doctor** - former surgeon turned cannibal trap-master. Ambushes player in abandoned hospitals. Signature: pharmaceutical grenades, scalpel melee. Drop: surgeon's kit, Battlefield Medic perk.
- **The Broadcaster** - ghost-DJ entity that takes over radios in its presence. Audio torture attack (blurred vision + shaky aim). Melee range lethal. Drop: broken microphone, Radio Silence perk.
- **The Warden** - prison-escapee boss, swings a chain shiv, uses prison architecture. Spawns only at prison ruins. Drop: shiv-pattern knife, Intimidate perk.

### [UNDECIDED] Roaming vs anchored cryptids

- Anchored: spawns only at specific biome / location. Predictable hunts.
- Roaming: migrates across zones. Surprise encounters.
- Likely mix: most are anchored, Howler and Doctor roam.

### [LIKELY] Cryptid evidence and tracking

- Before you see a cryptid: tracks, mutilated corpses, distant howls, panicked radio chatter
- Skilled Survivalist (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)) can read signs to predict location
- Survivor NPCs offer paid hunt contracts on specific cryptids

---

## Event signaling (how players learn what's happening)

### [COMMIT] Radio broadcasts

- In-game radios (handheld + base + vehicle) pick up event chatter
- NPC announcer voice lines ("crash reported near the quarry, no survivors confirmed")
- Encrypted channels for black-market events (requires decryption item)
- Cross-ref audio layer in VoxelEngine

### [LIKELY] Environmental cues

- Smoke columns from crashes, fires
- Sky color shift during emissions
- Distant gunfire / screams carry over terrain
- Flocks of birds flee event zones
- Infected migration visible at range

### [LIKELY] Map icons (limited)

- Only events the player has *discovered* (heard on radio, seen cue, met NPC giving contract) appear on map
- No global event tracker. You have to be connected to the world to know the world.

### [REJECT] Global popup notifications

- No "EVENT STARTED!" banner for everyone. Breaks immersion, homogenizes meta.

---

## Multi-crew interaction at events

### [LIKELY] Three-faction standoff potential

- Big events often pull multiple crews. Chaos is the feature, not the bug.
- Loot is shared-nearest-owner or roll-based (design detail TBD)
- Truces emerge spontaneously - "you take the west wreck, we take east"
- Event AI doesn't care about factions - will aggro anything that shoots

### [UNDECIDED] Event-specific PvP rules

- Should some events be flagged safe-zone (refugee rescue, etc.)?
- Should others double the PvP stakes (crashed transport = no safe-zone)?
- Lean: same PvP rules everywhere, event design shapes natural behavior

---

## Gameplay verbs dynamic events enable

- Hear distant thunder, check radio, realize an emission is rolling in - sprint for the nearest sealed cellar with 90 seconds to spare
- Radio crackles: "black-market confirmed, grid 44-7, eight minutes" - drop your scavenge and run across the map with ammo to trade
- Escort a refugee NPC column through bandit territory as paid contract, earn faction rep worth the detour
- Camp the crash site smoke column with a sniper, ambush the rival crew that shows up to loot
- Track three-toed tracks through mud in the burn zone, corner The Scorched One for the Pyromancer perk
- Raid a broadcast tower held by a rival faction, claim it, set music to your squad's anthem
- Stand over Mother Mutation's corpse while your crewmate harvests the mutagen vial for chem trade
- Pass a refugee column in the distance, choose to rob it, gain infamy, spend a week avoiding bounty hunters
- Horde siege hits the town - barricade yourselves on church rooftops while bells ring and bodies pile in streets
- Radio picks up encrypted coords, decrypt with Engineer skill, find black-market vendor with the rare weaponmod you've been tracking
- Cryptid sign scout: find The Howler's territory by reading tree scratches, lure with sound bait, trap with bear traps (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

---

## Open questions

1. **Event cadence** - how many active events at a time? How long between events in a zone?
2. **Solo vs squad balance** - should some events scale down for solos, or stay squad-difficulty?
3. **Persistent scars** - every crash leaves wreckage forever? Could clutter the map over months.
4. **Cryptid respawn timing** - after kill, how long before new spawn? Per-server vs per-instance?
5. **Encrypted channels** - how does a player acquire decryption items? Rare loot, quest reward, black-market itself?
6. **Event skipping** - cooperate with rivals for mutual loot, or does first-to-contact rule always apply?
7. **Cross-event interaction** - can an emission kick off mid-convoy? Mid-cryptid fight?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Event scheduler | World state + persistence + RNG seed per zone |
| Cryptid AI | Entity + AI system (VoxelEngine Phase 12) |
| Radio broadcast | Audio layer + distance attenuation |
| Smoke columns | Particle/volumetric system |
| Scar persistence | OPFS region files + terrain carving state |
| Emission overlay | Weather + post-processing pipeline |
| Convoy AI | Pathing + vehicle physics |

---

## Next actions

1. Define event schema (JSON data-driven event template: zone, cues, phases, rewards, AI waves)
2. Prototype one scripted event end-to-end (Crashed Transport) as proof of concept
3. Radio broadcast audio pipeline - pick one cryptid, author 3 distress calls, test range attenuation
4. Cryptid AI statemachine spike (The Howler first - stealth tests core AI perception)
5. Lock event signaling rules (radio, visual, map) before UI work

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
