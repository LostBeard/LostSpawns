# Combat - Brainstorm and Plan

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

Combat is **gunfeel first**. Every round has weight. Every miss is wind. Every hit matters because bodies have locations, not HP bars.

DayZ rewards the prepared: zeroed scope, read wind, steady breath, trigger squeeze. Tarkov rewards the realistic: armor plates absorb rounds, arms bleed when hit, a leg shot crumples you. Lost Spawns pulls from both and layers **suppression** and **stealth** as first-class mechanics.

**Design goals:**

1. **No bullet sponges.** A 7.62 to the head ends a fight. Armor is real. Positioning matters more than HP.
2. **Ballistics simulated.** Drop, wind, travel time, caliber energy. Zeroing matters. Long shots take skill.
3. **Suppression is a mechanic.** Near-miss rounds shake vision, force cover, delay aim. Cover-and-pin tactics work.
4. **Stealth is viable.** Silent weapons + noise discipline + camo + takedowns = full stealth playthrough.
5. **Weapons break and jam.** Condition matters. Maintenance matters. Panic reloads happen.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Entity system** (VoxelEngine Phase 12) - hit detection, hit locations, damage model
- **Ballistics simulation** - projectile path, gravity, wind, travel time (new system)
- **Audio engine** - gunshot range, suppressor attenuation (cross-ref [PLAN-Audio-Design.md](PLAN-Audio-Design.md))
- **Animation system** - recoil, reload, ADS, melee swings
- **UI** - HUD with ammo count, hit markers, suppression effects
- **Medical system** - bullet wounds, bleeding, hit-location effects (cross-ref [PLAN-Medical.md](PLAN-Medical.md))

---

## Ballistics

### [COMMIT] Real projectile physics

- Every bullet travels at caliber muzzle velocity
- Affected by gravity (drop) and wind
- Travel time matters at long range (moving targets need lead)
- No hitscan except at point-blank

### [COMMIT] Caliber system

- Distinct cartridges: 9mm, .45 ACP, 5.56, 7.62x39, 7.62x51 (NATO), .308, .338 LAPUA, 12 gauge, .50 BMG
- Each caliber: muzzle velocity, drop curve, penetration, energy-on-impact
- Ammo types within caliber: FMJ, hollow point, AP (armor piercing), tracer, subsonic

### [LIKELY] Zeroing

- Adjust scope elevation for range
- Default zeroes 100m/200m/300m etc, player can set custom
- Requires Marksman skill to zero accurately (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

### [LIKELY] Wind

- Wind direction + speed reported on HUD (via compass + flag visuals)
- Affects bullet drift over distance
- Marksman perk reduces uncertainty in reading wind

### [UNDECIDED] Coriolis / spin drift

- Ultra-long range artifacts
- Fun for sim enthusiasts, clutter for most players
- Lean [DEFER] - only relevant at 800m+ shots, rare in Lost Spawns scale

---

## Hit locations and damage

### [COMMIT] Per-body-part damage

- Head: one-shot-kill potential with rifle caliber (unless helmet)
- Neck: lethal bleed, high damage
- Chest: vital organs, damage scaled by armor plate (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))
- Abdomen: bleeding + pain, not immediate kill
- Arms: drop weapon risk, aim shake
- Legs: cannot sprint, reduced speed, fall down at zero leg HP

### [LIKELY] Armor-vs-caliber interaction

- Tier 3 plate blocks 5.56 FMJ, reduces 7.62x51 to blunt damage
- Tier 4 plate blocks most calibers except AP / .50
- Unarmored shot through soft tissue leaves full wound
- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) body armor

### [LIKELY] Penetration through materials

- Cover material matters: drywall (most calibers penetrate), car door (5.56 penetrates, stops at plate), concrete (stops all but AP)
- Encourages hard cover vs soft cover tactics

### [LIKELY] Wound effects (cross-ref [PLAN-Medical.md](PLAN-Medical.md))

