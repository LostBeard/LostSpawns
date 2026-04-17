# PLAN - Animal Wildlife, Hunting & Fishing

**Status:** Design draft, 2026-04-17 (Tuvok)
**Owner:** Captain (LostBeard)
**Audience:** Gameplay designers, AI programmers, animation team, balance team

---

## Purpose

Wildlife is the peninsula's second food source (after scavenging) and its primary renewable resource. Hunting and fishing are core survival loops alongside looting, farming, and trading. This plan defines the animals that exist, how they behave, how the player interacts with them, and how the ecosystem interacts with itself.

Lost Spawns is DayZ-inspired but takes cues from Red Dead Redemption 2 (tracking, kill quality, materials), The Long Dark (wolves and bears as real threats, fishing depth), and STALKER (mutated wildlife as the tension between vanilla and cryptid).

## The Pitch

You're hungry. You have a rifle, a knife, and four rounds. Deer tracks in the mud lead north. You follow. The deer is downwind so you have a chance. If you shoot, infected a quarter-mile away hear the rifle and start walking your way. If you miss, you've wasted a round and the deer bolts and you've got no backup plan. If you hit clean in the lungs, the deer runs thirty meters and drops and bleeds and you have forty minutes before the meat starts going off in the August sun. If you hit in the gut, the deer runs into the swamp and you follow a blood trail for an hour before you find what's left.

Hunting in Lost Spawns is about **decisions that cascade**. Not minigames. Not fast-food. The reward is real and the risk is real.

---

## Design principles

### [COMMIT] Every animal is a real animal first

Before any mutation, every species in the game has a real-world behavioral model. Deer flee. Wolves pack-hunt. Bears defend cubs. Crows are smart and remember faces. We don't ship cardboard targets that walk in circles.

**Why:** Players who played RDR2, The Long Dark, Hunter's Call, or who hunt in real life will spot fake animals in five seconds. The difference between "good hunting game" and "great hunting game" is whether the designers knew about eye-lines, wind direction, and what a gut-shot deer actually does.

### [COMMIT] Hunting is loud unless you work for silence

A rifle shot is the loudest sound in the game. It attracts infected from hundreds of meters (cross-ref PLAN-Infected-AI.md sound sensitivity). Bows are near-silent. Traps are silent but require setup. Spears are melee-range. The rifle hunter gets a deer and a zombie horde; the bow hunter gets a deer and fifteen more minutes of peace.

### [COMMIT] Meat spoils, pelts cure, bones cure

No infinite-freshness inventory. Raw meat goes off in hours in summer, days in winter. Pelts cure over real-world time with work (salt, stretching, drying). Bones need cleaning and drying. This ties into [PLAN-Base-Building.md] (smokehouse, tannery, bone-crafting station) and PLAN-Survival-Needs.md (food spoilage, food poisoning).

### [COMMIT] Populations respond to hunting pressure

An area that gets hunted over-hunts. Deer become scarce. Predators move on. Eventually the area recovers, but the local player who cleared a forest learns they have to travel farther or move bases. This prevents the "one-deer-farm" that plagues survival games where spawns are static.

### [LIKELY] Taint exists on a gradient

Some wildlife populations carry low-level SERAPH-3 infection (cross-ref PLAN-Lore-History.md, Cascade 2 mutagenic tail). Visually detectable at high taint (fur patches, clouded eyes, hunched posture). Meat from tainted animals makes the player sick. This is a gameplay risk/reward: a hungry player might eat the suspicious deer.

### [COMMIT] Ecosystem, not zoo

Animals eat each other. Wolves hunt deer. Bears catch fish. Crows scavenge corpses. Infected eat what they can catch. A deer carcass the player leaves becomes a wolf meal, then a crow meal, then bones. The systems tick even when no player is watching (cross-ref PLAN-Dynamic-World-Events.md offline ecosystem simulation).

---

## Wildlife catalog

