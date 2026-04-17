# PLAN - Day/Night Cycle

> **Status:** DESIGN BRAINSTORM
> **Related:** PLAN-Environment-Hazards.md, PLAN-Infected-AI.md, PLAN-Survival-Needs.md, PLAN-Audio-Design.md, PLAN-Vision.md

---

## The Pitch

The day/night cycle is not decoration - it is a **gameplay clock.** Daytime is scavenging, travel, social interaction, building. Night is threat, stealth, fear, preparation. The world changes character every 45 minutes, and the player has to change with it or die.

No server day is the same. Full moons swarm infected. Eclipses spawn cryptids. Dawn brings the safest windows. Sunset is the most dangerous transition - your eyes haven't adjusted, the world hasn't warned you, and the night things are just waking up.

---

## Time Model

### [COMMIT] Base Cycle Length

- **Real-time day cycle:** 60 minutes (45 day / 15 night default)
- Server configurable: 30 min to 24 hours (hardcore servers run real-time clock)
- Time of day synced across all players on a server
- Persistent across player logout (world keeps turning)

### [COMMIT] Time Periods

| Period | Duration | Sun Angle | Feel |
|--------|----------|-----------|------|
| **Pre-Dawn** | 3 min | -10° to -5° | Cold, dark, quiet. Infected retreating. |
| **Dawn** | 5 min | -5° to +15° | Mist, low sun, golden hour. Safest window. |
| **Morning** | 10 min | +15° to +45° | Full visibility, cool. Best for travel. |
| **Noon** | 10 min | +45° to +70° | Hot, harsh shadows. Scavenge zones clearer. |
| **Afternoon** | 10 min | +70° to +30° | Golden light, warm. Perfect combat visibility. |
| **Dusk** | 5 min | +30° to -5° | Sunset, danger transition. Eyes lose adjustment. |
| **Early Night** | 5 min | -5° to -15° | Dark, cold, first infected activity peaks. |
| **Deep Night** | 7 min | -15° to -25° | Pitch dark without moon. Cryptids roam. |
| **Late Night** | 3 min | -25° to -10° | Coldest. Fatigue hits hardest. |

### [COMMIT] Server Configurable

- Admin can lock time (eternal night for horror servers, eternal day for chill servers)
- Day/night ratio adjustable (60/40, 50/50, 30/70 for "dark world" servers)
- Cycle length multiplier (0.5x to 10x real-time)

---

## Sun & Moon

### [COMMIT] Sun

- Physically accurate solar position based on latitude (assume 40°N for default map)
- Sun direction drives the main directional light for shadows
- Sun color shifts: cold white at noon → warm gold at golden hour → red at horizon
- Sun affects: lighting, temperature, visibility, solar panel power generation

### [COMMIT] Moon

- Tracks its own orbit (not just sun-opposite)
- Provides secondary light source at night (brightness by phase)
- Moon phases (see below) affect gameplay significantly
- Moon rises and sets at different times than sun (some nights have no moon)

### [LIKELY] Moon Phases

8-phase cycle over ~7 real-world days (server configurable):

| Phase | Brightness | Effect |
|-------|------------|--------|
| **New Moon** | 0% | Pitch dark. Infected extra-aggressive. Cryptids common. |
| **Waxing Crescent** | 10% | Barely visible. Infected aggressive. |
| **First Quarter** | 30% | Usable light. Standard night. |
| **Waxing Gibbous** | 60% | Bright. Combat viable without NVGs. |
| **Full Moon** | 100% | Blood-moon event. Horde spawns. Rare loot drops. |
| **Waning Gibbous** | 60% | Bright. Standard night. |
| **Last Quarter** | 30% | Usable light. Standard night. |
| **Waning Crescent** | 10% | Barely visible. Cryptids hunt. |

### [UNDECIDED] Eclipses

Rare scripted events.

- **Solar eclipse** - daytime goes dark for 3 minutes. Cryptids spawn during the eclipse. Unique loot. Unique achievement.
- **Lunar eclipse** - full moon turns blood red. Horde intensifies. Some faction NPCs fall silent (superstition). Rare faction quest triggers.
- Frequency: ~once per 7 real-world days for solar, ~once per 30 days for lunar

### [LIKELY] Stars

