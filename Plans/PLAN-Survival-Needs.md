# Survival Needs - Brainstorm and Plan

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

Your body is a **system that wants to fail**. Hunger. Thirst. Sleep. Warmth. Morale. Ignore any of them long enough, you die. Tend them all and you **outlast** every predator in the world.

DayZ makes hunger and thirst survival pillars. Project Zomboid adds morale and sleep. The Long Dark makes cold an opponent. Lost Spawns pulls from all three: the loop of **scavenge, cook, drink, rest, repeat** is the unglamorous engine beneath the firefights and raids.

**Design goals:**

1. **Needs are always quietly ticking.** Not alarms every 3 minutes - gentle drift that punishes neglect over hours.
2. **Cooking is a skill chain.** Raw → cooked → preserved. Each step a craft, each gate a depth.
3. **Morale is mechanical.** Not a meter of happiness for vibe - a real stat affecting aim, XP gain, fatigue.
4. **Routines form naturally.** Morning boil water, afternoon scavenge, evening cook, night sleep. Emergent, not forced.
5. **Gear and bases support the loop.** Fridges preserve food. Beds grant sleep. Campfires restore warmth. Survival needs drive your whole inventory.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Entity system** (VoxelEngine Phase 12) - player body state (hunger, thirst, sleep, temp, morale)
- **Persistence** (Phase 8 OPFS) - needs saved per session, decay continues during offline (within limits)
- **Clothing + temperature** (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md)) - warmth input
- **Crafting + cooking stations** (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md)) - food prep chain
- **Base + beds + refrigeration** (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md)) - support infrastructure

---

## Core needs

### [COMMIT] Hunger

- Stages: **Full → Fed → Peckish → Hungry → Starving → Critical**
- Full = max stamina, morale bonus
- Hungry = stamina regen halved
- Starving = HP drain, max HP reduced
- Critical = faint risk, major HP drain
- Rate: ~6-8 in-game hours from Full to Hungry under normal exertion

### [COMMIT] Thirst

- Stages: **Hydrated → Moist → Thirsty → Dehydrated → Critical**
- Faster tick than hunger (heat accelerates)
- Dehydrated = stamina regen blocked, accuracy shake
- Critical = HP drain, confusion (blurred UI)
- Heat zones double rate (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md) heatstroke)

### [COMMIT] Sleep / fatigue

- Awake time ticks up
- After ~18-24 hrs awake: **Tired** (stamina regen penalty, reduced XP gain)
- After ~30 hrs: **Exhausted** (aim shake, slow reaction)
- After ~40 hrs: **Sleep deprived** (hallucinations, vision blur, random small damage tick)
- Sleep in safe bed restores quickly; sleep in bedroll slowly
- Caffeine / stims delay effects temporarily (with crash penalty)

### [COMMIT] Warmth / body temperature