- Bleeding severity by caliber + location
- Broken bones from high-caliber limb hits
- Pain reduces accuracy + stamina

### [REJECT] Global HP bar

- No unified HP. Bodies have parts. Parts have states.

---

## Weapon feel

### [COMMIT] Recoil patterns

- Per-weapon recoil signature (vertical climb + horizontal drift)
- Learnable - skilled players control recoil via mouse compensation
- Marksman skill reduces magnitude (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md))
- Auto-fire accumulates recoil, first-round is most accurate

### [LIKELY] ADS vs hip fire

- Aim Down Sights: accurate, slower to move, tunnel vision
- Hip fire: fast, spread wide, CQB only
- Switch smoothly, each has valid role

### [LIKELY] Weight and handling

- Heavy weapons raise slower, tire arm faster (stamina drain)
- Light weapons snap fast, kick harder relative to mass
- Bipod deployable on prone for heavy weapons (near-zero recoil)

### [LIKELY] Breathing and steadiness

- Hold breath for steady scope (consumes stamina)
- Cardio affects hold duration (Fitness skill)
- Movement + prone + crouch modify stability

### [LIKELY] Weapon handling animation

- Reload animations full length (not instant)
- Chamber check, magazine check (verify round loaded)
- Weapon inspect animation (look at it, immersion + quick ammo check)

---

## Attachments

### [COMMIT] Modular attachments

- Cross-ref [PLAN-Crafting.md](PLAN-Crafting.md) mod install system
- **Optics**: iron, red dot, holographic, 4x scope, 10x scope, thermal, night vision
- **Suppressor**: reduces muzzle flash + sound, slight velocity penalty
- **Barrel**: short (CQB), standard, extended (long-range)
- **Grip / Foregrip**: reduces horizontal recoil or vertical
- **Stock**: folding, standard, extended - affects handling + recoil
- **Flashlight / Laser**: target illumination + marking
- **Bipod**: prone stability

### [LIKELY] Ammunition type swap

- Single mag can be loaded with mixed types (FMJ + tracer + tracer every N rounds)
- Encourages spotter/marksman coordination (tracer = last-round notification)

### [LIKELY] Attachment slots per weapon

- Not every weapon accepts every attachment
- Custom rails, proprietary mounts = realism + scarcity
- Crafting skill + rank enables rare mounts

---

## Suppression mechanics

### [COMMIT] Suppression effect

- Rounds passing near head (within ~2m) cause suppression
- Effects: vision shake, breath hitch, ADS pause, increased aim sway
- Doesn't damage but forces the target to pause
- Duration ~0.5-1.5 sec per burst

### [LIKELY] Sustained suppression

- Continuous fire = longer suppression, deeper effect
- Pinned down target cannot effectively return fire
- Cover saves lives under suppression

### [LIKELY] Suppression resistance

- Fitness + Marksman skills reduce suppression magnitude
- Nerve-of-steel perk (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)) - near-immunity at high rank

### [UNDECIDED] Suppression from explosions

- Grenade blasts apply heavy suppression + temporary deaf/shake
- Lean [LIKELY] - great for breach-and-clear tactics

---

## Melee

### [COMMIT] Melee weapon tiers

- **Improvised**: pipe, bat, crowbar, branch (low damage, low condition)
- **Crafted**: machete, axe, knife (moderate damage, crafted at station)
- **Specialty**: fire axe, katana, shiv, fire-tempered spear
- **Cryptid drops**: cryptid-themed melee (Doctor's scalpel, Warden's shiv) - unique effects

### [LIKELY] Directional attacks

- Light attack (fast, low damage)
- Heavy attack (slow, high damage, stun on connect)
- Block (consume stamina, reduce damage)
- Parry (timed block, stagger attacker)

### [LIKELY] Stamina system

- Every swing consumes stamina
- Out of stamina = slow swings, no blocks, can't sprint
- Fitness skill extends stamina pool

