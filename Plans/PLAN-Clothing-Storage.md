# Clothing and Storage - Brainstorm and Plan

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

DayZ proves that **clothing and storage aren't mechanics - they ARE the game**. Every piece of gear you find, wear, lose, or hand to a stranger carries meaning. A tattered jacket with three hidden pockets pulled off a corpse in a wet field contains someone's dead character's last hope. Gear is narrative - what you're carrying IS your story.

**Design goals:**

1. **Gear is story.** Every item worn or carried encodes a past. Condition tiers, repairs, bloodstains, faction patches - all visible context at a glance.
2. **Layered wearables.** Base / mid / outer / accessories, each a distinct slot with its own protection, insulation, capacity, and visibility impact.
3. **Storage is spatial.** Containers exist in the world, not in a magic menu. Buried stashes tie to moldable terrain. Base storage ties to structures. Tents leave physical footprints.
4. **Condition matters.** Every item degrades with use, damage, environment. Pristine → worn → damaged → badly damaged → ruined. Repair is a core survival loop.
5. **Visibility is tactical.** What you wear tells other players what you have, what you can do, and whether you're worth killing. Camouflage and muted colors are gameplay, not cosmetic.

---

## Foundation (what exists today)

**Nothing yet in VoxelEngine or Lost Spawns** - this is greenfield design. But the foundation it'll sit on:

- **Entity system** (VoxelEngine Phase 12) - players, NPCs, items as entities. Clothing attaches to entity.
- **Persistence** (Phase 8 OPFS region files) - player inventory + world containers serialize through existing save path.
- **Moldable terrain** ([PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md)) - buried storage slots directly into the terrain modification system.
- **Block structures** (existing block system) - base storage is furniture-blocks with associated inventory.

---

## Clothing system

### [COMMIT] Layered wearables (DayZ-style slot taxonomy)

Every character has fixed clothing slots. Each slot holds one garment. Each garment has its own sub-inventory capacity.

| Slot | Examples | Capacity | Main role |
|------|----------|----------|-----------|
| **Headgear** | hat, cap, beanie, balaclava, helmet, gas mask | small | protection, insulation, filter |
| **Face/eyewear** | sunglasses, goggles, NVG mount, scarf, respirator | minimal | vision, filter |
| **Top layer** | t-shirt, long-sleeve, thermal base | small | insulation base, minimal storage |
| **Mid layer** | sweater, hoodie, fleece | medium | warmth, medium storage |
| **Outer layer** | jacket, coat, parka, ghillie suit, rain slicker | large | weather protection, MOST visibility impact |
| **Vest** | plate carrier, chest rig, fishing vest, ammo vest | high-priority | tactical storage, armor mount |
| **Gloves** | work, tactical, winter | none | hand protection, dexterity mod |
| **Pants** | jeans, cargo, military trousers, waders | medium-large | storage, leg protection |
| **Boots** | sneakers, work boots, combat boots, rubber | none | foot protection, noise mod |
| **Belt** | tool belt, utility belt, pistol belt | quick-access | holster, knife sheath, small pouches |
| **Backpack** | day pack, hiking, military ALICE, ghillie pack | largest | primary hauling, speed penalty |

Slots are exclusive - you can't wear two jackets. Layering happens across slots (thermal base under shirt under jacket).

### [COMMIT] Condition tiers

- **Pristine** - brand new or fully repaired. 100% effectiveness.
- **Worn** - normal use. Small effectiveness drop.
- **Damaged** - visibly rough. Notable drops in protection/insulation. Repairable.
- **Badly damaged** - shredded. Major drops. Repair barely viable.
- **Ruined** - destroyed. Unusable, can tear for rags/materials.

Degradation from: time worn + activity level, damage taken (bullets, shrapnel, falls, claws, fire), environmental exposure (soaking, burning, corrosion).

### [LIKELY] Insulation, waterproofing, temperature

