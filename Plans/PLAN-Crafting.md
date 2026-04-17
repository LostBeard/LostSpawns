# Crafting - Brainstorm and Plan

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

Crafting is the verb that turns **scavenging into progression**. Every recipe is a small puzzle + a resource decision. You don't just "have" things - you made them from what the world gave you. The moment a player crafts their first sewn-together ghillie hood or their first crude zip gun is when Lost Spawns graduates from a survival shooter into a survival craft.

**Design goals:**

1. **Every crafted item feels earned.** No vending-machine menus. Materials are scarce enough that decisions hurt.
2. **Recipes are stories.** A rusty zip gun made from plumbing pipe and a nail says something no looted M4 ever could.
3. **Crafting ties every system together.** Clothing, storage, traps, explosives, medical, food, base structures - crafting is the spine connecting them.
4. **Learn by doing.** Skill grows with repetition. Knowledge grows with discovery (books, notes, schematics).
5. **Craft what matters for survival.** Bandages before plate carriers. Rope before helicopter. Progression is survival-first.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield design. Foundation this sits on:

- **Entity + item system** (VoxelEngine Phase 12) - items as entities with metadata
- **Block system** (existing) - crafting stations are placeable blocks with attached logic
- **Persistence** (Phase 8 OPFS) - crafted items + learned recipes serialize per player
- **Shared material categories** - needs a new recipe registry / item-category taxonomy

---

## Crafting model

### [COMMIT] Hand-craft anywhere (basic recipes)

- Simple recipes don't need a station
- Just open craft menu, select recipe, consume materials, short animation (~2-5 seconds)
- Limited to what you can make with hands + basic held tools
- Examples: bandage from rag, rope from plant fiber, torch from stick + cloth + fuel, splint from branches + cloth

### [COMMIT] Crafting stations (Fallout 76-inspired depth)

Each station unlocks a tier of recipes. Stations are placeable blocks with their own condition + durability + rank progression. **Repair uses the same stations as crafting** - one physical workbench, two verbs.

**Station types:**

- **Workbench** - general tools, knife handles, simple wood + metal work, general repair
- **Forge** - smelt metal from ore, shape ingots, make nails / hinges / weapon parts
- **Campfire** - cook food, boil water, purify, smelt small amounts, basic field repair for scavenged gear
- **Sewing station** - faster / better clothing crafts, large garment work (tents, backpacks), clothing repair
- **Chemistry bench** - medical, explosives, dyes, fuel refining
- **Gunsmith bench** - weapon-specific work (barrels, triggers, stocks, scope mounting), weapon repair + mod install
- **Armor bench** - armor crafting, plate fitting, armor repair + mod install
- **Generator + power tools** - powered industrial stations (electronics, optics, firearm machining) require power link

### [LIKELY] Station ranks (Fallout 76 progression model)

Each station type has Ranks 1-3. Player unlocks higher ranks through progression + material investment.

| Rank | Recipes unlocked | Repair cap |
|------|------------------|------------|
| **Rank 1** | Basic survival crafts | Worn |
| **Rank 2** | Intermediate (most scavenger gear) | slightly above Worn |
| **Rank 3** | Master-tier (scoped rifles, plate carriers with mod slots) | Pristine |

Rank upgrade costs materials + time (e.g. Rank 2 gunsmith = 50 iron + 20 scrap electronics + rare schematic loot). Rank upgrades are permanent for that specific station block - build a new one and you start at Rank 1 again.

### [COMMIT] Scrap and mod (Fallout 76 parallel)

- **Scrap** - break down any category-matching item at its station for raw materials
  - Weapons at gunsmith, armor at armor bench, clothing at sewing station
  - Scrap yield scales with item condition + player skill
  - **Knowledge scrapping** - scrapping a new weapon/armor variant you haven't seen before TEACHES its mod recipes (F76 mod-learning loop)
- **Mod install / remove** - modular slots on weapons and armor
  - Weapons: scope, barrel, stock, grip, underbarrel, ammunition type
  - Armor: ballistic plates, pouch attachments, camo wrap, insulation liner
  - Swap mods at station between raids (long-range scope for overwatch, red dot for CQB)
  - **Removing a mod from a RUINED item saves the mod** - canonical survival-scavenger moment

### [LIKELY] Portable field workbench

