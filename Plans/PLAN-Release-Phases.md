# PLAN-Release-Phases

Status markers: [COMMIT] = locked decision, [LIKELY] = leaning strongly, [UNDECIDED] = open, [DEFER] = punt to later phase, [REJECT] = ruled out.

## Purpose

Map how Lost Spawns ships. Which features gate each phase. What we refuse to ship broken. Who tests when. How we recover from a bad release. When monetization (donations only) opens. The whole runway, from first public tech demo to the thing we can hand to strangers and not be embarrassed.

Every phase has a theme, a hard ship-gate, and a set of features that must work to the No-Compromises standard (CLAUDE.md rule 1). Nothing half-finished crosses a phase boundary.

## Release Philosophy

[COMMIT] **Every release is the final release.** CLAUDE.md rule 1. No "we will fix it in beta." If the tech demo ships with broken combat, that is the combat everyone remembers. Phase gates exist so we are honest about what is and is not ready. If a feature is not ready, it does not ship in that phase, it waits for the next one.

[COMMIT] **Ship what works, not what is planned.** A phase is defined by the feature list that is actually ready, not the one we hoped would be ready. We will re-scope phases before we will ship broken features.

[COMMIT] **Public playtests are tests, not trailers.** We do not dress a tech demo up as a full game. The UI, the text, the splash screen, the marketing all say exactly what phase the build is in. Players understand what they are signing up for.

[COMMIT] **No deceptive monetization ever.** No early-access purchases for features that may never ship. No battlepasses. No loot boxes. No microtransactions of any kind. Donations only, post-1.0. Donors get a thank-you, nothing gameplay-related.

[COMMIT] **Saves are forward-compatible across phases.** A character created in Alpha 1 loads in Alpha 2 and 1.0 (though the world around them may have changed). We version save files, we never ask players to start over because we were lazy.

[LIKELY] **World state is not forward-compatible.** A persistent multiplayer shard may get a reset between major phases because the world schema changed too much. We announce resets weeks in advance and offer character keepsakes (cosmetics, logbook, stats).

## Phase Overview

| Phase | Name | Theme | Audience | Duration (target) |
| --- | --- | --- | --- | --- |
| 0 | Pre-Alpha Tech Demos | Prove the engine | Internal + SpawnDev fans | Rolling, ongoing |
| 1 | Closed Alpha | Core survival loop | 50-200 invited testers | 2-3 months |
| 2 | Open Alpha | Multiplayer at scale | Public, with big warning label | 3-6 months |
| 3 | Closed Beta | Feature-complete polish | Curated community | 2-3 months |
| 4 | Open Beta | Final stress test | Public, near-shippable | 1-2 months |
| 5 | 1.0 Launch | The real thing | Everyone | The day |
| 6 | Post-Launch Seasons | Live content | Everyone | Indefinite |

[COMMIT] Phase numbers are public. The title bar says "Lost Spawns - Alpha 1.3" not "Lost Spawns." No hiding what phase we are in.

## Phase 0: Pre-Alpha Tech Demos

### Purpose

Prove the technology works in a browser, on modest hardware, with the SpawnDev stack. Nothing close to a game yet. These are engineering milestones we are showing off, not a product.

### What Ships

[COMMIT] **Voxel terrain demo.** Walk around a chunk of the world. 2-4 km viewable. No cryptids, no survival, just terrain and day/night. Runs at target FPS on Desktop-High.

[COMMIT] **Single cryptid encounter demo.** Spine-Wolf in a small arena. Player has a weapon. Wolf has AI. Damage, death, loot drop. Proof that combat loop works.

[COMMIT] **P2P connection demo.** Two browsers find each other over DHT, see each other moving in a shared 100m x 100m space. No gameplay, just "we are both here and our positions sync."

[COMMIT] **VR locomotion demo.** Quest 3S user in a small environment, testing all the locomotion modes from PLAN-VR-Controls. Motion sickness baseline test.

### What Does Not Ship

Crafting, base building, vehicles, water, large-scale multiplayer, quests, NPCs with dialog, save system. None of it. If the tech demo needs it, we are doing the tech demo wrong.

### Audience