- Each garment has an insulation rating - contributes to core temperature model
- Waterproofing percentage - full waterproof outer + non-waterproof layers is the right combo
- **Overheating** - winter gear in summer = heatstroke, dehydration
- **Wet gear** = reduced insulation until dried by fire or ambient sun
- **Hypothermia** under-dressed at night or in rain
- Visible indicator: breath fog in cold, sweat stains in heat

### [COMMIT] Camouflage and visibility

Gear has a color/camo profile that directly affects detection range from other players and AI.

- **Bright colors** (hi-vis orange, red, white) - seen at 500+m
- **Urban colors** (grey, black, dark blue) - blend into buildings
- **Woodland camo** - forests, green biomes
- **Desert camo** - arid biomes
- **Winter camo / snow smock** - blizzards, snow biomes
- **Ghillie suit** - near-invisible at range in matching biome, slow movement, rustles

Detection range is `camo_match × biome × lighting × range`. Wearing woodland in a desert = spotted from 300m; wearing desert in desert = 80m.

### [LIKELY] Body armor and plates

- **Helmet** - reduces headshot damage, stops handgun rounds, cracks on big hits
- **Plate carrier** - front/back plate slots, various tiers
- **Soft armor** (Kevlar vest) - stops handgun, weak against rifle
- **Hard plates** (Level III/IV ceramic) - stop rifle, heavy
- **Leg/arm armor** - niche, heavy, rare
- Plates have condition INDEPENDENT of carrier - swap plates mid-raid if you find better

### [LIKELY] Hidden pockets

- Some garments have a "hidden" inventory sub-compartment
- Not visible to inventory-inspect from other players (if that mechanic exists)
- Small capacity (1-2 slots)
- Custom-modded gear = more hidden slots
- Gameplay: smuggle a key/map/small valuable past a capture or search

### [UNDECIDED] Clothing customization / patches

- Dye clothing (natural dyes crafted from plants)
- Sew on faction patches, name tags, unit insignia
- Identity / tribe marker
- Could be cool, could be bloat - post-v1.0 unless it emerges as core to a faction system

### [UNDECIDED] Body type / size variation

- Different players = different proportions
- Clothing fits loose / perfect / tight
- Loose = slight silhouette obscuration, tight = slight restriction
- Adds dimension but UI complexity

### [REJECT] RPG loot stats (glowing swords, +5 strength)

- Not the vibe. Realism-adjacent survival, not loot-explosion RPG.

### Gameplay verbs clothing enables

- Strip a fresh corpse, layer up in the dead player's warmer jacket before hypothermia sets in
- Patch a damaged jacket with a sewing kit scavenged from an abandoned house
- Don a ghillie suit, crawl into a pre-dug hollow in a hillside, wait out the patrol
- Swap a cracked plate for a pristine one mid-firefight behind cover
- Tuck the apartment key into your jacket's hidden pocket before entering trader zone
- Rip a ruined shirt into rags for bandages when medical supplies are gone
- Dress a captured player in tattered rags and bright orange so your squad spots them at 500m if they try to run
- Dye your woodland camo a darker shade by the campfire with crushed berries
- Tape up a nearly-ruined pair of boots with duct tape for one more night on patrol

---

## Repair and maintenance

Repair is not a sub-feature - it's a **core survival loop**. Broken gear is a thousand small decisions. Did you carry enough kits? Can you afford to waste one on a botched attempt? Is this jacket worth saving, or should you tear it for rags?

### Repair taxonomy (material to kit)

| Material | Primary kit | Scavenged substitutes |
|----------|-------------|------------------------|
| Cotton / denim / canvas | Sewing kit | Needle + thread, fishing line + needle |
| Leather | Leather sewing kit | Thick thread + awl, duct tape (temp cap) |
| Rubber / plastic | Plastic repair kit | Epoxy, duct tape |
| Hard armor (plate, helmet) | Epoxy putty | None - looted only |
| Metal (tool edges, weapon parts) | Tool kit / grinder | Sharpening stone, file |
| Electronics (radio, scanner, NVG) | Electronic repair kit | Screwdriver + harvested parts |
| Universal emergency | Duct tape | Widely available, capped effectiveness |

### [LIKELY] Tiered repair

