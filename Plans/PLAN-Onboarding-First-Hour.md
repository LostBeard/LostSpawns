# Lost Spawns: Onboarding - The First Hour

## Status Legend
- **[COMMIT]** settled design decisions
- **[LIKELY]** strong preference, expect to commit
- **[UNDECIDED]** open
- **[DEFER]** post-1.0
- **[REJECT]** explicitly not doing

---

## Premise

Every new player's first hour decides whether they have a second hour. Survival games are famously unforgiving to newcomers: DayZ throws you naked on a beach with zero context and it is a miracle anyone ever finds their second hour. Minecraft solved this with a tutorial tutorial-lite world + clear goals. Lost Spawns sits in the DayZ register but we cannot afford to lose players to first-hour confusion.

The Lore plan gives us the gift of a *narrative cold start*: the cryo-shelter wakeup. A player waking up from cryo has permission to know nothing, to ask obvious questions, to be taught by the world. This is the best onboarding device any survival game has ever had. Use it.

This plan defines what happens from "click New Game" to "you are free in the open world, understand the basics, have survived one night, have made one choice." That is the first hour.

---

## Design Principles

### 1. Diegetic Tutorial

**[COMMIT]** No "Press E to interact" bubbles. No "welcome to the tutorial" dialog. No modal pop-ups with text instructions. All teaching happens through environment, found items, audio logs, and in-world NPCs.

**[COMMIT]** The cryo shelter itself is a tutorial zone. Objects in it are designed to teach interaction verbs (open, pick up, combine, read, eat, drink). Players who explore the shelter learn the controls.

**[COMMIT]** Control prompts are context-sensitive and minimal. The first time you approach an interactable object, a small prompt fades in briefly. Every subsequent time, no prompt (unless player toggles always-on prompts in Accessibility).

### 2. Player Can Fail Slowly

**[COMMIT]** The first hour is forgiving. Hunger, thirst, temperature degrade slowly. The player can make mistakes and recover. The tutorial zone has safe supplies if they get desperate.

**[COMMIT]** Death in the first hour is rare-but-possible. If a new player does something reckless (run into a cryptid territory), they can die. But the world is not actively hunting them during onboarding.

### 3. Present Decisions, Not Instructions

**[COMMIT]** At each step, the player has real choices with real consequences. Not "do the thing the tutorial wants." Choices like: leave by the main entrance (safer, longer) or the service tunnel (faster, stranger noises). Take the pistol or the bat. Talk to the survivor you find, or pass them by.

**[COMMIT]** Every choice is valid. No "wrong answer" dialog box. The world responds naturally.

### 4. One Hour Is Enough To See The Pillars

**[COMMIT]** In the first hour, a player will:
1. Wake up (lore)
2. Explore a confined space (tutorial verbs)
3. Scavenge and equip (loot loop)
4. Leave shelter (transition to open world)
5. Encounter wildlife (non-hostile + hostile)
6. Encounter a survivor (social pillar)
7. Build a fire (crafting + survival)
8. Sleep through a night (time + weather + stealth)
9. Wake up in the world for real (closing of tutorial)

Not everything. The first hour does not contain a base, a vehicle, a cryptid Tier 2+, or PvP. Those come later. The first hour contains *the taste*.

---

## Act 1: The Shelter (Minutes 0-15)

### Opening Sequence

**[COMMIT]** New Game cold-boots into a black screen + audio:
- Muffled hiss of decompression
- Distant alarm, faint and slow
- Synthetic voice (Aether Group emergency AI) speaking through static: "Revival cycle complete. Subject: [name prompt]. Please remain calm. All personnel have... been evacuated... Please..."
- Voice cuts out. Silence.

**[COMMIT]** Vision fades in to the inside of a cryo pod. Player is lying back. Low blue light. Hands visible. A name-input prompt appears diegetically (on a screen inside the pod) - keyboard or hand-pointer input. Player types or selects a name.

**[LIKELY]** For VR: the name is spoken aloud or selected from a suggested list. Typing in VR is awkward.

**[LIKELY]** Character creation: minimal. Skin tone, hair, voice preset, optional pronouns selection for NPC dialog. Body presets to fit clothing. No deep customization - this is a survival game about what you do, not what you look like. Detailed cosmetics can be earned or found in-world.

### Pod Exit

