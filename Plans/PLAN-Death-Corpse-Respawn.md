# Death, Corpse, and Respawn - Brainstorm and Plan

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

**Death means something.** Not permadeath-by-default - Lost Spawns isn't a roguelike. But real stakes. Your gear stays on your corpse. Someone can loot it. Getting back to your body is a **mini-story**: fight off the pack that killed you, or watch a rival strip you clean before you arrive.

DayZ's gear-on-corpse is one of the scariest systems in games. Tarkov makes every raid a death-risk that wipes gear. Minecraft's death drop is the common pattern. Lost Spawns lands between: **corpse persists, gear stays, timer runs, recovery is a race**.

Permadeath is an **opt-in mode** for hardcore players. The character gets deleted. Their name stays on a memorial at a trader town.

**Design goals:**

1. **Gear drops on your corpse.** Full inventory stays. No magic respawn-with-everything.
2. **Corpse recovery is a race.** Timer before corpse despawns. Rivals may beat you there.
3. **Respawn at last safe bed.** Base bed > trader town bed > random coast (DayZ classic fallback).
4. **Skill loss hurts but doesn't reset.** Small XP drop, perks unequip. You rebuild, you don't restart.
5. **Permadeath mode is optional** - and memorialized. Your name goes on a statue.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Entity system** (VoxelEngine Phase 12) - corpse entities, gear state on body
- **Persistence** (Phase 8 OPFS) - corpse location, gear contents, timer state
- **Base bed system** (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md)) - respawn anchors
- **Player progression** (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)) - XP loss + perk unequip on death
- **Medical system** (cross-ref [PLAN-Medical.md](PLAN-Medical.md)) - revival window before true death

---

## Death state machine

### [COMMIT] Player state transitions

- **Alive** - normal
- **Downed** - 0 HP but still savable (cross-ref [PLAN-Medical.md](PLAN-Medical.md) revive window, 60-90s)
- **Dead** - true death, corpse entity spawned, player goes to respawn screen
- **Respawning** - brief camera transition, then back to world at respawn point

### [COMMIT] Death causes

- **Combat** - bullets, melee, cryptid attacks
- **Environmental** - falls, drowning, fire, lightning
- **Hazards** - rad sickness, chem exposure, infection (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- **Starvation / thirst** - long-term neglect (cross-ref [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md))
- **Disease** - untreated infection, sepsis (cross-ref [PLAN-Medical.md](PLAN-Medical.md))
- **Suicide** - option in pause menu (preserves agency at late-stage wounds)

### [LIKELY] Death cause affects corpse state

- Combat: riddled with bullet holes (visual + medical salvage - blood bag type known)
- Fire: burned corpse, gear partial loss (melted plastics, ruined fabrics)
- Explosion: dismembered (partial gear recovery, some items destroyed)
- Starvation/thirst: intact, gear preserved

---

## Corpse mechanics

### [COMMIT] Corpse entity

- Spawned at death location
- Contains: all inventory, worn clothing, equipped weapons, backpack contents
- Visible to any player (no tag, no waypoint unless player marked it)
- Physically interactable (loot UI like a container)

### [COMMIT] Corpse despawn timer

- Default 30-60 minutes (server config)
- After timer: corpse and contents permanently removed
- Forces time pressure for recovery

### [LIKELY] Corpse protection window

- First 5 minutes: only owner can loot (prevent instant kill-and-strip)
- After grace: open to all
- Trade-off: must reach own corpse fast

### [LIKELY] Infected eat corpses

- Cross-ref [PLAN-Infected-AI.md](PLAN-Infected-AI.md)
- Corpses attract infected over time
- If infected reach corpse, gear scatters + partial destruction
- Disincentivizes leaving corpse in hostile zone

### [LIKELY] Weather + time damage

- Rain degrades fabric/cardboard items on exposed corpse
- Extreme heat ruins food/chem items
- Sealed gear (vault, backpack, hazmat suit) protected

### [UNDECIDED] Body bags

- Craft item to wrap corpse, pack into container, carry home
- Allows burial + recovery from hazard zones
- Lean [LIKELY] - cool logistics element

---

## Respawn rules

### [COMMIT] Respawn priority

1. **Last safe base bed** (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md))
2. **Safe-zone respawn point** (trader town bed you paid for / rented)
3. **Random coast / edge spawn** (DayZ classic fallback if no bed anchor exists)

### [LIKELY] Bed anchoring

- Sleep in a bed to set it as respawn anchor
- Only one active anchor at a time (swap by sleeping elsewhere)
- Base bed preferred, needs to be safe (claimed, not under siege)

### [LIKELY] Respawn gear

- Starter kit on respawn (bandage, knife, canteen, bare clothing)
- Same as fresh-spawn gear (keeps progression honest)
- No pay-to-win revive-with-gear

