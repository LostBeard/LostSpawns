# Environment Hazards - Brainstorm and Plan

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

The world is trying to kill you quietly. Bullets are the obvious threat. **The air, water, ground, sky, and your own body are the others.** Radiation pools in basements. Chemical clouds drift with wind. Contaminated rivers rot your gut. A blizzard freezes you to the steering wheel of your own vehicle.

Survive those and you might pick up **mutations** - F76-style trade-offs that reshape your playstyle. A mutation isn't a punishment or a gift; it's a *tradeoff* the world carved into you.

Hazards shape where you go, what you wear, how long you stay. NBC gear becomes progression: your first gas mask is a milestone.

**Design goals:**

1. **Hazards ≠ damage ticks.** Each hazard has its own mechanic, detection method, and counter gear.
2. **Progression through preparation.** Level 1 player avoids yellow zones. Level 10 player hunts rare loot in them wearing full hazmat.
3. **Mutations are sideways changes, not upgrades.** Every mutation gives + and takes -. Player picks which ones to keep.
4. **Detection before damage.** Geiger clicks, chem detectors, smell, visuals - you get a chance to react.
5. **Hazard world scars stay.** Once contaminated, stays contaminated until cleansed (if ever).

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Weather + atmosphere system** - wind direction drives chem clouds, emissions
- **Entity system** (VoxelEngine Phase 12) - player body state (dose, contamination, mutations)
- **Flood-fill lighting** (Phase 14) - can be adapted for contamination spread sim
- **Persistence** (Phase 8 OPFS) - hazard zones, mutation state per character
- **Audio** - Geiger ticks, alarms, wheezing, wind-carried chem hiss

---

## Hazard categories

### [COMMIT] Radiation zones

- Procedural + hand-placed hotspots (reactor meltdowns, nuke sites, crashed transports)
- Dose accumulates over time (rads)
- Stages: none → exposed → sickness (nausea, shakes) → acute (damage over time) → lethal
- Radiation is cumulative across sessions until cleansed (rad-away, clinic visit, sleep in decontam shower)
- Visible cue: shimmer in air, glowing pools in reactor zones
- Detection: Geiger counter ticks from distance, faster closer, panic-fast at lethal

### [COMMIT] Chemical contamination

- Toxic gas clouds drift with wind
- Tankers, spill sites, cryptid bile pools, industrial ruins
- Bypasses clothing insulation - requires sealed gas mask
- Inhalation: coughing, vision blur, damage over time, permanent lung damage if severe
- Skin contact: burns (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) for hazmat suit)
- Detection: chem detector, yellowish haze, smell warning ("sharp chemical tang")

### [LIKELY] Biological zones

- Infected hive locations, cryptid lairs, mass graves
- Airborne pathogen, plus touch risk from contaminated surfaces
- Risk: infection stages (fever → weakness → conversion)
- Treatment: antibiotics, cures from Chemist (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md))
- Detection: buzz of flies audio, greenish particulate, blood smell
- Counter: respirator + gloves + don't touch anything

### [COMMIT] Weather hazards

- **Cold** - hypothermia. Clothing insulation matters (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))
- **Heat** - heatstroke, water demand doubles, armor weight doubles fatigue
- **Rain** - wet gear loses insulation, makes cold deadly, mutes footsteps but also mutes yours to enemies
- **Storms** - lightning strikes metal gear / open ground, thunder masks gunfire
- **Fog** - detection range collapses, cryptid cover, silent ambush meta
- **Blizzard** - visibility near-zero, cold lethal without shelter, tracks fill fast
- **Wildfires** - propagate with wind, drop burn-scar zones that become hazard sites later

### [LIKELY] Water contamination

- Not all water safe to drink. Stagnant pools, blood-tainted rivers downstream of events, contaminated industrial water
- Stages: fine → bad stomach → dysentery → severe dehydration
- Boiling sometimes helps (kills biologicals), never helps against rads or chems
- Water purifier tool (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md)) is progression gear
- Visual cues: color (muddy, yellow, green film), animal skeletons nearby

### [UNDECIDED] Anomaly / exotic zones

- STALKER-inspired: gravity anomalies (crush player), electrical anomalies (arc damage), psi fields (hallucinate, false AI enemies)
- Cool but scope-heavy
- Lean: one type for v1.0 (gravity anomaly that flings debris) - scope creep otherwise

### [DEFER] Structural hazards

- Crumbling floors, rotten beams, fall-through damage
- Interesting but needs structural integrity simulation to land well - defer until core simulation is solid