- Accurate constellation rendering (Big Dipper, Orion, Polaris for navigation)
- Players can use Polaris to find north when compass is broken
- Some cryptid worshippers (Mothfolk) reference specific stars in lore
- Meteor showers - rare event, drops meteor iron for ultra-rare crafting

---

## Night Threats

### [COMMIT] Infected Behavior Changes

See PLAN-Infected-AI.md for details. Summary:

- **Night shift:** Shamblers more aggressive, Runners more common
- **Cryptids:** Only spawn at night (Howler, Stalker, Screamer by moonlight)
- **Sound sensitivity doubles at night** - every footstep matters
- **Visibility range halves** - infected can close on you before you see them
- **Pack sizes grow** - daytime 2-5, nighttime 4-10, full moon 10-20

### [LIKELY] Night-Only Events

- **Blood Moon horde** (full moon) - massive horde approaches nearest base/town
- **Cryptid hunt** (new moon) - unique cryptid spawns and hunts a random player
- **Silent Night** (rare) - infected vanish for one night. Something else is out there.
- **Broadcasting Ghost** - radio picks up dead faction's old broadcast (lore event)

### [LIKELY] Cold Shift

- Night temperature drops 10-20°C depending on biome
- Desert biome: hot day (+40°C), freezing night (-5°C)
- Forest biome: warm day (+25°C), cool night (+10°C)
- Arctic biome: cold day (0°C), lethal night (-30°C)
- Players need layered clothing or fires (see PLAN-Survival-Needs.md, PLAN-Environment-Hazards.md)

### [UNDECIDED] Fatigue Penalty

- Players who stay awake through multiple nights accrue fatigue
- Fatigue reduces aim accuracy, movement speed, healing rate
- Forced to sleep (bed, campsite) or take stimulants
- Sleeping in unsafe area is risky - player is defenseless, infected can find them

---

## Visibility System

### [COMMIT] Ambient Light Levels

- Real-time dynamic lighting using the same flood-fill system for day/night
- Night uses moon as secondary directional light
- Interior darkness stays dark even at noon (torches, flashlights needed)
- Sky color affects ambient - blue dome at noon, red at dusk, black-star at deep night

### [COMMIT] Night Vision Gear

- **Flashlight** - short cone, gives position away to everyone
- **Headlamp** - hands-free flashlight, same range
- **Glow sticks** - short-range area light, throwable, lasts 30 min
- **Night Vision Goggles (Gen 1)** - greenscale, needs battery, lights bloom (loud lights blind you)
- **Night Vision Goggles (Gen 3)** - clearer greenscale, better low-light sensitivity
- **Thermal Scope** - weapon-mounted, sees warm bodies through fog but not walls
- **IR Illuminator** - invisible to naked eye, visible to NVG. Covert lighting tool.

### [LIKELY] Natural Night Vision

- Night-adapted eyes (no bright lights) give improved night vision after 30 seconds
- Sudden bright light (muzzle flash, flashbang) resets adaptation
- The **Night Eyes mutation** (F76-inspired, see PLAN-Environment-Hazards.md) gives permanent improved night vision at the cost of day-time photosensitivity

### [LIKELY] Moonlight Silhouettes

- Open sky under full moon = players visible at 100+ meters
- Under forest canopy = darker than open field
- Inside buildings = pitch dark without light source
- Stealth gameplay rewards choosing terrain (forest vs. field)

---

## Stealth Advantages

### [COMMIT] Night Stealth Bonus

- Footstep sound range halved
- Visibility to infected halved
- Crouched + night + forest = near-invisible at 30+ meters
- Stealth takedowns (see PLAN-Combat.md) get bonus damage at night

### [LIKELY] Moving by Moonlight

- Darker moon phases reward scouts and assassins
- Lighter moon phases reward defenders (see the attackers coming)
- Strategic decision: raid on a new moon, defend on a full moon
- Experienced players plan raids around lunar calendar

### [UNDECIDED] Sound Masking

- Wind at night is louder than day (reality check needed)
- Rain masks footsteps (huge stealth buff)
- Thunder masks gunshots briefly
- Cryptid howls mask footsteps in a radius (dangerous buff)

---

## Temperature & Weather Interaction

