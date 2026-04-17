# Vehicles - Brainstorm and Plan

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

Vehicles are **rare, fragile, and transformative**. Finding one is a milestone. Fixing one is a project. Driving one turns a two-hour hike into a ten-minute run - and makes you a target from every ridge. DayZ got this right: the hijacked truck is half the stories.

Lost Spawns builds on that: you don't spawn with a vehicle, you build one from a rust-pile. Wheels, battery, spark plugs, radiator, fuel. Tow it to your base. Fix it in a garage. Drive it into a fight.

**Design goals:**

1. **Every vehicle has history.** Wrecks are found where someone crashed, not placed on a spawn grid.
2. **Repair is a project, not a button.** Diagnose what's missing, scavenge parts, install at a workbench, test drive, iterate.
3. **Vehicles break.** Dings, punctures, engine failures. Route planning matters. Spare tires matter.
4. **Vehicles carry cargo.** Trucks haul loot. Bikes ride solo. Helicopters deliver raids. Logistics scale with vehicle class.
5. **Vehicles persist.** Park it, it's there later. Lock it, someone may hotwire it. Losing your truck hurts.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Vehicle physics** - chassis + wheels + suspension (new engine system)
- **Entity system** (VoxelEngine Phase 12) - vehicles as moving entities with seats
- **Persistence** (Phase 8 OPFS) - vehicle location, condition, contents saved per world region
- **Crafting + repair** - part registry, workbench integration (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))
- **Terrain** - collision with voxel + SDF ground (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

---

## Vehicle catalog

### [COMMIT] Land vehicles

- **Bicycle** - T1 starter, silent, pedal-powered (no fuel), low carry, frail
- **Motorcycle** - fast, low carry, loud, easy to hide
- **Sedan / hatchback** - 4 seats, moderate trunk, civilian parts
- **Pickup truck** - 2 seats + bed (large cargo), off-road capable
- **Military truck (deuce-and-a-half)** - 8 seats, massive bed, rugged, slow
- **ATV / quad** - 1-2 seats, off-road specialist, medium cargo
- **Civilian van** - 6 seats, high cargo, poor off-road
- **Armored vehicle (APC/Humvee)** - rare, military loot, armor plating, gunner port

### [LIKELY] Water vehicles

- **Rowboat** - manual oars, silent, 2-3 seats, fishing platform
- **Skiff / small motorboat** - outboard engine, 4 seats, coastal
- **Fishing boat** - larger motor, 6 seats, onboard storage + gear rack
- **Speedboat** - fast, loud, smuggler favorite
- **Pontoon raft** - player-craftable from logs + lashing (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))

### [LIKELY] Air vehicles

- **Helicopter** - very rare, requires pilot skill + complex parts + runway/open ground
- **Ultralight / hang glider** - rare, short range, no return trip
- **Drone (recon)** - small, remote-piloted, scouting tool not transport

### [UNDECIDED] Trains

- Post-apocalyptic rail lines with abandoned trains?
- Cool idea (S.T.A.L.K.E.R. tram) but heavy scope for v1.0
- [DEFER] unless natural event fit (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) convoy ambush)

### [REJECT] Spawn-with-vehicle

- No "start with a car" shortcut. Scavenge it.

---

## Part system

### [COMMIT] Required parts to run

Each vehicle class requires a checklist of parts:

- **Engine block** - cannot be bypassed (core repair)
- **Battery** - start the vehicle, runs electrics
- **Spark plugs** - combustion engines only
- **Radiator** - overheats + fails without it
- **Fuel tank** - holds fuel
- **4 × tires** (or class-appropriate count) - flat tires = limp mode
- **Transmission** - drives the wheels
- **Brakes** - can drive without, but will regret it
- **Windshield / windows** - optional but exposes driver

### [LIKELY] Part condition and failure

- Each part has condition 0-100%
- Below 30%, random failure chance (dead battery mid-drive, tire blowout)
- Failed part must be replaced; field repair possible for some (patch tire) but quality reduced

### [LIKELY] Part compatibility

- Not every part fits every vehicle (truck tire ≠ bike tire)
- Categories: civilian light, civilian heavy, military, motorcycle, boat, aircraft
- Encourages specialization + cross-vehicle part scavenging

### [UNDECIDED] Electric / solar vehicles

- Some rare vehicles run on battery + solar rechargeable
- Silent, eco, no fuel dependency - but slow recharge
- Lean [LIKELY] for 1-2 specific vehicles (solar ATV, electric moped)

---

## Fuel system

### [COMMIT] Fuel types

- **Gasoline** - most civilian vehicles
- **Diesel** - trucks, military, boats
- **Jet fuel** - aircraft
- **Electric** - batteries + solar
- **Pedal** - bikes (human fuel)

