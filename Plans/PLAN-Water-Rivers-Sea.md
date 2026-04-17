# Lost Spawns: Water, Rivers, and Sea

## Status Legend
- **[COMMIT]** settled design decisions
- **[LIKELY]** strong preference, expect to commit
- **[UNDECIDED]** open
- **[DEFER]** post-1.0
- **[REJECT]** explicitly not doing

---

## Premise

Lost Spawns is set on a peninsula. That is a deliberate design choice. A peninsula has three coasts, one border to a larger landmass, and internal waterways connecting them. Every map spawns players on land but water is never more than a few kilometers away. The sea is the backdrop of every long horizon shot. A river likely runs through your forest. A lake sits in your valley.

Water is also the most common gameplay failure mode for survival games. Either water does nothing (Minecraft) or water does everything including magic (Subnautica). Lost Spawns sits in the DayZ register: water is a traversal option, a hazard, a resource, a set of hiding places, a food source, and a slow death to the unprepared.

This plan defines how water works from "I am thirsty" to "I am swimming to shore from a sinking motorboat in a storm while a Drowner pulls at my leg."

---

## Design Principles

### 1. Water Is Not Safe

**[COMMIT]** Water can kill you. Drowning, hypothermia, disease from drinking bad water, aquatic cryptids, being unable to escape a current. Water is a system to be respected, not a trivial travel layer.

### 2. Water Is Not Punishing For Its Own Sake

**[COMMIT]** Players who understand water can use it as a tool: traversal shortcut, hiding place, fishing spot, scent-washing mud removal. Players who respect it survive.

### 3. Consistent Rules

**[COMMIT]** The same water voxel that you can swim in, you can fish in, you can drink from (with or without consequence), you can draw for cooking. Water is one simulation, not multiple disconnected systems.

### 4. Peninsula Geography Enables Stories

**[COMMIT]** Three coasts + rivers = approach options. A raid party can come by boat, by beach, by bridge. A survivor can flee by sea. The geography shapes faction conflict.

---

## Water Types

### Freshwater

**[COMMIT]** Freshwater voxels: rivers, lakes, streams, pools, wells.
- Safe to drink when clean (most of the interior)
- Contaminated by corpses, industrial runoff, proximity to cryptid territory
- Supports freshwater fish
- Can be purified by boiling or tablets

### Saltwater

**[COMMIT]** Saltwater voxels: sea, ocean, bays.
- Never safe to drink (dehydrates faster than not drinking)
- Can be desalinated (sunlight + tarp, or crafted distillation setup)
- Supports saltwater fish
- Kills land crops (seaspray damage to coastal gardens, see PLAN-Gardening-Agriculture when written)

### Brackish

**[LIKELY]** Estuaries where rivers meet the sea. Half-salt, half-fresh. Rich fishing. Dangerous visibility (murky).

### Still vs. Moving

**[LIKELY]** Water has a "current" vector per voxel. Rivers have a downhill flow. Lakes and ponds are still. Sea has tidal motion (slow, rhythmic).

**[LIKELY]** Strong currents carry swimmers downstream. Players can use this for traversal (float down to a camp) or get into trouble (pulled past the safe crossing into rapids).

### Depth

**[COMMIT]** Water has depth defined by voxel column height. A lake can be 1m deep in shallows and 20m in the middle. Depth affects:
- Light penetration (dark at depth)
- Visibility of submerged objects
- Swim speed (deeper = slightly slower due to no kick-off-bottom)
- Types of fish present
- Ability to stand vs. having to swim

---

## Swimming

### Basic Motion

**[COMMIT]** The player can swim on the surface or dive under. Transitions are smooth.

**[COMMIT]** Surface swim:
- Speed: ~70% of walking
- Stamina drain: present but slow (1-2 min per full stamina)
- Visibility: above water, normal
- Noise: audible splashing if fast, quiet if slow

**[COMMIT]** Dive (underwater):
- Speed: slightly faster than surface (using both arms and legs)
- Stamina drain: higher
- Oxygen meter counts down (~45 sec for an untrained character, ~90 sec for an experienced one)
- Visibility: limited to ~8-15m depending on water clarity
- Noise: much reduced on surface, nearly silent underwater

### Drowning