### [LIKELY] Reach

- Spear beats machete beats knife in reach
- Engagement dance - stay out of reach while exploiting yours

### [LIKELY] Stealth melee kills

- Approach from behind silently (cross-ref [PLAN-Audio-Design.md](PLAN-Audio-Design.md))
- Execute instant-kill animation
- Drag body to concealment

---

## Stealth

### [COMMIT] Noise profile

- Movement noise scales with: surface (cross-ref audio), speed, gear weight, stance
- Sprinting on gravel = loud. Crouch walking on grass = near-silent.
- Infected + enemies have hearing profile - loud steps detect, quiet steps miss

### [COMMIT] Visibility profile

- Light level (day/night), cover, camo clothing (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md)), range
- Full ghillie at long range in bush = near-invisible
- Muzzle flash + flashlight = beacon

### [LIKELY] Suppressor effect

- Reduces gunshot audible range (but not to zero)
- Reduces muzzle flash brightness
- Slight velocity penalty = shorter effective range
- Wears faster (more condition drain) than unsuppressed

### [LIKELY] Subsonic ammo

- Travels below speed of sound, eliminates sonic crack
- Suppressor + subsonic = fully quiet from distance
- Short effective range, reduced penetration

### [LIKELY] Stealth takedowns

- Melee: approach from behind, execute, silent
- Garrote (specialized): rarer, slower, fully silent
- Pistol w/ suppressor to base of skull: quick execution

### [LIKELY] Corpse handling

- Drag bodies to concealment
- Corpses attract scavengers + infected over time (cross-ref [PLAN-Infected-AI.md](PLAN-Infected-AI.md))

---

## Throwables and demolitions

### [COMMIT] Throwable catalog

- **Frag grenade** - lethal radius + shrapnel
- **Smoke grenade** - visual concealment + signal color
- **Flashbang** - temporary blind + deaf
- **Molotov** - fire zone, area denial + burn damage
- **Tear gas** - forces mask, disorients
- **Throwing knife** - silent single-target (rare)

### [LIKELY] Demolitions

- **C4** - detonator + charge, breach walls, vehicle kill
- **Shaped charge** - focused blast, breach armored doors/walls
- **Satchel charge** - large area demolition
- **RPG / rocket launcher** - vehicle kill, building damage
- **Breach charge** (door-sized) - fast breach, minimal collateral

### [LIKELY] Throw arc preview

- Hold key shows projected trajectory (short visible arc, last portion faded)
- Cooking grenades (hold after pin pull) - risk/reward timing

---

## Weapon condition and failure

### [COMMIT] Weapon wear

- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) condition tiers
- Pristine → Worn → Damaged → Badly damaged → Ruined
- Each round fired degrades condition (slow)
- Mud, water, grit accelerate wear

### [LIKELY] Jams

- At Damaged+ condition, random jam chance per shot
- Clear jam: pull bolt, release mag, cycle (full animation)
- Ignorable at Pristine/Worn, significant at Badly Damaged

### [LIKELY] Catastrophic failure

- At Ruined, firing risks breaking the weapon permanently
- Component failure: bolt breaks, stock cracks, barrel bulges
- Reputation: "never carry a ruined rifle into a firefight"

### [LIKELY] Field strip + clean

- Clean weapons at workbench (fast) or in field (slow) to restore condition
- Cleaning kit consumable (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))
- Weaponsmith skill enables higher condition restoration

---

## Combat UI and feedback

### [COMMIT] Minimal HUD

- Ammo count on current mag + spare mags
- Weapon condition indicator (subtle)
- Stamina + breath hold indicator
- Health via character status icons (not a bar - per-part indicators)

### [LIKELY] Hit markers

- Subtle hit feedback (audio tick + faint visual) when you land a shot
- No enemy HP bars, no damage numbers
- Ambiguity preserves tension

### [LIKELY] Suppression visuals

- Screen edge darkening + slight blur during suppression
- Audio duck (everything muffles briefly)

