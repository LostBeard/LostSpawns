# PLAN - Accessibility

**Status:** Design draft, 2026-04-17 (Tuvok)
**Owner:** Captain (LostBeard)
**Audience:** Everyone. This plan touches UI, audio, input, gameplay, and narrative teams.

---

## Purpose

Lost Spawns is built on strong design choices - no HUD, diegetic UI, ambient audio storytelling, no quest markers - that, left unchecked, can exclude disabled players. This plan defines how we preserve the game's design identity while making it playable for as many people as possible.

Accessibility is not a feature set bolted on at the end. It is a parallel design track that runs next to every system. Writing this plan now, while most systems are still in design, means accessibility can be built in rather than retrofitted.

## The Pitch

A blind player using a screen reader should be able to experience the world through audio cues, directional radio, NPC conversation, and haptic feedback. A deaf player should never miss a critical sound cue - every infected growl, every gunshot direction, every thunderstorm should have a visual counterpart available as an option. A player with limited motor function should be able to hold-to-interact instead of rapid-tapping, remap any control, and scale the difficulty to match their capacity. A colorblind player should see every UI state correctly. A dyslexic player should read holotapes without fighting the font.

**Accessibility is how we respect every player the Captain built this for.** Not an afterthought. Not a checkbox for a store page. A design principle.

---

## Design principles

### [COMMIT] Options, not forced changes

The default experience is the Captain's design vision. Accessibility features are individual opt-ins that a specific player can enable. A player who wants the harsh, unadorned world gets it. A player who needs color-correction, subtitles, or hold-to-interact enables those for themselves without affecting anyone else or dumbing down the shared world.

**Why:** Removing hard design choices ("add a minimap," "show quest markers") to serve accessibility would make the game worse for the design's target audience AND would not solve the underlying accessibility problem. Options that preserve the design while enabling different players is the correct answer.

### [COMMIT] Never require a single sense

If information is delivered through a single channel (audio only, visual only, motor only), that information must have an equivalent alternate channel available as an option. Every critical audio cue has a visual option. Every critical visual cue has an audio option. Every timed interaction has a hold-instead option.

### [COMMIT] Accessibility is tested by disabled players

We will not ship an accessibility pass that has only been tested by sighted/hearing/non-motor-impaired developers. Before any accessibility-relevant release, recruit testers who actually use these features as a daily necessity. Their feedback is the only feedback that matters.

### [COMMIT] Respect the player's autonomy

No "god mode," no "story mode," no patronizing labels. A player who enables hold-to-interact is not playing an "easy" version. A player who uses directional audio cues is not being handed the game. Accessibility options are described by what they DO, not by the player demographic they serve.

### [LIKELY] Settings preset bundles

In addition to granular toggles, provide preset bundles: "Low Vision," "Hearing Impaired," "Motor-Limited," "Cognitive-Friendly," "All Accessibility On." New players pick a preset, then fine-tune. Veterans can ignore presets.

---

## Visual accessibility

### [COMMIT] Colorblind modes

- **Deuteranopia mode** (red-green)
- **Protanopia mode** (red-green)
- **Tritanopia mode** (blue-yellow)
- **Grayscale mode** (for checking contrast-only design)

Modes affect UI elements only, not world rendering (world must look natural). Every colored UI state has a secondary discriminator (shape, pattern, position) so color is never the sole information carrier.

### [COMMIT] High-contrast UI mode

Option to render all HUD/menu text on high-contrast backgrounds. Important for low-vision players. Bold outlines, thicker strokes, increased spacing.

### [COMMIT] Text scaling

Independent scale sliders for:
- **Menu text** (25% to 300%)
- **In-world text** (signs, holotape transcripts, terminal output)
- **Subtitle/caption text**
- **NPC dialog text**

### [COMMIT] Dyslexia-friendly font option

Built-in alternate font (OpenDyslexic or equivalent) selectable for all in-game text. Optional bionic-reading style (bold-first-letters) as a separate toggle.

### [COMMIT] UI opacity and background

User-controlled opacity on all UI backgrounds. Low-vision players often need opaque backgrounds to read text; immersive players want transparent. Give both.