- Normal, Cold, Freezing, Hypothermic, Lethal Cold (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Inverse: Warm, Hot, Heatstroke, Lethal Heat
- Driven by: ambient temp, wet gear, wind, clothing insulation, activity (exertion raises temp), fire proximity
- Wet + cold = deadliest combo

### [LIKELY] Morale / sanity

- Stages: **Steady → Low → Poor → Breaking → Broken**
- Gain from: sleep in safe bed, hot meal, fire, music/radio (cross-ref [PLAN-Radio-Comms.md](PLAN-Radio-Comms.md)), squad proximity
- Lose from: witnessing horror (cryptids, gore, corpses), isolation, low-needs states, permadeath of squadmate
- Effects: low morale = aim shake, reduced XP gain, increased noise by accident (stumbles)
- Broken: temporary "panic" state - uncontrolled movements, dropped items, reduced response time

### [UNDECIDED] Oxygen

- For water (swimming/diving) + gas zones (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Not strictly "survival need" in daily sense, more situational
- Lean [LIKELY] as event-driven, not a daily meter

### [REJECT] Combined "energy" meter

- Individual meters reflect different systems realistically
- Collapsing to one hides the depth and removes player planning

---

## Food system

### [COMMIT] Food categories

- **Raw meat** - risk of parasites/illness if uncooked
- **Cooked meat** - safe, high calorie
- **Canned food** - shelf-stable, moderate calorie, common loot
- **Fresh produce** - fruits, vegetables (from gardens/scavenge) - vitamins, low calorie
- **Preserved** - smoked/dried/salted meat, jerky, pickled veg - long shelf life
- **MRE (military)** - rare, huge calorie, long shelf
- **Junk food** - chips, candy bars - low nutrition, good morale (small boost)
- **Fungi / foraged** - risk of poisonous; Survivalist skill identifies safe ones

### [LIKELY] Nutrition profile

- Calories (hunger refill), Water content (thirst refill), Nutrients (long-term health), Risk (poisoning chance)
- Varied diet gives small bonuses (healing rate, morale)
- Monotonous diet (canned beans only) causes malaise

### [LIKELY] Food spoilage

- Fresh food has timer
- Refrigeration (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md) power) pauses/slows
- Spoiled food still edible but: illness risk + morale penalty
- Canned/MRE/preserved: very long shelf life

### [LIKELY] Food safety

- Raw meat parasite chance
- Unwashed produce contamination
- Contaminated water source food (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Cooking kills parasites
- Washing reduces produce risk

### [LIKELY] Dietary mutations (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Carnivore mutation: big bonus from meat, nausea from veg
- Herbivore mutation: big bonus from veg, nausea from meat
- Drives player to specific food chain routes

---

## Water system

### [COMMIT] Water sources

- **Bottled water** - common loot, pre-packaged, safe
- **Canned drinks** - soda, juice, sports drinks - safe, morale/hydration split
- **Rain water** - from catchers (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md)) - safe if collected clean
- **Running stream** - fresh but can be contaminated downstream of events (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- **Pond / lake** - stagnant, requires purification
- **Well** - deep, generally clean, may be poisoned/contaminated near hazard zones

### [COMMIT] Water purification chain

- **Boiling** - kills biologicals, does NOT remove rads/chems
- **Tablets (iodine/chlorine)** - portable, slow, biologicals only
- **Purifier filter** - portable gear, biologicals + chems (not rads)
- **Activated charcoal filter** - chems + tastes
- **Lead filter + distillation** - rads (slow, base-bound)
- **Combination water purifier (late-game)** - all threats

### [LIKELY] Canteen + container tiers

- Small plastic bottle → metal canteen → hydration pack (backpack integrated)
- Higher tier = larger + durability
- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) slot for belt-mounted water

### [UNDECIDED] Drinking direct from stream without container

- Natural immersion - crouch at water, drink
- Contamination risk higher
- Lean [LIKELY] as animation-only interaction

---

## Cooking chain

### [COMMIT] Cooking stations (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))

- **Campfire** - portable, slow, requires fuel, smoke signal to enemies
- **Stove** - base-bound, faster, safer (no smoke)
- **Smoker** - dedicated station for preservation (smoke-cure meat)
- **Brick oven** - bakery recipes (bread, pies)
- **Industrial kitchen** - rare, large batch, special recipes

### [COMMIT] Cooking recipes

Simple taxonomy, expandable:

- **Boiled** - water + raw ingredient (simplest, low value)
- **Grilled** - open fire + raw meat (fast, good)
- **Stewed** - pot + multiple ingredients + time (best nutrition + morale)
- **Fried** - oil + pan + ingredient (luxury, good morale)
- **Baked** - oven + dough + filling (bread, pies)
- **Smoked** - smoker + meat + time (preservation focus)

### [LIKELY] Cooking skill (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md) Cook specialty)

- Higher skill = better outcomes (more calories from same ingredients, lower spoilage chance, rare recipe unlocks)
- Master chef: can craft signature dishes that give meaningful buffs

### [LIKELY] Preservation methods

- **Smoking** - extends meat shelf by weeks
- **Drying (jerky)** - extends meat shelf by months
- **Salting** - similar to drying, needs salt (resource)
- **Canning** - preserves for many months, needs jars/lids (craftable + rare parts)
- **Pickling** - preserves veg + rare flavor variety
- **Freezing** - base fridge (cross-ref power grid) - pauses spoilage entirely

### [UNDECIDED] Nutrition rot

- Old preserved food loses nutritional value slowly
- Adds realism but punishes long-term stockpiling
- Lean [DEFER] unless exploit appears (infinite food stockpile)

---

## Sleep and rest

### [COMMIT] Sleep locations

- **Bare ground** - slow recovery, low morale, exposure risk
- **Bedroll** - portable, moderate recovery
- **Sleeping bag** - portable, better recovery, warmth bonus
- **Bed at base** - fast recovery, morale bonus, safe from most threats (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md))
- **Luxury bed / hammock** - top tier, full recovery, mood buff

### [LIKELY] Sleep process

- Lie down → skip time forward (variable hours)
- Cannot skip if unsafe (enemies nearby, hazard active)
- Partial sleep (interrupted) gives partial benefit
- Shared safe-zone sleep with squadmates = morale bonus

### [LIKELY] Dream / hallucination at sleep deprived

- Sleep-deprived state: short dreamlet on next sleep (cosmetic)
- Severe deprivation: waking hallucinations (fake enemies, distorted audio) - dangerous
- Cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md) psi anomalies if implemented

### [UNDECIDED] Night-only sleep rule

- Force sleep only at night (realism)
- Or allow any time (flexibility)
- Lean: allow any time, night sleep grants extra recovery

---

## Stimulants and drugs

### [COMMIT] Stim catalog