### [LIKELY] Wound indicators

- Red outline on wounded body part (visible on character model peek)
- Blood trail in environment behind wounded player
- Bleeding sound cue

---

## Combat interactions with other plans

### Medical (see [PLAN-Medical.md](PLAN-Medical.md))

- Wounds have real medical effects
- Bleeding, fractures, shock
- Revival, stabilization

### Clothing + Armor (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Body armor plates absorb rounds
- Helmets save head shots (sometimes)
- Camo clothing affects visibility

### Audio design (see [PLAN-Audio-Design.md](PLAN-Audio-Design.md))

- Gunshot range, suppressor attenuation, echo in buildings
- Near-miss crackle (supersonic)

### Infected AI (see [PLAN-Infected-AI.md](PLAN-Infected-AI.md))

- Gunshots aggregate infected
- Suppression pull for silent play

### Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Marksman, Melee, Fitness skills
- Combat perk cards (recoil, reload, crit)

### Vehicles (see [PLAN-Vehicles.md](PLAN-Vehicles.md))

- Mounted turrets, drive-by shooting, RPG anti-vehicle

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Raid physics via demolitions
- Auto-turrets share ballistics with player weapons

---

## Gameplay verbs combat enables

- Dial scope to 400m, hold for one mil of wind, drop a bandit off a rooftop with a single 7.62 round
- Sprint-slide into cover under automatic fire, let the suppression pass, pop out and three-round-burst
- Stack outside a door with a squad, breach charge, flashbang, clear the room with hip-fired shotguns
- Ghillie up in a treeline, wait 40 minutes without moving, kill a Howler cryptid with one suppressed .338 LAPUA round
- Cook a frag 2.5 seconds, throw it around a corner, detonate mid-air for no-cover kills
- Pin an enemy squad behind a wall with sustained fire, flank them while your teammate keeps them honest
- Carry a suppressed subsonic .22 pistol as your silent-takedown tool, never fire it in groups
- Find your favorite AKM is at Damaged condition, field-strip it in a safe room before your next raid
- Realize the tank infected takes AP rounds, swap your mag mid-fight, dump it into the head
- Parry a cryptid's heavy swing, stagger it, follow with a spear thrust to the throat
- Use a thermal scope to find a stealth-suited rival in fog, a thing no other optic would catch
- Drop a smoke grenade to cover a squadmate's medevac drag out of the killzone

---

## Open questions

1. **Hitscan vs projectile inside CQB** - full simulated at all ranges? (Performance tradeoff.)
2. **Headshot immunity ceiling** - does highest armor tier ever block a .50 cal? (Lean: no.)
3. **Recoil control skill cap** - how much can Marksman reduce recoil before feels unearned?
4. **Ammo availability balance** - common calibers cheap, rare calibers scarce - how scarce?
5. **Melee vs gun balance** - stealth melee should be viable; open-field melee probably not vs guns?
6. **Grenade counter-play** - should there be grenade catch + throw-back like classic FPS?
7. **Combat tutorial** - how do we teach complex ballistics without a 2-hour onboarding?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Ballistics | Projectile simulation + wind + gravity |
| Hit locations | Entity hitboxes + per-part state |
| Armor interaction | Clothing plate system + penetration table |
| Suppression | Near-miss detection + vision/aim effects |
| Attachments | Mod system (cross-ref Crafting) |
| Melee | Animation rig + stamina + directional input |
| Stealth | Audio propagation + visibility math |
| Throwables | Projectile physics + area effect |
| Weapon condition | Item state schema + wear simulation |

---

## Next actions

1. Pick one weapon for end-to-end proof (AKM - common, iconic, modular)
2. Ballistics prototype (projectile + drop + wind + travel time visual debug)
3. Hit-location + wound-effect integration with medical plan
4. Suppression test harness (near-miss detection + visual effect)
5. Recoil control spike (recoil curve, player input compensation, feel iteration)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
