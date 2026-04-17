# Medical - Brainstorm and Plan

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

**A wound is not a number. It's a problem to solve.** Bullet in the gut bleeds. Femur snaps and you cannot sprint. Untreated cuts fester. Shock kills faster than the bullet that caused it. A squad medic isn't a "healer class" - they are a field engineer running triage under fire.

DayZ gets close. Tarkov goes deeper. Project Zomboid adds stages. Lost Spawns builds a system where **medicine is craft + intuition + inventory management under stress**.

**Design goals:**

1. **Wounds are structured state, not damage numbers.** Each one has type, location, severity, stage.
2. **Triage is a verb.** Stop bleeds first, set bones second, pain meds last. Order matters.
3. **The wrong drug hurts you.** Morphine in shock is good, morphine in overdose is fatal.
4. **Skill shapes outcomes.** A low-Medic field dressing works but leaves scars. A master Medic can reset shattered legs.
5. **Recovery takes time.** Not instant heal-from-full. Bleeding stops in seconds, broken leg heals in days. Plan around injury.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Combat system** (cross-ref [PLAN-Combat.md](PLAN-Combat.md)) - hit locations + wound generation
- **Clothing + armor** (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md)) - armor damage attenuation
- **Entity system** (VoxelEngine Phase 12) - per-part body state
- **Crafting** (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md)) - medical item recipes via Chemist + Medic skills
- **Base building** (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md)) - medical room, surgery station, decontam shower

---

## Injury types

### [COMMIT] Bleeding wounds

- Bullet wounds, shrapnel, blade cuts, animal bites
- Severity: minor / moderate / severe / arterial
- Arterial: lethal in seconds without tourniquet
- Visual: blood pool under player, red trail while moving
- Audio: wet flow sound, labored breath

### [COMMIT] Fractures

- **Hairline** - stable, pain, slight movement penalty
- **Simple fracture** - limb usable with splint, heavily slowed
- **Compound fracture** - bone through skin, bleeding + pain, requires surgery
- **Shattered** - field-irreparable without surgery, cannot bear weight
- Locations: arm, leg (left/right each)
- Falls, explosions, high-caliber hits, cryptid heavy strikes

### [LIKELY] Bruises and contusions

- Blunt impact damage
- Pain + reduced ability
- Heal naturally over game-hours without treatment
- Can mask worse injuries (check carefully)

### [LIKELY] Burns