---

## Detection gear

### [COMMIT] Geiger counter

- Tick rate scales with dose per second
- Headphone-capable for silent scouting
- Battery hungry
- Tier 1 (analog, noisy) up to Tier 3 (digital, silent, logs dose history)

### [COMMIT] Gas / chem detector

- Passive sniffer, beeps when entering hazardous air
- Cheap cigarette-pack-sized model up to military field-grade (directional)

### [LIKELY] Biohazard detector

- Rare, late-game. Detects pathogen density in air
- Distinguishes active infection zones from empty corpses

### [LIKELY] Thermometer / environmental monitor

- Reads current temperature + humidity
- Useful to plan layers before leaving base (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

### [UNDECIDED] Dosimeter badge

- Passive wearable, tracks cumulative dose visible to squad members
- Nice detail, maybe [DEFER]

---

## NBC protective gear

### [COMMIT] Gas masks

- Filter slots (disposable consumable), filter lifetime in hours of exposure
- Tier 1: civilian dust mask (chem only, short duration) → Tier 4: military NBC mask (all threats, long filter life)
- Fogs up in cold / rain (visual penalty - tradeoff)
- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) for slot integration

### [COMMIT] Hazmat suit

- Full-body, seals chem + bio
- Heavy, hot (heatstroke risk), clumsy (reduced speed/stamina)
- Tears from damage - torn suit leaks
- Repair via patches (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

### [LIKELY] Lead-lined vest / apron

- Reduces rad dose rate (not bio/chem)
- Heavy, slow, but you can keep your face exposed
- Layer under/over other clothing

### [COMMIT] Anti-rad / anti-tox meds

- **Rad-X** - blocks rad uptake (prophylactic, take before exposure)
- **Rad-Away** - purges accumulated rads (take after)
- **Anti-tox** - neutralizes chem poisoning if taken in time
- **Antibiotics** - treats bio infections
- Chemistry skill crafts these (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))

### [LIKELY] Respirator (reusable)

- Reusable compact version of gas mask
- Weaker than military mask but much lighter + filter-free
- Best for brief entries

---

## Mutation system

### [COMMIT] Mutation acquisition

- Heavy radiation / bio exposure risks a mutation roll on recovery
- Roll pool: ~10-15 mutations, weighted
- Multiple mutations can stack but some conflict (game enforces mutual exclusion)
- Mutations persist across deaths (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md) skill degradation is separate)

### [COMMIT] Mutation catalog (F76-inspired)

Each mutation is `benefit | drawback`.