### [LIKELY] Screen-reader support

Full screen-reader compatibility for menus, inventory, journal, map, radio dial. In-game narrator mode (TTS) that reads found documents, holotape text, NPC dialog on demand.

**Why:** Blind players CAN play open-world survival games. The Last of Us Part II proved it. The question is whether we can support the world-feel of Lost Spawns through audio depth. The answer is mostly yes - direction, distance, and identity of every significant sound source is already part of the design. Cross-ref PLAN-Audio-Design.md.

### [LIKELY] Visual cue for audio events

Optional overlays for critical audio:
- **Directional damage indicators** (arrow showing where damage came from, toggleable)
- **Gunshot direction indicators**
- **Infected audio direction indicators** (subtle visual ping at screen edge)
- **Thunderstorm visual warning** (lightning flash always visible; if thunder audio off)
- **Wildlife proximity indicators** (when a bear is near but silent in thick cover)

These are OFF by default. Players opt in. Preserves the default design.

### [COMMIT] Camera motion and motion sickness

- Independent FOV slider (60-120)
- Motion blur toggle
- Camera shake toggle (separate sliders for weapon recoil, explosion, walking bob)
- Chromatic aberration toggle
- Film grain toggle
- Head bob toggle (footstep camera motion)
- Vignette toggle

Motion sickness is a real accessibility issue, not a matter of taste. All options per-player.

### [LIKELY] Lighting accessibility

- **Gamma slider** (standard)
- **Low-light boost** (limited night-vision that works via UI brightening rather than in-world vision enhancement; optional)
- **Flash-reduction mode** (dampens lightning, explosions, muzzle flashes for photosensitive players)

---

## Hearing accessibility

### [COMMIT] Subtitles and captions

- **Subtitles** for all spoken dialog, holotapes, NPC voice lines
- **Captions** for significant sound effects (gunshot, infected growl, glass breaking, thunder)
- Subtitle speaker identification
- Subtitle positioning options (top, bottom, left, right)
- Subtitle background/contrast options (per visual accessibility)
- Subtitle size scaling (per visual accessibility)
- Caption filtering (all, critical only, gameplay-relevant only)

Subtitles and captions are never disabled by default. Players opt OUT if they don't want them.

### [COMMIT] Visual directional audio indicators

For players with hearing loss or deafness:
- **Sound direction radar** (compass-edge indicators showing direction of each significant sound within hearing range)
- **Sound-source distance** (indicator size scales with distance)
- **Sound-source type** (icon differentiation: gunshot vs growl vs footstep vs thunder)

OFF by default. Deaf players opt in and gain parity with hearing players on sound-based threats.

### [COMMIT] Sound mix controls

- **Music volume**
- **SFX volume**
- **Ambient volume**
- **Voice/dialog volume**
- **Radio volume**
- **Weather volume**
- **UI volume**

Independent sliders. Enables players with partial hearing to emphasize what they can hear.

### [LIKELY] Mono audio mode

Collapses stereo to mono (useful for players with single-sided hearing loss). All positional information still conveyed via the visual directional audio indicators.

### [COMMIT] No audio-only puzzles

No puzzle in the game requires the player to hear a specific sound to solve. Every audio cue required for progress has a visual or textual equivalent. If a holotape contains a coded message in the audio (morse code etc.), the code is also delivered visually.

### [LIKELY] Haptic feedback

For players using compatible controllers (PS5 DualSense, Xbox, or haptic-capable PC peripherals), critical audio events trigger controller haptics. Gunshot = haptic pulse. Infected growl = rumble. Footstep direction = directional vibration. This provides a third channel alongside audio and visual.

---

## Motor accessibility

### [COMMIT] Full input remapping

Every key, button, and axis is fully remappable. No hardcoded inputs. Multi-button actions can be rebound to single buttons. Modifier keys can be reassigned.

### [COMMIT] Hold-vs-toggle options

Every "hold to do X" interaction has a toggle alternative. Crouch, aim-down-sights, sprint, carry-heavy-object, interact-with-container, sneak, block. Players who cannot physically hold buttons get toggles.