Organized by biome (cross-ref PLAN-World-Biomes-Regions.md). Not every species is in every biome. Species can overlap biomes (bears exist in boreal forest AND mountain).

### [COMMIT] Mammals - herbivores

| Species | Primary biome | Size | Food yield | Material yield | Notes |
|---|---|---|---|---|---|
| White-tailed deer | Boreal, Plains | Medium | ~40 meals | Hide, antler, bone, sinew | Core meat source. Flee behavior. Wind-aware. |
| Moose | Boreal, Swamp | Large | ~100 meals | Hide, antler (massive), bone | Rare. Aggressive in rut. Can kill a player with a charge. |
| Wild boar | Boreal, Plains, Swamp | Medium | ~35 meals | Hide, tusk, bone | Aggressive. Charges if cornered. Group-spawn. |
| Rabbit | All temperate biomes | Small | ~3 meals | Hide, bone | Abundant. Caught with snares. Low payoff, low cost. |
| Hare | Plains, Mountain | Small | ~4 meals | Hide, bone | Faster than rabbit. Snare-only viable. |
| Squirrel | Boreal, Urban | Tiny | 1 meal | Pelt (decorative), bone | Snack food. Abundant. Slingshot kill. |
| Goat (feral) | Mountain, Coastal cliffs | Medium | ~25 meals | Hide, horn, bone | Pre-Cascade escaped livestock. Hard to reach. |
| Cow (feral) | Plains, Settler's Hold periphery | Large | ~80 meals | Hide, horn, bone, dairy (if living) | Pre-Cascade escaped livestock. Domesticatable (cross-ref [UNDECIDED] section). |
| Sheep (feral) | Plains, Mountain | Medium | ~25 meals | Hide, wool, horn, bone | Pre-Cascade escaped livestock. Wool is a renewable if herded. |
| Horse (feral) | Plains | Large | N/A - not food | Hide (post-mortem only) | See [DEFER] Mounts section. Kill only as atrocity. |

### [COMMIT] Mammals - predators/omnivores

| Species | Primary biome | Size | Threat | Notes |
|---|---|---|---|---|
| Wolf | Boreal, Mountain | Medium | High (pack) | Pack hunters (3-7). Hunt deer. Will attack wounded player. |
| Black bear | Boreal, Mountain | Large | High (solo) | Omnivore. Forages berries + fish. Aggressive with cubs. |
| Brown bear | Mountain (rare) | Massive | Extreme | 1-2 spawn across map. Named, persistent. |
| Coyote | Plains, Burn Scar | Small | Low (pack) | Scavenger. Scared of humans unless starving. Packs of 2-4. |
| Red fox | All temperate | Small | Minimal | Opportunist. Raids snares. Steals small game. |
| Raccoon | Urban, Boreal | Small | Minimal | Clever. Raids camps for food. Can open containers. |
| Feral dog | Urban, Plains | Small-Med | Medium (pack) | Pre-Cascade pets gone wild. Packs of 3-8. Aggressive to humans. |
| Bobcat | Boreal, Mountain | Small | Low-Medium | Ambush predator. Quiet. Surprises unwary players. |

### [COMMIT] Birds

| Species | Primary biome | Yield | Notes |
|---|---|---|---|
| Crow | All | 1 meal + feather | Smart. Scavenges corpses. Warns other wildlife of player presence. |
| Raven | Boreal, Mountain | 1 meal + feather | Larger crow. Sometimes leads to kills (follows bears). |
| Chicken (feral) | Urban ruins, Settler's Hold | 2 meals + feather | Pre-Cascade livestock. Easy kill. Lays eggs if herded. |
| Duck | Swamp, Coastal | 3 meals + feather + down | Flies when alarmed. Shotgun/bow food. |
| Canadian goose | Coastal, Plains | 5 meals + feather + down | Noisy. Aggressive. Flocks can kill a low-HP player. |
| Wild turkey | Boreal, Plains | 8 meals + feather | Seasonally abundant. Shy. |
| Seagull | Coastal | 1 meal + feather | Numerous. Coastal nuisance. Nest-raid for eggs. |
| Pigeon | Urban | 1 meal + feather | Abundant in city centers. Easy slingshot food. |
| Hawk | Boreal, Mountain | 1 meal + feather + talon | Rare. Hunts rabbits/squirrels. Observation-only usually. |
| Owl | Boreal, Mountain | 1 meal + feather + talon | Nocturnal. Rare. Feather is high-craft material. |

