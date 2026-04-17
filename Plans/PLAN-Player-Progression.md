# Player Progression - Brainstorm and Plan

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

Progression in Lost Spawns is **survival competency made visible**. Learn by doing. Equip perks for tactical identity. Pick what you practice, become good at it. Don't grind XP on rats to unlock the next firefight - fight, heal, build, scavenge, and watch your character quietly master what you've actually been doing.

**Design goals:**

1. **Learn by doing.** Skills improve through use, not from a grind treadmill. Stitch wounds = medic skill. Swing an axe = melee skill. Craft weapons = gunsmith skill.
2. **Tactical identity via perks.** Fallout 76-style equipped-perk slots force players to commit to a playstyle loadout for each session. Swap between raids, not mid-fight.
3. **No hard classes.** A character is shaped by what you do, not what you pick at creation. A surgeon-engineer-scavenger is a valid character.
4. **Progression enables, never gates content.** Everything is reachable at level 1 with skill, courage, and luck. Perks and skills make it reliable, not possible.
5. **Deaths sting but don't reset.** Skills degrade slightly on death, perks unequip. Grinding back is short; the punishment is narrative (you were killed) not mechanical (start over).

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Entity system** (VoxelEngine Phase 12) - player entity owns skills + perks
- **Recipe registry** ([PLAN-Crafting.md](PLAN-Crafting.md)) - skill gates unlock recipes
- **Persistence** (Phase 8 OPFS) - skills + perks saved per character
- **UI system** - perk card view, skill sheet, level-up notifications

---

## Skill system

### [COMMIT] Specialty skills (learn-by-doing)

- **Weaponsmith** - craft/repair firearms, mod installation
- **Tailor** - craft/repair clothing, ghillie suits
- **Medic** - bandages, surgery, pharma synthesis
- **Cook** - food prep, preservation, water purification
- **Chemist** - chemistry bench crafts (explosives, cures, dyes, fuel)
- **Engineer** - electronics, scope repair, generator maintenance
- **Marksman** - long-range accuracy, scope steadiness, reduced sway
- **Melee** - close-combat damage + speed with melee weapons
- **Fitness** - stamina regen, carry weight, sprint endurance
- **Scavenger** - spot loot from distance, find rare items in containers
- **Survivalist** - faster primitive crafts, better foraging yield, environmental resistance

### [COMMIT] Skill levels 1-10

- Each skill starts at 1
- Successful action grants XP in relevant skill
- Leveling gets slower at higher tiers (steep curve past 7)
- Level 10 is master-tier, very rare, visible via title / badge
- No XP cap past 10 - encourages mastery even without visible level change

### [LIKELY] Skill XP sources

- **Successful actions** - bandaging a wound = medic XP, successful repair = repair-skill XP
- **Critical moments** - first kill with a weapon type = bonus XP, surviving fatal-looking situation = fitness XP
- **Teaching** - demonstrating a recipe/action to another player grants both parties XP (social hook)
- **Reading books** - skill books found as loot grant one-time bump (F76 parallel)

### [UNDECIDED] Skill decay

- Unused skills slowly drop (F76 hunger-system parallel)
- Pros: encourages generalist behavior, realistic rust
- Cons: punishes casual players, nagging maintenance
- Lean REJECT for v1.0 unless it emerges as a pacing tool

### [LIKELY] Death penalty on skills

- Death drops each skill by a small amount (1-5% of current)
- Top-tier skills drop less (master doesn't forget basics)
- Brief "rebuild" period after respawn
- Narrative punishment (you were killed) not mechanical restart

### [REJECT] Hard class selection at character creation

- No "pick warrior / rogue / mage" model
- Character is defined by what you do, not what you picked when you first loaded in

---

## Perk cards (Fallout 76-inspired)

### [COMMIT] Equipped-perk slot system

- Player has ~10 perk slots (scales with Level, from 3 at L1 to 10 at L20+)
- Each equipped perk grants a specific effect while slotted
- Unequipped perks sit in collection, inactive
- Swap perks at safehouse/base OR quick-equip via UI (loadout)

### [COMMIT] Perk card categories (4 streams)

Simpler than F76's 7 SPECIAL stats. Each category has tiered cards.

- **Combat** - weapon handling, recoil reduction, reload speed, damage with specific weapon classes
- **Crafting** - crafting speed, quality boost, recipe bonuses, station efficiency
- **Survival** - environmental resistance, stamina, carry weight, hunger/thirst resistance
- **Social** - trade pricing, reputation decay, teaching XP share, faction bonuses

### [LIKELY] Perk acquisition

- Earn perk card choices on level-up (pick 1 of 3 offered)
- Some perks taught by NPCs (trade goods for perk, like recipe schematics)
- Rare perks drop from cryptid-style bosses (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))
- Perks can be traded between players (card packs as barter goods, cross-ref [PLAN-Economy.md](PLAN-Economy.md))

