# UI, HUD, and Player-Facing Screens - Brainstorm and Plan

**Status:** Living brainstorm. Decisions get locked as features mature.
**Owner:** Captain (TJ)
**Consulted:** Tuvok (research/planning), Data (GameUI editor)
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

The Lost Spawns UI is **diegetic-first**. Every piece of information the player needs lives in the world: a paper map you unfold, a watch on your wrist, a status muttered by your character, the texture of your gear. HUD is the exception, not the rule. When HUD is required, it is minimal, transient, and ignorable by players who want a more immersive experience.

Inspired by Dead Space (HUD on the suit), Metro Exodus (watch + map item), DayZ (no HUD survival info), Subnautica (PDA item for everything), and Skyrim's compass (single thin bar at top is the only persistent UI).

**Design goals:**

1. **Diegetic over abstract.** A wristwatch beats a clock UI. A paper map beats a minimap. Hand status (bleeding, hunger pang) beats a hunger bar.
2. **HUD is opt-in for tutorials, opt-out for veterans.** Toggle every HUD element in settings. Hardcore mode strips HUD to bare minimum.
3. **Built on SpawnScene UI (GPU-rendered).** No HTML overlay. No DOM. Cross-ref [PLAN-Vision.md] technical foundation.
4. **VR-first design, desktop-second.** Every screen designed to work in VR (no menu walls of text, all interactive elements at hand reach).
5. **No clutter.** A new player should never see more than 3 HUD elements at any time without invoking them.
6. **Consistent across modes.** VR / desktop / mobile share a single design language; controls adapt, layout shifts.

---

## Foundation (what exists today)

**Partial:** SpawnDev.GameUI (Data is editor) provides UIElement / UIRenderer / FontAtlas system, GPU-rendered, no HTML overlay. Cross-ref AubsCraft R&D.

**To build:**
- HUD layer (transient overlays)
- Inventory screen
- Paper map item with marking
- Journal (cross-ref [PLAN-Quests-Storyline.md])
- Crafting UI (cross-ref [PLAN-Crafting.md])
- Base build mode UI (cross-ref [PLAN-Base-Building.md])
- Pause / settings menus
- Death screen / respawn UI (cross-ref [PLAN-Death-Corpse-Respawn.md])
- VR-specific interaction layers (hand menus, item examination)

---

## HUD philosophy

### [COMMIT] Minimal persistent HUD by default

Default HUD shows ONLY:

1. **Compass strip** (thin bar at top, 1cm tall, fades when not needed)
2. **Crosshair / interaction reticle** (when weapon drawn or interactable in range)
3. **Active quest objective text** (one line, bottom-left, only if player marked a quest active)

That's it. No health bar, no minimap, no ammo counter, no weapon icon. All other info accessed by item or gesture.

### [LIKELY] Contextual HUD pop-ins

Appear briefly only when the data changes:

- **Health flash** when damaged (red vignette + brief health bar at edge of screen)
- **Stamina drop** when low while sprinting (peripheral vignette, no number)
- **Item pickup toast** (item name + icon, 2-second fade, bottom-right)
- **Region name** when entering new region (top-center, slow fade)
- **Tooltip on look** (interactable name when crosshair hovers, brief)

### [UNDECIDED] Visible ammo count

- Diegetic: open weapon to check magazine, tap chamber to count
- HUD: small number near crosshair when weapon drawn
- Lean: HUD shows current/max for ~2 seconds after firing or reloading, then fades. Toggle off in hardcore.

### [REJECT] Always-on health/hunger/thirst bars

- Hard reject for default HUD. Use diegetic indicators (animation, color tint, audio cues, character voice).
- Available as toggle for accessibility / casual play, OFF by default.

### [REJECT] Floating waypoint markers

- Hard reject. Cross-ref [PLAN-Quests-Storyline.md] no-GPS rule.

### [REJECT] Damage numbers / hit markers