- First / second / third degree
- Fire, Molotov, cryptid acid (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- First: pain only. Second: pain + slow healing + infection risk. Third: permanent damage + long recovery.
- Cold burn from lethal cold similar mechanics

### [LIKELY] Concussion

- Head blunt impact
- Symptoms: blurred vision, audio dulling, minor aim shake
- Severe: nausea + temp unconsciousness
- Rest + painkillers

### [LIKELY] Dislocations

- Joint forced out of socket (falls, grapples)
- Cannot use limb until reset
- Self-reset: possible with pain + skill
- Medic-reset: faster, less pain

### [UNDECIDED] Lacerations vs punctures

- Differentiate knife cut from spike puncture? (Both bleed, but infection + bleeding profiles differ.)
- Sim depth vs clarity tradeoff - lean collapse into "cut wound" with severity tiers

---

## Bleeding mechanics

### [COMMIT] Bleed rate and cumulative

- Each bleeding wound has rate (blood loss per second)
- Cumulative: three minor wounds = severe total
- HP isn't the meter - **blood volume** is (shared total)

### [COMMIT] Stopping the bleed

- **Direct pressure** (manual, slow, must stay in place)
- **Bandage** (all severity - quick bandages mediocre, proper ones better)
- **Pressure bandage** (heavy severity)
- **Tourniquet** (arterial - stops bleed but risks limb necrosis over time)
- **QuikClot / hemostatic** (powerful single-use, may need medic skill)

### [LIKELY] Bandage tier system

- Rags (makeshift, low quality, reopen chance)
- Cloth bandage (standard)
- Sterile field dressing (high quality, low reopen)
- Hemostatic gauze (critical bleeds)
- Cross-ref [PLAN-Crafting.md](PLAN-Crafting.md) Medic skill crafts higher tiers

### [LIKELY] Reopen risk

- Poorly dressed wound reopens during exertion
- Sprint + combat raises risk
- Skill drops reopen chance sharply

### [LIKELY] Blood transfusion (late game)

- IV kit + blood bag (type-matched) to restore blood volume directly
- Wrong blood type = fatal reaction
- Blood types tracked per character
- Universal donor O- rare + valuable

---

## Fracture treatment

### [COMMIT] Splint (field treatment)

- Craftable: sticks + cloth/rope
- Tier 1: basic splint - stabilizes, 50% mobility restored
- Tier 2: proper splint (kit) - 75% mobility restored
- Reduces pain, stops "cannot walk" state

### [LIKELY] Cast (base treatment)

- Requires plaster + wraps at medical station
- Solid recovery curve, requires weeks of game time
- Cannot be worn under armor (trade-off)

### [LIKELY] Surgery

- Compound + shattered fractures require surgical reset
- Surgery station at base + surgical kit + Medic skill
- Full recovery if skilled; permanent limp if botched
- Unconscious during (consume time)

---

## Infection

### [COMMIT] Infection stages

- **Contamination** - wound untreated, dirt present (initial)
- **Mild infection** - fever, slow HP drain, morale penalty
- **Severe infection** - high fever, significant drain, delirium
- **Sepsis** - systemic, rapid drain, terminal without IV antibiotics

### [LIKELY] Infection sources

- Raw wounds (any bleeding left open long)
- Dirty water exposure
- Bite wounds (cross-ref [PLAN-Infected-AI.md](PLAN-Infected-AI.md))
- Bad food (cross-ref [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md))
- Hazard zones (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

### [COMMIT] Antibiotics chain

- **Disinfectant** (saline, iodine, alcohol) - clean wounds pre-bandage, prevent contamination
- **Oral antibiotics** - mild infection, slow to take effect
- **Injected antibiotics** - severe infection, faster
- **IV drip antibiotics** - sepsis, requires medical station + Medic skill

### [LIKELY] Disinfectant usage pattern

- Apply BEFORE bandage to skip infection roll
- Skipping disinfectant = risk even with good bandage
- Encourages medic discipline

---

## Pain system

### [COMMIT] Pain accumulation

- Each injury adds pain
- Total pain causes: aim shake, movement speed penalty, stamina drain
- High pain: reduced accuracy, impaired reaction
- Critical pain: unconscious risk

### [COMMIT] Painkiller catalog

- **Aspirin / ibuprofen** - mild pain, no side effects
- **Codeine** - moderate pain, slight drowsiness
- **Morphine** - strong pain, reduced alertness, slow reaction
- **Fentanyl patch** (rare) - powerful, dependency risk
- **Local anesthetic** - injectable, zone-specific, for surgery

### [LIKELY] Overdose risk

- Too many painkillers in short window = depressed breathing, unconscious, risk of death
- Player must track timing
- Medic skill extends safe dose window

### [LIKELY] Addiction / dependence

- Repeated opiate use = dependency
- Withdrawal: shake, morale drop, pain amplified when sober
- Detox at base over game days

---

## Shock

### [COMMIT] Shock definition

- After severe trauma (heavy blood loss, large bone break, burn area)
- Symptoms: low BP, cold/pale, confused (HUD blur)
- Unchecked = slide into death even after bleeding stops

### [LIKELY] Shock treatment

- **Lay flat, elevate legs** (positional, skill-based)
- **Keep warm** (blanket, campfire)
- **IV saline** - restores BP fast
- **Epinephrine shot** - emergency cardiac
- **Blood transfusion** (cross-ref above) - root cause fix

### [LIKELY] Shock stages

- **Compensating** - body coping, subtle signs
- **Progressive** - BP dropping, obvious symptoms
- **Decompensated** - critical, minutes to respond
- **Irreversible** - fatal without immediate intervention

---

## CPR and revival

### [COMMIT] Cardiac arrest state

- Player drops to zero HP from gunshot/blast/drowning/electrocution
- Window opens: ~60-90 seconds to revive before true death
- CPR by squadmate buys time, epinephrine restarts rhythm

### [LIKELY] Defibrillator

- Late-game gear, rare loot
- Auto-diagnoses rhythm, guides user
- One-shot revive with high success rate

### [LIKELY] Drowning / electrocution specifics

- Water: CPR focus (pump water out, rescue breath)
- Electricity: assess heart rhythm, defib if VF (visible on gear)
- Skill differentiates outcomes

### [REJECT] Instant revive syringe

- No "stim pack of life" that solo-revives full health
- Revival is a team act, slow, imperfect

---

## Diseases and illnesses

### [LIKELY] Common illnesses

- **Common cold** - morale + stamina penalty
- **Flu** - harder, multi-day duration
- **Pneumonia** - bad cough, slow aim, worsening from cold exposure
- **Gastroenteritis** - dehydration from bad food (cross-ref [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md))
- **Dysentery / cholera** - water-borne, severe

### [LIKELY] Exotic diseases (from world events)

- **Infected bite contagion** (cross-ref [PLAN-Infected-AI.md](PLAN-Infected-AI.md)) - infection risk on bite wounds
- **Mutation triggers** (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- **Cryptid-specific venoms / curses** - thematic effects per cryptid (The Howler's silence curse, etc.)

### [LIKELY] Treatment chain

- **Rest + fluids** - time-based recovery
- **OTC meds** - accelerate recovery
- **Prescription meds** - crafted by Chemist, target specific diseases
- **Vaccines** - prophylactic, rare, long-term protection

---

## Medical gear and consumables

### [COMMIT] Essential kit

- Bandages (various tiers)
- Disinfectant wipes/spray
- Painkillers (tiered)
- Antibiotics (tiered)
- Splints (craftable)
- Sutures + needle (wound close)

### [LIKELY] Field medic bag

- Dedicated slot item (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))
- Large capacity for medical inventory
- Quick-access UI when worn
- Skilled slot bonus (Medic perk cards)

### [LIKELY] Surgical kit (rare)

- Scalpel, forceps, sutures, surgical needle
- Required for surgery station use
- Takes specific slot space

### [LIKELY] Vital signs monitor

- Wearable or portable
- Shows BP, pulse, O2, temp
- Great for squad medic to triage at a glance
- Tier 1 analog → Tier 3 digital wireless

### [LIKELY] Autoinjectors (pre-loaded syringes)

- **Adrenaline** - emergency boost
- **Saline** - shock treatment
- **Morphine** - pain
- **Atropine** - chemical/nerve agent counter
- Single-use, fast deploy, costly

---

## Medical stations

### [COMMIT] Base medical room

- Cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md) medical station
- Bed for recovery (faster heal tick)
- Stocked cabinets (storage)
- Cleansing area (decontam shower integration with hazards)

### [LIKELY] Surgery station

- Rank 2+ medical bench (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))
- Required for compound fractures, deep wound removal, organ repair
- Lights + sterile field - requires electricity (cross-ref Base Building power)
- Skill required (Medic 5+)

### [LIKELY] Pharmacy

- Chemistry bench configured for meds production
- Antibiotics, anti-rads, stim packs
- Chemist skill makes recipes

---

## Medic as role

### [LIKELY] Medic perk loadout (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Heal speed, bandage efficiency, tourniquet precision
- Shared medic XP with patient (teaching loop)
- "Battlefield Medic" cryptid-drop perk = drag-to-safety bonus

### [LIKELY] Squad medic gameplay

- Dedicated role: stays mid-squad, rolls forward when hit
- Drag downed allies to cover (animation + movement penalty)
- Triage prioritization: critical bleeds first

### [UNDECIDED] Medic hotkey wheel

- Quick-select common items (bandage, painkiller, stim) during stress
- Polished UX detail, not scope-blocking

---

## Medical interactions with other plans

### Combat (see [PLAN-Combat.md](PLAN-Combat.md))

- Hit location + caliber determines wound severity
- Armor mitigates but plates can shatter under AP rounds
- Headshots lethal except with helmet + angle

### Clothing (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Armor absorbs / converts wounds (gunshot → blunt trauma from plate stop)
- Medical bag slot
- Hypothermia / heatstroke feeds into shock/pain

### Crafting (see [PLAN-Crafting.md](PLAN-Crafting.md))

- Medic + Chemist skills craft kits + pharma
- Chemistry bench for pharmaceutical recipes

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Medical room, surgery station, decontam shower
- Power for lights + vital signs monitor equipment

### Environment hazards (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Rad-away, anti-tox, antibiotics
- Mutation cure via Chemist pharmacy
- NBC gear reduces exposure before medical ever needed

### Infected AI (see [PLAN-Infected-AI.md](PLAN-Infected-AI.md))

- Bite wounds + infection chance
- Cryptid-specific injuries and venoms

### Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Medic specialty skill drives outcomes
- Medical perk cards (healing loadout)
- Teaching XP on patient heals

### Survival needs (see [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md))

- Food poisoning triggers dysentery
- Malnutrition slows wound recovery
- Rest in safe bed accelerates all healing

---

## Gameplay verbs medical enables

- Drag a downed squadmate out of line-of-fire, apply tourniquet to the femoral bleed, stop the arterial flow just before shock hits
- Roll a Medic 8 master surgery on a compound femur fracture, full recovery - or roll a Medic 3 botched attempt and watch your patient limp forever
- Push through a Hungry + broken-arm + infection debuff stack to reach your base medical room, collapse on the cot, sleep it all off
- Triage three squadmates after a horde siege (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md)): bleeds first, splints second, painkillers last
- Find a rare defibrillator at a crashed ambulance event, save a teammate's heart after electrocution in a wet basement trap
- Craft injectable antibiotics at your Chemistry bench, sell them on the bulletin board for premium ammo
- Overdose on pain meds trying to finish a mission, pass out in a firefight, wake up captured by the bandit crew who didn't kill you
- Field-disinfect a bullet hole before bandaging under suppression fire, skip the infection roll, walk away without sepsis two days later
- Master Medic level 10 unlocks field surgery without station - revive severed-hand allies with the Specialist perk
- Keep a blood bag with matching type in your backpack, transfuse a bleeding-out squadmate during a cryptid boss fight
- Apply splint to a broken leg from a fall, hobble to the nearest vehicle, drive to a safe zone instead of dying in the woods
- Spend a long night at a trader town's clinic, have a wandering NPC medic stitch your gut wound - pay in cigarettes

