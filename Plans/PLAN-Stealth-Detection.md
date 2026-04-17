# Lost Spawns: Stealth & Detection

## Status Legend
- **[COMMIT]** settled design decisions
- **[LIKELY]** strong preference, expect to commit
- **[UNDECIDED]** open
- **[DEFER]** post-1.0
- **[REJECT]** explicitly not doing

---

## Premise

In DayZ, the single most consequential skill is reading the world: knowing who else is out there before they know you. Stealth is not a mini-game or a class ability - it is the baseline skill that separates a player who survives an hour from a player who survives a week.

Lost Spawns inherits this. Every encounter with another player, NPC, or cryptid is an information asymmetry problem. Who sees first has initiative. Who hears first can choose to engage or retreat. Who smells first (animal AI) gets the warning that humans cannot.

This plan defines the sensory model for AI, the stealth tools available to players, and the detection signals shown in the HUD. It tightly couples with PLAN-Weather (rain masks sound), PLAN-Cryptid-Biology (per-species senses), PLAN-Combat (detection sets the initial conditions), and PLAN-Audio-Design (audio cues).

---

## Design Principles

### 1. Symmetric Sensing

**[COMMIT]** AI senses use the same rules as player-observable information. An AI that can "see" the player can also be seen by the player. An AI that "hears" something is making a sound that a player nearby could also hear.

**[COMMIT]** NPC vision cones are not arbitrary. They are defined by realistic FOV (humans ~180deg peripheral, ~30deg foveal; wolves narrower peripheral, wider forward; hawks extreme forward resolution). Cryptids inherit their base species' senses modified by mutation.

**[COMMIT]** NPC hearing is not a radius pulse. Sound propagates through the world respecting terrain, walls, rain, and wind.

### 2. No Magic Detection

**[COMMIT]** An NPC cannot detect a player they have no sensory basis for detecting. No "aggro radius" that bypasses occlusion. No omniscient AI.

**[COMMIT]** Cryptids with enhanced senses (scent for Spine-Wolves, subsonic hearing for Hive-Queens) are documented. Players can learn and counter.

**[REJECT]** "You were spotted because the game said so." If a player gets spotted, the reason must be diegetic and explainable.

### 3. Player Has Same Information Rights

**[COMMIT]** The player has a HUD. But the HUD's information comes from what the character could observe - footprints visible on the ground are highlighted subtly, loud sounds trigger a directional indicator, etc. The HUD layer amplifies perception, not fabricates it.

**[COMMIT]** Optional HUD: every detection signal can be turned off for hardcore players. In VR the defaults are lower (less clutter); in flat-screen the defaults are slightly more generous to compensate for reduced sensory presence.

### 4. Stealth Rewards, Not Punishes

**[COMMIT]** Successful stealth grants real advantages: bonus damage on surprise shots (rifle + suppressor + undetected target = one-shot potential on most enemies), access to areas full of hostiles without combat, the ability to study enemy patterns and pick your moment.

**[COMMIT]** Failed stealth has real consequences but is not game-over. Alerted enemies investigate, which is itself a tactical opportunity (lure them to a trap, circle around, retreat to break line-of-sight and re-stealth).

---

## Sensory Model

### Vision

**[COMMIT]** Every NPC has a vision cone defined by:
- **Max range** (species and light-level dependent)
- **FOV angle** (species dependent, reduced at longer ranges)
- **Acuity** (how "obvious" must the target be to register)

**[COMMIT]** Visibility of a target is computed from:
- **Light level** at target's voxel (day, dusk, night, inside shadow)
- **Contrast** between target and background (clothing vs. environment)
- **Motion** (stationary vs. walking vs. running)
- **Occlusion** (foliage, walls, geometry - partial occlusion reduces detection)
- **Distance** (inverse-square with acuity modifier)
- **Posture** (standing taller silhouette, crouch half, prone minimal)

**[COMMIT]** Target "visibility score" is compared to the NPC's vision acuity at the target's distance and position. If visibility > acuity, the NPC sees.

**[LIKELY]** Night gameplay is genuinely dark. Moonless nights: flashlight is required for navigation but draws attention. NVGs shift the balance back.

### Hearing

**[COMMIT]** Sound is emitted by the world at a known volume per event: footsteps have a base level, weapon fire has a high level, broken glass has a startle-level, etc.