**[COMMIT]** If oxygen hits zero underwater: drowning damage begins. Cannot be stopped by anything except surfacing. Dead in ~10 seconds of oxygen-zero.

**[COMMIT]** Unconscious players in water drown. Rescuing unconscious friends from water is a real skill.

**[LIKELY]** Panic effect at low oxygen: vision blurs, sound muffles, movement becomes clumsy. Informs player to surface without needing a UI scream.

### Swimming with Gear

**[COMMIT]** Heavy backpacks and heavy armor reduce swim speed and increase stamina drain. Carrying a rifle, full pack, and wearing plate armor = swimming is a crisis.

**[LIKELY]** Players can drop items underwater to lighten load. Dropped items fall to the bottom and stay there (can be retrieved if you can get back down). Some items sink, some float (balsa wood floats, cans float if full, steel sinks).

**[LIKELY]** Before swimming, players can stow weapons (reducing hand availability but reducing drag). Drawn weapons cannot be used while swimming without a specific "surface + aim" pose.

### VR Swimming

See PLAN-VR-Controls. Breaststroke motion or button fallback. Underwater, sweeping arms provides locomotion.

### Surface Combat

**[LIKELY]** Firing a weapon from water:
- Surface: possible but heavily reduced accuracy. Weapon still functions (unless wet - see below).
- Underwater: most firearms do not fire underwater. Dedicated harpoon guns work.

**[LIKELY]** Firing at a swimmer from land: targets are partly submerged, harder to hit, but also slower-moving. Fair PvP fight.

### Diving Speed and Equipment

**[LIKELY]** Fins (rare loot): increase underwater swim speed ~40%, reduce stamina drain. Worn in the flippers slot.

**[LIKELY]** Diving mask: reduces visibility penalty underwater, no other effect.

**[LIKELY]** Scuba tank (very rare, end-game loot): replaces oxygen meter with tank duration (10-30 min depending on tank condition). Crit rare military find.

**[LIKELY]** Underwater flashlight: illuminates ~5-10m in dark water. Essential for deep exploration.

---

## Hydration System

See PLAN-Survival-Needs for the core thirst meter. Water-specific details:

**[COMMIT]** Drinking from unclean water sources carries disease risk. Possible consequences:
- Cholera: severe diarrhea, rapid dehydration, needs rehydration salts + rest
- Giardiasis: nausea, slower thirst loss, minor HP drain
- Heavy metal poisoning: from industrial runoff, permanent-unless-cured tremor effect

**[LIKELY]** Safe water sources:
- Boiled (any water, boil ~10 game-minutes)
- Purification tablets (rare loot, crafted at Warden camps)
- Running rivers above human/cryptid habitation (usually safe)
- Wells in established NPC camps (safe)

**[LIKELY]** Unsafe water:
- Standing puddles
- Downstream of corpses or cryptid territory
- Near industrial ruins (chemical contamination)
- Saltwater (always unsafe, dehydrates)

**[LIKELY]** Rain can be collected via tarp + container. Clean unless player is in a contamination zone.

---

## Fish and Fishing

See PLAN-Animal-Wildlife-Hunting-Fishing for the core fishing system. Water-specific details:

**[COMMIT]** Freshwater fish: trout, bass, perch, catfish, pike
**[COMMIT]** Saltwater fish: mackerel, cod, flounder, sardine, tuna (rare, deep-sea)
**[COMMIT]** Brackish-specific: eel, striped bass
**[LIKELY]** Cryptid-adjacent aquatic species (see Cryptid section below)

**[LIKELY]** Fish populations respond to:
- Water quality (polluted = fewer, weaker)
- Time of day (feeding patterns)
- Weather (storms bring different fish to surface)
- Overfishing by players (local depletion)

**[LIKELY]** Large bodies of water sustain more fish than small. A lake can be fished daily; a pond goes dry in a week of heavy use.

---

## Boats and Watercraft

See PLAN-Vehicles for the vehicle framework. Water-specific watercraft:

### Rowboat

**[COMMIT]** Small wooden rowboat. 1-2 passengers. Oar-powered. Silent but slow. Findable, repairable, craftable.

### Motorboat

**[COMMIT]** Small outboard-motor boats. 2-4 passengers. Fast but loud. Requires fuel. Findable at marinas, boat yards.

### Fishing Boat