### [LIKELY] Fuel scarcity + refining

- Pre-war fuel cans rare, most fuel stale (requires stabilizer)
- Refuel stations drain over server lifetime
- Player can refine crude (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md) Chemist skill) - slow, requires refinery station
- Fuel trades as commodity (cross-ref [PLAN-Economy.md](PLAN-Economy.md))

### [LIKELY] Siphoning

- Drain fuel from abandoned vehicles with hose + container
- Drain from other players' vehicles (theft vector)
- Skill increases yield + speed

---

## Vehicle storage and cargo

### [COMMIT] Cargo scaling

- Bike saddlebag: tiny
- Motorcycle panniers: small
- Sedan trunk: medium
- Pickup bed: large (+ roof rack extendable)
- Truck/Van: huge
- Boat: medium + fishing gear rack
- Helicopter: medium (weight sensitive)

### [LIKELY] Exposed vs sealed cargo

- Truck bed exposed - loot visible to passersby, rain damages
- Trunk sealed - private, weather-proof, locked
- Roof rack exposed + tarpable

### [LIKELY] Weight affects handling

- Overloaded truck brakes slower, tips on turns, drinks more fuel
- Passenger count affects weight

### Cross-ref with [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) storage containers - vehicle cargo follows same rules (slot grid, weight, weather exposure)

---

## Theft and security

### [COMMIT] Lock + hotwire

- Owner can lock vehicle (keyed or code)
- Thief can hotwire: timed skill action, noisy, may fail
- Engineer skill (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)) reduces hotwire time

### [LIKELY] Vehicle alarm

- Upgrade part - trips audible alarm if tampered
- Attracts infected + nearby players (double-edged)

### [LIKELY] Kill switch / hidden cutoff

- Installed part that disables engine unless toggled in secret location
- Defeat requires time + Engineer skill

### [UNDECIDED] GPS tracker

- Install on your vehicle, see location on personal map even if stolen
- Counter: jammer destroys signal
- Lean [LIKELY] - great for recovery gameplay

### [LIKELY] Garage doors and walls

- Cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md) - enclosed garage protects vehicle from weather + casual theft
- Walls and doors create physical barrier - thieves must breach

---

## Combat with vehicles

### [LIKELY] Weapon racks

- Mount rifles/shotguns for quick draw from driver/passenger seats
- Cross-ref [PLAN-Combat.md](PLAN-Combat.md)

### [LIKELY] Mounted turrets

- Pickup bed or truck roof turret (machine gun)
- Gunner position, requires operator
- Ammo box attaches to turret, reloads via inventory

### [LIKELY] Ramming

- Vehicle mass + speed = blunt trauma damage to infected/players
- Damages vehicle too (front-end condition drops)
- Dedicated reinforced bumper part mitigates damage

### [LIKELY] Drive-by shooting

- Fire personal weapons from passenger windows
- Accuracy penalty while vehicle moves
- Driver cannot shoot and steer well (context switch penalty)

### [UNDECIDED] Vehicle armor upgrades

- Bolt steel plates to doors, windows (makeshift up-armor)
- Adds weight, reduces speed, increases protection
- Lean [LIKELY] for late-game specialist vehicles

---

## Vehicle physics + terrain

### [COMMIT] Off-road vs on-road

- Road blocks (asphalt, concrete) = high speed, low wear
- Grass + dirt = medium
- Mud + snow = slow, risk of stuck
- Rock + rubble = slow + damage

### [LIKELY] Water crossing

- Shallow streams: crossable at speed (with risk)
- Deep rivers: vehicle floods, engine stalls, eventually destroyed
- Amphibious vehicles (rare military): can traverse

### [LIKELY] Getting stuck

- Mud, snow, deep sand - traction fails
- Recovery: winch (attached to tree/vehicle), push from passenger, dig out with shovel (cross-ref terrain carving)

### [LIKELY] Collision damage

- Wall, tree, cliff, other vehicle
- Damage scales with speed squared
- Front-end > doors > rear > wheels in damage distribution

---

## Persistence

### [COMMIT] Park-where-you-leave

- Vehicle stays at last parked location
- OPFS region file stores vehicle entity + state
- Survives server restart

### [LIKELY] Chunk unload behavior

- If no player is near, vehicle is dormant (not simulated)
- Loads back when player enters chunk
- Saves CPU + supports huge numbers of vehicles world-wide

### [UNDECIDED] Vehicle decay

- Left outside for weeks, paint rusts, tires deflate slowly, battery drains
- Realism tax - may punish casual players
- Lean [LIKELY] with very slow decay (weeks), offset by garage protection

### [LIKELY] Vehicle inventory persists