### [COMMIT] Fish

Freshwater, brackish, saltwater variation by water body.

| Species | Habitat | Size | Notes |
|---|---|---|---|
| Brook trout | Mountain streams | Small | Clean water only. Quick-cook food. |
| Rainbow trout | Rivers | Small-Med | Abundant in clean rivers. |
| Lake trout | Lakes | Medium | Deeper water. Boat/dock useful. |
| Bass | Lakes, slow rivers | Medium | Predatory. Lures work. |
| Catfish | Rivers, swamp | Medium-Large | Bottom feeder. Scavenges carrion. Ugly but filling. |
| Northern pike | Lakes, slow rivers | Medium-Large | Predator. Bites player in waist-deep water. |
| Carp | Still water, swamp | Medium | Tolerant of dirty water. Edible but unpleasant. |
| Crayfish | Streams, lake shallows | Tiny | Easy catch by hand. Snack food. |
| Clam | Coastal flats | Tiny | Dig at low tide. Raw-edible. |
| Mussel | Coastal rocks, rivers | Tiny | Attached clusters. Filter feeder - taint risk in dirty water. |
| Crab | Coastal | Small | Trap-catchable. Premium food. |
| Cod | Coastal ocean | Medium | Requires boat or pier. |
| Salmon (run) | Coastal rivers, seasonal | Medium-Large | Seasonal event (cross-ref PLAN-Dynamic-World-Events.md). |
| Eel | Swamp, brackish | Small-Med | Hard to catch. High nutrition. |

### [COMMIT] Reptiles, amphibians, bugs

| Species | Biome | Use |
|---|---|---|
| Garter snake | Plains, Boreal | Bait, minor food |
| Rat snake | Urban, Swamp | Bait, minor food |
| Rattlesnake | Plains (rare) | Danger + bait + hide |
| Bullfrog | Swamp, pond | Bait + meal (legs) |
| Painted turtle | Pond, swamp | Meat + shell |
| Snapping turtle | Swamp | Meat + shell + danger (bite) |
| Earthworm | Any soil | Fishing bait |
| Grub/larva | Rotting logs | Fishing bait, survival food |
| Cricket | Any | Fishing bait |
| Honeybee (wild hive) | Boreal, Plains | Honey + wax + sting risk |

### [LIKELY] Cryptid-adjacent wildlife (tainted)

Some animals exist in "partially tainted" form - early SERAPH-3 infection, no full cryptid transformation. Visually distinct (dull fur, cloudy eyes, aggressive posture, lumps). Meat is dangerous.

- **Tainted deer** - stumbling, vacant eyes, meat causes vomiting + debuff
- **Pack-mutant wolf** - larger, louder, fights more aggressively than normal wolf
- **Ashen crow** - colorless, follows infected mobs like vultures, does not eat player corpses
- **Sodden frog** - oversized, near-swamp cryptid territory, poison bite
- **Silent deer** - fully tainted, does not run from player, catatonic, one step from cryptid

These should be distinct enough from full cryptids (cross-ref PLAN-Dynamic-World-Events.md) to read as "sick wildlife" not "boss monster." The gradient matters for world-building.

### [UNDECIDED] Pre-Cascade escaped exotics

Should the peninsula have a pre-Cascade exotic animal park or zoo that released its occupants? This would justify occasional encounters with animals that don't belong: an emu flock in the plains, escaped peacocks in an urban ruin, a rare cougar (mountain lion) in the forest.

