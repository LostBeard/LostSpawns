# Lost Spawns: VR Controls & Physical Interactions

## Status Legend
- **[COMMIT]** settled design decisions
- **[LIKELY]** strong preference, expect to commit
- **[UNDECIDED]** open, needs discussion
- **[DEFER]** out of scope for 1.0
- **[REJECT]** explicitly not doing

---

## Premise

VR is the headline play mode for Lost Spawns. The Vision doc puts it first: "full-scale 1:1 world. Walk the ruins, loot buildings, hunt deer, defend your base." Desktop and mobile exist to broaden reach; VR exists to deliver the vision.

The constraint that shapes every VR decision: **browser WebXR on Quest 3S**. We are not shipping a PCVR-first game and backporting. We are shipping a Quest-first game and letting PCVR consume it via streaming (see the Vision doc). Quest 3S performance and ergonomic constraints define the baseline.

The opportunity: physical interaction. Grabbing a door handle, nocking an arrow, reaching into your pack - these are what make VR survival games feel alive in a way no flat-screen game can match. We commit to that feel.

---

## Hardware Baseline

**[COMMIT]** Target device: Meta Quest 3S at 72Hz minimum, 90Hz preferred. This is the floor. Older Quest 2 devices get a reduced-fidelity fallback (see PLAN-Performance-Targets for budgets).

**[COMMIT]** Controllers: Touch Plus (Quest 3/3S), Touch Pro, Vive wands, Index Knuckles, WMR wands. Any WebXR controller with trigger + grip + thumbstick + primary/secondary buttons is supported. Detection via WebXR input profiles API.

**[LIKELY]** Hand tracking: supported as an alternative input mode. See "Hand Tracking Mode" section below.

**[COMMIT]** PCVR: played via WebXR on desktop browser through Oculus Link, Steam Link to headset, or Virtual Desktop. Also our own SpawnDev PC-streamed-VR path (see Vision doc). The game does not know or care - it is a WebXR app.

**[LIKELY]** Apple Vision Pro: supported as WebXR immersive-vr endpoint. Input via visionOS pinch+gaze or paired gamepad. Ergonomics differ; see "Vision Pro Specifics" section.

---

## Control Philosophy

### The Three Rules

1. **Anything you can hold in the real world, you should be able to hold in the game.** Weapons, tools, lanterns, maps, fish on a hook. If it has a physical shape, it has a grip point and your hand closes around it.

2. **Diegetic UI wherever possible.** Your inventory is not a menu. It is the backpack on your back. Your map is not a screen. It is a folded piece of paper you unfold in your hands. Your health status is not a HUD bar. It is the way your breath fogs, the tremor in your aim, the blood on your sleeve.

3. **Physical effort should track in-world effort.** Swinging an axe into a tree means swinging your arm. Sprinting means pumping your arms. Heavy loads slow your arm speed. This is not a "realism simulator" - it is a deliberate choice to make the world feel consequential.

### The Three Escape Valves

1. **Comfort options exist for everything.** Physical swing is optional; button-to-swing fallback is first-class, not punished. See PLAN-Accessibility.

2. **Seated play is fully supported.** Nothing requires standing. Nothing requires turning 360 degrees. Everything reachable standing is reachable seated.

3. **Short sessions get short-session ergonomics.** If a player has only 20 minutes, they can play. If they have 3 hours, the systems reward deeper engagement without punishing casual play.

---

## Locomotion

### Movement Modes

**[COMMIT]** Smooth locomotion: thumbstick to move, constant velocity. Default for experienced VR players.

**[COMMIT]** Teleport locomotion: point + trigger to teleport. Default for new VR players, recommended for motion-sick players.

**[LIKELY]** Room-scale blending: physical walk in your play space moves your in-game position 1:1. Combines with smooth or teleport for traversal beyond the play space.

**[LIKELY]** Arm-swing locomotion: swing your arms to walk/run, hands-free direction via head orientation. Good for immersion, tiring over long sessions.

**[UNDECIDED]** Run via pumping handles on a physical device (Cybershoes, KatVR). Not a priority for 1.0 but the protocol supports gamepad-style input so these devices would work out of the box if they expose the WebXR gamepad.