- **Caffeine** (coffee, energy drinks) - delay sleep need, mild alertness, mild crash after
- **Amphetamine / stim pack** - heavy fatigue delay, performance boost, hard crash + fatigue debt
- **Painkiller** (aspirin, ibuprofen) - pain reduction, no stim effect
- **Morphine** - strong pain reduction, slow reaction time, risk of dependency
- **Adrenaline shot** - burst combat boost, crash
- **Nicotine** (cigarettes, patches) - mild morale + focus, health cost long-term

### [LIKELY] Dependency and tolerance

- Repeated stim use reduces effectiveness
- Sudden stop = withdrawal debuff
- Slow detox at base medical station

### [LIKELY] Crash penalties

- After stim wears off, fatigue/morale debuff
- Stacking stims = longer crash, risk of collapse

### [UNDECIDED] Recreational drinking

- Alcohol as morale + social mechanic
- Drunk state impairs aim, movement
- Lean [LIKELY] as trade good + limited morale tool

---

## Survival needs interactions with other plans

### Clothing (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Insulation drives temperature stat
- Wet gear collapses insulation
- Fuel storage (belt flasks, hydration pack)

### Crafting (see [PLAN-Crafting.md](PLAN-Crafting.md))

- Cooking stations, smoker, brick oven
- Canning jar crafting, jerky drying
- Water purifier gear crafting

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Kitchen placement, fridge (power-dependent), pantry
- Beds and sleeping quarters
- Rain catchers, well pumps

### Environment hazards (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Contaminated food/water from hazard zones
- Temperature sim feeds warmth need
- Mutation diets

### Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Cook skill unlocks recipes + preservation techniques
- Survivalist skill identifies safe forage
- Fitness skill extends activity before fatigue
- Survival perk cards amplify needs resilience

### Economy (see [PLAN-Economy.md](PLAN-Economy.md))

- Water, food, cigs are informal currencies
- Quality dishes + preserved food = premium trade goods
- Fuel for cooking stoves commodity

### Medical (see [PLAN-Medical.md](PLAN-Medical.md))

- Food poisoning stages
- Dehydration feeds into shock
- Malnutrition affects healing rate

### Audio (see [PLAN-Audio-Design.md](PLAN-Audio-Design.md))

- Cooking sounds (sizzle, bubble) immersion
- Snoring at sleep (detectable by enemies)

---

## Gameplay verbs survival needs enable

- Spend the dawn boiling a pot of pond water, filtering it through activated charcoal, filling every canteen before the day's hike
- Kill a deer with a single arrow, field-dress it, haul the meat back to camp, smoke half for jerky and grill the other half for tonight
- Run out of food mid-raid, gnaw on pre-war chocolate bar for morale, push through Hungry stage on willpower
- Spot the Marsupial mutation's hunger penalty kick in on day three, spend your scavenge time farming canned goods instead of gear
- Sleep in a hammock at a high-rep trader town, wake with a morale bonus and a discount from the innkeeper
- Pop an amphetamine stim to stay awake for an all-night defense against a horde siege (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md)), crash hard the next morning
- Master Cook level 10 unlocks a signature stew that gives your whole squad a 20-minute combat buff before a raid
- Realize the stream near a crashed transport is contaminated, backtrack three kilometers to a clean well
- Build a rain catcher on your base roof, never worry about water at home again
- Trade a jar of home-preserved pickles for a rare perk card at a refugee market
- Run morale to Broken witnessing The Doctor cryptid's lair, spend the next week at home base recovering via hot meals + squad company
- Push a Tactical Espresso espresso recipe to master, wake your squad every raid-morning with a collective alertness buff

---

## Open questions

1. **Tick rate** - how fast do needs decay? Balance between tension and tedium.
2. **Offline decay** - do needs tick while player is logged out? Caps to prevent "log in dead" traps?
3. **Morale visibility** - show exact stage or fuzzy ("feeling off")?
4. **Hallucination behavior** - scripted false enemies, or random environmental distortions?
5. **Alcohol as drug** - in scope, or cut for safety/rating concerns?
6. **Cross-player food sharing** - squad cooking pot (shared buff) as mechanic?
7. **Vitamin / nutrient tracking** - simulate individual nutrients or aggregated "balanced diet"?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Needs simulation | Entity state + tick system + persistence |
| Food/water items | Item registry + consumable effects |
| Cooking stations | Crafting stations (cross-ref Crafting) |
| Temperature | Weather + clothing insulation (cross-ref Hazards + Clothing) |
| Sleep time skip | Game time system + safety checks |
| Morale effects | Stat pipeline + UI + behavior hooks |
| Refrigeration | Power grid (cross-ref Base Building) |
| Poisoning / disease | Medical system integration |

---

## Next actions

1. Define needs tick rates + stage thresholds (needs balance pass)
2. Build consumable effect schema (ingestible → which needs → how much)
3. Prototype cooking end-to-end: raw meat → campfire → grilled → eat (one pipeline)
4. Morale stage integration with XP gain + aim shake
5. Sleep-deprivation hallucination feasibility spike (visual + audio cues)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
