# Radio and Communications - Brainstorm and Plan

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

**The radio is alive.** Every frequency a discovery. Static, music, distress signals, propaganda, coded partisan transmissions, encrypted black-market coords. A survivor with a radio knows more than a survivor without one. Information is power - and the radio is how the world tells its story to anyone listening.

DayZ's radio was a novelty. Arma's ACRE made comms a skill. Tarkov's radio system gives intel. Lost Spawns goes further: **in-world radios are core infrastructure**. Broadcast stations claimable. Frequencies discoverable. Encryption a progression. Morse a fallback.

**Design goals:**

1. **Radios are equipment, not UI.** Tune dials, swap batteries, manage antennas. Physical, breakable, losable.
2. **Frequencies are a game.** Scan bands, find channels, decode. Every radio a treasure chest.
3. **VoIP routes through radios.** Proximity voice + radio-band voice. No magic cross-map chat.
4. **Broadcasting is power.** Claim a tower, reach a region. Your squad's music plays on every radio in zone.
5. **Encryption is tradecraft.** Scramblers, key exchange, direction finding. A quiet information arms race.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Audio engine** - voice capture, streaming, spatialization (new systems)
- **Entity system** (VoxelEngine Phase 12) - radios as held/placed items
- **Networking / P2P** - voice routing between players (cross-ref SpawnDev libraries)
- **Persistence** (Phase 8 OPFS) - frequency lists, broadcast claims, encrypted key storage
- **Dynamic events** (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md)) - NPC broadcasts drive event signaling

---

## Radio devices

### [COMMIT] Device tiers

- **Civilian handheld** - CB/FRS, short range, low clarity, no encryption. Common loot.
- **Pro handheld** - VHF/UHF, mid range, higher clarity, simple scrambler support.
- **Military handheld** - encrypted, long range, battery-hungry, rare loot.
- **Vehicle-mounted** - powerful antenna, long range, vehicle-dependent (cross-ref [PLAN-Vehicles.md](PLAN-Vehicles.md)).
- **Base station** - stationary, biggest range + highest clarity, power-grid dependent (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md)).
- **Backpack radio (man-pack)** - mobile base station, heavy slot, very long range.

### [LIKELY] Condition and repair

- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) condition system
- Condition affects clarity (static) + range
- Smashed antenna: short-range only until repaired
- Engineer skill (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md)) repairs at higher tiers

### [LIKELY] Battery drain

- Radios consume batteries while on
- Transmit draws more than receive
- Battery as currency (cross-ref [PLAN-Economy.md](PLAN-Economy.md))
- Hand-crank / solar charger alternative for emergencies

### [UNDECIDED] Antenna upgrades

- Field-deployable antenna boosts handheld range
- Portable mast for basecamp
- Rare crafted item
- Lean [LIKELY] late game

---

## Frequency bands

### [COMMIT] Band types

- **CB (27 MHz)** - civilian, unregulated, local range, crowded
- **VHF (136-174 MHz)** - line-of-sight, good urban range, common handheld
- **UHF (400-512 MHz)** - penetrates structures better, shorter range
- **Shortwave (3-30 MHz)** - long range via ionosphere bounce, static-heavy
- **Amateur / HAM** - wide bands, enthusiast equipment, best for skilled operators
- **Military (encrypted)** - proprietary frequencies, requires decryption gear

### [LIKELY] Band selection matters

- Urban combat: UHF best
- Long distance: shortwave
- Rapid chatter: VHF
- Covert: military encrypted

### [LIKELY] Atmospheric propagation

- Night / solar activity shifts shortwave reach
- Weather affects clarity (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Emissions disrupt all bands briefly
- Line-of-sight respected (mountains block VHF/UHF)

---

## Broadcasting

### [COMMIT] Player broadcast

- Claimed broadcast tower (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) tower heist event)
- Connect radio/mic + broadcast live voice or queue tracks
- Region-wide reach
- Tower ownership = your content on every tuned radio

### [LIKELY] Playlist + music

- Upload/select music from curated library (rights-safe)
- Loop when not live
- Schedule segments

### [LIKELY] Automated loops

- Morse beacons
- SOS / distress auto-sender
- Propaganda loops
- Pre-recorded broadcast while operator offline

### [LIKELY] Base station broadcast

- Small-radius broadcasts from your home base
- Not tower-level reach, but personal
- Identifies base as active to nearby players (tradeoff - discoverability vs stealth)

### [UNDECIDED] Pirate stations

- Rogue broadcast points that pop up dynamically (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))
- Transmit lore, plot hooks, secret coords
- Lean [LIKELY] as narrative vehicle

