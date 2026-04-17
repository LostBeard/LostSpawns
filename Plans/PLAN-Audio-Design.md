# Audio Design - Brainstorm and Plan

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

**Sound is DayZ's scariest feature.** A snapped twig = death. A distant gunshot = choice: run toward, run away, hide. Footsteps on gravel give you away. Birds scatter when someone else approaches a treeline.

Lost Spawns commits to **audio as a core pillar**. Not set dressing. Not ambiance. A **simulation** - positional, occluded, attenuated, material-aware. Your ears are your best scope.

**Design goals:**

1. **Full 3D positional audio.** HRTF binaural when supported, stereo fallback. You can locate a sound in 3D space.
2. **Occlusion and propagation are real.** Walls muffle. Open fields carry. Water damps. Caves echo.
3. **Surface-aware footsteps.** Gravel, grass, mud, tile, metal, carpet each distinct in volume and timbre.
4. **Suppressors and subsonic are god-tier.** Silence pays for itself many times over.
5. **Ambient tension score.** Music breathes with danger level - calm, alert, combat, horror.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Spatial audio engine** - WebAudio API (browser) with HRTF + distance falloff (cross-ref SpawnDev.BlazorJS wrappers)
- **Physics + occlusion** - raycast from emitter to listener for occlusion factor
- **Voxel + SDF terrain** - block/material type per position for surface footsteps
- **Entity system** (VoxelEngine Phase 12) - emitters attach to entities
- **Content pipeline** - sample libraries organized by material/action/weapon

---

## Spatial audio core

### [COMMIT] 3D positional audio

- Every sound emitter has world position
- Listener position updated from camera/head in VR (cross-ref README WebXR VR)
- HRTF binaural panning for headphones
- Stereo panning fallback for speakers

### [COMMIT] Distance attenuation

- Inverse-square falloff (real-world physical)
- Per-sound clamp for short sounds (avoid 0-volume pops)
- Noise floor cutoff for performance (sounds below threshold dropped)

### [LIKELY] Doppler effect

- Moving sources shift pitch (passing vehicle, bullet whizz)
- Adds realism + supersonic-crack vs muzzle-blast distinction
- Cross-ref [PLAN-Combat.md](PLAN-Combat.md) near-miss detection

### [LIKELY] Air absorption

- High frequencies attenuate faster over distance
- Distant gunshots sound bassier/duller than near ones
- Natural realism touch

### [UNDECIDED] Volumetric emitters

- Large sources (wildfires, rivers, waterfalls) as area sounds, not point
- Adds authenticity
- Lean [LIKELY] for specific types

---

## Occlusion and propagation

### [COMMIT] Wall occlusion

- Raycast from emitter to listener detects wall intersections
- Per-material absorption coefficient (concrete blocks most, wood medium, glass weak)
- Occluded sound low-pass filtered (muffled)

### [LIKELY] Propagation by path

- Sound "finds" path around corners via portal system (open doorways, windows)
- Cave echo different from open field echo
- Diffraction simulation at corners (weak reverb + low-pass)

### [LIKELY] Reverb zones

- Tagged volumes (building interior, cave, large hall) get reverb parameters
- Transition smoothed at zone boundaries
- Drives "you can hear you're in a big space" immersion

### [LIKELY] Water damping

- Underwater emitters heavily low-passed + attenuated when listener above water
- Vice versa (above-water source heard muffled by underwater listener)
- Splash vs swim detected correctly

### [UNDECIDED] Dynamic occlusion via voxel density

- Terrain blocks contribute to occlusion automatically (full dirt hill = full occlusion)
- Requires fast occlusion raycast over SDF + voxel
- Lean [LIKELY] via sampled raycasts, refine with iteration

---

## Surface-aware footsteps

### [COMMIT] Footstep material detection

- Block/terrain type under player foot determines sample
- Categories: gravel, stone, grass, dirt, mud, wood, tile, metal, carpet, snow, ice, water, leaves

### [COMMIT] Surface noise profile

- Per-surface volume + timbre
- Gravel: loud, scratchy
- Mud: squelchy, medium
- Tile: sharp, loud (reflective)
- Carpet: muffled, quiet
- Grass: soft, very quiet
- Metal: resonant, loud

### [LIKELY] Stance modifier

- Crouch: -50% volume
- Prone: -75% volume
- Walk: baseline
- Run: +20% volume
- Sprint: +40% volume

### [LIKELY] Weight modifier

- Carry weight affects step volume (heavy load = thud)
- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) encumbrance

### [LIKELY] Gear sound