**[LIKELY]** Larger working boats. 4-8 passengers + cargo. Shallow trawler or deep-sea variant. Rare findable at coastal towns.

### Raft (Crafted)

**[LIKELY]** Crafted from logs + rope. Slow, crude, unreliable in rough water. Early-game water traversal option.

### Sailboat

**[LIKELY]** Sailboats exist at marinas. Require wind direction management, skill to captain. Silent, long-range, no fuel.

**[UNDECIDED]** Sailboat operation complexity. Could be a dedicated mini-system (rigging, jibing, tacking) or simplified (point direction, sail appears). Lean toward simplified-but-wind-aware.

### Canoe / Kayak

**[LIKELY]** Single-person paddle craft. Stealth tier. Portable (carry on back over short distances). River-friendly.

### Jet Ski

**[DEFER]** Post-1.0 if the scene calls for it. Too arcade-y for initial vibe.

### Large Ships

**[DEFER]** Post-1.0. A beached freighter as a location is fine; sailing a freighter is not.

### Vehicle Damage in Water

**[LIKELY]** Boats take damage from collisions, storms, gunfire. Damaged boats take on water. Water accumulation sinks the boat when it exceeds capacity.

**[LIKELY]** Bilge pump item can reduce water accumulation. Crafted patches fix small hull breaches.

**[LIKELY]** Sinking boat: players must jump/swim. Gear must be secured or lost.

---

## Rivers as Natural Obstacles and Highways

**[COMMIT]** Rivers in Lost Spawns are navigable AND obstacles. They separate regions but can be bridged, forded at shallow points, or swum across.

**[LIKELY]** Bridges: pre-built (some intact, some damaged), rare to find intact after the Cascade. Strategic chokepoints. Destroying a bridge changes regional traffic patterns for all players.

**[LIKELY]** Fords: shallow crossings where the river is waist-deep. Traversable on foot but slow + visible + dangerous to stand in during combat.

**[LIKELY]** River mouths: where rivers meet the sea. Good fishing, easy landing for boats, popular settlement sites.

**[LIKELY]** Rapids: impassable except by skilled boaters; sink most rafts. Some rivers have rapids sections clearly audible before you see them.

**[LIKELY]** Waterfalls: impassable by boat, possible to dive off (serious fall damage risk - survivable from low falls, lethal from high).

---

## Coastal Mechanics

### Tides

**[LIKELY]** Coastal water level rises and falls on a ~12-hour cycle (halved real-time for ~6-hour game cycles or scaled to the day-night cycle).

**[LIKELY]** Low tide exposes tidal pools, beachcombing opportunities, and sometimes shipwrecks accessible only at low tide.

**[LIKELY]** High tide covers beach routes, can strand boats on rocks, floods coastal bunkers partially.

**[UNDECIDED]** How dramatic are the tides? Realistic tides are 1-3m. Game tides could be more extreme for gameplay (5-8m) but push into absurdity.

### Beachcombing

**[LIKELY]** Beaches wash up loot over time. Wreckage from old ships, washed-up bottles (with notes or items), seaweed (crafting material), driftwood.

**[LIKELY]** Beachcombing is a low-stakes survival activity. Rewards slow and modest but reliable.

### Shipwrecks

**[LIKELY]** Pre-Cascade shipwrecks dot the coast and shallows:
- Small fishing boats (accessible by wading)
- Cargo containers (washed off tankers, tiered military/industrial loot)
- Full ships (freighter, trawler - major locations)
- Submarines (rare, top-tier end-game location)

**[LIKELY]** Diving into wrecks requires oxygen management, flashlights, equipment. High-reward exploration content.

### Coastal Military

**[LIKELY]** Coastal defense sites from pre-Cascade: radar stations, coastal batteries, lighthouses, abandoned navy bases. Tier-heavy military loot.

**[LIKELY]** Some have underwater approaches accessible only by dive.

### Storm Damage

**[LIKELY]** After a named storm (see PLAN-Weather), coastal areas show damage: debris on beaches, shifted sandbars, new beachcomb loot from disturbed waters, temporary flood zones.

---

## Aquatic Cryptids

The Cryptid plan flagged aquatic cryptids for this plan. Here they are.

### Drowner (Homo cascade-3, aquatic adaptation)