### Turning

**[COMMIT]** Snap turn (30/45/60/90 degrees configurable). Default for motion-sick players.

**[COMMIT]** Smooth turn (speed configurable). Default for experienced players.

**[COMMIT]** Head-relative turning: you can also just physically turn your body. Always works, never disabled.

### Crouch / Prone

**[COMMIT]** Physical crouch: squat in real life, your in-game avatar crouches. Tracked automatically from head height.

**[COMMIT]** Button crouch: for seated players, a dedicated button toggles crouch state. Avatar animates; head height is not faked (does not cause simulator sickness).

**[LIKELY]** Prone: double-tap crouch button OR physical floor pose (detected by head dipping very low + sustained). Most people will not physically go prone; that is fine. Button input is equivalent.

### Climbing

**[LIKELY]** Ladders: grip both hands alternately on rungs to climb. No button, no stick - physical motion.

**[LIKELY]** Ledges: grip a ledge with either hand, pull yourself up. If both hands grip, you are hanging; mantle by thumbstick-forward or swing up.

**[UNDECIDED]** Free-climbing on any climbable surface (rocky walls, broken buildings). Adds traversal options but needs a clear visual affordance for "climbable." Likely 1.0 only for designed climb points, free-climb DEFER.

### Swimming

**[LIKELY]** Breaststroke motion with both hands. Movement vector = hands' forward stroke. Kicking (if tracked, otherwise automatic). Looks goofy from outside but feels correct inside.

**[LIKELY]** Button fallback for seated players.

---

## Weapon Handling

### Universal Grip Rules

**[COMMIT]** Every weapon has at least one grip point. Larger weapons have two (fore-grip + trigger hand). Grip is physical: controller position + grip button hold.

**[COMMIT]** Letting go drops the weapon. No "weapon stays attached." This is deliberate - forces awareness of your hands, adds tension.

**[COMMIT]** Holsters on body: weapon slots on hips (pistol, knife), back (rifle, bow), chest (grenades, flashlight). Reach to the slot, grip, pull out.

**[LIKELY]** Sling: rifles can hang on a physical sling off your shoulder. Different from back-holster - sling-hung weapon is half-drawn, quick-deployable but encumbering.

### Melee

**[COMMIT]** Swing detection based on controller velocity + trajectory. Hit detection uses the weapon's geometry (swept volume between last and current frame).

**[COMMIT]** Heavy weapons (axe, sledgehammer) require a "windup" - the swing must start with controller moving slowly, then accelerating. Prevents flail-spam cheese.

**[LIKELY]** Dual-wielding: two one-handed weapons. Each hand independent. Melee + melee, melee + pistol, etc.

**[LIKELY]** Blocking: hold weapon up to block incoming strikes. Blocked hits drain stamina instead of health (for certain weapon matchups - a spear does not block an axe).

### Firearms

**[COMMIT]** Two-hand required for rifles and shotguns. One-handed rifle use is possible but massively inaccurate (realistic - your elbow starts to drop).

**[COMMIT]** Physical reload: grab magazine from vest pocket, insert into weapon, chamber round (charging handle / slide). Magazine drops to floor or into pocket based on where you release it.

**[LIKELY]** Beginner assist: "auto-reload" mode inserts a fresh mag on trigger click when empty, no physical motion required. For Accessibility and new-player comfort.

**[COMMIT]** Two-stage trigger: Touch Plus / Index Knuckles have analog triggers. Half-pull for aiming-down-sights convergence, full-pull fires. Matches real trigger ergonomics.

**[LIKELY]** Recoil: physical recoil animation on the virtual weapon, not your real controller. Some headsets/controllers have haptic impulses; use them for the feel.

### Bow

**[COMMIT]** Nock arrow: reach to back quiver, grab arrow, bring to bow hand.
**[COMMIT]** Draw: grip bow with off-hand, grip string with main hand, pull back. Draw weight haptic feedback.
**[COMMIT]** Release: release grip button to release arrow. Trajectory includes actual draw length for damage + range.