SpawnDev fans, Hacker News, dev.to readers, GitHub watchers. People who understand what a tech demo is. We do not post it on TikTok or game-subreddits. The goal is technical feedback and ecosystem visibility, not hype.

### Ship Gate

- [COMMIT] Runs at target FPS on Desktop-High and Desktop-Mid (PLAN-Performance-Targets tiers).
- [COMMIT] Build timestamp visible in console and in main menu.
- [COMMIT] All six ILGPU backends tested and marked with a capability banner ("You are on WebGPU-High" etc).
- [COMMIT] No crashes on a 30-minute session.
- [COMMIT] Source is public on GitHub from day one.
- [COMMIT] Credits the SpawnDev Crew in the README.

### Deliverables

- Public URL at `lostspawns.spawndev.com` (or similar) that always points at the latest tech demo.
- Write-up article on dev.to explaining what was built and what is next.
- Video or animated GIF embedded in the article.
- Link to source.
- Link to Discord / forum for feedback.

## Phase 1: Closed Alpha

### Purpose

Prove the survival loop. One player, sometimes a few, living in the world long enough to care when they die. Core systems working end-to-end: hunger, thirst, shelter, a cryptid hunts you, you hunt it back.

### What Ships

[COMMIT] **Onboarding (PLAN-Onboarding-First-Hour).** The cryo-shelter wakeup through the first night is shippable. Nothing past Free Play transition needs to be gold, but the first hour must be.

[COMMIT] **Core survival loop (PLAN-Survival-Needs).** Hunger, thirst, temperature, stamina, sleep. Cooking, drinking, eating.

[COMMIT] **Combat (PLAN-Combat).** Melee, ranged, throwing. One weapon per category feels good, not ten that feel mediocre.

[COMMIT] **Stealth (PLAN-Stealth-Detection).** Symmetric sensing, vision and sound at least. Scent can be a stretch.

[COMMIT] **Cryptid Tier 1 (PLAN-Cryptid-Biology).** Spine-Wolf, Huskling, Marrow-Elk, Scavver. Tier 2 and 3 deferred.

[COMMIT] **Terrain carving (PLAN-Terrain-Carving).** Dig, place, collapse. Small-scale only, no mega-structures.

[COMMIT] **Crafting (PLAN-Crafting).** Enough recipes to furnish a small shelter and maintain one weapon.

[COMMIT] **Clothing and storage (PLAN-Clothing-Storage).** Full layered clothing, basic containers.

[LIKELY] **Basic base-building (PLAN-Base-Building).** Enough to build a one-room shelter. Large bases deferred to beta.

[LIKELY] **Small-squad multiplayer.** 2-8 players on a shard. Not 64. Not 100. Just enough to prove P2P holds up with friends.

[COMMIT] **Save/load for solo.** Single-player worlds save to OPFS. No world loss between sessions.

### What Does Not Ship

Vehicles, boats, water depth past knee-high, quests past the onboarding thread, dynamic world events, player factions (PLAN-Factions-Squads), radio comms (PLAN-Radio-Comms), modding (PLAN-Modding-Plugin-System), economy, trading. VR is a stretch goal for Alpha 2, not Alpha 1.

### Audience

Invited testers. Sign-up form on the site. We pick 50-200 for the first wave based on who can give useful feedback: SpawnDev community, known playtesters, indie game devs, streamers who do technical coverage (not hype machines).

[COMMIT] Closed Alpha has an NDA on public streaming/video for the first two weeks. After that, testers can show whatever they want. We trust our testers; the NDA is to give us time to fix the first wave of bugs before the internet sees them.

### Ship Gate

- [COMMIT] Runs at target FPS on at least Desktop-High, Desktop-Mid, and Laptop (PLAN-Performance-Targets).
- [COMMIT] Zero known hard crashes over a 2-hour session.
- [COMMIT] Core loop playable end-to-end: wake up -> survive first night -> keep living through next day.
- [COMMIT] All systems listed above pass the No-Compromises standard (CLAUDE.md rule 1).
- [COMMIT] Full test suite (PlaywrightMultiTest) green across all 6 ILGPU backends before build is pushed to tester URL.
- [COMMIT] CI benchmarks (PLAN-Performance-Targets) show no frame-time regression vs prior Alpha build.
- [COMMIT] Bug tracker (likely GitHub Issues) publicly visible, testers can file.
- [COMMIT] In-game feedback button that captures state + screenshot + log.