**Recommendation: yes, in a small quantity (~4-6 species).** Adds surprise and texture. Specifically: cougar (mountain), emu (plains, rare), peacock (urban, ornamental), ostrich (plains, dangerous kick), wild pig variants (boar + domestic crosses). Captain's call.

### [DEFER] Insects-as-food at scale

Crickets, grasshoppers, mealworms - realistic post-collapse food source. Real mechanical implementation deferred to post-v1.0 unless PLAN-Survival-Needs.md demands it.

### [DEFER] Marine megafauna

Whales, dolphins, seals visible but not interactive. Deferred. If Captain wants ocean depth later, a marine plan gets its own document.

---

## Hunting mechanics

### [COMMIT] Tracking

Every animal leaves tracks on appropriate terrain. Tracks are visible to the player's eye (not an icon overlay). Track types:

- **Hoofprints** (deer, moose, boar, goat, cow, horse)
- **Pawprints** (wolf, bear, fox, coyote, dog)
- **Feet + claws** (bear differentiates front/back, deeper in wet soil)
- **Scratches on trees** (bear, cougar)
- **Scat** (species-identifiable, freshness-readable)
- **Feathers on the ground** (bird recent passage)
- **Blood trail** (wounded animal - critical to recovery)

Tracks fade over real time (rain accelerates fading). A player with a basic tracking skill can read tracks accurately. A player with advanced tracking (cross-ref PLAN-Player-Progression.md) can estimate species, direction, size, and freshness. This replaces the "tracker button" of shallow hunting games with actual skill-building.

### [COMMIT] Wind and scent

Every mammal has a scent radius upwind and a visual radius 360-degrees. Player approaches should be from downwind. Wind direction is indicated by blowing grass, leaves, cloth on player gear. No arrow icon in the HUD (cross-ref PLAN-UI-HUD.md diegetic-first).

### [COMMIT] Weapon choice consequences

| Weapon | Sound | Range | Damage | Meat quality |
|---|---|---|---|---|
| Knife (melee) | Silent | Touch | High (sneak) | Best if clean kill |
| Spear (thrown) | Near-silent | 15m | High | Best if clean kill |
| Bow (wood) | Quiet | 40m | Medium | Best if broadhead |
| Crossbow | Quiet | 60m | High | Best if bolt placement |
| Slingshot | Near-silent | 20m | Low (small game) | Best - no fragmentation |
| Trap (snare) | Silent | Set once | Fatal to small game | Depends on death cause |
| Trap (pit/deadfall) | Silent | Set once | Fatal to medium-large | Often spoils internal organs |
| .22 rifle | Loud | 80m | Low-Med | Clean if head/heart |
| Hunting rifle | Very loud | 300m | High | Clean kill common |
| Shotgun (slug) | Very loud | 50m | Very high | Clean kill |
| Shotgun (birdshot) | Very loud | 30m | Medium (spread) | Wastes a lot of meat |
| Handgun | Loud | 30m | Low-Med | Rarely clean |

Infected sound-attraction radius scales with weapon loudness (cross-ref PLAN-Infected-AI.md). Every gunshot is a tradeoff.

### [COMMIT] Shot placement

Animal anatomy is modeled for hit location effects:

- **Head/brain:** Instant kill, clean meat, pelts undamaged
- **Heart/lungs:** Fatal within 30-120 seconds, clean meat, blood trail to follow
- **Gut:** Slow bleed-out (hours), tainted meat yield, very long trail, high chance to lose the animal
- **Limb:** Non-fatal, animal flees wounded, must be tracked and finished
- **Miss:** Animal bolts, learned behavior (this area spooked)

Quality of kill determines materials recovered. A deer taken with a clean heart-shot from a bow produces intact hide + full meat yield + intact antlers. A deer blasted by a shotgun slug from 10m might produce 50% meat yield + shredded hide + usable antlers.

### [LIKELY] Animal AI behavioral states

Every animal has behavioral states: feeding, alert, fleeing, wounded, aggressive, sleeping, mating (seasonal). State transitions are driven by sound, smell, visual, threat, and other animals. A pack of wolves chasing a deer pushes that deer toward the player; the player can exploit or get caught in the crossfire.