- **Thick Skin** - +damage resistance | -movement speed, harder to sprint
- **Night Eyes** - clear vision in darkness | light sensitivity, muzzle flashes blind
- **Adrenal Surge** - extra damage at low health | stamina drain at full health
- **Healing Factor** - slow passive regen | +hunger/thirst rate
- **Marsupial** - +jump height, +carry weight | -INT (crafting speed penalty)
- **Plague Walker** - toxic aura damages nearby infected | self-damage tick
- **Herd Mentality** - bonus stats in squad | penalty when solo
- **Scaly Skin** - water resistance, slower dehydration | crafting/repair speed penalty
- **Empath** - team damage shared (reduces ally damage taken) | you take extra when allies are hit
- **Carnivore** - huge nutrition from meat | vegetables cause nausea
- **Herbivore** - huge nutrition from plants | meat causes nausea
- **Bird Bones** - faster sprint, lighter fall damage | easier to stagger, -carry weight
- **Speed Demon** - faster sprint | faster hunger/thirst
- **Grounded** - resist electrical damage + EMP | cannot use tech gear (scopes, night vision)
- **Talons** - unarmed damage boost | cannot equip gloves (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

### [LIKELY] Mutation cure

- Crafted by Chemist (late-game recipe)
- Consumable, removes one rolled mutation (player chooses which)
- Expensive - commits the player to living with mutations they like

### [LIKELY] Mutation suppressor (temporary)

- Pharma that suppresses drawbacks while active (hours)
- Still allows benefits
- Recipe is valuable trade good (cross-ref [PLAN-Economy.md](PLAN-Economy.md))

### [UNDECIDED] Visible mutation effects

- Cosmetic changes (scales on skin, glowing eyes, etc.)
- Other players can spot mutated survivors - social stigma in lore, gameplay tell
- Good immersion, but risks feeling like a costume layer - revisit during art direction

### [REJECT] Pure-upgrade mutations

- No mutation is all upside. Every one has cost.

---

## Hazard zone lifecycle

### [LIKELY] Persistent hazard zones

- Hazard state saved to OPFS region file
- Once contaminated, stays contaminated until actively cleaned
- Scars the world map in players' memory and gear kits

### [LIKELY] Active decontamination

- Player can clear small hazard zones with NBC gear + cleanup kit (Chemist crafted)
- Rewards: map gets "clean" again, grateful NPC factions, possible rare loot beneath contamination

### [UNDECIDED] Hazard spread simulation

- Does radiation bleed outward over time? Chem cloud diffuse?
- Could be cool but expensive simulation cost
- Lean: fixed radii, drift on wind only for active clouds

### [LIKELY] Seasonal weather hazards

- Winter regions: cold gear mandatory
- Summer: heatstroke risk in urban heat islands
- Seasonal event: "the long winter" stretch with blizzards every few days

---

## Hazard interactions with other plans

### Clothing (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Insulation values feed weather damage calc
- Hazmat + mask fills specific slots
- Wet clothing loses insulation (rain + cold = deadly)

### Crafting (see [PLAN-Crafting.md](PLAN-Crafting.md))

- Chemist skill gates rad/tox/bio/mutation cures
- Chemistry bench is the counter-hazard workshop

### Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Survivalist skill = environmental resistance passives
- Survival perks stack hazard tolerance
- Chemist skill determines cure crafting success

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Sealed rooms (airtight + sealed doors) become decontam shelters
- Decontam shower station removes cumulative rads

### Dynamic events (see [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))

- Emissions = world-wide hazard pulse
- Crashed transports spawn rad fields
- Contamination spill = player-triggered new hazard zone

---

## Gameplay verbs hazards enable

- Watch a green haze roll down a valley road on the wind, reroute two kilometers over a ridge to avoid the chem cloud
- Hear the Geiger tick rise from gentle to panicky, turn around before the dose becomes lethal
- Pop a Rad-X before diving into the reactor, pop a Rad-Away when you emerge, live to loot another day
- Get caught in a blizzard without insulated boots, barricade yourself in a cabin with a lit fireplace, wait it out
- Roll Night Eyes after a bio incident, become a night-raid specialist who can't stand noon sunlight
- Stack Marsupial + Bird Bones for parkour insanity at the cost of every crafting stat
- Craft mutation suppressors for a squadmate with Plague Walker so they can run field medic without cooking allies
- Drink from a green-filmed pond by accident in a firefight, spend the next day tracking down antibiotics
- Hazmat up and walk into an emission-zone anomaly field, loot three rare crafting materials, dodge gravity anomalies on the way out
- Use a Tier 3 Geiger counter to map a contaminated building room-by-room, find the one safe-dose corner with the safe underneath the floorboards
- Survive a wildfire by running downhill into a river, watch the flames chase you across the tree line
- Run the full Cure protocol at a Chemistry bench, pick which mutation to drop, keep the one you grew to love

---

## Open questions

1. **Dose math visibility** - exact rads number or fuzzy "low/med/high" band? Sim depth vs clarity.
2. **Mutation persistence across respec** - does a respec token (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)) clear mutations or preserve them?
3. **NBC gear durability** - how fast do filters burn? Slow = low tension. Fast = constant chore.
4. **Cross-hazard damage stacking** - rad + chem at once, additive or multiplicative?
5. **Animal reactions** - do local wildlife flee contamination, or die in it as visual warning?
6. **Server-wide emission timers** - synced globally or per-player? Sync = MP event, async = solo-friendly.
7. **Mutation cosmetic vs gameplay** - do mutations show on character model, and if so does that reveal strategy to other players?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Dose / contamination state | Entity system + persistence |
| Hazard zone persistence | OPFS region file + zone metadata |
| Chem cloud drift | Weather + wind + volumetric rendering |
| Geiger / detector audio | Spatial audio + range attenuation |
| Mutation system | Stat pipeline + UI + cure recipes |
| NBC gear slots | Clothing layer system (cross-ref Clothing plan) |
| Emissions | Post-processing pipeline + global event sync |

---

## Next actions

1. Define dose model (rads/sec per zone, absorb rate with gear, saturation caps)
2. Prototype one hazard end-to-end (radiation) + its detection gear (Geiger counter)
3. Lock mutation data format (stat pipeline integration, save/restore, cure recipe)
4. Design hazard zone metadata (type, intensity, radius, decay rules)
5. NBC gear art + slot mapping with Clothing plan owner

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