- Contents of trunk/bed saved with vehicle
- Steal the truck = steal its contents

---

## Multiplayer seat layout

### [COMMIT] Driver + passenger + gunner

- Each seat is a distinct interaction point
- Driver: controls vehicle
- Passengers: can look around, shoot from windows
- Gunner: operate mounted turret
- Back of pickup bed: exposed but can stand and fire

### [LIKELY] Seat-specific controls

- Driver can't reload weapons while driving (balance)
- Passenger can loot while moving
- Gunner cannot reload without fellow passenger handing ammo (teamwork)

### [UNDECIDED] Convoy / escort formation

- Squad marks a lead vehicle, others auto-follow at distance
- Useful for convoy escort events (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))
- Lean [DEFER] unless AI driver tech mature

---

## Vehicles interactions with other plans

### Terrain carving (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

- Carved roads enable fast travel across rough terrain
- Trenches + berms block vehicle approach
- Buried mines disable wheels

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Garage blocks for indoor storage
- Fuel station placements, refueling at base
- Workshop ranks enable higher-tier repairs

### Crafting (see [PLAN-Crafting.md](PLAN-Crafting.md))

- Workbench required for advanced part install
- Chemist refines fuel from crude
- Engineer skill drives repair quality

### Combat (see [PLAN-Combat.md](PLAN-Combat.md))

- Mounted turrets + weapon racks
- Vehicle AP rounds (RPG, .50 cal) designed for vehicle kill

### Economy (see [PLAN-Economy.md](PLAN-Economy.md))

- Fuel as currency
- Parts (batteries, tires) as commodities
- Vehicle resale by informal markets

### Dynamic events (see [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))

- Convoy ambush events loot vehicle + cargo
- Crashed helicopter events place rare aircraft

---

## Gameplay verbs vehicles enable

- Stumble across a rusted pickup in the woods, log coords, spend a week gathering a battery and four tires to bring it home
- Push a motorcycle by hand through a forest trail to reach a hidden base location without burning fuel
- Mount a machine gun on your truck bed, drive a cryptid hunt as mobile fire support
- Siphon fuel from an abandoned bus at a gas station, fill your jerry cans for the journey home
- Park your pickup outside a rival base, loot the interior, drive off with their gear in your truck bed
- Lose your truck to a hotwire thief, use the GPS tracker to follow them to their base for revenge
- Craft a pontoon raft from lashed logs, float cargo across a river no vehicle can ford
- Navigate a blizzard in a pickup, engine overheats from salt + cold, limp to the nearest sheltered garage
- Land a helicopter on a crashed transport event (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md)) before the other crews can arrive on foot
- Pedal a bicycle through a Scorched One's burn zone in silence, bypassing the cryptid's hearing aggro
- Drive-by a bandit camp at night with flashlights off, let the gunner rake the campfire
- Roll a deuce-and-a-half through mud, spend an hour winching it out of the bog with your squad's help

---

## Open questions

1. **Vehicle rarity cap** - how many active vehicles per server? Too many = parked-car-apocalypse, too few = empty roads.
2. **Fuel economy realism** - real-world mpg (punitive) vs game-balance mpg (easy)? Lean balance.
3. **Vehicle combat damage model** - HP per section or simulated part damage? Sim deeper = cooler, HP simpler.
4. **Helicopter learning curve** - realistic (hard) vs arcade (easy)? Lean mid-realistic with trainer mode.
5. **Fuel stabilizer requirement** - does all pre-war fuel require it, or first-tank free? Pacing question.
6. **Car keys** - is "having the key" a persistent item, or is every vehicle hotwireable? Lean: keys exist, hotwire is fallback.
7. **Boat ocean scope** - coastal only, or deep-water travel? Server scope question.

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Vehicle physics | New chassis + suspension simulation (engine work) |
| Part system | Item registry + vehicle state schema |
| Fuel system | Consumable + persistence + refinery crafting |
| Cargo storage | Storage schema shared with containers |
| Mounted weapons | Combat system + seat-specific input |
| Persistence | OPFS region file with vehicle entities |
| Hotwire / lock | Skill checks + Engineer skill (progression) |
| Water vehicles | Water physics + buoyancy (new) |
| Aircraft | Flight physics + pilot skill + runway mechanic |

---

## Next actions

1. Pick one vehicle for end-to-end proof (pickup truck is DayZ signature)
2. Define part registry schema (JSON: vehicle class, required parts, compatibility, condition rules)
3. Prototype park-and-persist round-trip (drive → OPFS save → reload → drive)
4. Fuel system first-draft (tank, consumption, refuel at gas station)
5. Mounted turret spike as proof of vehicle-mounted combat

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