### [COMMIT] Temperature Drop

- Night temperatures drop progressively
- Early night: -5°C from day baseline
- Deep night: -10 to -15°C from day baseline
- Late night: coldest point, before sunrise warms

### [LIKELY] Morning Fog

- Dawn brings fog in valleys and near water
- Fog reduces visibility to 30-50m
- Fog lifts by mid-morning
- Vehicles headlights reflect fog, giving position away

### [LIKELY] Evening Heat Release

- Desert biome releases heat after sunset (thermal imaging useless for 30 min)
- Forest biome cools quickly (ambient + ground-level)
- Thermal scope users need to account for time of day

---

## Sleep Cycle

### [UNDECIDED] Sleep Mechanic

- Players can sleep in beds (base) or sleeping bags (field)
- Sleeping skips time (fast-forward) IF all players on server agree
- Otherwise sleep gives buffs: rested bonus (XP, regen, morale)
- Can only sleep in safe zones (base claim, safe area) - otherwise attacked
- Unsafe sleep is a gambler's option - huge rested buff, but if infected find you, instant wake at low health

### [UNDECIDED] Time Skip Vote

- Squad can initiate a "skip to dawn" vote if all squadmates are in safe area
- Useful for avoiding a threatening night
- Other players on server are unaffected (they still play real-time)
- Does NOT skip cryptid events, horde events, or quest timers

---

## Sun Signs & Superstition (Flavor)

### [UNDECIDED] In-World Lore

- NPCs and factions reference sun signs, moon phases, and cryptid activity
- The Faithful interpret eclipses as judgment
- The Mothfolk track constellations for pilgrimage
- The Settlers reference the "old calendar" (pre-infection)
- Radio DJs announce moon phase and temperature at dawn/dusk

### [UNDECIDED] Zodiac Events

- 12 monthly zodiac events tied to sun position
- Capricorn (Jan): rare frost biome mutations appear
- Aries (Apr): extra infected spawn briefly
- Leo (Aug): radioactive solar flares - temporary rad increase
- Flavorful, not mechanically critical, but rewards long-term servers

---

## Performance Notes

### [COMMIT] Lighting Update Rate

- Sun position recalculated every 5 seconds (imperceptible lag)
- Shadow maps refresh incrementally (not full per-frame update)
- Ambient light baked into chunks at 1 Hz during twilight transitions
- Moon phase calculated once per frame (cheap)

### [LIKELY] Skybox System

- Procedural skybox using SDF clouds + star field texture
- Sun disc and moon disc as billboard sprites
- Atmospheric scattering for sunset colors
- Runs in a dedicated render pass before terrain

### [UNDECIDED] Dynamic Weather Tie-In

- Day/night affects weather frequency (thunderstorms more common at dusk)
- Weather affects day/night visibility (overcast nights are darker)
- Rain/snow syncs to temperature curve

---

## UI

### [COMMIT] Time Display

- Clock widget in corner shows 24-hour time (configurable)
- Sun/moon indicator shows current celestial position
- Moon phase icon always visible at night
- Temperature reading below clock

### [LIKELY] Calendar

- Persistent calendar showing day count, moon phase, upcoming events
- Some factions gate quests by day (weekly rotations)
- Eclipse predictions shown 3 days in advance (astronomical NPCs sell predictions)

---

## Open Questions

- Real 24-hour clock vs. arbitrary "Day 1, Day 2" numbering?
- Do eclipses need to be announced in advance (narrative buildup) or surprise the player?
- Should sleep be a skip-time option or only a rested-buff option?
- How hard do we lean into the lunar/zodiac lore - gimmick or serious?
- Does the server admin set the start season, or does it rotate?

---

## Dependencies

- PLAN-Infected-AI.md - Night spawning, cryptid appearances, full moon horde
- PLAN-Environment-Hazards.md - Cold shift, temperature zones, mutations
- PLAN-Survival-Needs.md - Fatigue, sleep, warmth
- PLAN-Audio-Design.md - Night ambience, wind, cryptid howls
- PLAN-Vision.md - Overall mood and atmospheric goal
- SpawnDev.ILGPU - Flood-fill lighting updates, dynamic shadow maps
- SpawnDev.BlazorJS - System clock sync for real-time cycles