- Each repair raises condition by ONE tier. Ruined is unrecoverable.
- Tier gained depends on kit quality × skill × material match:
  - **Perfect kit + skilled player** = tier up, small chance of +2 tiers
  - **Duct-tape emergency** = tier up but caps at Damaged (never reaches Pristine)
  - **Wrong kit** = half tier up, full kit consumed

### [COMMIT] Weapon maintenance cycle

Weapons are different from clothing - they accumulate fouling from use, not just damage.

- **Cleaning cycle** - fire N rounds without cleaning → jam probability rises
- **Cleaning kit** - rod + patch + bore brush, consumable patches
- **Oil** - prevents rust in humid biomes, speeds reload slightly
- **Field strip** - animation (~30 seconds), weapon stowed, player vulnerable
- **Jam clears** - work the charging handle, cycles bad round (short animation, vulnerable)
- **Condition tiers** - same pristine-to-ruined scale as clothing
- **Ruined weapon** - fires unpredictably or refuses to cycle; catastrophic failure risk (misfire damages the shooter)

### [LIKELY] Tool maintenance

- Axe / pickaxe / shovel / knife edges dull with use
- **Whetstone** restores sharpness incrementally
- **Power tools** (drill, chainsaw, angle grinder) need fuel + filter cleaning
- Dull tools cut/dig slower and produce less harvested material per swing

### [LIKELY] Storage container maintenance

- **Rusty barrel** - unmaintained barrel rusts over time, contents start degrading
- **Rotting tent** - exposed fabric tent rots, loses capacity, eventually collapses
- **Wooden crate** - splits and warps in rain, repair with nails + planks
- Container maintenance prevents slow attrition on stored gear

### [LIKELY] Skill influence

- Repair skill starts low, grows with successful attempts (learn-by-doing)
- Low skill: higher chance of kit-waste, tier-downgrade, or ruin-on-fail
- High skill: faster repair, better tier recovery, occasional free repairs
- Skill slowly decays if unused for long periods (DayZ-style atrophy, optional)

### [LIKELY] Failed repair consequences

- **Wasted kit** (common) - materials consumed, gear unchanged
- **No tier change** (partial) - kit consumed, gear unchanged (same as wasted but feels worse)
- **Tier DECREASE** (botch) - rare, gear gets worse than before
- **Ruined on fail** (catastrophic) - only on already-Badly-Damaged items with emergency substitutes
- Failure risk shown BEFORE commit - "75% success chance" visible on craft confirm

### [UNDECIDED] Repair stations