### [LIKELY] Perk swap between raids

- Player changes loadout before deploying
- Example loadouts:
  - **Stealth infil** - crouch speed, noise reduction, hidden-pocket bonus
  - **Heavy assault** - armor max, recoil control, grenade radius
  - **Field medic** - heal speed, pharma crafting, team-bandage bonus
  - **Quartermaster** - carry weight, scavenge rate, trade pricing
- Swap costs nothing, but you're committed until next safehouse

### [UNDECIDED] Perk rank-up

- Some perk cards stack (2× card = stronger effect)
- Adds collection meta, could clutter
- Lean simple: each card either equipped or not, no stacking

### [REJECT] Mutually-exclusive perk trees

- No "you chose stealth, can't also be heavy assault" tree gating
- All perks accessible, slot count is the only constraint

---

## Progression interactions with other plans

### Crafting unlock gates (see [PLAN-Crafting.md](PLAN-Crafting.md))

- Skill level gates recipe discovery (chemistry recipes need Chemist 5)
- Skill level gates station rank upgrades (can't upgrade bench past Rank 2 without Weaponsmith 7)
- Knowledge-scrapping at a station gives XP to that specialty

### Repair quality tied to skill (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Higher repair skill = better tier recovery + lower kit waste
- Specialty-matched skill matters: tailor for clothing, weaponsmith for guns

### Hazard resistance via skills + perks (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Survivalist skill gives cold/heat resistance
- Survival perks stack hazard tolerance
- Chemist skill determines mutation-cure crafting success

---

## Reset / respec

### [LIKELY] Rare respec tokens

- Found as rare loot (military intelligence documents, hidden safe contents)
- Consumable: grants a full skill / perk reset
- Skills restart at current-max-minus-N (not full zero)
- Encourages experimentation without permanent regret

### [REJECT] Paid / cash-shop respec

- Not the vibe. Earn it.

---

## Gameplay verbs player progression enables

- Spend a week solely sewing and patching gear, return to combat as a master tailor with a ghillie suit no one can replicate
- Teach a new player how to craft rope, both earn Survival XP from the moment
- Swap from "heavy assault" to "field medic" loadout before joining a squad as raid support
- Read a rare surgery book found in a hospital, bump Medic skill to unlock splint-resets
- Die to a cryptid boss, lose 3% across all skills, spend the evening rebuilding via base maintenance chores
- Equip a "Quartermaster" perk loadout and make your first visible profit at the player marketplace
- Grind Weaponsmith past level 8 to unlock the masterwork rifle recipe at your Rank 3 gunsmith bench
- Trade a rare Combat perk card you won't use for three Survival cards that a squadmate needs
- Reach Melee 10 by hand-clearing a dozen infected with just a machete, earn the "Butcher" visible badge

---

## Open questions

1. **Level cap** - uncapped (F76-style) or fixed (say L50)?
2. **XP multipliers** - faction bonuses, teaching bonuses, first-kill bonuses - how many modifiers before math is opaque?
3. **Skill visibility to others** - can other players see your levels via inspect, or only inferred from gear/behavior?
4. **Perk slot growth curve** - linear (1 slot per level) or front-loaded?
5. **Specialty vs generalist balance** - how steep is specialist payoff vs jack-of-all-trades?
6. **Perk card trading permissions** - always allowed, or locked until faction-trust threshold?
7. **Multi-character** - one character per save slot or alts?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Skill framework | Entity + persistence system |
| Perk registry | New shared library (similar to recipe registry) |
| Level-up UI | Blazor UI + WebGPU overlay |
| Skill XP events | Instrumentation hooks across all game actions |
| Perk effect system | Stat/effect pipeline (new) |

---

## Next actions

1. Define skill XP event registry (which actions grant XP to which skills)
2. Lock perk card data format (JSON-driven, hot-reloadable)
3. Prototype one specialty skill end-to-end (Weaponsmith) as proof of concept
4. Design UI mockup for perk equip / skill sheet
5. Decide skill-decay question before implementation locks in

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