### [LIKELY] Calls and lures

Animal calls (deer grunt, duck call, elk bugle) craftable from wood + reed. Scent lures (apple bait, salt lick, fish oil) attract specific species. Both require specific knowledge (no tutorial pop-up - learned from NPCs or found documents). Cross-ref PLAN-Crafting.md.

### [COMMIT] Baiting and trapping

- **Snares** - rabbit/hare/small game, set on trails, passive
- **Deadfall traps** - bait-triggered, mid-size game, resource-cheap but single-use
- **Leghold traps** - holds but does not kill, player must return, illegal-feeling
- **Pit traps** - dug over time, large game, work-intensive
- **Cage traps** - live capture (small game), can release for [UNDECIDED] domestication
- **Fish traps** - woven baskets in streams, passive, requires return

Traps provide passive food generation - a hunter with ten snares around their base gets a baseline protein supply without active hunting. Important for mid-game balance.

### [REJECT] Instant-kill hunting minigames

No QTEs, no "press F to slit throat" prompts on immobilized animals, no skinning minigames. Skinning/butchering is an action that takes time. Not a button-masher.

---

## Fishing mechanics

### [COMMIT] Fishing method variety

- **Hand-catch** - crayfish, clams, mussels, frogs (shallow water only)
- **Spear fishing** - requires sight + practice, fresh kill, no bait, works in clear shallows
- **Pole fishing** - rod + line + hook + bait, most versatile, works in most water
- **Net fishing (cast net)** - coastal and river, catches multiple at once, heavy labor
- **Trap fishing** - woven basket traps, passive, return after hours
- **Ice fishing** - DEFER (snow biome deferred)

### [COMMIT] Bait types matter

- **Worms** - panfish, trout, catfish
- **Minnows** - bass, pike
- **Grubs** - trout, sunfish
- **Artificial lures** (if crafted or scavenged) - bass, pike
- **Chum** (rotting meat/fish) - catfish, carp, attracts large fish
- **Shiny metal** (pre-Cascade fishing tackle) - various predators

Bait must be renewable for fishing to be a sustainable loop. Worms dug from dirt, grubs from rotting logs, minnows from small traps.

### [COMMIT] Water body quality

Water condition affects fish availability:
- **Clean mountain stream** - trout, clean fish
- **Clear river** - trout, bass
- **Muddy river** - catfish, carp
- **Lake** - bass, pike, lake trout, sunfish
- **Swamp** - catfish, carp, eel (high taint risk)
- **Brackish** - eel, some bass, crab traps work
- **Coastal saltwater** - cod, bass, mackerel, crab, lobster

Polluted water (cross-ref PLAN-Environment-Hazards.md - the Coalton Refinery) produces tainted fish. Eating is dangerous.

### [COMMIT] Fish fight mechanic

Landing a fish is not automatic. Rod tension, line strength, and player skill determine success. A player fighting a 15-pound pike on a 4-pound line will snap the line. Realistic not punishing - the rod tension UI is diegetic (rod bend visible in first-person).

### [LIKELY] Seasonal events

- **Salmon run** - coastal rivers, 2x per in-game year, massive catch opportunity, bears also show up
- **Sturgeon season** - deep river, named rare fish, trophy
- **Eel migration** - swamp/brackish, seasonal dense population

Cross-ref PLAN-Dynamic-World-Events.md. Fishing events draw player attention to specific regions at specific times.

### [REJECT] Skill-check-only fishing

No "press this button in time" minigame. Fishing is ambient, slow, and sometimes a zone where a player takes a quiet break from the apocalypse. That's a feature, not a bug.

---

## Butchering, preservation, cooking

### [COMMIT] Butchering takes real time

Skinning a rabbit: ~30 seconds. Butchering a deer: ~5-8 minutes. Butchering a moose: ~20 minutes. Sound is attracted during the butchering process (quiet but not silent). Player is vulnerable during the task. This turns every big kill into a tactical decision: butcher here (risky), or drag home (slow, limits what you take).