- Loose gear rattles when moving (canteens, metal clips)
- Cross-ref clothing condition + kit layout
- Taped down / silenced gear = less rattle
- Perks reduce rattle (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

---

## Weapon audio

### [COMMIT] Gunshot distance falloff

- Muzzle blast (sharp transient near) + supersonic crack (at distance along projectile path)
- Near-shooter: blast dominant
- Downrange from shooter: crack first, then blast
- Environmental reverb appropriate to surroundings

### [COMMIT] Suppressor effect

- Blast reduced dramatically (-20 to -30 dB)
- Supersonic crack still present unless subsonic ammo
- Muzzle flash also reduced (visual + audio coupling)

### [LIKELY] Subsonic ammo

- No supersonic crack
- Combined with suppressor = near-silent from distance
- Short effective range (reduced velocity)

### [LIKELY] Caliber-distinct sound

- 9mm pop vs .50 boom easily differentiable
- Hardcore players learn to identify by sound
- Cross-ref [PLAN-Combat.md](PLAN-Combat.md) caliber list

### [LIKELY] Indoor vs outdoor

- Indoor fire = reverb + painful loudness + temporary hearing muffle
- Outdoor = environmental-appropriate reverb
- Close-quarters tactics trade-off

### [LIKELY] Weapon handling foley

- Bolt cycling, magazine insertion, chamber check
- Heard by nearby players if close
- Immersion + tactical (hear enemy reload)

---

## Ambient soundscape

### [COMMIT] Biome ambience

- Forest: birds, rustle, distant animals
- Plains: wind, grass
- Urban ruin: debris shift, distant sirens, rust creaks
- Industrial: electrical hum, distant metal
- Coastal: waves, gulls
- Cave: drip, echo, silence
- Cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md) for hazard-specific ambience

### [LIKELY] Weather audio

- Rain (intensity-scaled), thunder, wind, blizzard howl
- Rain on metal roof vs dirt very different
- Cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md) weather

### [LIKELY] Wildlife audio cues

- Birds flee when disturbed (early warning for player movement)
- Dogs bark at strangers
- Deer rustle in brush
- Cryptid sounds at long range (Howler call, Doctor whistle)

### [LIKELY] Time-of-day shift

- Day: bright bird calls, active wildlife
- Dusk: cricket, owl
- Night: quieter, distant howls, wind
- Dawn: first-light chorus
- Cross-ref [PLAN-Day-Night-Cycle.md](PLAN-Day-Night-Cycle.md)

### [UNDECIDED] Radio leakage

- Abandoned radios playing eerie emergency loops at certain ruins
- Pre-collapse TV static from broken televisions
- Environmental storytelling via audio
- Lean [LIKELY] - strong atmospheric win

---

## Noise propagation as gameplay

### [COMMIT] Noise event system

- Every loud action emits noise event (volume, position, duration)
- Entity perception samples noise events at their position
- Infected + NPCs aggro on noise (cross-ref [PLAN-Infected-AI.md](PLAN-Infected-AI.md))

### [COMMIT] Suppression meta

- Quiet play viable with suppressor + subsonic + silent surfaces
- Creates stealth specialist loadouts

### [LIKELY] Noise meter HUD option

- Optional UI element showing your own noise output
- Toggle off for hardcore (learn by ear)
- On by default for onboarding

### [LIKELY] Infected migration via noise

- Regional noise accumulates → infected migrate to source
- Stealth preservation keeps zone calm
- Cross-ref [PLAN-Infected-AI.md](PLAN-Infected-AI.md) heat map

### [LIKELY] Animal panic cues

- Birds burst from trees when someone moves
- Useful recon: spot another player at distance via bird scatter
- Bidirectional: you trigger birds = enemy sees your approach

---

## Voice and VoIP

### [COMMIT] Proximity voice

- Cross-ref [PLAN-Radio-Comms.md](PLAN-Radio-Comms.md) VoIP integration
- Open mic or push-to-talk
- Range attenuation + occlusion like any sound
- Muffled by gas mask, clearer via radio

### [LIKELY] Radio-voice simulation

- Player voice through radio adds compression, slight distortion, static
- Lo-fi radio feel
- Different from direct proximity voice

### [LIKELY] Whisper mode

- Toggle to whisper (reduced range, quieter)
- Useful for stealth squads

### [UNDECIDED] Voice-based infected aggro

- Open-mic shouting at detected infected-proximity = aggro chance
- Hardcore but natural
- Lean [LIKELY] - rewards silent comms in dangerous zones

---

## Music and tension score

### [COMMIT] Adaptive ambient score

- Dynamic stems (calm, alert, combat, horror)
- Crossfade based on threat level (nearby enemies, cryptid presence, low HP)
- Never overrides diegetic audio (drops under gunfire)

### [LIKELY] Silence moments

- Before cryptid reveal: ambient cuts completely
- Before emission: 5-second silence warning
- Builds tension through absence

### [LIKELY] Location-specific score

- Safe zones: warmer tones
- Hives: dissonant drones
- Cryptid territory: specific motif per cryptid
- Cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) cryptid themes

### [LIKELY] Player-selectable music off

- Option to disable adaptive score entirely (hardcore purists want diegetic only)
- Settings toggle

### [UNDECIDED] Broadcast-station music as diegetic score

- Music playing from claimed tower fills the region in-world
- Score-less sections filled by nearby tuned radios
- Lean [LIKELY] - unique immersion angle

---

## Crafting and foley audio

### [LIKELY] Crafting feedback audio

- Hammer on anvil, sewing machine hum, chemistry bubbling, saw cutting wood
- Distinct per-station (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))
- Heard by nearby players (active base = audible)

### [LIKELY] Tool use