**[LIKELY]** Spring-assist for players who cannot physically hold a draw: half-pull locks, release is a button press. Preserves the feel without requiring sustained tension.

### Throwing

**[COMMIT]** Grenades, rocks, knives: grip + arm swing + release. Trajectory matches real throw (velocity + angle). Safe pin pull on grenades is a physical pull with other hand before throw.

**[LIKELY]** Throw-back mechanic: you can grab a live grenade and throw it back. Hero moment ready.

---

## Inventory & Interaction

### Backpack

**[COMMIT]** Your backpack is on your back. Reach over your shoulder to access. Pulling it forward opens it into a visible grid floating in front of you.

**[LIKELY]** Grid size scales with pack size (see PLAN-Clothing-Storage). A small pack is a 4x4 grid. A large military pack is 8x10.

**[COMMIT]** Items have physical shapes in the pack. Grab an item by reaching in and pinching (controller trigger). Drag out or snap to a slot.

**[LIKELY]** Auto-stow toggle: grabbing a freshly-looted item could auto-stow in first available slot. For players who find pack management tedious.

### Vest / Pockets

**[COMMIT]** Vest pockets: hip pouches, chest pockets, shoulder strap loops. Each holds specific item types (magazine pouches hold mags, shoulder loop holds a grenade, etc.). Quick-access compared to backpack.

**[LIKELY]** Touch the pocket to see contents (a small preview appears). Reach in to grab.

### Quick-Use Items

**[COMMIT]** Bandage: pull from pocket, apply to wound (touch to the bleeding limb). Animation plays; character applies the bandage.

**[COMMIT]** Food/drink: pull from pocket/pack, bring to mouth. Head proximity triggers consumption animation.

**[COMMIT]** Flashlight: pull from pocket, hold like a flashlight, press button to toggle. Point with your hand.

**[LIKELY]** Two-handed operations: a bandage needs one hand to hold the bandage and one hand to apply. Blood bag needs one hand to hold the bag up, other hand to insert the needle.

### Containers

**[COMMIT]** Looting containers (crates, lockers, backpacks on corpses, cupboards in houses) requires a physical reach + grab. The container opens as a floating grid near you.

**[LIKELY]** Transfer: drag items from container grid to your pack grid. Physical drag, not menu-click.

---

## World Interaction

### Doors

**[COMMIT]** Grab door handle, push/pull physically. No "press button to open." Physically move the door.

**[LIKELY]** Locked doors give haptic resistance when you try to open. Key in lock is a separate two-hand operation.

**[COMMIT]** Kicking doors: charge at the door. Detection is head + body velocity + orientation. Hits are satisfying.

### Windows

**[LIKELY]** Open: grab + slide. Broken: reach through (carefully - shard damage if too fast).

### Climbing Through

**[LIKELY]** Broken windows, gaps in walls: physically duck, turn body, go through. Room-scale-friendly.

### Buttons, Switches, Dials

**[COMMIT]** Physical press with finger (controller tip). Switches toggle physically. Dials rotate with controller wrist twist.

**[LIKELY]** Generators: pull start cord physically. Pump shotguns, crank radios - all physical motions.

### Pickups

**[COMMIT]** Small items (ammo, food, scrap): reach and grab with trigger.
**[COMMIT]** Large items (weapons, tools): reach, grip, drag out.
**[LIKELY]** Auto-pickup toggle for small loot (ammo counts, scraps) - one-press-to-loot for players who find micro-pickups tedious.

---

## Driving & Vehicles

### Cars

**[COMMIT]** Grab steering wheel with both hands. Rotate. Left foot on clutch (grip button on left controller), right foot on gas/brake (trigger on right controller). Shift gears with right hand on shifter.

**[LIKELY]** Automatic transmission option for comfort: no clutch, no gears. Gas + brake + steering only.

**[LIKELY]** Seated-comfort mode: snap-to-steering-wheel, physical grab optional. Thumbsticks fallback for steering if desired.

### Boats

**[LIKELY]** Rowboat: grip both oars, pull back alternately. Physical rowing.
**[LIKELY]** Motorboat: grip tiller, steer.

### Bicycles