### [LIKELY] Spawn invulnerability

- 5-10 seconds of invulnerability + invisibility after respawn
- Prevents spawn camping at known beds
- Clears when player moves or shoots

### [UNDECIDED] Squad respawn

- Respawn near squadmate optional (cross-ref [PLAN-Factions-Squads.md](PLAN-Factions-Squads.md))
- Lean: yes with cooldown (5-10 min) to prevent zerging a position
- Risk: squadmate dies, you can't respawn at them

### [REJECT] Respawn at death location

- Undermines stakes of death
- No "click respawn button" at corpse

---

## Death penalties

### [COMMIT] XP loss

- Small skill XP drop across all specialty skills (1-5% of current)
- Top-tier skills drop less (master doesn't forget basics)
- Cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md) skill system

### [COMMIT] Perk unequip

- All equipped perk cards reset to unequipped
- Must re-equip at next safehouse
- Cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)

### [LIKELY] Stat reset

- Hunger, thirst reset to moderate (not full) - respawning feeds you a little, not a feast
- Hygiene reset (shower scrubs the post-death)
- Warmth neutral

### [LIKELY] Repeated-death penalty stacking

- Die three times in an hour = increased penalties (more XP loss, longer respawn)
- Discourages suicide-rush tactics
- Decays to baseline over time

### [UNDECIDED] Gear drop vs gear partial-save

- F76 pattern: drop "junk" only, keep gear
- Tarkov pattern: lose everything
- Lean Tarkov-style (full drop) for survival stakes
- Softened by fast recovery + corpse timer

### [REJECT] Full character wipe on normal death

- Normal mode = recoverable death
- Permadeath is separate opt-in

---

## Permadeath mode

### [LIKELY] Opt-in hardcore character

- Character creation flag (irreversible)
- Visible badge/tag to other players (both as warning + respect)
- Different save slot, separate from standard characters

### [LIKELY] Permadeath consequences

- First death = character deleted
- Gear drops on corpse as usual
- Name + backstory added to memorial at trader towns
- Optional: written "last words" set at character creation, appear on memorial

### [LIKELY] Permadeath rewards

- Unique cosmetic reward at kill thresholds (badge on clothing, unique cryptid trophy)
- Leaderboard entry (longest-surviving permadeath character per server)
- Earn rare trader stock unlocks (legacy character bonus)

### [UNDECIDED] Permadeath-only zones

- Certain high-risk zones flagged "permadeath vibe" where normal characters face special rules
- Lean [DEFER] - permadeath is character-wide opt-in

### [REJECT] Forced permadeath for all

- Would kill accessibility + casual play
- Opt-in preserves choice

---

## Corpse recovery gameplay

### [COMMIT] Solo recovery

- Respawn, equip starter gear, retrieve vehicle/supplies, race to corpse
- Terrain knowledge + speed + preparation matter

### [LIKELY] Squad recovery

- Squadmates rush to body, defend/loot, carry gear home
- Can drag corpse to safe zone (cross-ref [PLAN-Medical.md](PLAN-Medical.md) drag mechanic)

### [LIKELY] Body bag recovery

- Pack corpse into body bag, carry to base
- Retrieve gear safely in controlled environment
- Emotional weight: funeral for permadeath characters

### [LIKELY] Kill-cam notification

- Killer gets notified their victim has a corpse at coords (optional)
- Encourages clean-up / loot-camping
- Can be disabled server-side for harder play

### [UNDECIDED] Corpse lock-box option

- Rare item: attach to corpse to prevent looting until unlocked
- Needs pickup-by-owner OR time to decay
- Lean [LIKELY] as late-game insurance

---

## Ghost / spectate mode

### [LIKELY] Post-death spectator

- While in respawn screen, spectate killer or nearby players briefly
- 30-second cap to prevent wall-hack info leak
- Cosmetic: cinematic angle of your own corpse

### [UNDECIDED] Recent-death replay

- Short replay of last 10 seconds before death
- Useful for learning + highlighting
- Lean [LIKELY] with opt-out for hardcore

### [REJECT] Live spectate as ghost

- Cannot follow living players post-death (meta info leak)
- Only respawn or go to menu

---

## Bounties and death-related economy

### [LIKELY] Infamy bounty

- Cross-ref [PLAN-Economy.md](PLAN-Economy.md) infamy system
- High-infamy player kill = bonus loot drop (bounty pouch)
- NPC bounty hunters actively pursue infamous players
- Players can collect bounties by turning in infamous-kill proof

### [LIKELY] Kill receipt system

- Killing another player yields a "kill receipt" - usable as proof at infamy-collector vendors
- Cross-references with bounty list
- Economic incentive for PvP against targeted infamous players