- Hard reject. Hit feedback is impact sound + blood spurt + enemy reaction animation. No "47 damage" floats.

### [REJECT] Killfeed banners

- Hard reject. No "X killed Y with Z" banners. PvP outcomes are felt, not announced.

---

## HUD elements (per-element spec)

### [COMMIT] Compass strip (top-center)

- Thin bar, ~1cm tall, partial-transparent, top edge of screen
- Cardinal directions (N, S, E, W) + intercardinals (NE, NW, SE, SW)
- Custom map markers placed by player show as ticks (cross-ref map below)
- Fades when player has not turned in ~3 seconds; reappears on rotate
- Toggle: ON / FADE / OFF in settings

### [COMMIT] Interaction reticle

- Tiny dot in screen center
- Expands to full crosshair when weapon drawn (cross-ref [PLAN-Combat.md])
- Color shifts when over interactable (item, NPC, door)
- Tooltip text appears below reticle for ~1 second on hover

### [LIKELY] Status icon strip (peripheral, bottom-left)

- Tiny icons that ONLY appear when relevant:
  - Bleeding (red drop)
  - Cold / hot (snowflake / sun)
  - Hungry / thirsty (only at critical, last 20%)
  - Disease (yellow biohazard, late stage)
  - Fracture (white bone)
  - Inebriated (UNDECIDED, wavy lines)
- Maximum 3 visible at once (rotate priority)
- No bars, no numbers, just icon (icon brightness shows urgency)

### [LIKELY] Active quest objective (bottom-left, single line)

- Player chooses one active quest in journal
- Text: "Find the doctor's note in Old Mercy Hospital"
- Updates as objectives complete
- Toggle off in settings for hardcore play

### [UNDECIDED] Subtitle line for proximity dialog

- When NPC speaks, subtitle line at bottom-center
- Toggle: ON for accessibility, OFF default for VR immersion
- Lean: ON default, OFF in VR mode

### [LIKELY] Notification toast (item pickup, status change)

- Bottom-right, ~3 lines max
- Item icon + name + quantity
- Fade after 2 seconds
- Disable for "every grass clip" pickups (only show meaningful items)

### [LIKELY] Damage vignette

- Red edge tint when taking damage
- Intensity scales with damage taken
- Brief health bar visible at top corner only during/right after damage

### [DEFER] Boss health bar

- Cryptid encounters could show boss health bar
- Lean: NO. Cryptid health visible by their stagger / limp / bleed animations
- Defer the decision; current lean is no boss bar

---

## Inventory screen

### [COMMIT] Grid-based with weight + bulk

- Grid layout (DayZ-style, item shapes occupy multiple cells)
- Weight (kg) + bulk (cubic dm) tracked separately
- Weight affects stamina (cross-ref [PLAN-Survival-Needs.md])
- Bulk affects what containers you can fit your gear in

### [COMMIT] Containers expandable

- Backpack, vest, belt pouches each have their own grid
- Click container to expand its grid
- Drag items between containers
- "Hands" slot is a transient grid for current item

### [LIKELY] Item examination

- Right-click / hold-grab in VR: examine item in 3D
- Full description, weight, condition, lore (if any)
- Useful for evidence items, holotapes, found documents
- Cross-ref [PLAN-Quests-Storyline.md] document reading

### [LIKELY] Equipment paper-doll

- Visual character with clothing slots (head, torso, legs, feet, hands, accessories, backpack, belt, vest)
- Drag clothing to slot
- Cross-ref [PLAN-Clothing-Storage.md]

### [LIKELY] Quick-access slots

- 4-8 hotkey slots (numpad / d-pad / VR ring)
- Drag items here for quick equip
- Mostly weapons, healing items, food, tool

### [UNDECIDED] Auto-sort buttons

- Sort by category, weight, value
- Lean: yes, but only as a single "tidy" button, no fancy filters

### [REJECT] Infinite-bag inventory