### [COMMIT] No rapid-input requirements

- No quick-time events (already REJECTED in other plans)
- No mash-to-break-grapple
- No hold-and-rotate puzzles

The player should never lose progress because they physically cannot press a button fast enough. Any gameplay that looks like it might require rapid input is redesigned.

### [COMMIT] Aim assist

Optional aim assist with adjustable strength (0-100% slider). Not cheating - many players have reduced precision from motor impairments. Aim assist IS disabled in PvP by default (competitive fairness) but enabled in PvE by player choice.

### [LIKELY] One-handed play support

- Full controller remapping to allow one-handed play
- PC: mouse-only or keyboard-only modes
- Specific quest-giver/vendor interactions that might otherwise require two-hand combos are redesigned to work with single-input alternatives

### [LIKELY] Adaptive controller support

- Xbox Adaptive Controller compatibility
- Custom input profiles exportable/importable (so a disability-accessible settings community can share configurations)

### [COMMIT] Difficulty scaling

Independent sliders for:
- **Enemy damage taken** (how much HP infected/cryptids have)
- **Enemy damage dealt** (how hard they hit the player)
- **Resource scarcity** (how much food/water/ammo spawns)
- **Timing pressure** (how fast status effects degrade)

A player with motor limitations may want slower status degradation (more time to react). A player who wants a more punishing game cranks scarcity. These are independent of accessibility - they're also gameplay customization.

### [LIKELY] Auto-actions

Optional:
- Auto-loot nearby items
- Auto-reload when out of ammo
- Auto-heal at low HP if med items present
- Auto-drink when water available and thirsty
- Auto-climb standard obstacles

Each independently toggleable. Default OFF. These reduce button-press frequency for motor-impaired players.

### [REJECT] "Accessibility easy mode" bundle

Do NOT package motor accessibility with difficulty reduction. A motor-impaired player might want HARD gameplay. Decoupling accessibility and difficulty is correct design.

---

## Cognitive accessibility

### [COMMIT] Tutorial and onboarding options

- **Full tutorial** (new-player friendly guided intro with tooltips)
- **Minimal tutorial** (tooltips disabled, world reveals itself)
- **No tutorial** (experienced survivors start unassisted)

Player choice at start. Retroactively toggleable in settings.

### [COMMIT] Pace controls

- **Pause works in single-player** (tutorial, menu, inventory pause the world)
- **Slower time-of-day option** (cross-ref PLAN-Day-Night-Cycle.md - default is 1:48 real:game; slower is a multiplier)
- **Slower survival tick** (cross-ref PLAN-Survival-Needs.md - hunger/thirst degrade more slowly)

### [LIKELY] Optional simplifications

- **Auto-sort inventory** (on/off)
- **Auto-categorize items** (on/off)
- **Persistent quest journal markers** (marks last-known locations of NPCs mentioned in quests; off by default, preserves no-waypoints design, on for players who cannot hold spatial info reliably)
- **Recipe-memory** (game remembers crafting recipes once seen; off by default keeps the found-a-recipe-book moment; on for cognitive load reduction)

### [COMMIT] Readable text

All text is readable in-context. No micro-text. No text cluttered over moving backgrounds. Minimum contrast ratios meet WCAG AA (4.5:1 for body, 3:1 for large text).

### [LIKELY] Audio description option

Optional TTS narration that describes on-screen visual events for low-vision or newly-learning players. "A deer has entered the clearing. Wind is from the north." This is separate from screen reader and optional.

### [COMMIT] Difficulty of social mechanics

- **Simplified dialog trees** (longer dialog options shown as short labels for quick scanning)
- **Dialog history log** (review what was said; on by default, some players need to re-read)
- **Slow dialog advance** (text-scroll speed adjustable, instant-show option)
- **Auto-advance dialog** (reads-aloud and advances on its own, for hands-free play)

---

## Input variety

### [COMMIT] Keyboard + mouse

First-class. Full rebinding. Customizable mouse sensitivity (horizontal, vertical, ADS, mouse-wheel). DPI-aware.

### [COMMIT] Gamepad

