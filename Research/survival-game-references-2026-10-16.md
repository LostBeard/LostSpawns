# Survival game references (2026-10-16)

TJ's direction after trying the VR build: "DayZ voxel, less Minecraft" - a survival game, not a hand-holding one; no tags that show through walls; look at DayZ, Rust, Strayed (a Rust-like VR game) and others. This is what a first pass of reading turned up (thin in places: searches returned little on exact HUD details) and what it means for Lost Spawns. Sources are listed at the end; claims marked (wiki) come from community wikis.

## What the references do

**DayZ**
- Loss is the core of it: die and you lose everything; Dean Hall kept hardcore because loss gives weight and makes players value the character. No micro-transactions that buy survival. "No interference with the player's playstyle." Emergent encounters over scripted ones.
- Almost no UI telling you about the world: no minimap, no kill feed, no name tags; ears carry the information (audio is load-bearing).
- Vitals are a few small icons in a corner (hydration, hunger, temperature, blood, health), white when fine, yellow when moderate, red when critical, with extra icons for sickness, wetness, bleeding. Eating and drinking do not update them at once.

**Rust**
- Hunger, thirst, health, cold (cold speeds hunger drain; dehydration costs health and speed). Comfort items (campfire, furnace, chair, rug) speed healing and slow hunger. Bases and raiding are central; servers wipe on a schedule.
- Hardcore mode strips the HUD: no compass, no player marker on the map unless you carry a compass item (and then no heading), fog of war that clears as you explore.
- Respawn at a placed sleeping bag (cloth gathered from plants); you start with a rock and a torch.

**Strayed (VR, Rust-like, 100 player servers, weekly wipes)** (wiki)
- Stick click opens a hot bar wheel; a backpack icon opens the grid; grip picks up ground loot; trigger drags items from crates; B quick-transfers and auto-stacks. Map on the off-hand trigger. Deployables: hand to the spot, trigger; blue = valid, red = invalid. Melee is the arm swing, a bow is nocked and drawn with the hands, guns take a magazine from the shoulder and are racked by hand. Teleport or smooth locomotion, snap or smooth turning, all optional.

**The Walking Dead: Saints & Sinners (VR)** (reviews)
- Body-anchored storage works: backpack on one shoulder, two-handed weapon on the other, flashlight and notebook on the chest, one-handed weapons on the hips; about nine in ten actions felt intuitive; items snap to the hand from a distance with a rumble. Weapons and objects have weight; junk as backup weapons was liked.
- Grid-menu inventories are slow with controllers and bad under pressure; holster grab zones were finicky; button crouching felt artificial (real crouching is better).

## What it means here

Done already (this session): no creature tags (switch kept for an admin view, `?tags=1`), nothing aimed at through walls, minimap no longer marks animals, loot or buildings, duller grade and tints, messages as a line on the wrist display instead of toasts, a small forearm display shown only when looked at, real crouch = sneak, real swings strike with haptics.

Next, in rough order:
1. **Body-anchored quick access in VR** (S&S, Strayed): grip at a shoulder, hip or the chest draws the item in that hotbar slot; the menu stays for crafting and the backpack. No markers on the body: you learn where things are.
2. **Pick loot up by hand** (Strayed): grip with the hand (or the ray, for the seated) on a pile, not automatic pickup by walking over it; the item snaps to the hand with a buzz.
3. **A bedroll** (Rust, Strayed): craftable, placed, sets where you respawn; sleeping near it. Loss without a bedroll sends you back to the start with nothing but the starter kit.
4. **Fewer numbers, more signs** (DayZ): colour-coded vitals icons that mean something only when yellow or red; breath and heartbeat sound at low health (already there), shivering and a dimming view when cold or hungry instead of bars.
5. **Audio as information** (DayZ): positional sound for animals and the infected (footsteps, groans), muffled indoors; no UI for threats.
6. **Line of sight for the infected** (DayZ/stealth): they notice by sight and sound, not through walls (they currently notice within 18 blocks through anything).
7. **A map you carry** (Rust hardcore): the map and the minimap behind a map item; a compass item for heading.
8. **Comfort** (Rust): shelter and sitting by a fire speed healing and slow hunger.
9. **Placement ghost** (Strayed): a blue or red outline for blocks and deployables before they are placed.
10. **Weight and clumsiness** (S&S): heavier tools swing slower and cost more stamina.

Not for now: wipes, raiding and PvP (Phase 2 networking), guns (PLAN-Combat.md).

## Sources
- Dean Hall at GDC 2013: https://www.engadget.com/2013-03-29-gdc-2013-dean-hall-on-the-pillars-of-dayzs-design.html
- DayZ status icons (tip thread, 2013) and wiki: https://dayz.wiki.gg/wiki/Hunger
- Rust (overview): https://en.wikipedia.org/wiki/Rust_(video_game); Hardcore HUD changes: https://rust.facepunch.com/news/harder-core
- Strayed controls and tutorial (wiki): https://strayed.wiki.gg/wiki/Controllers , https://strayed.wiki.gg/wiki/Basic_Tutorial ; launch: https://www.uploadvr.com/strayed-vr-early-access-launch/
- Saints & Sinners: https://worthplaying.com/article/2020/6/8/reviews/119907-ps-vr-review-the-walking-dead-saints-sinners/ , https://www.destructoid.com/review-the-walking-dead-saints-sinners/ , https://www.roadtovr.com/cas-chary-present-walking-dead-saints-sinners-gameplay-overview/