**[COMMIT]** Sound attenuates:
- With distance (inverse-square in open air)
- With occlusion (walls, terrain reduce by material-specific amounts)
- With weather (rain and wind mask, thunder completely overwhelms for a moment)
- With the listener's own activity (you can't hear as well while running)

**[COMMIT]** NPC hearing is a threshold: sound level at NPC - NPC noise floor = detection strength. High enough = alerted. Moderate = investigate. Low = ignored.

**[LIKELY]** Player-sourced sounds are annotated with direction when audible. The player HUD can show a pulse on the compass indicating where a loud sound came from (toggleable).

### Scent

**[LIKELY]** Scent is a mechanic only some species use:
- Spine-Wolves: strong scent tracking
- Bone-Bears: moderate
- Domesticated dogs (if we add companion dogs): strong
- Most others: not tracked

**[LIKELY]** The player has a "scent" value based on recent activity:
- Clean-washed: minimal
- Covered in mud/blood: masks human scent
- Recently bleeding: high scent
- Using scent-masking items (animal glands, smoke): reduced

**[LIKELY]** Wind direction matters. A player downwind of a Spine-Wolf can approach undetected. Upwind: detected at much greater range. Players can observe wind direction visually (see PLAN-Weather).

### Touch / Tripwire

**[LIKELY]** Some cryptids (Glass-Spider specifically) use web-like tripwires. Passing through alerts them. Players can see webs and avoid, or cut them.

**[LIKELY]** Player-placeable tripwires: cans on strings, improvised alarms. Detect approaching entities and trigger audio.

### Subsonic / Special Senses

**[LIKELY]** Hive-Queen pulse (see PLAN-Cryptid-Biology) is a subsonic detection field. Players within the field appear to all nearby cryptids automatically. Players can:
- Avoid entering the field (environmental cues tell you)
- Shield themselves with specific materials (thick lead or concrete attenuates)
- Destroy the Hive-Queen to remove the field

**[LIKELY]** Scrap-Hawks have extreme visual acuity - effectively they see you anywhere in open terrain. Counter: stay under tree cover, inside buildings, under overhangs.

---

## Player Stealth Tools

### Posture

**[COMMIT]** Three postures: stand, crouch, prone.
- **Stand:** normal move speed, easy to see
- **Crouch:** reduced move speed, smaller silhouette, quieter footsteps
- **Prone:** very slow move speed, minimal silhouette, quietest movement

**[COMMIT]** Posture affects:
- Vision detection (height + silhouette)
- Hearing detection (footstep volume)
- Accuracy (supported vs. unsupported firing)
- Weapon presentation time (slower from prone)

### Pace

**[COMMIT]** Three paces: walk slow, walk, run, sprint.
- **Walk slow:** silent on most surfaces, very slow
- **Walk:** quiet, normal speed
- **Run:** audible, stamina-draining over time
- **Sprint:** loud, rapid stamina drain, fast

**[LIKELY]** Pace interacts with surface:
- Pavement/stone: louder
- Grass/dirt: medium
- Mud/soft earth: quiet but leaves tracks
- Gravel: very loud (distinct crunch)
- Metal/industrial: very loud
- Snow: quiet but leaves very visible tracks
- Leaves/twigs: startle-crack that alerts nearby AI

### Clothing

**[LIKELY]** Clothing affects stealth:
- **Color** vs. background - camouflage patterns are strictly better for hiding in matching biomes
- **Material** - leather and wool are quieter; rigid plastics creak; metal armor clanks
- **Weight** - heavy armor slows movement, increases noise
- **Wet** - wet clothes are heavier and squelch audibly

**[LIKELY]** Ghillie suits are tier-3 crafted items. Extreme camouflage bonus at the cost of mobility, warmth regulation, and visibility to allies (friendly-fire risk in PvP).

### Leaning and Peeking

**[COMMIT]** Q/E (or thumbstick lean in VR) leans left/right. Lets you expose only part of your body around a corner. Other entities can only see the exposed portion.

**[COMMIT]** Crouching while leaning = combined minimum silhouette.

**[LIKELY]** Leaning out of cover while prone is not a thing (physically awkward, would be a mocap mess). Use low-cover vaulting instead.

### Slow Movement for Silent Approach

**[LIKELY]** Walk-slow is enabled by a toggle or held modifier. Character takes exaggerated careful steps, maximum time per footfall, no surface noise.