- **Workbench** in base unlocks better tools, faster repair, higher cap
- **Sewing machine** replaces hand-stitching for big jobs (large tents, coats)
- **Grinder** for metal tool edges and weapon parts
- **Gunsmith bench** for weapon-specific work (trigger, barrel, optics)
- Trade: mobility (field repair) vs quality (base repair)
- If base stations ship, field repair keeps a lower tier cap (can't reach Pristine without station)

### [REJECT] Instant repair / full-restore "potions"

- Not the vibe. Every repair is a small story, a small cost, a small choice.

### Gameplay verbs repair enables

- Clean your rifle by the campfire after a long day, oil it against morning rust
- Field-strip a jammed gun under fire, pray no one peeks around the corner in the 12 seconds you're stowed
- Duct-tape a nearly-ruined coat for one more cold night, knowing it won't survive the week
- Sharpen your axe blade on a whetstone before a long logging run
- Swap parts between two damaged rifles to make one functional one (a.k.a. kit-bashing)
- Refuse to repair an heirloom jacket pulled off a friend's body - wear the damage as memorial
- Trade repair services at a player settlement - your high skill IS your currency
- Waste your last sewing kit on a bad roll, curse, continue
- Maintain a rusty barrel at your forward cache so the stashed rifle inside doesn't degrade
- Choose between repairing three Damaged items at low risk, or one Badly Damaged item at high risk, with only two kits left

---

## Storage - containers in the world

### [COMMIT] Tent family

- **Small tent** - personal, 2-3 slots worth, cheap to craft
- **Medium tent** - squad-size, 40+ slots, needs cleared ground
- **Large/military tent** - base-size, 100+ slots, tarps + poles + stakes
- Physical world objects, visible from air/distance
- Hide by building around them, placing in deep forest, draping camo netting
- **Persistence** across sessions, decay timer if no interaction
- **Raid vulnerability** - "fabric" hitbox, cuttable with knife, destroyable by explosives

### [COMMIT] Barrel storage

- Metal barrel, ~50 slots, weatherproof
- **Camo paint** - painted to match biome (craft: netting + spray paint + dirt)
- Rust condition - poorly maintained barrel ruins contents over time
- Cannot be moved once placed (heavy)
- Looted or crafted from scrap metal

### [COMMIT] Wooden crate

- Cheap, craftable, ~20 slots
- Stacks well (physical placement, not magic grid)
- Less weatherproof than barrel - contents degrade in rain
- Broken with axe/pickaxe

### [LIKELY] Ammo can / small lockbox

- Waterproof, small (5-10 slots)
- Ideal for documents, ammo, sensitive items
- Small enough to carry INSIDE a backpack - nested container

### [COMMIT] Buried stash bag / protector case

**Directly ties to moldable terrain + buried-stash detection sections of [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md).**

- **Protector case** - watertight plastic, 10-15 slots, metal-detector-visible
- **Stash bag** - cloth/burlap, 15-20 slots, NON-METAL, defeats basic detector
- Buried with moldable terrain brush: dig hole → drop container → refill hole
- Position persists at exact coords; shovel at recorded spot to unbury
- **GPR can still find them** unless layered / distracted / placed near ambient void
- **The full arms-race payoff** with the detection system from the terrain plan

### [LIKELY] Base storage - furniture blocks

- Cabinets, lockers, gun safes, footlockers, desks
- Each a placeable block with attached inventory
- Condition of the block affects security (rusty lock = easier to break, gun safe = only breach charges)
- **Flagged for PLAN-Base-Raiding.md** (future) - locks, alarm integration, safe-cracking

### [LIKELY] Vehicle storage

- Trunk, bed, glovebox, under-seat
- Capacity by vehicle class (car trunk vs truck bed vs van interior)
- Vehicle destruction destroys contained items (big loss, high stakes)
- Flagged for PLAN-Vehicles.md (future) if vehicles land in v1.0

### [LIKELY] Persistence and decay

- All containers persist across sessions (Phase 8 OPFS region files)
- Inactivity decay: containers without recent owner interaction "fade" (contents vanish, container eventually despawns)
- DayZ-style: tents decay 7-45 days depending on activity
- Keeps the world from filling with abandoned loot

### [UNDECIDED] Locking systems

- Combo lock, padlock, keyed lock, keypad, biometric
- Each has break-in tools (bolt cutters for padlock, drill for combo, thermite for safe)
- Alarm integration (flagged for base-raiding)
- Shared key management for squad use

### [REJECT] Magic bag of holding (infinite inventory)

- Not the vibe. Everything has physical footprint.

### Gameplay verbs storage enables

- Paint a barrel woodland-green, half-bury it at the base of a tree, mark position by triangulation
- Tuck a stash bag into a dug hole, cover with dirt, smooth with moldable brush so no disturbance tell remains
- Cache a full respawn kit in a buried protector case so if you die far from base, you have gear nearby
- Empty a looted barrel into your own tent, relocate the barrel, repaint it
- Build a gun safe into a hidden compartment behind a cabinet in your base
- Combination-lock your most valuable container and memorize the code rather than writing it down
- Leave an intentionally rusty barrel near a trail as a decoy - real stash is 50m deeper in the woods

---

## Inventory model (UI + mechanics)

### [COMMIT] Slot-based grid (DayZ style)

- Each item occupies N slots (1x1, 2x1, 2x2, 3x2, etc.) in a grid
- Garments present as sub-grids when opened
- Tetris-style packing - pack smart or lose capacity
- Weight affects movement/stamina but isn't the primary constraint

### [COMMIT] Quick-access belt + holster slots

- 2-3 hot-swap slots for immediate draw (holster, knife sheath, grenade loop)
- Mapped to number keys (1, 2, 3)
- Faster than menu-swapping in combat

### [LIKELY] Weapon slings

- Primary rifle slot (back sling)
- Secondary weapon slot (side sling or shoulder)
- Pistol in holster (belt)
- Hot-swap with a key, brief swap animation

### [LIKELY] Two-handed action restrictions

- Using the terrain brush / GPR scanner / binoculars = two-handed
- Must stow the gun first (animation time)
- Creates "do I scan now, or keep my rifle ready" tension
- Squad: one player scans while others cover

### [UNDECIDED] Weight as hard cap vs soft cap

- **Hard cap** - over-weight = can't pick up more
- **Soft cap** - over-weight = slower, stamina drain, but still carryable
- Lean SOFT - more interesting decisions, less UI friction

### [UNDECIDED] Inventory UI: drag-drop vs hotkey

- Mouse drag to move items is DayZ-proven, intuitive
- Hotkey + context menu is faster
- Support both

---

## Cross-references

### Tie-ins with [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md)

- **Buried stash bags** - full hide/seek arms race with metal detector and GPR
- **Ghillie suits** - synergy with moldable terrain (crouch in dug hollow, near-invisible at range)
- **Camo paint on barrels** - manual per-biome blending with surroundings

### Tie-ins with future `PLAN-Base-Raiding.md`

- Base furniture storage (vaults, safes, cabinets in player-built structures)
- Locking systems and alarm integration
- Breach tools matched to storage tier (bolt cutter / angle grinder / thermite)

### Tie-ins with future `PLAN-Vehicles.md`

- Vehicle trunks and beds
- Mounted storage crates on trucks/trailers
- Vehicle destruction destroying contained items

---

## Persistence and performance

### Storage persistence

- Worn items + container contents serialize per-player or per-chunk
- Phase 8 OPFS region files - worn inventory in separate player save file
- World containers (tents, barrels, crates, buried stashes) serialize with their chunk

### Inventory UI performance

- Grid renders once per open, cached while open
- Hover tooltips lazy-load
- Drag-and-drop reuses entity-grab mechanics

### Decay background task

- Background service scans chunks for owner-less containers
- Decay timer advances per real-time hour
- Runs only for chunks actively loaded (zero cost for offloaded regions)
- Expired containers vanish on next chunk load

---

## Open questions

1. **Capacity cap** - strict realism (no infinite hoarding) acceptable, or do we need a magic buffer?
2. **Insulation tuning** - how cold before bad gear kills? Challenge, not frustration.
3. **Repair kit scarcity** - ubiquitous (cheap/often) or rare (preserve gear)?
4. **Condition display** - exact percentage (immersion-breaking) or vague tier badge (more DayZ)?
5. **Backpack-dropped ownership** - dropped pack contents stay in pack; does it count as "yours" for decay timer?
6. **Raw-item burial** - just bury a rifle directly, or require a container? Lean REQUIRE (gameplay cost for the container).
7. **Character silhouette with pack** - visible backpack shape = tactical, but rig/animation overhead.
8. **Inventory search/inspect by others** - can a captor see inside your pockets without you opening them? If yes, hidden pockets matter; if no, they don't.

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Clothing model | Phase 12 entity system + entity-attached items |
| Storage containers | Phase 12 + block inventory hooks |
| Buried stashes | [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) moldable terrain |
| Base storage | Future `PLAN-Base-Raiding.md` |
| Vehicle storage | Future `PLAN-Vehicles.md` |
| Inventory UI | Blazor UI + WebGPU overlay system |
| Persistence | Phase 8 OPFS region files |
| Decay | Phase 16 chunk streaming service |

---

## Next actions

1. Review with Data - identify library surface for entity-attached inventory
2. Prototype slot-grid UI in a test scene before committing to DayZ-style packing
3. Lock in layered-clothing slot taxonomy (the 11-slot table above)
4. Define condition-tier system as shared lib (applies to clothing AND structures AND tools)
5. Build buried-stash container as the first storage type (fastest tie-in with moldable terrain)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