### [COMMIT] Yield per animal

Yield is realistic and scales with size. A deer gives:
- Raw meat (~40 units, each unit = 1 meal)
- Hide (1 large, for armor/clothing/cover)
- Antlers (1 pair, craft material + decor)
- Bones (long bones for tools, ribs for stock, skull for display)
- Sinew (bowstring material - critical)
- Organs (heart, liver - nutrition-dense food; brain - tanning agent in some recipes)

The full use of an animal is a player-choice craft loop. A lazy player butchers for meat only. A thorough player spends the extra time and gets hide + sinew + bone, which over a season means self-sufficient gear.

### [COMMIT] Preservation methods

| Method | Time | Shelf life | Equipment |
|---|---|---|---|
| Raw (cold storage) | Instant | Hours (summer), days (winter) | Cooler, cellar, snow |
| Cooked (fire) | Minutes | 1-2 days | Any fire |
| Smoked | Hours | 2-3 weeks | Smokehouse (Base-Building) |
| Salted/cured | Days | 4-6 weeks | Salt + dry place |
| Jerky | Hours-day | 1-2 months | Drying rack |
| Canned | Hour | 6-12 months | Jars + boiling |
| Frozen | Instant | Indefinite | Pre-Cascade freezer (requires working power - rare) |

Cross-ref PLAN-Base-Building.md for smokehouse/drying rack/salting station. Cross-ref PLAN-Survival-Needs.md for spoilage mechanics and food poisoning.

### [COMMIT] Cooking quality