---

## Receiving and scanning

### [COMMIT] Manual tuning

- Dial UI to turn frequency knob
- Real-time audio from frequency
- Squelch to filter static below threshold

### [LIKELY] Scan mode

- Auto-sweep through band, stop on signals
- Marks detected frequencies on personal freq list
- Time cost (slower to scan wide, faster on memorized list)

### [LIKELY] Frequency directory

- Known frequencies saved in device
- Pre-loaded partial list (major stations, emergency)
- Player adds discovered ones

### [LIKELY] Signal strength meter

- Shows reception quality
- Directional antenna + strength = direction finding (DF)

---

## Encryption

### [COMMIT] Scrambler module

- Attached to radio, encrypts outgoing + requires matching key for incoming decode
- Tier 1-3 encryption strength
- Basic scrambler defeats casual eavesdrop; military breaks low tiers

### [LIKELY] Key exchange

- Players manually share key codes (trust-based)
- Scramblers and decoders set to same key
- Change keys periodically (rotating paranoia)

### [LIKELY] Decryption attack

- Listening to encrypted channel + decoder + skill + time = break key
- Engineer skill + rare decoder gear
- Valuable intel reward

### [LIKELY] Direction finding

- Specialized receiver triangulates transmitter origin
- Radar operator gameplay (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) radar operator role)
- Counter: move while transmitting, short bursts

### [UNDECIDED] One-time pad

- Pre-shared key sheet, unbreakable if properly used
- Hardcore encryption for elite squads
- Cool but complex - lean [DEFER] to post-v1.0

---

## VoIP integration

### [COMMIT] Proximity voice

- Talk + hear nearby players (range ~30m, attenuation by distance + walls)
- Always on (push-to-talk or open mic)
- Occluded by walls (cross-ref [PLAN-Audio-Design.md](PLAN-Audio-Design.md))

### [COMMIT] Radio-band voice

- Transmit through tuned radio on set frequency
- All radios tuned to that frequency receive
- Distance attenuation + static based on signal strength
- Tower-based broadcast uses radio-band layer

### [LIKELY] Push-to-talk binding

- Key to open radio mic
- Optional voice-activation mode (handsfree, but broadcasts cough/breathe)

### [LIKELY] Muffled / gear-affected voice

- Gas mask muffles voice
- Helmet mic clearer through radio
- Cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md)

### [LIKELY] Squad channel convenience

- Squads auto-get a shared scrambled frequency
- Still routed through player radio gear - lose radio = lose squad comms
- Scrambling transparent to squad members

### [UNDECIDED] Text-only radio fallback

- Typed messages on frequency for those without mic
- Appears as subtitle to listeners
- Accessibility + low-bandwidth option
- Lean [LIKELY] for accessibility

---

## Morse code

### [LIKELY] Morse as low-bandwidth channel

- Key-in dots and dashes, transmit
- Very long range (shortwave Morse reaches further than voice)
- Learn-by-doing: reading Morse is a skill developed via practice
- Decoded Morse displays as text for listener

### [LIKELY] Morse beacons

- Automated Morse distress (SOS)
- Abandoned military sites broadcast in-world lore via Morse
- Decryption gear can auto-decode for convenience

### [UNDECIDED] Morse tutorials in-world

- Old military radio manuals as loot, teach Morse gradually
- Flavor bump - lean [LIKELY]

---

## NPC broadcasts

### [COMMIT] Event announcements

- Faction vendors, survivor stations broadcast event alerts
- Cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md)
- Players with radios hear events earlier than those without

### [LIKELY] Faction propaganda

- Different factions run stations with own tone
- Military: situation reports, patriotic music
- Medical: drug warnings, infection bulletins
- Smugglers: encrypted coordinate drops
- Farmers: market days, weather forecasts

### [LIKELY] Emergency broadcast system

- Pre-collapse government loop on a fixed frequency
- Eerie, frozen-in-time recording
- Lore + environmental storytelling

### [LIKELY] Cryptid-related broadcasts

- Distress calls about The Howler
- Survivor warnings about The Doctor
- Cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) cryptid evidence

### [UNDECIDED] Dynamic NPC weather / trader reports

- Traveling NPC merchants broadcast arrival
- Weather service forecasts emissions
- Lean [LIKELY]

---

## Interference and countermeasures

### [COMMIT] RF jammer

- Active device, blocks radio signals in area
- Power-hungry (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md) power)
- Cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) anti-GPR + anti-radio defensive tactic
- Defenders use to black out raiders' squad comms