### Deliverables

- Tester invitations with onboarding doc explaining what phase it is, what works, what does not.
- Discord or forum for real-time tester conversation.
- Weekly build cadence (not daily - testers need stability).
- Published changelog per build.
- One-page "Known Issues" doc kept up to date.

## Phase 2: Open Alpha

### Purpose

Find out what breaks when strangers show up. Scale multiplayer from 8 to 32 to 64 on a shard. Expose the P2P mesh to real network conditions: NAT hell, ISP shenanigans, packet loss, mobile hotspot jitter. Find the bugs that only show up at scale.

### What Ships (additive from Alpha 1)

[COMMIT] **Vehicles (PLAN-Vehicles).** At least one working ground vehicle end-to-end. Fuel, damage, repair.

[COMMIT] **Boats and coastal play (PLAN-Water-Rivers-Sea).** At least rowboat and fishing boat working, one coastal POI reachable by water.

[COMMIT] **Dynamic world events (PLAN-Dynamic-World-Events).** At least three event types firing on world timers.

[COMMIT] **Radio comms (PLAN-Radio-Comms).** Voice and text, proximity and channel-based.

[COMMIT] **Factions and squads (PLAN-Factions-Squads).** Squad creation, squad chat, squad markers.

[COMMIT] **Death and respawn (PLAN-Death-Corpse-Respawn).** Corpse looting, respawn cooldown, insurance mechanics if any.

[LIKELY] **Cryptid Tier 2.** Bone-Bear, Glass-Spider, Scrap-Hawk, Crawler.

[LIKELY] **Medical (PLAN-Medical).** Full injury/disease system, not just HP.

[LIKELY] **Quests Tier 1 (PLAN-Quests-Storyline).** Early Aether threads, Hilltop Station discoverable but not openable.

[LIKELY] **VR support (PLAN-VR-Controls).** Quest 3S playable end-to-end. May be marked "VR Alpha" within Open Alpha.

[LIKELY] **Mobile-Low browser support.** Chromebook / low-end Android. Runs the game even if it runs badly.

### What Does Not Ship

Modding (still defer to Beta), Tier 3 cryptids, Aether Group endgame content, Hilltop Station main quest, full weather system if Weather was still roughed-in in Alpha 1, economy tuning locked in.

### Audience

Public. Anyone can sign up and play. The UI says "Open Alpha" in big letters. We warn people about bugs, we warn people about wipes, we do not pretend this is 1.0.

[COMMIT] Open Alpha has NO NDA. Streamers welcome. Let the internet see what works and what does not. The worst case is bad publicity we can fix, which is still better than surprise-launching a broken 1.0.

### Ship Gate

- [COMMIT] 64-player shard sustained 30 minutes with acceptable frame time (PLAN-Performance-Targets).
- [COMMIT] All Alpha 1 features still passing their ship gates (no regression).
- [COMMIT] Mobile-Low tier runs the first hour at playable FPS.
- [COMMIT] VR Quest 3S runs first hour without motion sickness from obvious causes (no forced camera moves, locomotion options exposed, comfort vignette default on).
- [COMMIT] Public bug tracker, public roadmap, public changelog.
- [COMMIT] Daily smoke test runs on production shards.

### Deliverables

- Marketing push: dev.to articles, Hacker News Show HN, video walkthroughs.
- "What to expect in Open Alpha" doc.
- In-game survey after N hours of play (opt-in).
- Weekly community call (Discord stage / YouTube live) where we answer questions.
- Credit every contributor who files a useful bug or suggestion.

## Phase 3: Closed Beta

### Purpose

Feature freeze. Polish pass. Take everything that exists and make it work correctly, look right, sound right, feel right. No new systems land in Beta, only fixes, tuning, and content.

### What Ships (additive from Open Alpha, feature freeze otherwise)

[COMMIT] **Cryptid Tier 3.** Hive-Queen, Shadelark, Walking Tower. The big ones.