**[COMMIT]** The pod hisses and opens. Player sits up. First interaction:
- Step out of pod (or cross a small threshold in VR)
- Floor is cold metal
- Room is a cryo bay with ~8 other pods, all dark or damaged

**[COMMIT]** Immediately ambient audio: water dripping, emergency lights buzzing, distant wind somewhere in the facility. Visual: emergency red lighting pulses. Dust. Dead plants in planter boxes. Frost-burn patterns on walls.

**[LIKELY]** Other pods contain frozen remains. One pod has died open - pod is empty, suggests an escape. Another has skeletonized remains inside - pod malfunctioned. This establishes: cryo is not safe, you are lucky.

### Interaction Tutorial

**[COMMIT]** The first object: a personal locker by the pod labeled with the player's chosen name. Opening it is the first interaction prompt.

**[COMMIT]** Inside: basic clothes, a personal datapad, a family photo, a wedding ring or locket. Personal items that ground the player as a person.

**[LIKELY]** Datapad plays a brief pre-Cascade message from a loved one - "be careful, I'll see you when you wake up, love you." Humanizes the situation without overselling. The player is *someone*, not a blank avatar.

**[COMMIT]** Clothes prompt: equip via inventory drag or the clothing menu. Tutorial-appropriate first equipment interaction.

**[COMMIT]** Leaving the bay triggers the facility map opening. A fold-out paper map of the cryo shelter is on a table by the exit. Grab, unfold, read. Shows the layout ahead.

### Room-by-Room Tutorial

**[COMMIT]** The shelter is 4-6 rooms that each teach one core verb:

**Room: Medical Bay**
- Teaches: bandages, syringes, drinking water
- Find: medical kit with bandages, bottle of water, empty canister
- Environmental: a dead staff member in a chair, a note about the first Cascade day

**Room: Mess Hall**
- Teaches: eating, combining (can + can opener)
- Find: canned food, a few rotten items (contrast - not everything is safe to eat), a stove you can use
- Environmental: chairs knocked over, signs of a rush to leave

**Room: Armory (Emergency Locker)**
- Teaches: weapon equip, melee vs. firearm choice
- Find: a baseball bat, a rusted pistol, a few rounds of ammunition, a flashlight
- Choice moment: the player can take melee or firearm. Both work. Firearm has 4 rounds loaded.

**Room: Communications**
- Teaches: radio, reading holotapes
- Find: a radio on battery power playing a looping static-filled broadcast from a faction ("...Warden station... come in if you hear this... frequency shift...")
- Environmental: holotapes scattered - some pre-Cascade staff logs, some Aether Group internal. Players who read get more context, players who don't can still proceed.

**Room: Security / Exit**
- Teaches: doors, locks, keys
- Find: a keycard on a security guard corpse. A locked door to exit. A dimly visible corridor beyond.
- Environmental: the corridor has signs of combat - shell casings, blood streaks. The first hint of the world outside the shelter.

### Exit Choice

**[COMMIT]** Two exits from the facility:
1. **Main door:** Heavy, unlocked from inside. Leads to a front parking lot, overgrown. Safe approach.
2. **Service tunnel:** A door the security guard was running toward. Leads underground, exits at a treeline half a km from the facility. Spooky noises from the tunnel.

**[COMMIT]** Both work. The main door path introduces the surface world immediately. The service tunnel introduces "things moved underground after the Cascade" atmosphere.

**[LIKELY]** A small scripted cryptid encounter in the service tunnel if chosen - a Huskling wandering. First cryptid sighting. Non-hostile unless attacked. Player can sneak past, kill, or engage in dialog (Huskling will not respond meaningfully but the attempt works).

---

## Act 2: The Threshold (Minutes 15-35)

### First View of the World

**[COMMIT]** The moment the player exits the shelter is the *moment*. Camera control is theirs; no cinematic.

- Daylight (afternoon, mid-autumn)
- Overgrown parking lot - 30+ years of unchecked plant growth
- Distant view of the peninsula: forest, coast in the distance, a mountain on the horizon
- Weather: overcast, breezy
- Sound: birds, wind, no human signs

**[LIKELY]** A single NPC walks into view: a traveler, human, peaceful, waving. This is the first social contact.

### The Traveler

**[LIKELY]** Character: "Ben" or similar simple name. A Wayward faction scout doing circuit rounds. Knows cryo shelters sometimes revive occupants.

