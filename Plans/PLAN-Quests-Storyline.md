# Quests, Storyline, and Narrative - Brainstorm and Plan

**Status:** Living brainstorm. Decisions get locked as features mature.
**Owner:** Captain (TJ)
**Consulted:** Tuvok (research/planning)
**Last updated:** 2026-04-17

---

## Status markers

- **[COMMIT]** - committed to v1.0, active work or next in queue
- **[LIKELY]** - strong fit, assumed yes unless something knocks it out
- **[UNDECIDED]** - interesting, uncertain value/cost tradeoff, revisit before touching
- **[DEFER]** - post-v1.0 or beyond scope
- **[REJECT]** - considered and ruled out (with reason)

---

## Vision

Lost Spawns is not a quest game first. It is a survival sandbox first, and quests are the connective tissue that gives the sandbox a reason to exist. The story is **discovered**, not delivered. No glowing waypoints. No "press Q to track quest." The world tells you what happened and what is happening, and you decide whether to chase a thread or ignore it and survive your own way.

Inspired by Fallout 76 holotape archaeology + Morrowind's "ask around for directions" + DayZ's emergent player stories + STALKER's faction wars + Subnautica's data fragments.

**Design goals:**

1. **Story is environmental first, dialog second.** A burned-out classroom with kid-sized skeletons and crayon drawings of monsters is a quest hook. So is a holotape under the principal's desk.
2. **No required quest path.** A player who never accepts a single quest can still play and beat their own goals (build a base, hold territory, hit max skill in a craft).
3. **Quests respect player time.** No fail-state-on-timeout for time-shifted players. No "you missed the window" except in clearly marked time-sensitive content (e.g., dynamic events).
4. **Choice is real.** A faction quest that asks you to burn a refugee camp burns the camp. NPCs die and stay dead. The world remembers.
5. **No quest is solo-only or squad-only.** Every quest scales to party size or designs around being doable solo with effort.
6. **Player journals beat HUD trackers.** A tablet you can read at any time beats a floating waypoint indicator.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **NPC system** (VoxelEngine Phase 12 entity + dialog) - quest givers, persistent state
- **Inventory items** (Phase 7 from PLAN-Clothing-Storage.md) - holotapes, journals, keys, fetch items
- **World persistence** (Phase 8 OPFS) - quest state, NPC state, world flags
- **Radio system** (PLAN-Radio-Comms.md) - distress calls, broadcast quests
- **Faction system** (PLAN-Factions-Squads.md) - reputation, faction-gated quests
- **Audio logs / holotapes** (audio playback + interactive item)
- **Player journal UI** (PLAN-UI-HUD.md when written)
- **Terminal / computer interaction** (locked door codes, archives, lore dumps)

---

## The Big Story (main arc)

### [LIKELY] The Setting

The world died **eleven months ago** in The Cascade: a chained civilizational collapse caused by something that started as a contagion and spread into something stranger. Cities burned themselves down to contain it. Networks dropped offline region by region. The military set up quarantine zones and then went silent inside them. Some people went underground. Some changed.

You wake up in a **cryo-shelter** that was supposed to hold you for ninety days. Power failed. You've been under for eleven months. Above you, the world has rules you do not yet know.

### [UNDECIDED] Why cryo, why you?

Three possible framings, Captain picks:

- **A) Civilian shelter** - you bought a slot in a corporate shelter program ("Lazarus Plus") that promised they would wake you when the all-clear sounded. They didn't. You are an ordinary person.
- **B) Government conscription** - you were drafted into a Continuity-of-Operations program. You have a skillset (military, medical, technical). The waking you find a chain of command that is supposed to exist but doesn't.
- **C) Volunteer experiment** - you signed up for a research trial that included cryo. Side effects unknown. Some abilities, some risks (cross-ref PLAN-Player-Progression.md mutations).

**Recommendation: A**, because it lets every player be an everyman and the discovered story does the work of explaining what happened. Skill development happens in-game, not in backstory.

### [LIKELY] The Mystery (long-arc questions the player is pulled to answer)

1. **What was The Cascade?** Was it a single pathogen, a coordinated bioweapon release, an alien event, a runaway military experiment? Multiple conflicting accounts in the world; the truth is **knowable** but takes effort.
2. **Where did the military go?** Quarantine zones still have power, automated turrets, encrypted radio chatter. No people. Where did everyone go?
3. **What are the cryptids?** Mutated wildlife? Engineered weapons? Something that was never human? Each cryptid has lore that hints at one or more answers.
4. **Is there anyone in charge?** Distant radio signals from an organization called "**Aether Group**" claim to be coordinating reconstruction. Are they liberators, cult, or warlord puppeting a logo?
5. **Can it happen again?** Late-game lore reveals signs that The Cascade is not over.