Cooked food yields more nutrition than raw (realistic + gameplay-necessary). Overcooked food yields less (burned - don't leave the pot). Seasoning (cross-ref PLAN-Crafting.md herbs and salt) improves yield further. A gourmet player who grows herbs + cures salt gets measurably better food than a player who just chars raw meat over a fire.

### [LIKELY] Specialty foods

- **Stew** - multi-ingredient, bonus satiation, travel-friendly
- **Venison jerky** - travel food, long shelf life
- **Pemmican** - fat + meat + berries, extreme travel food, historical
- **Soup** - sick-player recovery food
- **Fish pie** - settler-trade item, high-value
- **Smoked eel** - Wayward caravan delicacy

---

## Ecosystem simulation

### [COMMIT] Population tracking per region

Each map region (cross-ref PLAN-World-Biomes-Regions.md) maintains per-species population counts. Hunting reduces the count. Recovery happens over time (births). Over-hunting triggers regional depletion, which is visible in-world (fewer tracks, no kills for multiple sessions).

### [COMMIT] Predator/prey interaction

Wolves hunt deer. Bears catch fish. Coyotes scavenge. If the deer population in a region drops, wolves move on. If wolves move on, deer recover. The sim ticks while offline; a player returning after a week finds the ecosystem changed.

### [COMMIT] Migration (seasonal)

- **Geese/ducks** migrate spring/fall (peninsula is flyway)
- **Salmon run** twice per in-game year
- **Deer rut** drives movement in autumn
- **Bears hibernate** in winter (if snow biome ships - currently DEFER)

Seasonal migration creates event hunting - a player who learns the salmon run can camp at the coastal river during the right week and catch a season's worth of meat in three days. This rewards knowledge, which is exactly the DayZ/Long Dark spirit.

### [LIKELY] Carrion chain

A dead animal attracts scavengers in a cascade:
1. Crows within minutes (alert ravens within hours)
2. Foxes, coyotes within hours
3. Wolves within a day (if any in region)
4. Bears within days (opportunistic)
5. Infected within variable time (drawn by smell + crows)

A player who leaves a carcass unbutchered is leaving a honey-trap that draws every scavenger (and every infected) in the neighborhood.

### [UNDECIDED] Invasive species

Should pre-Cascade escaped exotics (see earlier UNDECIDED section) spread and become invasive? An emu population in the plains that grows and displaces native species? Interesting systemic gameplay but a maintenance nightmare.

**Recommendation: cap exotics at static populations, no breeding.** Simpler. Captain's call.

---

## Cryptid and tainted wildlife interaction

Cross-ref PLAN-Lore-History.md and PLAN-Infected-AI.md.

### [COMMIT] Tainted meat is dangerous

Eating meat from a tainted animal causes:
- Short-term: nausea, vomiting, hydration loss
- Medium-term: minor chance of SERAPH-3 exposure (cross-ref PLAN-Medical.md)
- Long-term: if severe exposure survives, Stage-0 survivor trait (cross-ref PLAN-Player-Progression.md)

This makes wildlife taint a real gameplay system, not flavor text.

### [COMMIT] Cryptids prey on wildlife

Named cryptids (The Sodden in swamp, The Caller in forest, etc.) eat wildlife. Regions near cryptid territory have depressed wildlife populations. This is a gameplay signal - "low deer count here" means "something's hunting them already, be careful."

### [LIKELY] Cryptid-adjacent hybrids

Some animals in cryptid territory show partial transformation (giant crow, bloated frog, oversized wolf). These are between normal wildlife and named cryptids in threat level. Meat is fully tainted. Hide/bones may have unique craft uses (cross-ref PLAN-Crafting.md).

### [REJECT] Cryptid pets

You cannot tame a cryptid. No matter what the Mothfolk say. This is a firm design line; otherwise the narrative weight of cryptids collapses.

---

## Trophies, taxidermy, player stories

### [LIKELY] Trophy hunting

Named animals exist in specific regions:
- **"Old Antler"** - massive moose, boreal forest, legendary rack, 10+ years old
- **"The Grey Ghost"** - brown bear, mountain, kills any hunter who tracks it badly
- **"The Gar King"** - massive alligator gar, swamp, requires heavy tackle
- **"Shipwrecker"** - giant sturgeon, deep river

These are persistent, long-hunt targets. Killing one yields unique trophy items (mountable heads, special pelts, named materials) + reputation flag ("Hunter of the Grey Ghost" - NPCs react).

### [LIKELY] Taxidermy and display

In a player base (cross-ref PLAN-Base-Building.md), player can mount:
- **Antlers** on walls
- **Full head mounts** (deer, bear, moose) - requires taxidermy skill/equipment
- **Pelt rugs** on floors
- **Fish mounts** on boards
- **Skull collections** on shelves

Visual personalization of player space. Status symbols. Some NPCs react to elaborate trophies ("Impressive. You're the one who took that moose in the ridge? Word gets around.")

### [COMMIT] Player stories we want to enable

- The hunter who lives mostly off deer and trade, rarely enters cities
- The fisherman who runs a riverside shack and trades smoked catfish to every faction
- The trophy hunter who's after the Grey Ghost and has been for three real months
- The trap-line runner who has 15 snares circling their base and eats like a king
- The settlement butcher who processes animals for other players in exchange for labor
- The salmon-run camper who logs in only during event windows

---

## Animal companions and domestication

### [UNDECIDED] Dogs

Tamable feral dogs? Pre-Cascade pets found injured and rehabilitated? Bonded companion AI?

Pros: emotional attachment, DayZ/Fallout 76 appeal, hunting assistance, alert behavior
Cons: AI development cost, permanent-death emotional weight, pathing/stuck issues

**Recommendation: yes, limited.** A player can tame a feral dog through a long quest chain (find injured dog, heal, feed consistently over days, earn trust). The dog stays at their base or travels. Can die to infected/cryptids. Cannot be respawned. Real stakes.

### [UNDECIDED] Livestock

Cow/sheep/chicken/goat domestication for dairy, wool, eggs, meat?

**Recommendation: yes, at Settler tech level.** Settlers already do this lore-wise. Player can trade for a breeding pair, raise animals at their base. High maintenance (feed, water, shelter from predators/raiders), high payoff. Cross-ref PLAN-Base-Building.md + PLAN-Economy.md.

### [DEFER] Mounts (horses)

Horses exist on the peninsula (feral pre-Cascade) and riding them is the obvious DayZ/RDR2 fantasy. But mount AI, terrain traversal, inventory on-horse, combat from horseback, animation scope - large.

**Recommendation: DEFER to post-v1.0 expansion.** A Horse DLC is a valid future direction. Shipping horses in v1.0 risks bad first impression if the implementation is shallow.

### [DEFER] Birds of prey / falconry

Niche. Defer unless a writer wants to run with it.

---

## Dependencies and cross-references

| Plan | How this plan relates |
|---|---|
| [PLAN-Lore-History.md](PLAN-Lore-History.md) | Cryptid = human+animal splice; the animal side matters here |
| [PLAN-World-Biomes-Regions.md](PLAN-World-Biomes-Regions.md) | Biomes determine wildlife catalog |
| [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md) | Food spoilage, poisoning, hydration from cooking, nutrition from meat |
| [PLAN-Infected-AI.md](PLAN-Infected-AI.md) | Gunshot sound attraction, infected drawn to carrion |
| [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) | Cryptids prey on wildlife, salmon run/migration events |
| [PLAN-Base-Building.md](PLAN-Base-Building.md) | Smokehouse, drying rack, salting station, taxidermy, livestock pens |
| [PLAN-Crafting.md](PLAN-Crafting.md) | Hide tanning, bone tools, sinew bowstrings, herbs/seasoning |
| [PLAN-Medical.md](PLAN-Medical.md) | SERAPH-3 exposure from tainted meat, food poisoning |
| [PLAN-Player-Progression.md](PLAN-Player-Progression.md) | Tracking skill, butchering skill, fishing skill |
| [PLAN-Economy.md](PLAN-Economy.md) | Meat/hide/fish as trade goods with Settlers, Wayward |
| [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md) | Polluted water, Coalton Refinery contamination |
| [PLAN-Radio-Comms.md](PLAN-Radio-Comms.md) | Radio Greta occasionally announces salmon run, migration |

---

## Open Questions (Captain's call required)

1. **Dog companions - in for v1.0?** (recommendation: yes, limited scope)
2. **Livestock domestication scope** (recommendation: yes, tied to Settler tech)
3. **Exotic pre-Cascade escapees** (recommendation: yes, 4-6 species, static populations)
4. **Mounts (horses) - v1.0 or DLC?** (recommendation: DLC)
5. **Invasive species mechanics** (recommendation: static cap, no breeding for exotics)
6. **How much of the wildlife sim runs server-side while offline?** (recommendation: populations + migrations tick offline, named animals persist, tracks reset on player proximity)
7. **Cryptid-adjacent hybrid wildlife count** (recommendation: 5-8 named, tied to each cryptid)
8. **Trophy persistence on servers** (recommendation: one Grey Ghost per server; when killed, respawns after a real-time window)
9. **Do wildlife population counts show to the player anywhere?** (recommendation: no HUD number, but NPCs and Wayward traders may comment on local wildlife health)

---

## Writer's reference: style notes

- **Wildlife has dignity.** Animals are not monsters, not prey icons. A deer is a creature the player kills to survive; treat the moment.
- **Tainted wildlife is tragic.** A silent deer that doesn't flee is a sad thing, not a gameplay-only asset.
- **Hunters are not all cruel.** Settlers NPCs hunt to feed their people and may refuse to sell certain animal parts out of principle. Faithful NPCs hunt but ritualize the kill.
- **The Mothfolk do not hunt.** They forage only. Killing an animal is anathema to their doctrine. This matters for quest content.
- **Pre-Cascade wildlife stories exist too.** Hunters' cabins with journals. A failed hunting trip that ended in a call for help. A taxidermist's shop frozen mid-mount.

---

_End of plan. Balance team: this plan intentionally holds off on exact yield/damage numbers. Tuning happens in iteration. Design shapes the world first._