[COMMIT] **Full quest line (PLAN-Quests-Storyline).** Hilltop Station openable, Aether threads pursuable to conclusion (even if conclusion is "you figured out their plan, story unlocks next season").

[COMMIT] **Full weather (PLAN-Weather).** Storms, fog, snow, heat, wind coupling with stealth/water/combat.

[COMMIT] **Audio design pass (PLAN-Audio-Design).** Every cryptid has final sounds, every weapon has final sounds, music cues land.

[COMMIT] **UI/HUD final pass (PLAN-UI-HUD).** No placeholder art. No programmer-text. Localization hooks in place.

[COMMIT] **Accessibility full compliance (PLAN-Accessibility).** All toggles, all modes, tested by people with the relevant disabilities (paid playtesters, not assumptions).

[COMMIT] **Modding (PLAN-Modding-Plugin-System).** First-party modding works. Mod Hub browsable. At least ten internally-made mods demonstrate the system.

[COMMIT] **Economy tuning (PLAN-Economy).** Spawn rates, loot tables, craft costs locked in from data, not guesses.

### What Does Not Ship

Any new system we thought of mid-Beta. Feature creep is the enemy of shipping. If it is not in the feature list at Beta 1, it does not ship in 1.0, it ships in a post-launch season.

### Audience

Curated community. Active Open Alpha testers who filed useful bugs, content creators we have a relationship with, translators, modders. Not a huge group, 500-2000. We want focused feedback on polish, not a second round of "did you think about X feature."

### Ship Gate

- [COMMIT] Feature freeze in effect. Only bug fixes, content, tuning, localization, and approved-exception fixes.
- [COMMIT] Crash-free session rate above 99% on target platforms.
- [COMMIT] Every plan document's ship criteria met.
- [COMMIT] Full No-Compromises audit: every system either passes rule 1 or is cut from 1.0.
- [COMMIT] Localization pipeline working for at least 3 languages (EN, ES, DE likely; FR, JA, ZH-Hans stretch).
- [COMMIT] 500 testers sustained across 5+ shards for a weekend without total shard loss.

### Deliverables

- Beta changelog (cumulative, searchable).
- Post-mortem of Open Alpha published publicly.
- "Road to 1.0" article with the remaining ship-gates listed.
- Press kit assembled (screenshots, GIFs, capsule text, key art).

## Phase 4: Open Beta

### Purpose

Final dress rehearsal. Everybody's welcome. We stress every system under a realistic launch-day load. We catch the last bugs. We tune the last numbers. This is not a "free trial" for 1.0, it is a test.

### What Ships

Everything from Closed Beta, still frozen. Server capacity doubled. Infrastructure tested to realistic launch-day concurrent user count.

### What Does Not Ship

New features. Period. This phase exists to validate, not extend.

### Audience

Public. No gates. Anyone with a browser can play. Marketing blasts out: dev.to, Hacker News, relevant gaming press (if any have shown interest), SpawnDev fan channels, content creators with our permission.

### Ship Gate

- [COMMIT] Concurrent users at 5x expected launch load survived for a weekend without full outage.
- [COMMIT] Crash-free session above 99.5%.
- [COMMIT] All P1/P2 bugs resolved. P3 bugs tracked for post-launch.
- [COMMIT] Ops runbook written and tested: how to restart a shard, how to roll back a build, how to handle a DHT swarm split.
- [COMMIT] Final save format locked. No save breakage between Beta and 1.0.

### Deliverables

- Ops runbook (internal).
- Launch-day plan (internal + on-call rotation).
- Final pre-launch article.
- Community call announcing launch date.

## Phase 5: 1.0 Launch

### Purpose

Call it 1.0. Drop the "Beta" tag from the title. Ship the thing.

### What Ships

Everything from Open Beta, unchanged where possible. The goal of launch day is "same build as Friday" not "big new content." Stability matters more than surprise.

### Launch-Day Protocol