First-class. Xbox, PlayStation, Switch Pro, generic. Full rebinding. Stick sensitivity, dead-zones, response curves all adjustable. Gyro-aiming support (motion-controlled fine aim).

### [LIKELY] Touch

For potential mobile/tablet port. Not a v1.0 target but don't write UI that can't scale to touch.

### [COMMIT] VR

First-class. Cross-ref [PLAN-VR-Controls.md - NOT YET WRITTEN, Tuvok TODO]. VR has its own accessibility concerns (comfort options, seated play, standing play, teleport movement, smooth movement, vignette-for-motion-sickness).

### [COMMIT] Adaptive controllers

Xbox Adaptive Controller, Tobii eye-tracking, Quadstick, Azeron. We don't ship custom drivers - we support the APIs these devices expose through Windows/Xbox/SteamInput.

---

## Narrative accessibility

### [COMMIT] Content warnings

At game start, player chooses what content warnings they want displayed. Lost Spawns contains: body horror, violence, child endangerment (environmental only, no in-game child harm), religious fanaticism, isolation themes, suicide references (in found holotapes). Content warning system lets players see which quests/locations contain which themes before entering.

### [LIKELY] Skippable content

- **Flickering/strobing warnings** (with ability to skip scenes containing strobes entirely)
- **Extended psychological horror scenes** (skippable, story continues as if witnessed)

### [COMMIT] Safe-word / panic options

If the player enables the "distress signal" option, a specific input instantly pauses the game and brings up a calm blank screen with player-chosen reminder text ("you're okay, breathe"). Real-world accessibility for players with PTSD or panic disorders. Zero gameplay consequence.

### [REJECT] Censoring of core content

The world Lost Spawns depicts is dark. The Cascade killed a civilization. The themes exist and writers should not self-censor to avoid potentially uncomfortable content. Accessibility here is giving players informed consent and exit ramps, not sanitizing the world.

---

## Social / multiplayer accessibility

### [COMMIT] Text chat alternatives

Cross-ref PLAN-Factions-Squads.md. Players who cannot type quickly or cannot use voice get:
- **Pre-written messages** (full library of common phrases, one-click send)
- **Emote wheel** (already planned, expand)
- **Location pings** (already planned, critical for coordination without text)

### [LIKELY] Voice-to-text

If VoIP is enabled in squads, an opt-in voice-to-text transcript stream so deaf squadmates can read what teammates are saying.

### [LIKELY] Text-to-voice

Opt-in reverse: mute players can type and have it TTS'd to squadmates.

### [COMMIT] Mute and block

Full mute and block at player level. Reports route to server admins (cross-ref PLAN-P2P-Reputation-System.md).

### [COMMIT] Language support

UI localization at minimum: English, Spanish, French, German, Japanese, Korean, Chinese (Simplified), Portuguese, Russian. Subtitle language independent of UI language. Audio dub languages TBD based on budget.

### [LIKELY] ASL/deaf community awareness

In-world: a small percentage of NPCs can be deaf or hard-of-hearing, with signed dialog. This is narrative representation, not accessibility feature per se, but it matters. A deaf player seeing themselves reflected in the world matters.

---

## Testing and review

### [COMMIT] Recruit disabled testers early and often

- Engage with AbleGamers, SpecialEffect, The Game Accessibility Conference network
- Recruit testers in at minimum: blindness/low vision, deafness/hard of hearing, motor impairment (spinal cord injury, muscular dystrophy, cerebral palsy), cognitive impairment (autism spectrum, ADHD, dyslexia), photosensitivity

### [COMMIT] Accessibility audit before every major release

Not just v1.0. Every content update that adds new gameplay systems needs an accessibility pass. Treating accessibility as ongoing, not one-shot, prevents regressions.

### [LIKELY] Published accessibility report

Be transparent about what Lost Spawns supports and what it doesn't. Many players evaluate a game's accessibility before purchase. A public accessibility checklist (in the style of Can I Play That) on the game's page, updated with each release, lets players make informed choices.

---

## Dependencies and cross-references