**[LIKELY]** Dialog tree (diegetic speech, can be voiced or read):
- "You just wake up? I reckoned someone would, eventually."
- "You're in Aether's old Hilltop Station. Long story. Long enough that I won't tell it here."
- "You'll want food. And a fire by dark. I can point you toward a camp - I'll be heading there tomorrow - but if you'd rather strike out, I won't stop you."
- Player choices:
  - Accept Ben's help - he offers a direction (~500m south to a camp with a fire ring and minimal supplies)
  - Ask about the world - he gives brief cascade summary (not the whole story, just "something bad happened, lots of people died, some didn't")
  - Ask about what's dangerous - he warns about wolves with extra spines, water that makes you sick, and "the pale ones" (Husklings), and mentions "don't go near the radio tower north of here at night"
  - Ignore him, walk past - he shrugs, says "good luck" and goes his way

**[COMMIT]** Ben is the human face of survival. He is not a quest giver. He is a person going about his day who took a moment to be helpful. The player can like him or dislike him. He has no exclamation mark.

**[LIKELY]** Ben has a reputation score visible if the player uses the inspect mechanic. Moderate positive, Wayward faction. Teaches the reputation system without explaining it.

### Wildlife Encounter

**[LIKELY]** Between Ben and the next destination: a small clearing with a deer grazing. Deer is wildlife (ambient), will flee if approached fast. Teaches: not everything is hostile.

**[LIKELY]** If player has a firearm and shoots the deer: it dies. Teaches: hunting works, meat drops. A Spine-Wolf howls in the distance as a consequence - "you made noise, something heard."

**[LIKELY]** If player approaches slowly / doesn't shoot: deer leaves. Player proceeds.

### First Crafting Moment

**[COMMIT]** Before nightfall, the player should have built a small fire. The opportunity is set up at the suggested camp location.

**[COMMIT]** Camp has: a pre-made fire pit with logs, a few dead sticks, a fire-starter (flint + steel, or a lighter from the shelter). The crafting is simple:
- Pick up sticks
- Drop in fire pit
- Use flint or lighter on pit
- Fire lights