### [LIKELY] Loot-drop cosmetic

- Rare cryptid-drop corpse decoration (player chooses from permadeath cosmetics)
- Visible to lootee (respect / trophy)

---

## Kill-feed and notifications

### [LIKELY] Global kill-feed off by default

- No "PLAYER A killed PLAYER B" spam
- Information earns itself - radios, word of mouth, bodies you find

### [LIKELY] Squad kill-feed optional

- Squad sees its own kill/death events
- Helps coordination + debrief

### [LIKELY] Personal death screen

- "Killed by [name] with [weapon] at [coords]"
- Direction to corpse
- Timer until despawn

---

## Death interactions with other plans

### Medical (see [PLAN-Medical.md](PLAN-Medical.md))

- Downed state revive window (CPR, defib)
- Fatal wounds vs recoverable trauma
- Blood bag + IV systems

### Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Skill XP loss on death
- Perk cards unequip
- Respec tokens independent of death

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Bed respawn anchors
- Safe-zone bed rental at trader towns

### Economy (see [PLAN-Economy.md](PLAN-Economy.md))

- Infamy bounties
- Kill receipts
- Permadeath cosmetic trade

### Infected AI (see [PLAN-Infected-AI.md](PLAN-Infected-AI.md))

- Infected eat corpses over time
- Bite-wound-kill enables infection death type

### Factions / squads (see [PLAN-Factions-Squads.md](PLAN-Factions-Squads.md))

- Squad respawn-at-squadmate mechanic
- Squad kill-feed

### Environment hazards (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Hazard-zone death types
- Body bag recovery from contaminated zones

### Dynamic events (see [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))

- Event-specific risk (tower assaults, horde sieges)

### Audio (see [PLAN-Audio-Design.md](PLAN-Audio-Design.md))

- Death sound cues (ambient shift)
- Last gasp sound broadcast at corpse for nearby players

---

## Gameplay verbs death + corpse enable

- Die to a rival squad at a contested crash site, respawn at your base, drive back in your pickup, loot the survivors' corpses on your return
- Drag your squadmate's body in a body bag back to base, hold a funeral at the fire, bury gear in a stash beneath the ground (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))
- Watch the corpse despawn timer tick down as you fight infected away from your body, make it in the last 30 seconds
- Roll permadeath character, log 40 hours, die to The Doctor cryptid ambush, see your name etched on the memorial the next session
- Sleep at a trader-town inn bed for a session, swap your respawn anchor from base bed to the safe zone
- Rig a corpse lockbox on your body before a risky raid, buy insurance against the strip-and-leave scenario
- Follow a kill-cam angle of your own death, realize you were flanked from the cellar you didn't check
- Respawn-rush a contested location three times in an hour, eat the stacking penalty, capture the objective on attempt four
- Collect a kill-receipt from an infamous bandit kill, turn it in at the faction vendor for a premium payout
- Run squad-respawn on a squadmate in cover, leapfrog forward through a hot zone
- Pack a dead NPC caravan driver into a body bag, return to their refugee camp, earn rep for dignified return

---

## Open questions

1. **Corpse timer duration** - 30 min standard, server adjustable? Too short = lost gear, too long = corpse litter.
2. **Starter gear balance** - generous or bare? Lean bare (DayZ tradition) but with knife.
3. **Kill-receipt verification** - forge-proof? Cryptographic token via SpawnDev.Crypto?
4. **Permadeath badge visibility** - how far + always visible? Lean: identifiable in safe zones only.
5. **Body bag weight** - carrying full corpse is heavy - movement penalty balance?
6. **Head-shot instant vs downed** - does every damage type go through downed state, or some skip?
7. **Offline-kill fairness** - what if I'm killed while logged out (afk in base)? Lean: reduced damage offline (cross-ref Base Building offline cap).

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Corpse entity | Entity system + container interface |
| Persistence | OPFS region file + timer state |
| Respawn anchor | Base bed + safe-zone bed + fallback spawn |
| Downed state | Medical system revive window |
| Body bag | Craftable item + corpse attach logic |
| Infected corpse eating | AI corpse-interact behavior |
| Permadeath | Separate save slot + memorial NPC |
| Kill receipt | Cryptographic proof + vendor redemption |
| Kill-feed config | Server settings + UI opt-in |

---

## Next actions

1. Define corpse entity schema (inventory snapshot, cause-of-death tag, timer)
2. Prototype corpse drop + timer + loot round-trip (die → corpse spawns → loot → despawn)
3. Respawn anchor priority logic (bed > safe-zone > coast fallback)
4. Death penalty integration with skills + perks
5. Permadeath character flag + memorial NPC placement
6. Body bag crafting + corpse recovery flow

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