| Plan | How this plan relates |
|---|---|
| [PLAN-UI-HUD.md](PLAN-UI-HUD.md) | All UI options (scale, contrast, colorblind) apply to HUD and menus |
| [PLAN-Audio-Design.md](PLAN-Audio-Design.md) | Captions, mono audio, directional indicators |
| [PLAN-Combat.md](PLAN-Combat.md) | Aim assist, difficulty scaling, input alternatives |
| [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md) | Pace controls, slower degradation options |
| [PLAN-Vision.md](PLAN-Vision.md) | Low-light boost, flash reduction |
| [PLAN-Weather.md](PLAN-Weather.md) | Visual cues for audio weather events |
| [PLAN-Infected-AI.md](PLAN-Infected-AI.md) | Audio direction indicators (infected threat) |
| [PLAN-Day-Night-Cycle.md](PLAN-Day-Night-Cycle.md) | Slower time-of-day option |
| [PLAN-Quests-Storyline.md](PLAN-Quests-Storyline.md) | Quest journal marker option, content warnings |
| [PLAN-Factions-Squads.md](PLAN-Factions-Squads.md) | Text chat alternatives, pings |
| [PLAN-Radio-Comms.md](PLAN-Radio-Comms.md) | Radio subtitles, voice-to-text in radio |
| [PLAN-P2P-Reputation-System.md](PLAN-P2P-Reputation-System.md) | Mute/block infrastructure |
| [PLAN-VR-Controls.md - NOT YET WRITTEN](PLAN-VR-Controls.md) | VR-specific comfort and accessibility |
| [PLAN-Player-Progression.md](PLAN-Player-Progression.md) | Skill-based difficulty scaling interaction |

---

## Open Questions (Captain's call required)

1. **Screen reader support - in for v1.0?** (proposal: yes, full UI + TTS for found documents)
2. **Directional visual audio indicators - default behavior?** (proposal: off by default, prominent in accessibility menu)
3. **Aim assist in PvP - ever?** (proposal: no for competitive; yes for PvE, with clear visual indicator on PvP enter)
4. **Slower survival tick option - how much slower?** (proposal: 0.25x to 2x multiplier, player choice)
5. **Auto-actions - default off, or preset-recommended?** (proposal: all default off, surfaced in presets)
6. **Content warning granularity** (proposal: category-level, not quest-level, to avoid spoilers)
7. **ASL NPCs** (proposal: 2-4 NPCs across world, not tokenized; hire a consultant)
8. **Safe-word/panic input** (proposal: yes, unreservedly)
9. **Published accessibility report** (proposal: yes, following Can I Play That checklist)
10. **Accessibility tester compensation** (they are QA, compensate them at QA rate minimum)

---

## Writer's and designer's reference

### Things to never do

- **Never use color as the sole discriminator.** Red-green faction badges without shape/pattern discrimination exclude the colorblind.
- **Never require a specific audio cue for progress.** Unless the audio has a visual equivalent.
- **Never require rapid input.** Hold alternatives exist.
- **Never write critical information in tiny text.** Minimum font sizes apply to all in-world text.
- **Never embed accessibility settings behind a progress gate.** A new player must be able to access colorblind mode before starting.
- **Never make a disability a joke.** Even NPC dialog about characters with disabilities is written with dignity.
- **Never use scrolling-text-that-times-out for critical info.** Text stays until dismissed by player input.

### Things to always do

- **Always provide alternatives.** Every critical sense-channel has a substitute.
- **Always design with accessibility in mind from the start.** Retrofitting is harder than designing-in.
- **Always test with real users.** Our own assumptions are not accessibility.
- **Always expose player choice.** Opt-in options preserve both the design and the player's access.
- **Always credit disabled testers in game credits.** They helped build it.

---

## Style notes

- **Language in settings menus is literal and specific.** "Enable visual indicator when enemies attack from off-screen" is clearer than "Combat help."
- **No euphemisms for disability.** "Colorblind mode" is fine. "Visual-difference-friendly mode" is not.
- **Presets can have evocative names** (e.g., "Low Vision") but individual toggles stay functional.

---

_End of plan. This plan is not a product feature. It is how we respect players. Captain: please review and sign off before any system designer considers accessibility "out of scope." It is never out of scope._