**[LIKELY]** Grip handlebars both hands. Leg motion (tracked via controller if controllers held against knees, otherwise automatic) drives pedals.

---

## Combat Specifics in VR

### Aiming Down Sights (ADS)

**[COMMIT]** Bring weapon physically to your face. Scope/iron sights align. Look through. No button toggle.

**[LIKELY]** Virtual iron sights: the weapon model's sights align when your eye is close enough and the weapon is level. Accurate shooting requires a real "aim down sights" motion, not hip-fire.

**[COMMIT]** Scopes: the scope's eye relief is respected. Pull too far, you see around the scope. Correct distance, you see through. Provides natural aim discipline.

### Shooting From Cover

**[LIKELY]** Lean by physically leaning your body. Room-scale players can lean around corners. Seated players get button-lean (left/right on right thumbstick + crouch, or equivalent).

**[LIKELY]** Peek: bring only your weapon + one eye around cover. Physical motion. Risk model: your hand is exposed, head is not, but a skilled shooter can still wound your hand.

### Reloading Under Pressure

**[COMMIT]** Physical reload takes physical time. A trained player can reload a Glock in ~2 seconds; a new player takes ~5. This is skill-based gameplay - muscle memory matters.

**[LIKELY]** Fumble: dropping a magazine mid-reload costs you time to pick it back up. Haptic feedback on bad inserts.

### Hit Feedback

**[COMMIT]** Being hit: controller haptics pulse on the struck side. Visual red vignette. Audible grunt. Directional indicator on HUD (see PLAN-UI-HUD for VR HUD rules).

**[LIKELY]** Stagger: heavy hits push your camera slightly (momentary, not sustained - sustained camera push causes sim sickness).

---

## Physical Inventory Flow (Example Scene)

Player enters a house:
1. Grip door handle, push door open
2. Walk in (room-scale or smooth)
3. See a bottle of water on a counter
4. Reach out, pinch to grab, bring to mouth
5. Head proximity triggers drinking animation, thirst meter fills
6. Drop bottle (ungrip) - it falls to floor, physics-driven
7. Open a cupboard door (grip + pull)
8. See canned food, a rusty knife, a map
9. Grab the map, pinch to open. Two-handed unfold. Look at it, memorize landmark.
10. Fold map (bring hands together), stow in chest pocket
11. Grab can of food with one hand, reach behind to backpack with other hand
12. Slide can into an open slot in the pack grid
13. Hear a noise outside
14. Reach over shoulder, grab rifle, bring down to shoulder ready position
15. Crouch (physical squat) and move toward window
16. Peek through window, ADS, see a stranger

None of that used a menu. None of it required a button press except grip triggers. This is the baseline VR experience we are designing for.

---

## Hand Tracking Mode

**[LIKELY]** Hand tracking is supported as an alternative to controllers. Most interactions translate directly:

- Pinch = grab/trigger
- Point = select (for UI + weapon-aiming gestures)
- Fist = grip (weapons)
- Flat hand = stop / hold still (accepted as "cancel" in dialogs)

**[LIKELY]** Combat in hand-tracking mode is less precise but still functional. Melee feels great hand-tracked. Guns feel worse (no tactile trigger); we provide a "gun gesture" shortcut for aim-with-index-finger-extended.

**[UNDECIDED]** Hybrid hand+controller mode. Quest 3 can pass through one real hand next to the other controller. Experimental, see if it feels good.

**[DEFER]** Full finger-tracking with gloves (Index Knuckles capacitance, specialized gloves). Supported at the input level but no specific gameplay feature depends on it.

---

## Accessibility Tie-In

See PLAN-Accessibility for the full accessibility design. VR-specific accessibility items:

**[COMMIT]** Every physical motion has a button alternative. Grip, swing, reach, throw - all accessible via controller buttons alone for players who cannot do the physical motion.

**[COMMIT]** Seated mode is a first-class configuration, not a fallback. All interactions reachable seated.

**[COMMIT]** One-handed mode: all critical interactions can be done with a single controller. Two-handed weapons switch to one-handed aim-assist mode. Map unfolds via button. Reload is automatic.

**[COMMIT]** Height adjustment: avatar scales to real-world height. Short players can reach normal-height objects; tall players do not feel like the world is a dollhouse.