**[LIKELY]** Crouch-walk and prone-crawl are already quiet. Walk-slow is for standing approach in sensitive conditions.

### Suppression Items

**[COMMIT]** Firearms can mount suppressors (rare, valuable). Suppressed shot audibility dramatically reduced (from 200m base to 40-60m). Still audible at close range.

**[LIKELY]** Subsonic ammunition further reduces audibility but cuts velocity/damage. Risk/reward.

**[LIKELY]** Crossbow and bow: nearly silent. Core stealth weapon class.

### Throw-and-Distract

**[LIKELY]** Throwing objects creates a sound at the landing point. Can be used to lure AI away from a patrol or into a trap.

- Thrown bottle: glass shatter, medium-loud
- Thrown rock: thud, quiet
- Thrown flare: bright light + noise, very loud and visible
- Thrown firecracker: loud pop

### Knockout / Takedown

**[LIKELY]** Close-range from behind: silent takedown. Melee weapon or bare-hand chokeout. Kills or incapacitates with no sound.

**[LIKELY]** Silent takedown requires:
- Target unaware of player
- Player behind target
- Player within melee range, crouched or standing
- No other AI within view of the takedown (otherwise witnesses trigger alert)

### Scent Masking

**[LIKELY]** Items that reduce player scent:
- Mud application (full-body, washes off in rain or water)
- Smoke exposure (from fire, lingers)
- Animal gland rubbing (crafted from hunted animals)

**[LIKELY]** Scent reduction affects only scent-tracking AI (Spine-Wolves, Bone-Bears, companion dogs). Does not affect vision or hearing.

### Decoys

**[LIKELY]** Crafted items:
- Dummy: straw-stuffed mannequin in player clothes. Placed in an environment, can draw AI for a few seconds before they realize it is fake.
- Radio: plays a looping audio file (cult sermon, music). Attracts AI with curiosity response.
- Flasher: periodic bright strobe. Attracts visual AI.

---

## Detection UI

### Visibility Indicator

**[LIKELY]** An eye icon on the HUD shows the player's current visibility state:
- **Closed eye (hidden):** AI within range do not currently see you
- **Half-open (visible but not detected):** AI could see you but have not focused attention
- **Open eye (actively observed):** AI is watching you, will engage if you do anything suspicious
- **Alert icon:** AI has detected and is hostile

**[LIKELY]** In VR, this indicator is a small ambient glow around peripheral vision rather than a persistent icon. Less HUD clutter.

**[LIKELY]** Toggleable off for hardcore / minimal HUD.

### Audio Indicator

**[LIKELY]** A discrete audio indicator shows the loudness of your current action relative to ambient - a small ring that grows when you are noisy. Does not predict detection; shows your signal strength.

**[LIKELY]** Directional sound indicator on the compass: brief pulses indicating detected loud sounds nearby (gunfire, footsteps, cryptid vocalizations).

### AI State Indicator

**[LIKELY]** When you observe an NPC, their state is shown above them:
- **Unaware:** no icon
- **Investigating:** ? icon
- **Alerted:** ! icon
- **Engaging:** crosshair icon

**[LIKELY]** Only shown when the player is directly looking at the NPC (or has observed them recently). No omniscient AI state display.

**[LIKELY]** Toggleable off for hardcore / minimal HUD.

### Compass + Bearing

**[COMMIT]** A diegetic compass item (held in hand) shows bearing. Not a HUD overlay by default.

**[LIKELY]** Optional HUD compass can be enabled. Shows sound pulses. Hardcore mode disables it.

### Tracks and Trails

**[LIKELY]** When you look at the ground with attention (held look or crouched + look), recent footprints become visible as a subtle glow. Colors by recency (fresh = yellow, old = faded).

**[LIKELY]** Passive observation: tracks are visible in mud and snow without any UI highlight. Pavement and dry dirt: no visible tracks to the naked eye, only to the attention overlay.

---

## AI Behavior States

### State Machine

**[LIKELY]** Each AI has a state machine:

1. **Idle** - default behavior (patrolling, eating, standing)
2. **Curious** - detected something, moving to investigate
3. **Alerted** - confirmed threat detected, preparing to engage
4. **Engaging** - actively attacking or pursuing
5. **Searching** - lost sight, actively looking for target
6. **Retreating** - low health or tactical disadvantage, breaking contact