- Small deployable station, carried in pack (takes ~20 slots)
- Craft/repair at Rank 1 equivalent
- ~2 minutes to deploy/stow
- Must be crafted at a Rank 2+ workbench first (bootstrapped, can't craft a portable from nothing)
- Great for extended expeditions and forward caches

### [LIKELY] Contested public workshops (Fallout 76 Workshop system)

- World-spawned stations at fixed map locations (fuel station garage, abandoned factory, ranger post, lumber mill)
- Any player can USE them without claiming
- **Claiming** a workshop invests materials, grants:
  - Priority crafting queue
  - Slow material generation (one scrap per hour of real time)
  - Alarm trigger if enemies approach while claimed
- Claims are PvP-contestable - raiders can destroy the claim or defeat the claimer and re-claim
- **Natural PvP flashpoint** - creates the "fight for the gas station" dynamic F76 is known for
- Claim expires after X hours of inactivity, workshop returns to neutral

### [LIKELY] Power requirements

- Industrial / advanced stations (gunsmith Rank 3, chemistry, powered workbenches) need electrical power
- Sources: gasoline generator (fuel cost), solar panels (daytime only), battery bank (stores solar), water wheel (river-adjacent), wind turbine (exposed placement)
- Power outage = those stations disabled until restored
- **Sabotage vector** - enemy raid cuts your power, you can't repair/craft until generator is re-fueled; strike during the blackout you create

### Station condition and raid vulnerability

- Stations degrade with use (heat, mechanical stress)
- Damaged station drops effective rank (Rank 3 Damaged might work at Rank 2)
- Ruined station = non-functional, requires major repair with another station or reduced-cap hand tools
- Explosives + breach charges destroy stations outright
- **Destroying enemy base's main station cripples their repair/crafting for weeks** - major raid objective
- Flagged for `PLAN-Base-Raiding.md` (future)

### Gameplay verbs crafting stations enable

- Spend a week leveling your gunsmith bench to Rank 3 to unlock the masterwork rifle recipe
- Drop a portable field workbench in a cave you carved out, spend the rainy evening repairing armor
- Return to base, find your armor bench destroyed by raiders, realize you can't fit new plates until it's rebuilt
- Scrap a ruined plate carrier for its ceramic plates, reuse them on your new vest
- Swap the long-range scope off your sniper onto your marksman rifle before a specific mission
- Sabotage the enemy base's generator to disable their stations during the blackout window you've created
- Claim the fuel-station workshop, defend it against a rival faction for two nights, collect the material yield
- Teach an ally how to craft scope mounts by demonstrating at your gunsmith station (learn-by-watching)
- Scrap a looted rifle variant you've never seen before, unlock its mod recipes for future crafts

### [COMMIT] Recipe knowledge system

Three ways to learn recipes:

1. **[COMMIT] Known from start** - simple common-knowledge crafts (bandage, rope, torch, fire)
2. **[COMMIT] Discovered via loot** - books, schematics, handwritten notes scattered as world loot. Teach one recipe on read, become consumable or trade-able.
3. **[LIKELY] Experimentation** - combine compatible materials at a station, discover a recipe organically (Minecraft alchemy-style). Failure chance at low skill, success teaches the recipe permanently.
4. **[UNDECIDED] Teacher NPCs** - rare roaming or settled NPCs trade knowledge for goods. Scope depends on NPC system.

### Material categories

Taxonomy needed for recipe registry. Core categories:

- **Fiber** - plant matter, grass, hemp, cotton bolls
- **Cloth** - rags, fabric scraps, canvas, burlap
- **Leather** - hide (raw / tanned), rawhide strips
- **Wood** - sticks, logs, planks, seasoned wood
- **Metal** - scrap, iron ingots, steel, alloys
- **Stone / ceramic** - flint, gravel, cement, brick
- **Plastic / rubber** - bottle plastic, tire rubber, PVC pipe
- **Chemicals** - fuel, oxidizer, alcohol, acid, fertilizer, lye
- **Electronics** - wire, chips, batteries, LEDs, capacitors
- **Organic** - meat, plant food, bones, fat, blood

Recipes specify required categories + sometimes specific items (a crude bow needs "wood" but a hunting bow needs "seasoned hardwood").

### [LIKELY] Tiered progression

Recipes are tiered. Higher tiers require higher stations + more material types + more skill.

| Tier | Station needed | Examples |
|------|---------------|----------|
| **0** | Hands | Bandage, rope, torch, splint, dirty water bottle, rag mask |
| **1** | Workbench | Wooden crate, crude knife, axe handle, fishing rod, simple traps |
| **2** | Forge | Metal tools, nails, hinges, knife blade, metal-head arrows |
| **3** | Chemistry | Crafted explosives, pharmaceuticals, fuel refining, dye baths |
| **4** | Industrial | Electronics, scopes, firearm machining, NVG repair, radios |

### [LIKELY] Quality variance

Crafted item quality = function of:

- **Input material condition** - pristine cloth > ragged scraps
- **Player skill** in the relevant specialty
- **Station quality** (damaged workbench produces lower-tier output)
- **Recipe mastery** (craft same recipe many times → mastery bonus)

Output quality maps to standard condition tiers (Pristine, Worn, Damaged). A masterwork craft (high skill + pristine materials + perfect station) can rarely produce slightly better than factory-looted equivalent.

### [LIKELY] Learn-by-doing skill

- Specialty skills: weaponsmith, tailor, medic, cook, chemist, engineer
- Skill grows with each successful craft in that specialty (slow)
- Skill plateaus without new recipes (can't infinitely grind one easy recipe)
- Specialty skills can be shared: a master tailor in your squad crafts clothes for everyone

### [UNDECIDED] Craft time

- **DayZ-style** - mostly instant with short animation, keep flow
- **Rust-style** - longer times, queue-based, work-while-you-wait
- Lean DayZ short-animation for most crafts, longer (~10s+) only for Tier 3+ items
- Some crafts REQUIRE realtime (cooking, smelting, chemistry), use passive timers with visible progress

### [LIKELY] Crafting interruption

- Most crafts pausable (walk away, come back)
- Station-held crafts (smelting, cooking) continue in your absence if station has power/fuel
- Combat interrupts hand-craft attempts, wastes partial materials

### [UNDECIDED] Shared crafts

- Squadmate contributes materials to your craft from their own inventory (permission-based)
- Useful for big jobs (large tent, heavy armor)
- UI complexity vs coop moment

---

## Signature craftables (what players actually want to make)

Concrete craftable items, grouped by use. Every item here hooks into another plan.

### Survival core

- **Bandage** (rag) - patches bleeding
- **Splint** (sticks + cloth) - reduces broken-leg penalty
- **Torch** (stick + cloth + fuel) - night vision, early game
- **Rope** (plant fiber) - climbing, crafting input
- **Fire starter** (flint + steel) - lights campfires, ignites Molotovs
- **Saline / IV** (chemistry) - advanced medical

### Food and water

- **Cooked meat** (raw meat + fire) - no food poisoning
- **Smoked jerky** (meat + salt + low-heat smoke, long timer) - preserves
- **Preserves / pickled vegetables** (food + jar + brine)
- **Purified water** (dirty water + fire OR filter)
- **Alcohol distillation** (grain + still) - drinkable + antiseptic + Molotov fuel

### Tools (see PLAN-Terrain-Carving.md)

- **Crude shovel** - dirt/sand only, slow
- **Stone axe** - trees, low-tier blocks
- **Metal axe / pickaxe** - full range, requires forge
- **Whetstone** - tool maintenance
- **Crowbar** - pry-opens doors, breaks locks slowly, weapon

### Clothing (see PLAN-Clothing-Storage.md)

- **Rag mask / balaclava** (cloth)
- **Rag-wrap gloves** (cloth + leather strips)
- **Crafted ghillie hood** (burlap + grass strips) - camouflage tier
- **Repair patches** (cloth squares, pre-made for speed repair)
- **Simple backpack** (hide + rope + wood frame) - low-tier hauling
- **Gas mask filter replacements** (carbon + cloth + chemicals)

### Storage (see PLAN-Clothing-Storage.md)

- **Wooden crate** (planks + nails)
- **Ammo can lid** (salvage)
- **Buried stash bag** (burlap + leather strips) - NON-METAL, key for defeating detectors
- **Protector case** (plastic) - watertight but metal-visible

### Traps (see PLAN-Terrain-Carving.md)

- **Tripwire** (wire + anchor stakes)
- **Punji pit stakes** (wood + whittling time) - moldable-terrain-native
- **Bear trap repair kit** (springs + teeth)
- **Spike board** (plank + nails)
- **Improvised grenade / IED** (pipe + explosive + fuse)
- **Molotov** (bottle + fuel + rag + ignition source)

### Weapons

- **Sling / slingshot** (wood + cord + leather cup)
- **Crude bow** (wood + cord) + **arrows** (shaft + head + fletching)
- **Crude crossbow** (wood + metal + cord) + bolts
- **Zip gun / pipe gun** (pipe + firing mechanism + crude ammunition) - single-shot, unreliable, survival-horror vibe
- **Hunting knife** (metal blade + handle) - tool AND weapon
- **Improvised club / spear** (wood + metal head)

### Base components

- **Support pillars** (wood or metal)
- **Doors** (planks + hinges + lock)
- **Wall panels** (planks / metal sheets)
- **Window shutters** (wood)
- **Ladder** (wood + rope)

### Chemistry / advanced

- **Painkillers** (willow bark + distillation)
- **Antibiotics** (mold cultures + time, Fleming-style)
- **Fuel refining** (crude oil + distillation → gasoline / diesel)
- **Dyes** (berries + fixer)
- **Acid / base chemicals** (raw materials + chemistry station)
- **Explosives** (fertilizer + fuel + detonator)

### Electronics / industrial

- **Radio receiver / transmitter** (wire + chips + battery)
- **Scope / optic repair** (lenses + tube)
- **NVG repair** (phosphor tube + lens + battery)
- **Solar panel** (cells + frame + wiring)
- **Battery pack** (cells + casing)

---

## Crafted vs looted balance

Design pressure: if crafting is too efficient, scavenging loses purpose. If looting is dominant, crafting is decorative.

Target: **crafted = reliable baseline, looted = luxury/rare/better**.

- Crafted items max at Worn tier without masterwork skill + perfect materials
- Looted items can arrive at Pristine
- Crafted zip gun works but jams often; looted M4 is superior
- Crafted bandages equivalent to looted; crafted IED equivalent to looted grenade
- Highest tier items (military optics, plate armor, prescription medication) only looted

### [REJECT] Collect-100-of-thing grinds

- Not the vibe. Recipes should be small, satisfying, meaningful. Never stockpile 100 sticks to craft one thing.

### [REJECT] Auto-pick-up and auto-craft macros

- Every craft is a decision. No "keep crafting until materials run out" button.

---

## Gameplay verbs crafting enables

- Scavenge car alternator wires + fertilizer + fuel to craft a makeshift IED for tonight's raid
- Forge a hunting knife from a car spring + wooden handle at your base's forge
- Brew painkillers from wild willow bark at a chemistry bench, dosing yourself after a broken leg
- Sew a personal ghillie suit from burlap sacks and grass strips collected over a week of scavenging
- Trade crafted bandages to a roaming player for ammunition (no currency needed, craft IS currency)
- Teach a new player the rope recipe face-to-face in exchange for a share of their next crafts
- Build a hidden chemistry bench in a basement, brew pharmaceuticals, sell to bandits for ammo
- Discover a schematic for military-grade armor in a locked trader chest, slowly accumulate materials to make one
- Distill alcohol from corn, use half for drinking / trade, half for Molotovs
- Read a chemistry book on a quiet rainy day in a safehouse, learn the explosive-making recipe
- Kit-bash two damaged weapons into one functional one, masterwork-skilled player only
- Waste 30 minutes learning a recipe by experimentation that turned out to be worthless, laugh about it, continue

---

## Cross-references

- **Repair** - see Repair and maintenance section of [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md). Repair and crafting use the same skills, same stations, same materials.
- **Clothing craftables** - see Gear/Clothing section of [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md)
- **Storage craftables** - see Storage section of [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md)
- **Traps + explosives craftables** - see Traps and snares + combat-carving sections of [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md)
- **Base structures** - flagged for future `PLAN-Base-Raiding.md`
- **Vehicles + fuel** - flagged for future `PLAN-Vehicles.md`

---

## Open questions

1. **Recipe discovery model** - pure schematic (Rust-style), pure experimentation (Minecraft-style), or hybrid?
2. **Quality scaling range** - narrow (crafted ≈ looted) or wide (crafted << looted)?
3. **Craft animation duration** - blocks combat (DayZ) or doesn't (Rust)?
4. **Shared squad crafting** - ship in v1.0 or defer?
5. **Station ownership** - locked to builder, squad-shared, or public?
6. **Recipe ecosystem** - hundreds of recipes (Minecraft), dozens (DayZ)? Lean toward ~60-100 for v1.0.
7. **Skill atrophy** - decay if unused, or learn-once-forever?
8. **Food / cooking depth** - simple (cooked / raw) or rich (recipes, spoilage, combos)?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Recipe registry | New shared library for recipe data (JSON-driven) |
| Skill system | New player progression layer |
| Crafting stations | Block system + block-inventory hooks |
| Material taxonomy | Item category metadata (extends item system) |
| Crafting UI | Blazor UI + WebGPU overlay |
| Persistence | Phase 8 OPFS (player inventory + learned recipes) |
| Station destruction | Structural integrity + damage model |

---

## Next actions

1. Lock recipe registry format (JSON schema for recipe data, hot-reloadable)
2. Prototype Tier 0 crafts (bandage, rope, torch) as proof-of-concept end-to-end
3. Define material category taxonomy as canonical list
4. Pick recipe discovery model (schematic / experimentation / hybrid) before Tier 1 ships
5. Design crafting UI mockup (grid + recipe list + materials consumed preview)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