- [COMMIT] On-call rotation staffed for 72 hours continuous (TJ + crew volunteers).
- [COMMIT] Rollback plan rehearsed. If a critical bug lands, we can roll back to the Open Beta final build in minutes, not hours.
- [COMMIT] Communication channels staffed (Discord, forum, GitHub issues).
- [COMMIT] Status page publicly visible.
- [COMMIT] No post-launch content drop scheduled for the first two weeks. We let the dust settle.

### What Does Not Ship

A cash shop. A season pass. A paid DLC. A "1.0 exclusive edition." None of it. 1.0 is 1.0 for everyone.

### Announcements

Article on dev.to (TJ authors). Post on SpawnDev blog if one exists by then. Hacker News Show HN. Relevant subreddits (with the no-Reddit feedback memory in mind - we post announcements but we do not engage in the comments). YouTube if we have it. Twitter/X/Mastodon/Bluesky for linking out.

### Ship Gate

All Open Beta ship-gate criteria hold for the final build, unchanged.

### Post-Launch Support Commitment

- [COMMIT] Six months minimum of active bug-fix support.
- [COMMIT] Donations open (crypto via Trezor addresses in repo, per feedback_no_proton.md and reference_trezor_crypto.md). Donations never unlock gameplay.
- [COMMIT] Issues triaged within 48 hours.
- [COMMIT] If we cannot continue development for any reason, the source stays open, the binaries stay downloadable, the servers stay up as long as we can afford them, or the community gets a heads-up months in advance to fork and run their own.

## Phase 6: Post-Launch Seasons

### Purpose

Keep the world alive. Add content in focused, themed drops ("Seasons") instead of continuous slow drift.

### Season Cadence

[LIKELY] One season every 3-4 months. Each season has:

- New region or expansion of the peninsula (PLAN-World-Biomes-Regions).
- 1-2 new cryptids.
- 1 new major quest thread.
- 5-10 new weapons / crafting recipes.
- 1 new mechanic (e.g., "Season 2 adds the sewers" or "Season 3 adds the rail system").
- Tuning pass based on the prior season's data.

### Season Structure

- **Week 1-2:** Announcement, trailer, articles, press kit.
- **Week 3:** Closed Beta of the season (1-2 days).
- **Week 4:** Open Beta of the season (3-5 days).
- **End of month 1:** Season launches.
- **Months 2-3:** Stabilization, bug fixes, community content (mod highlights, player stories).
- **Month 3-4:** Next season announced.

### Season Themes (speculative)

[LIKELY] **Season 1: Static.** Radio signals lead players to discover the remnants of Hilltop Station staff who went silent.

[LIKELY] **Season 2: Below.** Sewer/tunnel expansion under the towns. Crawlers get their spotlight. Claustrophobia is the vibe.

[LIKELY] **Season 3: Tide.** Deeper water content, scuba gear, wrecks, Leviathan as a genuine threat. Unlocks more of PLAN-Water-Rivers-Sea.

[LIKELY] **Season 4: Verdant.** The Cascade starts growing in new ways. Plant-based cryptids. Lore deepens around what SERAPH-3 actually is.

Each season should have one new narrative thread that advances the Aether Group endgame without ever fully closing it.

### What Does Not Happen Between Seasons

- No silent content drops. Every addition is announced.
- No "live service" grind resets. Player progress persists across seasons.
- No FOMO mechanics. If you miss a season's limited event, the event returns seasonally or the content stays in the world.
- No season pass sales. Everything is free.

### Sunset Plan

[COMMIT] If and when active development ends, the game does not die.

- Source stays open.
- Binaries stay downloadable from GitHub Releases.
- Official shards stay online as long as operating costs are donation-covered.
- If costs exceed donations, announce sunset 6 months in advance.
- Community forks allowed and encouraged.
- The P2P swarm model means the game can survive without us - shards can be run by anyone.

## Communication Cadence Per Phase

| Phase | Frequency | Medium | Purpose |
| --- | --- | --- | --- |
| Pre-Alpha | Per milestone | dev.to article | Tech achievements |
| Closed Alpha | Weekly | Discord, changelog | Tester-facing |
| Open Alpha | Weekly | dev.to, Discord, community call | Public transparency |
| Closed Beta | Bi-weekly | Changelog, Discord | Focused feedback |
| Open Beta | Daily | Status page, Discord | Stability reporting |
| 1.0 | As needed | All channels | Launch + post-launch issues |
| Seasons | Monthly | dev.to, community call | Season planning + retrospective |