**[COMMIT]** Comfort options: vignetting during smooth locomotion, comfort-turn, fog-in on fast motion. Off by default for motion-tolerant players, toggleable.

**[LIKELY]** Motion sickness "quit safely" button: a grip + grip + A combo (any two controllers + face button) immediately teleports to a safe zone and pauses. No menu to navigate when you feel unwell.

---

## Vision Pro Specifics

**[LIKELY]** visionOS supports WebXR immersive-vr. Input differs from Quest:
- No controllers by default (can be paired)
- Gaze + pinch as the primary interaction
- Hand tracking is always on

**[LIKELY]** Design adjustments for Vision Pro:
- Gaze-based targeting for UI (auto-select what you look at, pinch to confirm)
- Pinch gestures replace grip for most interactions
- Combat requires paired controllers OR hand-tracking melee-only mode (gun gameplay is not great without a trigger)

**[LIKELY]** Vision Pro is a lean-back viewing experience more than a lean-in action experience. Combat-heavy gameplay may not land. Exploration, base management, trading, NPC dialog - these play well on Vision Pro.

**[UNDECIDED]** Do we ship a Vision-Pro-optimized UI scale? Text rendering on Vision Pro is sharper than Quest; we can use smaller fonts. Needs testing.

---

## Tabletop AR Mode (Quest Passthrough)

This is the god-mode view from the Vision doc. Editor mode also lives here.

**[LIKELY]** Passthrough on, virtual diorama of the world on your real table. You look down at a miniature version.

**[LIKELY]** Controls:
- Pinch on a block to select it
- Drag to move
- Pinch-and-spread to zoom / scale
- Two-finger tap to rotate the diorama
- Hover over a player avatar to see their info

**[LIKELY]** Use cases:
- Pre-plan raids on an enemy base
- Monitor your own base while someone else does the fighting
- World editing (terrain sculpt, structure placement)
- Collaborative editing with other AR peers (see PLAN-Editor-Collaborative - not yet written, Tuvok TODO)

**[LIKELY]** Toggle between first-person immersive and tabletop AR with a dedicated button. Smooth transition - the world scales up around you or down in front of you.

---

## PC-Streamed VR

See Vision doc for the full pitch. This is our own streaming path, not Oculus Link or Virtual Desktop.

**[LIKELY]** Desktop browser does all the rendering at desktop-grade quality. Encoded H.264/H.265 frames streamed over WebRTC to Quest. Quest decodes and displays. Controller input streams back.

**[LIKELY]** Latency target: under 60ms motion-to-photon including stream. Predictive pose compensation helps.

**[LIKELY]** QR-code pairing: scan QR on PC screen from Quest, auto-connects the two.

**[LIKELY]** This lets Quest users enjoy PC-grade fidelity (better lighting, higher draw distance, more NPCs) without buying a PC connection cable. Over a local wifi it is nearly indistinguishable from PCVR.

---

## Hand Pose & Avatar

**[COMMIT]** Your avatar has visible hands that match controller/hand tracking data. Other players see your hands posed correctly - pointing, gripping, open, fist.

**[COMMIT]** Gestures are visible to other players for social signaling:
- Wave hello
- Point
- Thumbs up / thumbs down
- Salute
- Middle finger (yes, it is in)

**[LIKELY]** Emotes: button combo (left thumbstick + gesture selection) plays canned emotes (dance, sit, lay down, surrender hands-up).

**[LIKELY]** Dynamic hand pose: holding a water bottle, your avatar's hand wraps around the bottle. Holding a knife, fingers curl around the grip. Looks right to peers watching you.

---

## VR Voice Chat

See PLAN-Networking-Multiplayer for the networking. VR-specific voice layer:

**[COMMIT]** Mic default push-to-talk: click left thumbstick to transmit. Mic icon appears on your avatar's mouth when active.

**[LIKELY]** Spatial audio: voice from peers is positioned in 3D where they are. You can localize a speaker by ear. Critical for tactical play.

**[LIKELY]** Megaphone item: hold to mouth + voice chat active = amplified voice range. In-world effect, not a separate channel.