**[LIKELY]** Base: human. Wrong: webbed digits, gills along the neck, skin chalk-white from lack of sun. Lurks in rivers, ponds, and shallow coastal water. Eyes bioluminescent faintly.

**[LIKELY]** Behavior:
- Grabs swimmers from below, pulls them under
- Not aggressive on land (slow and awkward out of water)
- Ambushes at night more often than day
- Usually solitary; occasionally in pairs

**[LIKELY]** Vulnerability: slow out of water. Firearms effective when Drowner is above surface. Underwater harpoon or melee underwater (desperate).

**[LIKELY]** Drops: cryptid marrow (water-variant), occasionally pre-Cascade items from long-past victims (watches, keys, personal effects).

### Leviathan (massive deep-water cascade-3)

**[LIKELY]** Base: ??? possibly whale, possibly fused mass like Walking Tower. Enormous, only in deep sea.

**[LIKELY]** Behavior:
- Does not approach shore
- Surfaces rarely - massive silhouette breaks the horizon as a rare event
- Attacks boats that venture far from coast
- Cannot be killed by conventional firearms

**[LIKELY]** Vulnerability: explosives to gills (impossible without boat), starve it of boats (avoid deep sea). Essentially a geographic hazard, not a kill target for 1.0.

**[LIKELY]** Narrative: sighting a Leviathan is a storytelling moment. Players who glimpse it write it on maps, warn others.

**[UNDECIDED]** Is the Leviathan killable at end-game with coordinated group effort + explosives? Tempting but complex. DEFER as a post-1.0 content beat.

### Gill-Scavver (aquatic Scavver cousin)

**[LIKELY]** Flightless Scavver variants that swim. Flocks in coastal shallows. Aggressive like normal Scavvers but in water.

**[LIKELY]** Vulnerability: shotgun from a boat. Fragile individually.

**[LIKELY]** Drops: Scavver feathers (waterproofed, higher trade value), fish they were eating.

### Corrupted Fish

**[LIKELY]** Regular fish species near high-pollution or Cascade-network water sources can be SERAPH-3-contaminated. Visually darker, scaled wrong, edible only if detected-and-cooked properly (tainted meat risk per PLAN-Cryptid-Biology).

---

## Diving and Exploration

### Free Diving

**[COMMIT]** Lungful of air, go down, come back. Limited by oxygen meter. 45-90 second dive depending on character fitness. Flashlights for dark depths.