### [LIKELY] Faraday room

- Sealed lead-lined room blocks all radio
- Safe comms inside (wired), silent outside
- Defeats direction finding + eavesdrop

### [LIKELY] Emission disruption

- World events disrupt bands temporarily (cross-ref [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))
- Squads without wire-comms lose coordination in emission windows

### [LIKELY] Frequency hopping

- High-end radios auto-hop frequencies per-second
- Requires synced receivers (squad uses shared schedule)
- Defeats casual eavesdrop + easier DF resistance

---

## Radio interactions with other plans

### Audio design (see [PLAN-Audio-Design.md](PLAN-Audio-Design.md))

- Range attenuation, static, tuning artifacts
- Voice muffling through gear

### Dynamic world events (see [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))

- NPC event announcements
- Broadcast tower heist events
- Pirate station lore drops

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Base station placement
- Power requirements
- Faraday rooms, RF jammers

### Clothing + storage (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Radio as belt/chest slot item
- Helmet mic integration
- Gas mask voice muffle

### Vehicles (see [PLAN-Vehicles.md](PLAN-Vehicles.md))

- Vehicle-mounted radios
- Range bonus over handheld

### Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Engineer skill: repair + craft radios
- Marksman: DF precision
- Social skill: broadcast reach quality

### Factions + squads (see [PLAN-Factions-Squads.md](PLAN-Factions-Squads.md))

- Squad auto-channel
- Faction broadcast stations

### Terrain carving (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

- Radar operator role
- Underground bases: radio shielding

### Economy (see [PLAN-Economy.md](PLAN-Economy.md))

- Frequencies sold as information goods
- Batteries = currency
- Encryption keys as premium trade

---

## Gameplay verbs radio comms enable

- Scan shortwave at dusk, catch a faint Morse distress beacon from a crash site 40 kilometers away, race to get there first
- Tune to encrypted channel with a stolen key, overhear rival squad coordinating a raid on YOUR base, ambush them inbound
- Claim a broadcast tower, set your squad's anthem to loop, other players learn your territory by the music
- Call over squad radio "two tangos, east wing, moving north toward stairs" during a raid (cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) radar operator)
- Lose squad comms to an emission, fall back to proximity voice + hand signals for the rest of the night
- Use direction finding gear to triangulate a pirate station, find the broadcaster's secret bunker, loot the studio
- Trade a stack of batteries at a faction vendor for a rare high-clarity military handheld
- Jam enemy base radios with an RF jammer during your raid, cut their reinforcements' signal completely
- Record a fake SOS distress call from a pre-war voice acting sample, lure enemy squads into your kill zone
- Learn Morse by reading pre-war manuals over the course of in-game weeks, start decoding the government emergency loop word by word
- Negotiate with a wandering trader over CB from across a valley before you even approach their position
- Hear your cryptid's signature distortion leak into your radio as The Broadcaster approaches, run for cover

---

## Open questions

1. **Voice chat infrastructure** - built on WebRTC (cross-ref SpawnDev libraries)? Server relay? P2P?
2. **Music library licensing** - strict Creative Commons curation, or player-upload with TOS disclaimer?
3. **Radio range vs server scale** - how does region-wide broadcast work across a DayZ-scale map?
4. **Encryption complexity** - player-tactical (key codes) vs real crypto (cross-ref SpawnDev.Crypto)? Lean game-logic simulation.
5. **Bandwidth cost** - voice streams can eat network. Compression strategy?
6. **Deaf / accessibility** - players without mic or hearing. Text fallback coverage.
7. **Griefing via open channels** - moderation tools, mute, report mechanisms?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Radio item + UI | Entity + inventory + interaction |
| VoIP | Voice capture + streaming + spatialization (WebRTC) |
| Frequency simulation | Channel routing + signal strength model |
| Broadcast towers | Tower entity + claim state + broadcast layer |
| Encryption sim | Key-match logic + decoder skill check |
| Direction finding | Transmitter location + gear + UI |
| RF jammer | Zone effect + signal blocking |
| Morse | Input binding + encoder/decoder |
| NPC broadcasts | Scripted audio tracks + event hook |

---

## Next actions

1. Pick radio stack (WebRTC voice + frequency routing layer on top)
2. Define radio item schema (device tier, range, condition, battery, antenna)
3. Prototype one radio pair (tune to frequency, transmit voice, receive with distance attenuation)
4. Broadcast tower claim integration (cross-ref dynamic events plan)
5. Encryption simulation spike (key match + decoder roll)
6. NPC broadcast pipeline (scripted audio triggered by world state)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