- Axe on tree, shovel on dirt, pickaxe on stone
- Material-specific sample
- Travel long distance (distinctive ring)

### [LIKELY] Inventory rattle

- Packing items makes quiet shuffle sounds
- Reload cluster (magazine click, chamber check) characteristic

---

## Special audio effects

### [LIKELY] Hearing damage

- Flashbang: tinnitus + muffle for seconds
- Prolonged loud sound (unsuppressed full-auto near ears): temporary hearing shift
- Ear protection (cans, earplugs) mitigate (cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

### [LIKELY] Emission audio

- Emissions emit region-wide distortion + rising tone
- Audible 5-second warning before peak
- Cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md) emissions

### [LIKELY] Cryptid audio signatures

- The Howler: distant wail (fakes + real)
- The Doctor: tuneless whistling
- The Broadcaster: audio distortion in nearby radios
- The Scorched One: crackling fire + labored breath
- Mother Mutation: wet birthing sounds
- The Warden: chain drag

### [UNDECIDED] Heartbeat when low HP

- Fast heartbeat when near death
- Classic convention
- Lean [LIKELY] with toggle-off option

---

## Audio interactions with other plans

### Combat (see [PLAN-Combat.md](PLAN-Combat.md))

- Weapon signatures (muzzle blast, crack)
- Near-miss suppression audio
- Suppressor physics

### Infected AI (see [PLAN-Infected-AI.md](PLAN-Infected-AI.md))

- Noise aggro driven by audio events
- Screamer alert chain

### Clothing + storage (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Helmet + gas mask voice muffling
- Gear rattle vs stealth suits
- Ear protection

### Radio comms (see [PLAN-Radio-Comms.md](PLAN-Radio-Comms.md))

- Voice through radio pipeline
- Broadcast station audio
- Tuning static artifacts

### Environment hazards (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Weather + emission audio
- Chem cloud hiss
- Biohazard buzz

### Dynamic events (see [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))

- Cryptid audio signatures
- Event signaling (smoke column is visual, but distant gunfire is audio)

### Survival needs (see [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md))

- Eating/drinking sounds
- Snoring during sleep (detection risk)
- Cooking foley

### Terrain carving (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

- Material-specific dig sounds
- Echoes in carved caves
- Trap audio cues

### Day/night cycle (see [PLAN-Day-Night-Cycle.md](PLAN-Day-Night-Cycle.md))

- Time-of-day ambience shifts
- Nocturnal wildlife audio

---

## Gameplay verbs audio design enables

- Hear a snapped twig behind you in a forest, spin around with shotgun raised, spot the Stalker at three meters
- Locate a sniper at 400m by the supersonic crack arrival time + muzzle blast delay, triangulate to the roof
- Whisper over proximity voice in a squad stealth run, keep below infected hearing threshold through a city
- Know your AK is about to jam because the action cycle sound changed over the last hundred rounds
- Walk on tile at the hospital, realize every step rings, switch to crouch walk or go back to gravel
- Identify an incoming event by its horn tone over distant radios before it hits your map
- Listen to dogs barking three blocks away, realize strangers are in town
- Reload in silence by muffling magazine drop with your hand (skill-gated animation)
- Recognize The Howler's distant wail before you see it, mark the direction, warn the squad with a single radio tick
- Catch a faint tuneless whistle in a hospital ruin, pack up and leave before The Doctor closes in
- Spot another player at 200m because birds scattered from their position
- Drop a rock off a ledge to bait infected away from your approach route

---

## Open questions

1. **HRTF licensing** - SpatialAudio default vs custom HRTF profiles? Cost/feasibility.
2. **Network voice bandwidth** - compression codec (Opus) vs quality trade-off.
3. **Sample library size** - budget for distinct samples. Player-facing asset size.
4. **Dynamic music middleware** - build in-house adaptive mixer vs license (Wwise, FMOD - not browser-native)?
5. **Audio accessibility** - sub-captions for critical sounds (gunshots, cryptid calls) for deaf players?
6. **Spatial audio without headphones** - fallback quality for speakers users?
7. **Moderation for open mic** - push-to-talk only, auto-mute, report?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Spatial audio core | WebAudio API + HRTF + listener updates |
| Occlusion raycast | Physics raycast + material data |
| Surface footsteps | Terrain block/material lookup |
| Gunshot audio | Weapon registry + caliber samples + suppressor modifier |
| Ambient soundscape | Biome system + time-of-day trigger |
| Noise events | Perception system (AI) + emitter registry |
| VoIP | WebRTC + spatial routing |
| Adaptive score | Music stems + threat-level tracking |
| Reverb zones | Tagged volumes + reverb impulse library |

---

## Next actions

1. Pick spatial audio stack (WebAudio + HRTF profile) and prototype positional 3D sound
2. Build footstep material sampler (voxel/SDF block → sample → play with volume modifier)
3. Gunshot audio system (muzzle + crack + environmental reverb + suppressor attenuation)
4. Occlusion raycast integration (wall muffle working end-to-end)
5. Noise event emitter/listener pipeline (integrates with Infected AI noise aggro)
6. Ambient biome soundscape (one biome end-to-end proof)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