### [LIKELY] Resolution model

- **No forced ending.** The world keeps existing whether or not you "solve" it.
- **Multiple discoverable truths.** Players who chase lore get answers. Players who don't, don't.
- **Late-game arc** ties together: the source of The Cascade, what Aether Group is really doing, and whether the player has the means to stop / restart / outlive it.
- Cross-ref [PLAN-Factions-Squads.md] - faction alignment determines which late-game arc is open.

### [DEFER] Post-v1.0 expansions

- "What happened in the cities" arc (urban quarantine zone reveal)
- "The first patient" arc (origin of The Cascade traced to a single location)
- Possible canonical "ending event" expansion (server-wide finale)

---

## Quest types

### [COMMIT] Main story quests

- Hand-authored, branching, choice-driven
- ~30-50 main quests across the v1.0 arc
- Mostly discovered organically (find a holotape, NPC mentions a name, radio broadcast names a coordinate)
- No quest pushed on the player at game start beyond: "you woke up. There is a door. Open it."
- Each main quest has 1-3 outcomes that change world state

### [COMMIT] Side quests

- NPC-given, often human stories (find my brother, recover my late wife's ring, hunt the bear that killed my dog)
- Less weighty than main, but reward unique items, rep, lore
- ~80-150 across v1.0
- Can be ignored without consequence

### [LIKELY] Faction quests

- Each major faction has a quest line (cross-ref [PLAN-Factions-Squads.md])
- Joining one faction permanently locks at least one rival faction's quests
- Faction reputation gates content (low rep with faction X = no quests, hostile NPCs)
- Faction quests reward unique perk cards, gear, and territory access

### [LIKELY] Radiant / repeatable quests

- Procedurally generated from templates (kill X infected at Y location, deliver Z to W NPC)
- Endless source of low-tier income and faction grind
- Generated only when the player walks within range of the giver, not pre-spawned in a quest-board
- Rewards scale to the area's threat tier

### [LIKELY] Community quests

- Server-wide or shared-world objectives (rebuild the bridge, defend the town from a horde wave, restore power to a settlement)
- Multiple players contribute (build action points, kill counts, item donations)
- Reward: shared infrastructure improvement that persists for everyone (the bridge is up for everyone now)
- Cross-ref [PLAN-Dynamic-World-Events.md]

### [LIKELY] Dynamic quests (from world events)

- A dynamic event spawns and the radio + environment broadcast it
- Engaging with it (entering the area, picking up the dropped distress beacon, speaking to a survivor NPC) creates an in-journal quest entry
- Walking away cancels it without penalty
- Cross-ref [PLAN-Dynamic-World-Events.md]

### [UNDECIDED] Lore-only quests

- Pure exploration quests (find the seven holotapes that tell story X)
- Reward: lore unlock + cosmetic + skill point
- Risk: feels like "collect-em-all" busywork unless every find tells a real story
- Lean: include for ~5-10 carefully designed sets in v1.0, no more

### [DEFER] Player-authored quests

- Player can write quest text, place a marker, and offer reward to other players via terminal
- Adds emergent content but requires moderation pipeline
- Defer to post-v1.0

### [REJECT] Daily / weekly login quests

- Hard reject. Forces players to log in to maintain reward streaks.
- Replaceable with radiant quests that exist whenever the player chooses to play.

### [REJECT] Battle pass / season quest

- Hard reject. We are not selling tiers.

---

## Quest delivery (how the player gets quests)

### [COMMIT] NPC dialog

- Talk to an NPC, they offer a quest
- Branching dialog tree (skill checks gated by player progression - cross-ref [PLAN-Player-Progression.md])
- Some NPCs have time-of-day or condition-of-NPC requirements (mortician only at funeral, mechanic only after 0700)

### [COMMIT] Holotapes / audio logs

- Found in the world. Pick up, play in any tape player or in-inventory device.
- Trigger quest entry only when the tape contains actionable info (coordinates, name, instruction)
- ~200+ holotapes across the world. Most are pure lore (not quests). The mix is intentional.

### [COMMIT] Found documents

- Notes, journals, letters, evidence boards
- Some are immediately readable in inventory; some require Engineer / Linguist skill to decipher (cross-ref [PLAN-Player-Progression.md])
- Lead to coordinates, NPCs, named locations

### [COMMIT] Radio broadcasts

- Distress calls become quests when player tunes the right frequency at the right time
- Cross-ref [PLAN-Radio-Comms.md] for radio mechanics
- Broadcast quests are time-sensitive (window measured in real hours), not permanent

### [LIKELY] Terminals / computers

- Hacked / unlocked terminals reveal mission orders, security logs, archives
- Reading a terminal entry can trigger a quest
- Some terminals are quest objectives in themselves (decrypt the terminal in the bunker)

### [LIKELY] Dead bodies

- Search a body, find a letter / mission orders / personal item
- Some bodies are placed for a story (a courier with sealed orders), others are random spawn
- Cross-ref [PLAN-Death-Corpse-Respawn.md] - persistent corpses may carry quest items

### [LIKELY] Environmental triggers

- Walking into a location triggers a quest entry ("you found the abandoned hospital - the air smells wrong")
- Activates a sub-quest to investigate
- Used sparingly to avoid auto-quest spam

### [UNDECIDED] Pet / companion finds

- Companion animal points out a body, a hidden cache, an anomaly (cross-ref Animal Wildlife plan when written)
- Lean LIKELY but defer until companion system designed

### [REJECT] Quest givers with floating !

- No floating exclamation marks. NPCs may have a unique hat / posture / tool that signals "this person has something to say" but no UI marker.

---

## Quest structure (under the hood)

### [COMMIT] Objective-driven, not waypoint-driven

- Each quest has 1-N objectives ("find the doctor's note", "speak to the mayor", "deliver the package to coordinates 37-12")
- Objectives can be completed in any order if the quest design allows
- No automatic GPS waypoint - the journal lists objective and any clue text the player has gathered

### [COMMIT] Branching outcomes

- Major quests have 2-5 different completions
- Outcomes shift world state (NPC alive / dead, faction reputation, settlement state, item availability)
- Save state of every quest decision to OPFS world state

### [LIKELY] Failure states

- Some quests can fail (NPC died, time window passed, evidence destroyed)
- Failure is rarely "you must reload" - the world adapts and the quest closes
- Fail can yield partial reward or trigger new quest (revenge quest after NPC dies)

### [LIKELY] Multi-stage quests

- Long quests stage out: investigate -> gather evidence -> confront -> resolve
- Each stage saves; player can leave for days and return
- Stages can branch (different evidence yields different confrontation paths)

### [LIKELY] Skill / perk-gated content

- Some objectives only complete via a skill check (Engineer fix the radio, Medic stabilize the patient, Linguist read the journal)
- Failed check has alternate path (use a different skill, find a workaround item, brute-force consequence)
- Cross-ref [PLAN-Player-Progression.md]

### [UNDECIDED] Time-shifted quests

- Some quests evolve over real-world time (NPC's wound progresses; faction war advances; cryptid moves)
- Risks: missing players punished
- Mitigation: time-shifted only on opt-in quests; window is **at least 7 real days** before state change; clear UI warning at quest accept

### [LIKELY] Hidden objectives

- Some quests have unstated optional objectives (find the side route, save the prisoner you weren't told about)
- Discoverable only by exploration / dialog choices
- Reward: bonus loot + secret-finder achievement / perk

---

## Quest tracking and UI

### [COMMIT] In-game journal

- Tabbed journal page in player UI (cross-ref [PLAN-UI-HUD.md] when written)
- Tabs: Main / Side / Faction / Radiant / Lore
- Each entry shows objective, hints gathered, NPC quotes, found document text
- Player can mark one quest as **active** to elevate clue text in HUD periphery

### [COMMIT] No GPS waypoint

- Hard rule. The journal tells you "the doctor's clinic is in Old Market District" - the player navigates from world signs, asking NPCs, finding the building.
- Cross-ref [PLAN-Vision.md] for navigation philosophy

### [LIKELY] Map markers (player-placed only)

- Player can drop a custom marker on the map (paper map item; pencil item to mark)
- No quest auto-marks. Player marks based on intel.
- Cross-ref [PLAN-UI-HUD.md] for map system

### [UNDECIDED] Audio cue when objective updates

- Soft chime when a quest stage completes
- Lean disable by default; toggle in settings; HUD log line is enough

### [REJECT] Quest objective auto-glow

- No glowing quest items in the world. Find them with description, not light.

---

## Lore and storytelling delivery

### [COMMIT] Environmental storytelling first

- Every interior has a story to read (skeleton positions, last journal entry, half-eaten meal, child's drawing)
- Designers author scene set-pieces with no dialog
- Cross-ref [PLAN-Base-Building.md] for player-built environmental stories on persistent servers

### [COMMIT] Holotapes are core lore vehicle

- Audio first, transcript available
- Voiced (placeholder TTS for prototype, real VO at v1.0 if budget allows; community VO submission if not)
- Found in marked locations + random spawns; rare drops from cryptids and named NPCs
- Player can listen in-inventory or via tape player base building module

### [LIKELY] Found documents

- Notes, letters, journals
- Hand-written font (unique typography per author)
- Some are partial / damaged - require Engineer or Linguist to recover full text

### [LIKELY] Terminal archives

- Long-form lore on hacked / repaired terminals
- Reading is opt-in (player chooses to spend time at a terminal)
- Terminals can run sub-systems (open a security door, vent a contaminated room)

### [LIKELY] NPC dialog lore

- Survivor NPCs have backstories (some procedurally generated, named ones hand-authored)
- Dialog trees include "ask about your past" branches the player can dig into
- Optional - never blocks main path

### [UNDECIDED] In-game books

- Pre-Cascade fiction, manuals, magazines (not lore-canon, just flavor)
- Lean: yes for ~50 items, mostly procedurally varied
- Risk: storage memory cost on persistent servers if ALL books are unique items

### [DEFER] Cinematics

- No cinematic cutscenes in v1.0. Camera-controlled scripted moments only (NPC walks past, world event flash).

---

## Player choice and consequence

### [COMMIT] World state matters

- Saved per-server / per-world (cross-ref [PLAN-P2P-Reputation-System.md])
- Killed NPCs stay dead
- Burned settlements stay burned
- Faction territory shifts when a faction quest line completes a takeover

### [COMMIT] Reputation system (cross-ref [PLAN-Factions-Squads.md])

- Quest choices shift faction rep
- Visible to player in journal
- Determines NPC hostility, trader prices, faction quest access

### [LIKELY] NPC permadeath

- Almost all NPCs can die permanently (player kill, accidental fire, faction war, cryptid attack)
- Quest givers who die close their quest line; alternative givers may exist for important arcs
- Some NPCs (lore-essential) may be flagged "essential" - rare, captain decides who

### [LIKELY] Settlement state

- Settlements can be sacked, fortified, lose population, gain population
- Quest decisions can drive settlement state (defend it, betray it, rebuild it)
- Settlement state visible on world map and in NPC dialog ("the harbor is gone now, since the bombing")

### [UNDECIDED] Player infamy

- A player who consistently kills NPCs / steals / breaks faction trust gains infamy
- Infamy triggers bounty hunters (NPC and player), denies access to certain settlements
- Lean LIKELY for v1.0 but tied to faction system depth

### [DEFER] Player legacy events

- Statue of player erected for hero deeds
- Memorial plaque for villainy
- Defer until base content is solid

---

## Multiplayer quest interaction

### [LIKELY] Squad quest sharing

- Squad members all see the same quest in their journals when one accepts
- Objectives can be split (one player goes north, another south)
- Reward distribution: each member gets full XP/rep, loot is per-pickup
- Cross-ref [PLAN-Factions-Squads.md]

### [LIKELY] Cross-server quest persistence

- Quest state belongs to the world (not the player) on persistent servers
- Joining a different server = different quest state for that world
- Player carries character (skills, gear) but quest progress is world-bound
- Cross-ref [PLAN-P2P-Reputation-System.md]

### [LIKELY] Faction-locked quests

- Joining Faction A means Faction B's quest line is closed (unless defection mechanic used)
- Cross-ref [PLAN-Factions-Squads.md]

### [UNDECIDED] PvP quest content

- Quest = "kill an enemy faction player who carries item X"
- Risks griefing if poorly designed
- Lean: yes, but require both players to opt in to PvP-flagged quests; no surprise hits

### [LIKELY] Mentor quests

- High-skill players can mark themselves as mentors
- New players can accept "shadow a mentor" quest to learn a system (cooking, base building, etc.)
- Mentor gets rep / cosmetic for completion

---

## Tutorial integration

### [COMMIT] Learn through play, not tooltips

- No "press WASD to move" pop-ups
- Cryo shelter intro contains environmental tutorial moments (a holotape labeled "If you're reading this, here is the basics")
- Survivor NPCs in early settlements offer tutorial-shaped quests (your first cooking quest, your first base placement quest)

### [LIKELY] Optional skill tutorials

- Each major skill has an opt-in tutorial quest from a master NPC
- Skipping is fine; some players prefer trial and error

### [LIKELY] Tutorial holotape "starter pack"

- Starting cryo-shelter contains 3-5 holotapes covering survival basics, navigation philosophy ("there is no GPS, ask people for directions"), and faction basics
- Reading is optional but heavily encouraged via location placement

### [REJECT] Forced tutorial gauntlet

- No 30-minute mandatory intro. Player can leave the cryo shelter and run free in 5 minutes if they want.

---

## Quest reward economy

### [COMMIT] Diverse rewards beyond XP/cash

- Unique gear (named weapons, armor with story)
- Perk cards (cross-ref [PLAN-Player-Progression.md])
- Faction reputation
- Crafting recipes / schematics
- Property / settlement access
- Cosmetic items (clothing, base decor)

### [COMMIT] No flat XP-only quests

- Every quest gives at least one of: unique item / lore / faction influence / world state change
- Pure XP grind is what radiant quests are for

### [LIKELY] Reward scaling by difficulty

- Higher-tier quests (harder enemies, longer chain, multi-stage) reward more
- Triggered by region threat level + player skill level

### [UNDECIDED] Diminishing returns on repeats

- Radiant quests scale down reward after N repeats per day to prevent grinding
- Lean: yes, but cap is "soft" (not zero)

---

## Edge cases and exceptions

### [LIKELY] Solo player path

- Every main quest must be solo-completable (with skill / preparation)
- Squad-only content is opt-in side quests, not story-critical

### [LIKELY] Pacifist path

- A player who refuses to kill humans (no NPC kills, no PvP) should still have access to ~70% of side quests and the main story
- Some quests have stealth / negotiate / sabotage alternatives to combat
- Cross-ref [PLAN-Combat.md]

### [UNDECIDED] Run-and-gun path

- A player who refuses to read holotapes / talk to NPCs should still have a path to "play and survive"
- They miss most lore but can pursue radiant quests, dynamic events, base building
- Lean: yes, supported, but they are foregoing depth

### [LIKELY] Save / load equivalent

- Persistent server worlds are the save state - no traditional save / load
- Solo / private servers can support save snapshots (player choice)
- Quest state restored from snapshot on load

---

## Anti-patterns to avoid

### [REJECT] Fetch-quest stacking

- No NPC who gives 5 fetch quests in a row with no narrative
- Each quest must have story justification

### [REJECT] Escort quest with broken AI

- If we cannot make NPC pathing reliable, no escort quests
- Broken escort is worse than no escort

### [REJECT] Quest items that clutter inventory forever

- Quest items dropped automatically on completion or labeled clearly disposable
- No vendor-trash quest items mistaken for valuable loot

### [REJECT] "Go talk to X" -> "Go talk to Y" -> "Go talk to Z" chains

- No telephone-game quests with no action
- Every quest stage must have a player-meaningful action

### [REJECT] Quest content gated behind grind

- A faction quest gated behind 1000 kills is rejected; max meaningful grind is ~5-10 sessions of natural play
- Story content is not a Skinner box

### [REJECT] Quest log overload

- Maximum ~10 active quests at any time (player picks which to track active)
- Older inactive quests auto-archive after N days of no progress
- No "you have 47 active quests" UI vomit

---

## Gameplay verbs quests enable

- Wake up in a freezing cryo-shelter, find a holotape labeled "Watch this first" under your pod, learn the world ended eleven months ago
- Walk into an abandoned schoolhouse, find children's drawings of cryptids on the chalkboard and a single skeleton in the principal's office, decide whether to read the journal she left
- Tune your handheld radio to 88.3 at midnight to hear an encrypted distress call coordinates - decrypt with Engineer skill, race three other crews to the wreck
- Help a mortician NPC bury her husband (he's already dead and no one helped her dig the grave) and gain her permanent friendship + a recipe for her late husband's stew
- Recover a corporate executive's holotape from his office in a quarantine zone, decide whether to deliver it to his widow (rep + cash) or sell it to Aether Group (cash + faction rep)
- Decline every quest a faction offers because you saw what they did to a neighboring settlement, and play the world as a freeholder
- Accept a community quest to rebuild a bridge, contribute 200 planks over a week of casual play, watch the bridge appear in your world the next time you log in
- Investigate a ruined hospital because the smell drew your dog, find evidence of cryptid experimentation, decide to publish the findings (Aether Group rep drop) or burn them (no one ever knows)
- Join a squad mate's main story quest mid-arc, see the same journal text they have, follow them into the bunker
- Find a holotape that names an NPC who's been in your settlement for weeks, realize they're not who they said they were, confront them in dialog with three response branches (kill, expose, blackmail)
- Take a faction quest to assassinate a rival faction's leader, fail because she had bodyguards, return weeks later with a sniper rifle and finish the job, watch the faction war shift in your favor on the world map
- Listen to a series of seven holotapes scattered across the wasteland that chronicle a single woman's months between The Cascade and her death, never meet her, but understand exactly what happened to her city
- Skip every NPC and every holotape, build a base, hold a territory, become known across the server as the trader at Junction Camp - your "story" is the one other players tell about you
- Encounter "The Doctor" cryptid in an abandoned hospital and find a holotape that hints she may not always have been a cryptid (cross-ref [PLAN-Dynamic-World-Events.md] cryptid lore)
- Late-game: gather enough Aether Group intel from terminals, holotapes, and NPC dialog to discover what they actually are, choose whether to expose them publicly, ally with them, or kill their on-world handler

---

## Open questions

1. **Voice acting** - real VO budget? Community-submitted? TTS placeholder for v1.0?
2. **Quest density** - target ~30-50 main / ~80-150 side / endless radiant. Is that the right curve? Too many? Too few?
3. **Mystery payoff** - how much of "what was The Cascade" is answered in v1.0 vs left open for expansions?
4. **Persistent server world state divergence** - if Server A players burn the camp and Server B players defend it, does that matter to anything beyond that server? (Lean: each server is its own canon.)
5. **NPC schedules** - do NPCs sleep, work, travel? Or are they static at their station? (Schedules add immersion but cost CPU and design time.)
6. **Quest item economy** - how do we prevent quest items from cluttering inventory across a long game?
7. **Failure cascade** - if player kills a quest-critical NPC, do all that NPC's quest lines just close, or do alternatives spawn?
8. **Tutorial quest acceptance** - opt-in (player must accept tutorial holotape) or environmental (cryo shelter tutorial fires automatically)?
9. **Cross-faction friendships** - can a high-rep player from Faction A still complete some Faction B quests via friendship with individual NPCs?
10. **Quest log persistence across character respawn** - if player character permadeaths (cross-ref [PLAN-Death-Corpse-Respawn.md]), does the new character inherit quest state for that world?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| NPC dialog | Entity + AI + dialog tree system (VoxelEngine Phase 12) |
| Quest log UI | UI/HUD system (PLAN-UI-HUD.md, not yet written) |
| Holotape playback | Audio system + interactive item interaction |
| Found documents | Inventory item system + readable item viewer |
| Terminal archives | Computer interaction system + lockpicking / hacking skill |
| Faction-gated quests | PLAN-Factions-Squads.md reputation system |
| Radio quests | PLAN-Radio-Comms.md frequency / broadcast system |
| Dynamic event quests | PLAN-Dynamic-World-Events.md trigger system |
| World state persistence | OPFS region files + per-world quest state |
| Squad quest sharing | PLAN-Factions-Squads.md squad system |
| NPC permadeath | Character entity persistence |
| Settlement state | Settlement entity + occupancy / damage tracking |
| Skill check gating | PLAN-Player-Progression.md skill system |
| Bounty hunter spawning | Faction infamy + dynamic spawn system |
| Holotape voice acting | Audio asset pipeline + VO budget decision |

---

## Next actions

1. Lock the framing (Civilian shelter / Government conscription / Volunteer experiment) - Captain decides
2. Author one main story quest end-to-end as proof of concept (Cryo Wakeup -> Find Survivors -> First Faction Contact)
3. Define quest data schema (JSON: id, stages, branches, dependencies, rewards, world flags)
4. Design holotape system: playback, transcript, in-base playback module
5. Author 5 lore-only holotapes that establish core mystery threads (Cascade, Aether, military disappearance, cryptid origins, missing answer)
6. Sketch journal UI (in PLAN-UI-HUD.md when it lands) - tabs, search, mark-as-active
7. Draft the first faction quest line outline for one faction (probably Survivors as default neutral) - shows how branching faction quests look
8. Lock the "no GPS waypoint" rule with Captain and design a navigation alternative (paper map markers, NPC directions, landmark callouts)
9. Decide on persistence model for quest state on player respawn (carry / reset / per-world)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