**[LIKELY]** Transitions:
- Idle -> Curious: sensor input above low threshold
- Curious -> Alerted: confirmed detection
- Alerted -> Engaging: target reachable
- Engaging -> Searching: lost sight of target
- Searching -> Idle: exhausted search, no leads
- Any -> Retreating: health critical, outnumbered

### Search Behavior

**[LIKELY]** When an AI loses sight of a target, it:
- Goes to last known position
- Looks around (rotational scan, widening cone)
- Checks nearby likely hiding spots (behind cover, inside obvious buildings)
- Calls out to allies (if NPC or cryptid pack)
- Eventually gives up and returns to idle after ~2 minutes of no new cues

**[LIKELY]** Players can exploit searches tactically - plant a distraction on the other side of a room, hide, let the AI investigate, slip past.

### Alert Propagation

**[LIKELY]** Alerted NPCs in a group communicate:
- Humans: shout, radio (long range)
- Wolf-cryptids: howl (audible 500m)
- Other pack cryptids: subsonic or behavioral signaling

**[COMMIT]** Propagation is bounded - not the whole map becomes alerted from one shot. Alerts have geographic reach.

**[LIKELY]** Alert decay: an area that was alerted 30+ minutes ago returns to idle unless fresh cues.

---

## PvP Stealth

### Against Other Players

**[COMMIT]** Same detection rules apply to other players as to AI. No special "wallhack" for PvP. No name tags visible through walls. If you cannot see them with your eyes, you do not see them.

**[COMMIT]** Player avatar visibility scales with distance and posture. A prone player in tall grass at 200m is essentially invisible to the naked eye.

**[LIKELY]** Binoculars and scopes extend player vision effectively, but tagging a player across the map is not trivial - you need to actively scope a specific direction.

### Name Tags and Inspection

**[COMMIT]** Player name tags appear only when:
- You directly look at another player's face from close range (~15m)
- You have exchanged a reputation query (see PLAN-P2P-Reputation-System)
- You are in the same clan (clan-visibility is opt-in)

**[COMMIT]** No global player list with positions. No radar showing all players in area. The world is the radar.

---

## Environmental Interaction

### Weather Effects on Stealth

See PLAN-Weather for full details. Summary:
- Rain masks footstep sound
- Wind masks ambient sound + carries scent directionally
- Fog reduces vision range for everyone
- Thunder briefly masks loud sounds (gunfire, explosions)
- Snow preserves tracks longer, dampens sound

### Time of Day

**[COMMIT]** Night is dark. Visibility ranges drop ~60-80% without light sources.

**[LIKELY]** Dawn and dusk are tactical sweet spots - not night-dark but low enough to hide movement in shadows.

**[LIKELY]** Full-moon nights are noticeably brighter than new-moon. Moon phase tracks in game time. Players with good almanac knowledge plan operations around moon phase.

### Light Sources and Attention

**[LIKELY]** Any light source (flashlight, lantern, campfire, flare) draws AI attention. Flashlight beam is a directional beacon - hostile AI can detect its cone well before the light actually illuminates them.

**[LIKELY]** Firing a weapon has muzzle flash - night-time gunfire is visible at enormous distances.

**[LIKELY]** Suppressors reduce muzzle flash as well as sound.

### Fire and Smoke

**[LIKELY]** Smoke grenades are a stealth tool - break line of sight, extract from a situation, isolate a target.

**[LIKELY]** Fire is a lasting effect - lit fires attract cryptids and humans from a distance. A campfire at night is a social signal (or a bait).

---

## Cryptid-Specific Detection

See PLAN-Cryptid-Biology for species detail. Stealth-specific notes:

**[LIKELY]** Spine-Wolves: strong scent, good vision, good hearing. Scent-mask and downwind approach required.

**[LIKELY]** Marrow-Elk: moderate senses, territorial. Entering territory triggers investigation regardless of stealth. Leave the territory = retreat to investigation state.

**[LIKELY]** Scavvers: flock visual detection. Hard to sneak past a swarm.

**[LIKELY]** Husklings: poor senses, easy to avoid. Moral question: do you sneak past, or put them down quietly?

**[LIKELY]** Bone-Bear: moderate senses, low peripheral vision (enormous frontal vision cone). Flank it.

**[LIKELY]** Glass-Spider: trip-wire based. Spot the webs, cut them, move through.

**[LIKELY]** Scrap-Hawk: extreme visual acuity in open terrain. Use cover or wait for fog.