**[LIKELY]** Radio item: hold to ear + voice chat active on a tuned frequency. Audio is radio-compressed-sounding (low-pass filter + static) for atmosphere.

---

## Performance Targets for VR

**[LIKELY]** 72Hz minimum, 90Hz target on Quest 3S. Foveated rendering enabled. Fixed-foveation with dynamic boost based on gaze (Quest Pro has eye tracking; use it when available).

**[LIKELY]** Render resolution per eye: 1920x1920 target (upscaled from a lower internal resolution if needed). Internal scale adjusts based on GPU headroom.

**[LIKELY]** Frame timing budget: 11.1ms at 90Hz. CPU + GPU must fit. Budget breakdown lives in PLAN-Performance-Targets (not yet written).

**[LIKELY]** ASW / Space Warp support: Quest's motion-smoothing fallback for frame-rate dips. Enabled by default - a 45fps game looks OK with ASW; without it, it looks terrible.

---

## Testing Strategy

**[COMMIT]** VR testing happens in a real headset. Emulated VR in a flat-screen browser is useful for code testing but cannot catch comfort/sim-sickness issues. TJ + Aubs will playtest each feature in Quest before it ships.

**[LIKELY]** Automated testing: headless-browser WebXR API can simulate controller input for interaction tests. Combat logic, inventory logic, door logic can all be tested without a real headset.

**[LIKELY]** Comfort QA: every new locomotion option goes through a 30-minute continuous playtest by a sim-sick-prone tester (Nikki has volunteered). If it makes her feel sick, it does not ship enabled by default.

**[LIKELY]** Cross-device QA: Quest 3S, Quest 2, Pico 4 (Android-side), Vision Pro, Index (PCVR via WebXR), WMR. Any device with a public dev-kit opportunity.

---

## Deliverables for 1.0

1. Smooth + teleport locomotion with comfort options
2. Snap + smooth turning
3. Physical weapon handling (melee, firearms, bow, throwing)
4. Backpack + vest pocket inventory
5. Physical door / window / container interaction
6. Vehicle driving (car, boat, bicycle)
7. Hand tracking as alt input
8. Seated + one-handed modes
9. Tabletop AR mode (toggle)
10. PC-streamed VR (internal, not third-party)
11. Spatial voice chat
12. Visible hand pose + emotes
13. Quest 3S / 3 first-party; Quest 2 reduced-fidelity; Vision Pro exploration-focused

---

## Open Questions

**[UNDECIDED]** Haptic gloves (bHaptics, Valve Index Knuckles, future Meta haptic-vest products) as optional finer feedback. Protocol-level input is fine; gameplay beyond what controllers already give is speculative.

**[UNDECIDED]** Full-body tracking. Vive trackers, SlimeVR, Index tracking. Nice for avatar immersion. DEFER to post-1.0 but design the avatar system to accept extra tracking inputs.

**[UNDECIDED]** Eye tracking beyond foveated rendering. Quest Pro + Vision Pro support it. Gameplay uses: enemy-AI-notices-where-you-are-looking, dialog UI that follows gaze, etc. Small but neat.

**[UNDECIDED]** Voice command input (a la Alyx's gravity glove voice commands). Speech-to-text is cheap now. Could add natural-language shortcuts ("grab my rifle" instead of reaching). Comfort win for some, annoying for others.

---

## Relationship to Other Plans

- **PLAN-Vision** - VR is the headline mode; this plan fills in the specifics
- **PLAN-Accessibility** - all physical interactions have button fallbacks; seated + one-handed modes are first-class
- **PLAN-Combat** - weapon handling specifics here, combat balance in Combat plan
- **PLAN-UI-HUD** - VR-specific HUD rules; no flat overlays, all diegetic
- **PLAN-Networking-Multiplayer** - spatial voice, gesture networking
- **PLAN-Clothing-Storage** - backpack + vest integrate with grid-in-3D
- **PLAN-Performance-Targets** (not yet written) - frame budget breakdowns for Quest 3S
- **PLAN-Editor-Collaborative** (not yet written, Tuvok TODO) - tabletop AR mode specifics