**[COMMIT]** Fire provides: warmth (temperature doesn't drain), light (night will soon come), food-cooking surface.

**[COMMIT]** Cooking tutorial: if player has meat from the deer (or a can from the shelter), placing it near the fire cooks it. Raw meat is unsafe (food poisoning). Cooked meat is full-nutrition. Teaches: cook what you hunt.

### Night Approaches

**[COMMIT]** Time flows during the first hour - real-time accelerated game time. By minute ~35 of play, in-game time is evening.

**[COMMIT]** Sky darkens slowly. Clouds roll in. The camp is sheltered but open. Sound design: wind picks up, distant howl, a cryptid's subsonic quaver (faint, suggestive).

**[LIKELY]** Ben arrives at the camp (if the player accepted his direction). He has extra sticks, a blanket. Shares dinner if the player shared theirs.

**[LIKELY]** If player struck out alone: they are alone by the fire. Solitary experience.

---

## Act 3: The First Night (Minutes 35-55)

### Sleeping

**[COMMIT]** Sleep is optional. The first night can be spent awake by the fire if the player chooses. But sleep advances time and provides some benefits (full stamina restore, slight health regen).

**[COMMIT]** Sleep rolls forward ~6 game-hours. Wake at dawn.

**[LIKELY]** If sleeping at an unprotected spot: chance of being approached by wildlife or cryptid. Ben's presence mitigates this.

**[LIKELY]** Player can choose to stay up - watching the fire, scanning the tree line. Rewards environmental storytelling: distant red glow (a bigger fire somewhere east), silhouettes moving through the trees, a shooting star.

### First Threat

**[LIKELY]** In the middle of the night (if player is sleeping) or while staying awake: a non-lethal encounter.
- A pair of Husklings approach the camp slowly, curious
- Fire keeps them at a distance (fire is a deterrent, see cryptid behavior)
- Player can let them approach further and engage, or they eventually lose interest and leave

**[COMMIT]** This encounter teaches: cryptids exist, fire matters, not everything attacks on sight. Morally gray first combat option, if player chooses it.

**[LIKELY]** If Ben is present: he stands watch with a rifle. If Husklings get too close, he shoots one calmly. Matter-of-fact, not heroic.

### Dawn

**[COMMIT]** Sky lightens. Fog rolls off the trees. Bird sounds resume. Fire is low.

**[COMMIT]** Ben is gone if he was there - left a small offering (jerky, a water bottle) by the fire. Note: "heading north today. camp is yours. stay sharp."

**[LIKELY]** If player slept: they wake up hungry, thirsty, cold-ish. The systems teach themselves.

---

## Act 4: Free Play Begins (Minutes 55+)

### The Transition

**[COMMIT]** After dawn, the tutorial framing fully ends. The UI prompt "tutorial complete" never appears. The world is just... the world now.

**[LIKELY]** A quest log entry appears: "Explore the peninsula." That is the only nudge. The player is free.

**[LIKELY]** The camp has a map of the local area posted on a tree. Shows: the cryo shelter, the camp, a Wayward outpost 2km south, a town 3km west, a Warden outpost 5km east. Player chooses.

### What the Player Has

**[COMMIT]** At this point, the player should have:
- Basic clothes from the shelter
- One weapon (bat or pistol with ~4 rounds)
- One or two bandages
- 1-2 food items
- A water bottle (possibly with water)
- A flashlight
- A personal photo / ring (sentimental, no stats)
- Map of the immediate area
- Optional: a direction from Ben, reputation with Wayward

**[COMMIT]** What they do NOT have: skills. Lockpicking, crafting recipes, combat proficiency - all these are the player's skill, not the character's. Same as DayZ.

### First Goal

**[COMMIT]** There is no forced first goal. The player can:
- Head to Wayward outpost (Ben's faction) - welcoming, trade, some questgivers
- Head to Warden outpost - cold reception, science-focused, high-value trades with SERAPH-3 samples
- Explore the ruined town nearby - dense loot, some cryptid risk
- Go inland to the forest - hunting, foraging, quiet
- Go to the coast - fishing, maybe a boat, views

**[COMMIT]** All five are valid first moves. None is "correct."

---

## Accessibility During Onboarding

See PLAN-Accessibility. Onboarding-specific notes:

**[COMMIT]** All text has a font-size adjust in the shelter's medical bay (first "settings terminal" - diegetic placement).

**[COMMIT]** Colorblind modes previewable on the datapad in the locker. Player can swap before exiting the shelter.

**[COMMIT]** Input remapping available from the medical bay terminal or main pause menu.

**[COMMIT]** Subtitles on by default for all spoken dialog, including Ben's. Toggleable.

**[COMMIT]** Content warning for the family-photo moment and the shelter corpses: a short in-shelter terminal message before the player exits the pod lists what kinds of imagery appear ahead, with an option to skip the cryo-bay introduction and spawn directly at the traveler encounter.

**[COMMIT]** Simplified mode available: for very new players or younger players, reduces cryptid spawn rates during first 3 hours, increases detection prompts, increases NPC dialog helpfulness. Togglable at any time.

---

## Pacing and Time Compression

**[LIKELY]** Real time to game time ratio:
- Shelter exploration: real-time (game time slows down, about 1:1 for the first 15 minutes)
- Outdoor play before night: real-time at 2:1 (30 real minutes = 1 game hour)
- Night + sleep: fast-forwarded if player sleeps
- Free play: standard 6:1 ratio (1 real hour = 6 game hours)

**[LIKELY]** The pacing is designed so the player experiences sunset and night within the first 45 minutes, and wakes to a new day within an hour. Establishes the day-night rhythm fast.

---

## What Is NOT In The First Hour

Explicitly kept out:

- Tier 2+ cryptids (Bone-Bear, Crawler, etc.)
- Hive-Queen encounters
- Shadelarks
- PvP engagements (by design; solo starting area is single-player until player chooses to travel to public zones)
- Vehicle handling
- Base building
- Raid mechanics
- Faction quests
- Trading
- Inventory pressure (first hour has generous space)

These come later. First hour has enough.

---

## Replay and Skip

**[COMMIT]** Returning players can skip the cryo-shelter intro. New Character menu has "Skip tutorial" option. Spawns directly at the traveler encounter with the equivalent starting inventory.

**[LIKELY]** Speedrun timer option for the main menu: "How fast can you do the First Hour?" Not a competitive leaderboard at launch but supports community speedrun interest.

**[LIKELY]** Variant openings for replay variety:
- Different cryo bays (different facilities - some could be research, some military, some civilian) offering slight variation in available items + lore
- Different traveler NPCs (different factions, different personalities)
- Different first-night threats (Husklings, Scavvers, Spine-Wolves distant)

**[DEFER]** Full randomized starting scenarios (post-1.0 content).

---

## Multiplayer in the First Hour

**[LIKELY]** First-hour tutorial is single-player always. The cryo shelter is instanced per-player.

**[COMMIT]** After the tutorial, player joins their chosen world. If friends invited them, they spawn near the invited friend if possible.

**[COMMIT]** If playing strictly multiplayer with a new player + veteran friend: friend can start a "guide mode" where they join a special parallel version of the tutorial as a ghost, able to talk to the new player and help navigate. NOT able to fight or interact with world.

**[UNDECIDED]** Co-op onboarding: two new players go through the tutorial together. Adds complexity but preserves the experience. Likely YES for 1.0; needs design work.

---

## Narrative Density

**[LIKELY]** The first hour has more narrative density than any other part of the game. Beyond the first hour, the player is in a sandbox and the world holds narrative that they can seek out.

**[LIKELY]** First-hour narrative hooks (not explicitly assigned but seeded):
- Hilltop Station and Aether Group (player learns the shelter origin)
- Family loved one from the pre-Cascade photo (personal stake)
- The Cascade (brief summary from Ben)
- The factions (one face from Wayward in Ben, reference to Wardens)
- Named threats (Spine-Wolves, Husklings, radio tower to avoid)
- Environmental storytelling (staff logs, evacuation signs, signs of combat)

**[LIKELY]** These are scattered - player who reads everything gets a richer experience; player who runs through still learns enough.

---

## Deliverables for 1.0

1. Cryo-shelter tutorial zone (full)
2. Pod wake-up sequence with character creation
3. Personal locker with family mementos + gear
4. 5-6 tutorial rooms teaching core verbs
5. Exit choice (main vs. service tunnel)
6. Service tunnel Huskling encounter
7. Outside-world reveal moment
8. Ben the traveler NPC with dialog tree
9. Deer ambient-wildlife encounter
10. Small camp with fire-building tutorial
11. First night with Husklings + Ben watch
12. Morning awakening + free-play transition
13. Local map with faction outpost pointers
14. Accessibility options accessible diegetically from medical bay terminal
15. Simplified mode toggle
16. Skip-tutorial option for returning players
17. Content warning for early dark imagery
18. Co-op tutorial (two new players) or ghost-guide mode for friend

---

## Open Questions

**[UNDECIDED]** Should we randomize the name on the locker based on character creation, or prescribe a placeholder that gets filled in? Prescribed is simpler; custom is personal. Leaning custom.

**[UNDECIDED]** Voice acting for Ben and the Aether AI. Recorded audio or TTS? Recorded feels better but adds production cost + localization cost. TTS sounds right for the synthetic AI but not for Ben. Maybe hybrid.

**[UNDECIDED]** How much of the Cascade story does Ben reveal? Too much = narrative dump. Too little = player disoriented. Probably keep Ben's summary to "something bad happened, you slept through it, here's the landscape now" and let players piece together the cause from artifacts later.

**[UNDECIDED]** What happens if the player dies during the first hour? Revert to pod wake? Skip to post-tutorial checkpoint? Probably: revert to the moment of exit from the shelter with retained knowledge of what went wrong.

**[UNDECIDED]** Does the first hour have any mandatory stealth section? Probably no. Mandatory stealth in tutorials is famously frustrating. Keep stealth as an available tool, not a required beat.

---

## Relationship to Other Plans

- **PLAN-Lore-History** - Aether Group, Hilltop Station, the Cascade as the narrative context
- **PLAN-Cryptid-Biology** - Husklings as the first cryptid; design appropriate to introduce
- **PLAN-Survival-Needs** - hunger/thirst/temperature degrade slowly during tutorial
- **PLAN-Combat** - first weapon choice, first potential kill
- **PLAN-Crafting** - fire-building as first craft, combining items in inventory
- **PLAN-Factions-Squads** - Ben as Wayward face, Warden/Wayward outposts teased
- **PLAN-Accessibility** - diegetic accessibility options, simplified mode, content warnings
- **PLAN-VR-Controls** - VR-specific onboarding (name input, physical interactions)
- **PLAN-Day-Night-Cycle** - first sunset + first night experience
- **PLAN-Weather** - overcast-then-clearing opener, night cold
- **PLAN-P2P-Reputation-System** - Ben's reputation teaches the system
- **PLAN-Audio-Design** - the opening audio mix is the first impression