**[LIKELY]** Depth pressure: unused below ~30m in free dive (player can't get that deep in one breath anyway).

### Scuba Diving

**[LIKELY]** Scuba tank + regulator + mask. Extends dive time to 10-30 min. Extremely rare loot; hoarded by Wardens.

**[LIKELY]** Deep dives (below 50m): pressure effects. Slow ascent required to prevent "the bends." Decompression stops modeled as stop-and-wait at depth markers.

**[LIKELY]** Scuba is an end-game specialty. Unlocks deep-sea wreck content, underwater bunker entries, some story content (pre-Cascade research facilities accessible only by dive).

### Underwater Visibility

**[LIKELY]** Visibility factors:
- Water clarity (lake > river > brackish > stormy sea)
- Depth (less light deeper)
- Time of day (noon > dusk > night)
- Weather (storms kick up silt)
- Underwater flashlight

**[LIKELY]** Visibility range in meters typically 5-20m. Silty river: 1-2m. Clear tropical-style lake: 30m.

### Salvage

**[LIKELY]** Shipwrecks and underwater loot spawns. Salvage takes time (retrieve item, swim back, surface). A good salvage dive is an event.

**[LIKELY]** Large salvage items require a winch + boat + crew. Coordinated multiplayer activity.

---

## Water-Based Combat

**[LIKELY]** Swimming combat is a distinct skill. Firearms have heavy penalties while swimming. Underwater harpoon is the dedicated weapon.

**[LIKELY]** Holding a knife while swimming: available, slow attacks, but can ward off Drowners in a pinch.

**[LIKELY]** Boat-to-boat: fire over the side, duck behind gunwale for cover, try not to capsize.

**[LIKELY]** Boat-to-shore: vulnerable target (slow), but elevated firing position (if boat has superstructure). Tactical tradeoffs.

**[LIKELY]** Depth charges / grenades in water: area-effect but concussive. Good against tight groups of Drowners near the surface.

---

## Weather and Water

See PLAN-Weather for full treatment. Summary:

- **Rain:** fills containers, raises river levels, increases humidity
- **Storm:** coastal danger - rogue waves, capsize risk for small boats
- **Fog:** reduces water visibility dramatically, hiding Drowners and obstacles
- **Freeze:** shallow rivers/ponds freeze solid in winter. Ice thickness varies. Thin ice breakthrough = cold + drowning risk.
- **Thaw:** broken river ice chunks become navigation hazards

---

## Water as Narrative

**[LIKELY]** Rivers are routes the Wayward (nomadic faction) use extensively. Their seasonal migrations follow waterways.

**[LIKELY]** Coastal towns are Settler strongholds. Fishing and boats provide food security.

**[LIKELY]** Pre-Cascade shipwrecks are holotape dense - scattered crews left final messages.

**[LIKELY]** The Cascade itself involved leakage into the watershed. Rivers carried pathogen downstream; coastal waters remain contaminated in patches.

**[LIKELY]** Hilltop Station (the origin site) is inland but has a river running past. Water tests downstream still show SERAPH-3 signatures. Faction mission content uses this.

---

## UI and Feedback

**[LIKELY]** Oxygen meter appears only when underwater or when breath is held. Fades from HUD otherwise.

**[LIKELY]** Depth indicator when diving: simple number (10m, 30m) in peripheral HUD.

**[LIKELY]** Water temperature indicator: cold water drains body temp even in summer. Cold-water warnings when approaching hypothermia.

**[LIKELY]** Wet status: visible on character (dripping, darker clothing). Drying takes time out of water. Wet clothing = cold penalty.

---

## Deliverables for 1.0

1. Freshwater vs. saltwater vs. brackish voxels
2. Current vector per water voxel, river flow
3. Swim (surface + dive) with oxygen + stamina
4. Drowning mechanics
5. Gear affects swim speed
6. Water contamination + disease system
7. Water purification (boil, tablet, clean source)
8. Boat variety: rowboat, motorboat, fishing boat, raft, sailboat simple, canoe
9. Boat damage + sink mechanics
10. Fish populations per water type
11. Tides on coast
12. Beachcombing
13. Shipwreck exploration
14. Coastal military sites
15. Drowner cryptid
16. Gill-Scavver cryptid
17. Leviathan event (non-killable rare sighting)
18. Free-dive + scuba dive equipment
19. Underwater visibility + flashlight
20. Water-UI (oxygen, depth, wet status)
21. Wet clothing affects temperature

---

## Open Questions

**[UNDECIDED]** Tsunami as a dynamic world event? Massive coastal reshape. Cool but complex to implement.

**[UNDECIDED]** Underwater caves as a biome? Stunning exploration content if done right. Very expensive.

**[UNDECIDED]** Aquaculture as a crafting system? Fish farms at player bases. Crosses into PLAN-Gardening-Agriculture scope.

**[UNDECIDED]** Surfing / recreation water activity? Probably no for the tone.

**[UNDECIDED]** Ice fishing as a winter activity? Likely yes, fits the peninsula climate.

**[UNDECIDED]** Submarine as a vehicle? Super cool end-game fantasy but complex to implement correctly. DEFER to post-1.0 likely.

---

## Relationship to Other Plans

- **PLAN-Cryptid-Biology** - aquatic cryptid species detailed here
- **PLAN-Animal-Wildlife-Hunting-Fishing** - fish species, fishing mechanics detailed there
- **PLAN-Vehicles** - boats are vehicles, shared framework
- **PLAN-Survival-Needs** - hydration, wet clothing temperature
- **PLAN-Medical** - water-borne disease model
- **PLAN-Weather** - storm + rain interactions with water
- **PLAN-World-Biomes-Regions** - peninsula geography, coastlines, rivers
- **PLAN-Combat** - water-specific combat rules
- **PLAN-VR-Controls** - breaststroke + underwater motion
- **PLAN-Day-Night-Cycle** - tidal timing, moon phase
- **PLAN-Factions-Squads** - Wayward river traffic, Settler coastal towns
- **PLAN-Gardening-Agriculture** (not yet written) - coastal salt damage, aquaculture option