- Hard reject. Carrying weight + bulk is a core survival mechanic.

### [REJECT] Auto-pickup of all items in radius

- Hard reject. Every pickup is a deliberate player action.

---

## Paper map item

### [COMMIT] Diegetic paper map

- Found in cars, gas stations, ranger stations
- Equip in hand to view (over hand in desktop, in actual hand in VR)
- Static map of the world with named regions, major roads, named landmarks (post-discovery)
- Player position NOT marked by default (cross-ref no-GPS rule)

### [LIKELY] Compass + map combo

- If player has compass equipped, can deduce position from terrain landmarks
- Skilled Survivalist (cross-ref [PLAN-Player-Progression.md]) gets a small position marker accuracy boost

### [LIKELY] Player marking

- Pencil item required to mark map
- Place custom markers (X for cache, circle for danger, etc.)
- Markers persist on the map item; lose the map, lose the markers
- Rare paper-map types include "self-marking" GPS map (high-end loot, cross-ref [PLAN-Combat.md] / military gear)

### [LIKELY] Map detail levels

- Standard paper map: regions, roads, big landmarks
- Survey map (rare loot): topographic contours, smaller landmarks
- Military map (very rare loot): all of the above + faction patrol routes (snapshot, doesn't update)

### [UNDECIDED] Shared map markings

- Squad members can share markers (paper map version of waypoints)
- Lean: yes, but requires both players to be holding map and within voice range

### [REJECT] Auto-marker for quest objectives

- Hard reject. Cross-ref [PLAN-Quests-Storyline.md].

---

## Journal

### [COMMIT] Tabbed journal

- Tabs: Main quests / Side / Faction / Radiant / Lore / Notes
- One quest can be marked active (shown in HUD)
- Each entry: title, objective, hints, NPC quotes, found document text
- Player can add own notes per entry

### [LIKELY] Lore tab archive

- Holotapes listened to, found documents read
- Searchable by title / keyword
- Linked entries (clicking an Aether Group reference jumps to all related entries)

### [LIKELY] Notes tab freeform

- Player writes own notes (in-game text field)
- For solo navigation reminders, faction analysis, base coordinates
- No length limit, but persisted to OPFS

### [UNDECIDED] Audio note recording

- Player records voice memo (using mic) saved as in-game audio note
- Could be played back later, shared with squad
- Lean: defer to post-v1.0, complexity-cost mismatch

### [LIKELY] Quest decision log

- Each major decision recorded ("burned the camp at Mill Creek", "spared the courier")
- Helps players track their choices for late-game arc decisions
- Read-only

---

## Crafting UI

### [COMMIT] Inventory-based crafting (DayZ-style)

- No crafting station for basic crafts
- Drag two items together in inventory to combine (rag + stick = torch)
- Tool requirements shown as tooltip ("requires knife")
- Recipe list available in journal once player has discovered it

### [LIKELY] Discovered-recipes journal page

- Player learns recipes by trying combinations or finding recipe holotapes
- Discovered recipes shown in journal Crafting tab
- Sortable by category (medical, food, weapon, tool)

### [LIKELY] Cooking interface

- At fire / stove: special cook menu
- Drag raw ingredients to slots, choose cook method (boil, fry, smoke, dry)
- Time-based (cooks while you do other things)
- Cross-ref [PLAN-Survival-Needs.md] cooking system

### [LIKELY] Workbench interface

- Workbench unlocks complex recipes
- Drag ingredients + tool + workbench presence = recipe options
- Cross-ref [PLAN-Crafting.md] + [PLAN-Base-Building.md]

### [UNDECIDED] Recipe favorites / shortcuts

- Star a recipe to pin it; one-click craft from inventory if ingredients present
- Lean LIKELY for v1.0

---

## Base build mode UI

### [LIKELY] Build mode toggle

- Hold a build hammer / blueprint tool to enter build mode
- Camera shifts to ghost overlay of buildable elements
- Snap-to-grid for blocks (cross-ref [PLAN-Base-Building.md])
- Cost shown per element (materials required)

### [LIKELY] Blueprint browser

- Library of blueprints (built from prefab system or saved player designs)
- Browse, select, place
- Pack-and-move system (C.A.M.P. style) handled here

### [UNDECIDED] Real-time material check

- Build UI grays out elements you don't have materials for
- Lean LIKELY, with toggle to show all (so player can plan)

---

## Pause and settings menus

### [COMMIT] In-world pause (where possible)

- Solo / private servers: time pauses, world freezes
- Persistent multiplayer servers: pause means you remain vulnerable; UI only freezes the menu
- Settings menu always available; doesn't pause multiplayer

### [LIKELY] Settings menu structure

- Tabs: Display / Audio / Controls / Gameplay / Accessibility / Multiplayer / VR
- VR tab includes height calibration, locomotion mode (smooth / teleport), comfort options (vignette during turn, etc.)
- Gameplay tab includes HUD toggles for every element

### [LIKELY] Difficulty / hardcore mode toggle

- Hardcore: HUD stripped to crosshair only, no quest markers, permadeath enabled
- Casual: full HUD options, no permadeath, easier resources
- Custom: per-toggle settings
- Cross-ref [PLAN-Death-Corpse-Respawn.md]

### [LIKELY] Accessibility options

- Subtitle on/off and size
- Colorblind modes (protanopia, deuteranopia, tritanopia) for inventory category colors
- HUD scale slider
- Aim assist (off by default; available in casual)
- Motion sickness reduction (snap turn, vignette, FOV reduction in VR)
- Keyboard remap, controller remap, VR controller remap

---

## Death and respawn UI

### [LIKELY] Death screen

- Camera detaches from corpse, fade to monochrome
- Brief stat summary (time alive, distance traveled this life, kills, bases built)
- Continue button (respawn options based on cross-ref [PLAN-Death-Corpse-Respawn.md])
- No taunting or scoreboard

### [LIKELY] Respawn map

- Choose respawn location (beach / random / squad / base)
- Live map shows safe spawns and threat overlay
- Some choices have penalty (cross-ref [PLAN-Death-Corpse-Respawn.md])

### [REJECT] Respawn timer minigame

- Hard reject. Death is meaningful enough; no minigame to "skip" it.

---

## VR-specific UI

### [COMMIT] No floating menus

- All UI lives in world: paper map in hand, journal as a notebook, inventory as backpack-on-back
- Settings / pause menu can be a "watch-bound" panel (look at watch -> menu opens)

### [LIKELY] Hand menus

- Press menu button on controller -> radial menu around hand
- Quick access to weapons, healing items, food, tools (8 slots)
- Selectable by direction + trigger

### [LIKELY] Item examination in hand

- Hold item, twist wrist to examine all sides
- Hand-rotation auto-zooms to readable text on found documents
- Holotapes inserted into wrist player

### [LIKELY] Inventory backpack interaction

- Reach behind to grab backpack
- Backpack appears in front, opens like a real bag
- Items can be physically grabbed and placed
- Grid view available as button toggle for non-physical inventory swap

### [LIKELY] Wristwatch UI

- Always on left wrist (or chosen wrist in settings)
- Look at it to see: time, compass, status icons (status icons mirror HUD strip)
- Tap watch face to toggle mini-map / quest objective / off

### [UNDECIDED] Voice command basics

- "Hey crew" -> opens squad chat
- "Note this" -> creates voice memo
- "Map" -> equips paper map
- Lean DEFER, complex to implement well

---

## Mobile / touch UI

### [LIKELY] Simplified HUD with touch buttons

- Virtual joystick (movement)
- Tap screen for look + interact
- Floating action button for use / shoot
- Simplified inventory (smaller grid, touch-friendly)

### [LIKELY] Reduced HUD scope on mobile

- Some VR/desktop systems trimmed for screen real estate
- Fewer simultaneous elements
- Auto-hide non-critical icons after delay

### [DEFER] Mobile-specific gestures

- Swipe to switch weapons, pinch to zoom (already in scope for Pinch-to-zoom-AR)
- Defer detailed mobile gesture spec to mobile UX sprint

---

## Tutorial UI

### [LIKELY] Hint card system

- Brief contextual hints appear (top-right, fade after 5 sec) for first-time actions:
  - "Press Q to open paper map"
  - "Drag items together in inventory to craft"
- Toggle off in settings; never reappear once dismissed
- All hints disabled in hardcore mode

### [LIKELY] In-world tutorial signage

- Cryo Shelter intro has signs / posters teaching basic mechanics ("In Case of Emergency: Find the Map First")
- More immersive than overlay tutorials

### [REJECT] Forced tutorial overlay walls

- No "GAME PAUSED, READ THIS" full-screen overlays. Hint card is enough.

---

## Visual style and theme

### [COMMIT] Diegetic-feeling typography

- HUD font matches in-world signage (used in faction signs, found documents, posters)
- Slight imperfection (paper grain, mild ink bleed) on text
- Consistent across all UI surfaces

### [COMMIT] Muted palette

- HUD elements use the world's muted color palette (no neon, no high-saturation)
- Status icons gray-on-translucent unless critical (then red)
- Quest objective text off-white on black with faint grain

### [LIKELY] Glitch / decay aesthetic

- HUD edges have subtle CRT scanlines / glitch artifacts (sells the post-apocalyptic feel)
- Damage to player causes HUD to glitch briefly (analog signal disruption)
- Cross-ref [PLAN-Vision.md] art direction

### [LIKELY] No transparency on critical alerts

- Status icons that demand attention (critical bleeding, severe disease) become solid color (no transparency) to grab eye

---

## Performance considerations

### [COMMIT] GPU-rendered (SpawnScene UI)

- All UI through GameUI / SpawnScene UI system (cross-ref AubsCraft R&D)
- Zero HTML overlay
- Zero IJSRuntime calls for UI
- VR-friendly (renders into XR layers correctly)

### [LIKELY] Sparse update model

- HUD elements update only when their underlying data changes
- Compass redraws on player rotation; map redraws on item open; inventory redraws on item move
- No 60Hz redraw of static UI

### [LIKELY] Mobile UI scaling

- Auto-detect device DPI and scale UI accordingly
- Larger touch targets on phones
- Cross-ref [PLAN-Vision.md] mobile play style

### [UNDECIDED] UI compression / atlasing

- Font atlas (already on roadmap from compression research)
- Icon atlas to minimize draw calls
- Lean LIKELY, follows GameUI implementation work

---

## Anti-patterns to avoid

### [REJECT] Mystery meat icons

- All UI icons paired with text label (or label appears on hover/look)
- No "what does this icon mean" guessing games

### [REJECT] Modal pop-ups blocking gameplay

- No "your inventory is full!" modal that blocks game until dismissed
- Use toast notification instead

### [REJECT] Carousel menus that auto-advance

- No spinning featured-content carousels in main menu
- Static, predictable, immediate

### [REJECT] FOMO timers in UI

- No "limited time event ends in 14:32:08" countdowns
- Cross-ref [PLAN-Quests-Storyline.md] no daily login quests

### [REJECT] Microtransaction shop in UI

- Hard reject. No shop. Period.

### [REJECT] Tutorial hand-holding past first hour

- Hint card system fades after first use; no permanent tutorial hooks

---

## Gameplay verbs UI enables

- Glance at wristwatch in VR to check time without breaking immersion
- Unfold paper map in hand, find a road bend visible in the distance, pencil-mark current position
- Open backpack physically in VR to swap a fresh magazine into your vest pouch
- See peripheral red vignette and feel hunger pang animation, realize you should eat soon (no hunger bar visible)
- Tap watch face to switch between time / compass / quest objective views
- Open journal, search "Aether" in lore tab, read all related entries chronologically
- Hold a holotape in hand, insert into watch player, listen while continuing to walk
- Mark a custom waypoint on paper map, share with squad mate by handing them the map
- Get hit, see brief health bar appear at screen edge, watch it fade as you bandage
- Examine a found document by twisting wrist in VR, zoom into smudged signature
- Drag rag and stick together in inventory to make a torch (no menu, no UI cluster)
- Build wall in base mode by ghosting it into place, see material cost grayscale (out of materials)
- See region name fade in as you cross from Boreal Forest into Burn Scar East
- Get a 2-line subtitle from a passing NPC who muttered something interesting, decide whether to follow up
- Toggle hardcore mode in settings, watch the HUD strip down to just crosshair

---

## Open questions

1. **Hardcore mode default difficulty** - hardcore gets stripped HUD; what's the default casual baseline? Lean: middle ground (compass + objective + status icons; no health bar).
2. **VR menu binding** - watch-bound vs left-controller-button vs gesture? Lean: watch-bound for system menu, controller button for inventory.
3. **Subtitle defaults** - VR off, desktop on? Or default-on everywhere?
4. **Map sharing in squad** - synced markers vs read-only handoff? Lean: handoff (give your map to squad mate, they have the markers).
5. **Mobile feature parity** - all mobile players get the full feature set, or trimmed for screen size? Lean: full feature set; UI shrinks/scales.
6. **Notification queue limit** - how many simultaneous toasts? Lean: max 3 visible, queue beyond.
7. **Quest objective HUD line on/off default** - on for casual, off for hardcore. Custom default?
8. **Boss cryptid health bar** - visible or invisible? Lean: invisible (read by animation).
9. **AR tabletop UI** - cross-ref [PLAN-Vision.md] AR mode. Separate UI sprint when AR is in scope.
10. **Settings menu in MMO mode** - should pause-menu time stop work in P2P sessions? Lean: no, you remain vulnerable.

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| HUD layer | SpawnDev.GameUI rendering pipeline |
| Inventory grid | Item system + weight/bulk model |
| Paper map | Map item + region discovery state |
| Journal | Quest system (PLAN-Quests-Storyline.md) |
| Crafting UI | Recipe system (PLAN-Crafting.md) |
| Build UI | Base building (PLAN-Base-Building.md) |
| Wristwatch UI (VR) | XR controller + hand-tracking input |
| Mobile UI | Touch input + mobile DPI detection |
| Settings menu | Player preferences persistence |
| Death screen | PLAN-Death-Corpse-Respawn.md |
| Hand menus (VR) | XR controller radial menu primitive |
| GPU font rendering | SpawnDev.GameUI font atlas |
| HUD glitch effects | Post-process pipeline |
| Quest objective HUD line | Quest journal + active-quest selection |
| Status icon strip | Survival + medical + status systems |
| Compass strip | Player rotation tracking |

---

## Next actions

1. Lock the default HUD scope (compass + reticle + active quest only, confirmed?)
2. Lock the watch-bound VR menu pattern (vs controller button)
3. Author the wristwatch model + animation as proof of concept
4. Author the paper map item end-to-end (texture, fold animation in VR, marker placement)
5. Implement inventory grid in GameUI (Data already editor here)
6. Define HUD element data schema (each element: id, default-on, hardcore-on, casual-on, position, scale, fade rules)
7. Sketch journal page layout for review (tabs, search, lore links)
8. Spike VR hand menu radial pattern in GameUI
9. Set HUD performance budget (target: <0.5ms HUD render time per frame at 90 FPS Quest 3S)
10. Cross-plan audit: walk through each existing plan, confirm UI references in this plan match (Quests journal, Map navigation, Crafting drag, Build mode, Death screen, etc.)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