## Versioning Scheme

[COMMIT] **Semantic-ish versioning for builds.** Not strict semver but adapted.

- `0.X.Y.Z` for pre-1.0. X is the phase (0=Pre-Alpha, 1=Closed Alpha, 2=Open Alpha, 3=Closed Beta, 4=Open Beta). Y is the milestone within the phase. Z is the build number.
- `1.X.Y` for post-launch. X is the season, Y is the patch within the season.
- Every build displays version and build timestamp in the main menu.
- Every build has a git hash tag.

### Examples

- `0.1.3.142` = Closed Alpha, 3rd milestone, build 142.
- `1.0.0` = 1.0 launch.
- `1.1.3` = Season 1, patch 3.
- `1.2.0` = Season 2 launch.

## Save Format Versioning

[COMMIT] **Saves have a version field.** Upgrades are one-way and automatic.

- Loading an older save in a newer build upgrades the save in place.
- Newer saves refuse to load in older builds (with clear error).
- We never break saves within a phase. Across phases, we use migration scripts.
- A player's progression (identity, stats, cosmetics, unlocks) survives any world reset.

[COMMIT] **World shards may reset between phases.** Characters survive, their buildings may not. We announce resets in advance.

## What Gets Cut

Every phase gate will produce a "does not make it" list. Items on those lists go to:

1. **Next phase:** If the system is close but not ready.
2. **Post-1.0 season:** If the system is not critical to the core experience.
3. **Never:** If we decided it was a bad idea.

[COMMIT] We publish the "cut" list after each phase. Transparency over hype.

## Risk Registry (per phase)

### Pre-Alpha risks

- ILGPU backend instability (one backend lagging).
- WebGPU spec changes mid-development.
- Asset pipeline not ready for even small demos.

### Alpha risks

- P2P mesh collapses above N players.
- Mobile-Low tier fundamentally unviable.
- VR motion sickness testing reveals locomotion needs redesign.
- Cryptid AI too dumb or too smart (game feel).

### Beta risks

- Localization pipeline bottlenecks.
- Modding security review finds a serious issue, delays mod feature ship.
- Economy numbers are unfixable without a wipe (we announce wipe if needed).

### Launch risks

- Launch-day traffic exceeds infrastructure.
- DHT swarm partitions under load.
- Newly-discovered critical bug in final build.

Each phase's ship-gate includes "risks from the prior phase's risk registry are mitigated or explicitly accepted."

## Platform Release Order

[COMMIT] **Web (Blazor WASM PWA) first, everywhere.** Desktop browsers day one. Mobile browsers as soon as Mobile-Low tier passes (Alpha 2).

[COMMIT] **VR via WebXR second.** Quest 3S, Quest 3, Pico 4. Targeted for Alpha 2. Vision Pro supported when hardware is widespread enough to test on.

[COMMIT] **No native desktop executable for 1.0.** The PWA is the desktop experience. Installable, offline-capable, respects platform conventions. This is the vision - prove Blazor WASM PWAs are first-class.

[DEFER] **Native builds (Tauri, MAUI, etc.)** evaluated post-1.0 if and only if a real shortcoming of the PWA model emerges. Default position: stay web.

[REJECT] **Steam launch.** Would require wrapping the PWA in something to satisfy Steam, lose platform-agnostic reach, introduce storefront fees. Our story is browser-first.

[REJECT] **App Store / Google Play.** Same reasoning. PWA install works on both platforms now. No need to pay platform fees or submit to store review.

## Compatibility Promises

[COMMIT] **Never break a browser we previously supported in the same major version.** If 1.0 runs on Firefox, 1.1 runs on Firefox. Dropping a browser happens only at a major version boundary and is announced a season in advance.

[COMMIT] **Source is always public.** GitHub, MIT-compatible license (exact license TBD but permissive). No private branches. No NDAs on the code itself (only tester-build NDAs for Closed Alpha, and only for two weeks).

[COMMIT] **Save files are a portable format.** Players can export and re-import their save. No cloud-lock-in.