**[LIKELY]** Crawler: excellent night vision, mediocre daylight. Day operations easier.

**[LIKELY]** Hive-Queen: subsonic detection within pulse range. Avoid the pulse field.

**[LIKELY]** Shadelark: supernatural - always seems to know. Not because of mechanical detection but because Shadelarks rarely let themselves be found at all. Parley is the stealth tool.

---

## Animation and Feedback

**[COMMIT]** Stealth animations are distinct. Crouch-walk has a specific gait. Prone-crawl has a realistic belly-drag. Silent takedown is a scripted animation with specific duration.

**[LIKELY]** AI animations telegraph state:
- Idle: relaxed, slow head turns
- Curious: head-up, alert, slow turning
- Alerted: stance shifts, weapon drawn (if human)
- Searching: animated head-look, weapon swept

**[LIKELY]** Sound design:
- Player actions have quiet but noticeable audio cues (shirt rustling for crouching, fabric sliding for prone)
- AI vocalizations signal state transitions ("what was that?", "over there!", growl + lowered head)

---

## Balance Considerations

**[COMMIT]** Stealth must remain viable at all levels of play. Early-game players with no gear must still be able to survive by sneaking. End-game players with suppressors must not be effectively invisible.

**[LIKELY]** Late-game counter-stealth tools in the world:
- Thermal scopes can pick up heat signatures (rare, military-tier loot)
- Motion-sensor traps can detect intruders
- Seismic sensors at certain military locations

**[LIKELY]** Stealth is most valuable against numerous weak enemies (Huskling packs, Spine-Wolves). Less effective against individually-powerful enemies (Bone-Bear detection is harder to slip past; if detected, you are probably dead).

---

## Deliverables for 1.0

1. Vision cone system (range, FOV, acuity)
2. Sound propagation with occlusion + weather effects
3. Scent system (wind direction, scent-tracking species)
4. Player posture: stand, crouch, prone with correct implications
5. Pace: walk slow, walk, run, sprint with noise + speed + stamina
6. Clothing stealth modifiers (color, material, weight)
7. Leaning / peeking from cover
8. Suppressor + subsonic ammo
9. Crossbow + bow as silent weapons
10. Silent takedown animation + mechanics
11. Throw-and-distract system
12. Scent masking items
13. Decoys (dummy, radio, flasher)
14. Track visibility (UI-enhanced + natural visibility in mud/snow)
15. Detection UI (eye icon, audio ring, AI state, compass pulses) - toggleable
16. AI state machine: idle, curious, alerted, engaging, searching, retreating
17. Alert propagation with geographic bounds
18. Weather integration (rain, fog, wind, thunder affecting stealth)
19. Per-cryptid-species sensor profiles

---

## Open Questions

**[UNDECIDED]** Do we have a "stealth experience" that improves with successful stealth kills/moves? Could feel grindy. Leaning toward no explicit progression - skills are in the player, not the character.

**[UNDECIDED]** Civilian NPC stealth: humans who are not hostile. Do they have detection at all, or are they always aware of the player and just not reacting? Affects trader/questgiver design.

**[UNDECIDED]** Cryptid detection of other cryptids. Intra-cryptid stealth. Probably binary: same pack = allies, different species = prey. Fits simulation without complication.

**[UNDECIDED]** Player-vs-player "griefing detection" - a clearly-hostile player stalking you. Any special support? Probably no; same rules apply. Part of the game's tension.

**[UNDECIDED]** Thermal vision for cryptids? A Spine-Wolf might see body heat better than ordinary vision. Could be a specific late-game species mechanic.

---

## Relationship to Other Plans

- **PLAN-Cryptid-Biology** - per-species sensor profiles referenced here
- **PLAN-Infected-AI** - the existing infected AI plan; this refines sensing for all AI
- **PLAN-Combat** - stealth bonus damage on surprise shots, suppressor/subsonic
- **PLAN-Audio-Design** - sound propagation rules share implementation
- **PLAN-Weather** - weather masking effects defined jointly
- **PLAN-Clothing-Storage** - clothing material / color / weight affect stealth
- **PLAN-UI-HUD** - detection indicators, toggleability
- **PLAN-VR-Controls** - physical lean, crouch tracking
- **PLAN-P2P-Reputation-System** - name-tag visibility rules integrate
- **PLAN-Day-Night-Cycle** - night darkness, moon phase