---

## Open questions

1. **Blood typing granularity** - full ABO+Rh or simplified? Lean full for gameplay depth (finding your type matters).
2. **Permanent scars** - do botched treatments leave permanent stat penalties? Harsh but memorable.
3. **Suicide mechanic** - if character is dying slowly, can player trigger end? Lean yes (preserves agency).
4. **AI medic NPCs** - village healers that treat for pay, or player-only role? Lean both.
5. **Quick-heal items** - should any single item restore fast (adrenaline shot full revive)? Lean no, keep process-heavy.
6. **Friendly fire medical** - can you "medical" a patient with wrong drug to kill them? Dark but emergent.
7. **Childbirth / pregnancy** - out of scope, mentioning only to explicitly mark as [REJECT] for v1.0.

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Wound state | Entity per-part state + schema |
| Bleeding sim | Blood volume tracker + tick |
| Fracture system | Skeletal schema + splint/cast items |
| Infection | Time-based state progression + antibiotics |
| Pain + painkillers | Pain accumulator + effects pipeline |
| Shock | Vital signs sim + thresholds |
| CPR + revival | Downed state + teamwork interaction |
| Medical stations | Base building + crafting ranks |
| Pharmaceuticals | Chemistry skill + recipes (Crafting) |

---

## Next actions

1. Wound schema (type, location, severity, stage, bleed rate, infection risk)
2. Prototype bleeding + bandage loop (wound → bleed tick → bandage → stopped)
3. Fracture + splint chain (fracture → cannot walk → splint → 50% walk restored)
4. Infection state machine (contamination → mild → severe → sepsis → treatment paths)
5. Shock mechanic first-draft (trigger conditions, BP tracking, saline/epi response)
6. Integrate with Combat plan hit-location for real wound generation

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