## Success Criteria by Phase

### Pre-Alpha Success

- Tech demo works on target hardware.
- SpawnDev community engaged with the project on GitHub.
- One Hacker News front page appearance is great-to-have, not required.

### Alpha Success

- 50-200 closed testers finding bugs we did not find internally.
- 500+ open alpha testers average session length above 30 minutes.
- Content creators making videos without us asking them to.
- Measurable improvement in "would recommend" survey scores between Alpha 1 and Alpha 2.

### Beta Success

- Feature list locked for 1.0 and everything on it passes ship-gate.
- Crash rate below 0.5% per session.
- Localization at launch coverage.
- Modding community already seeded with 10+ third-party mods.

### 1.0 Success

- Stable launch (no day-one rollback).
- Positive community reception (soft metric, not a number).
- 100,000+ unique players in first month is a nice-to-have, not a gate.
- Donations covering server costs within three months.

### Post-Launch Success

- Community-run shards outnumber official shards within year one.
- Season 1 ships on schedule.
- Modding community producing content we could not have made ourselves.
- Game outlasts us - sunset plan never needed or, if needed, smooth.

## Open Questions

[UNDECIDED] **Closed Alpha size.** 50, 100, 200? Resource cost vs feedback breadth tradeoff. Start at 50 and scale based on throughput.

[UNDECIDED] **VR at Alpha 1 or Alpha 2?** Current lean: Alpha 2 (needs more runway on PLAN-VR-Controls implementation). Could move earlier if VR work races ahead.

[UNDECIDED] **Licensing.** MIT vs Apache 2.0 vs BSD-3 for the engine, plus what license for game content (art, audio, lore text). Leaning MIT for code, Creative Commons BY-NC-SA for game content (remix yes, commercial use no).

[UNDECIDED] **Naming of the Closed Beta tester program.** "The Foundation," "Early Watch," "Survivors Circle" all in the running. Community vote post-Open-Alpha.

[UNDECIDED] **When to announce the 1.0 release date.** Either "we will announce when we know" (conservative) or "1.0 ships on date X, we are going to hit it or re-scope to hit it" (aggressive). Default: conservative, because CLAUDE.md rule 1 trumps calendar pressure.

[DEFER] **Esports / competitive play modes.** Not a design goal for 1.0. Could become a thing in a post-launch season if the PvP community asks for it, never forced.

## Interlocks With Other Plans

- **PLAN-Vision** sets the overall "what is Lost Spawns" answer. This plan sequences when each piece of the vision arrives.
- **PLAN-Performance-Targets** ship-gates every phase on frame time.
- **PLAN-Networking-Multiplayer** ship-gates Alpha 2 on scale testing and Beta on anti-cheat.
- **PLAN-Onboarding-First-Hour** is the gate on Alpha 1 playability.
- **PLAN-Modding-Plugin-System** is a Beta feature, not an Alpha feature.
- **PLAN-Asset-Pipeline** feeds every phase - asset quality and size are directly phase-gated.
- **PLAN-Accessibility** is gated Beta: full pass required before 1.0.
- **PLAN-VR-Controls** currently lands Alpha 2, could move earlier.

## Non-Goals

- No "soft launch" in one country. We are browser-based, geographic soft-launch is meaningless.
- No influencer-exclusive content drops.
- No pre-order bonuses.
- No "Deluxe Edition" at 1.0.
- No season one-week delay between streamers and players. Everyone plays at the same time.
- No dark-pattern retention mechanics (daily logins, streak rewards that punish breaks).

## Star Trek Analogy (because we have to)

Phase 0 is the Kobayashi Maru test - unwinnable scenarios at cadet level, confidence-builders.
Phase 1 is the Academy graduation run - first real crew, first real missions, lower stakes.
Phase 2 is getting the Enterprise-A out of spacedock. Everything rattles, we learn.
Phase 3 is the shakedown cruise. Small crew, controlled environments.
Phase 4 is leaving dry dock in front of the Federation press. Everyone watches.
Phase 5 is First Contact. We either inspire or embarrass ourselves.
Phase 6 is the seven-season run. We keep flying.
