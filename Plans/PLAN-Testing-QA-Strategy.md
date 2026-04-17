# PLAN-Testing-QA-Strategy

Status markers: [COMMIT] = locked decision, [LIKELY] = leaning strongly, [UNDECIDED] = open, [DEFER] = punt to later phase, [REJECT] = ruled out.

## Purpose

Lay out how we prove Lost Spawns works. Not just that it compiles. Not just that it does not crash on a happy path. That every shipped system behaves correctly with real data, on every backend, at every performance tier, with real players, under real network conditions. QA is how we earn the right to ship under CLAUDE.md rule 1.

This plan sets the test pyramid, the CI pipeline, the tester programs, the crash telemetry (opt-in), the bug triage workflow, and the ship-gate criteria that every phase must satisfy.

## Guiding Principles

[COMMIT] **No mock tests. Ever.** CLAUDE.md rule 5 and feedback_no_mock_tests.md. Every test hits real code paths: real renderers, real network stacks, real ILGPU kernels, real WebRTC channels. Mocks give fake confidence. We do not ship on fake confidence.

[COMMIT] **No fake tests.** A test that passes trivially (`Assert.NotNull(obj)`, `scale=1`, `offset=0`, identity parameters that bypass the actual logic) is not a test. Every test must fail when the production code is broken. If removing the test does not hurt confidence, the test was never useful.

[COMMIT] **Tests cover the full production use case.** CLAUDE.md rule 1. A torrent test that does not download a torrent is not testing downloads. A voxel test that does not render voxels is not testing voxels. An AI test that does not run the full AI cycle is not testing AI. Partial tests are lies.

[COMMIT] **Agents run tests, not TJ.** CLAUDE.md rule 5. TJ confirms a clean green in the demo. Any AI crew member who writes a test runs it themselves, gets it green on all relevant backends, and only then surfaces to TJ.

[COMMIT] **Feature-correctness beats test-correctness.** The tests verify code correctness. Tests passing does not mean the feature works. For UI/player-facing features, the agent must actually play the feature in the demo before claiming done (CLAUDE.md section on UI changes).

[COMMIT] **Budget for tests.** Every feature ship includes the tests. Tests are not "next sprint." Tests are part of done.

[COMMIT] **Public test suite.** Tests live in the open repo. Anyone can run them. Anyone can add them.

## Test Pyramid

### Layer 1: Unit Tests

**What:** Pure function tests. A single algorithm, a single class, a single kernel. Fastest layer. Highest count.

**Who writes:** Every feature author.

**Runner:** xUnit in .NET projects, PlaywrightMultiTest for Blazor WASM.

**Count target:** Thousands. We have already proven ILGPU.ML can sustain 3000+ real tests. Same standard here.

**Budget:** Whole suite runs in under 15 minutes. Each test under 500ms typical.

**Examples:**
- Voxel RLE compress/decompress round-trip with random data.
- Ed25519 signature verify-roundtrip with real keys.
- Stealth vision cone includes/excludes known test positions.
- Cryptid pathing on a fixed map produces the expected node sequence.
- CBOR serialization of every network packet type round-trips byte-identical.

### Layer 2: Integration Tests

**What:** Multi-system flows. Stealth + AI + combat triggered together. Save + load round-trip. P2P connection establishment end-to-end with real WebRTC. Asset pipeline pack + unpack + verify.

**Who writes:** Feature authors for their feature's integrations, crew at large for cross-system.

**Runner:** PlaywrightMultiTest driving real browsers with real backends.

**Count target:** Hundreds.

**Budget:** Whole suite runs in under 60 minutes. Each test under 30 seconds typical.

**Examples:**
- Two browsers find each other via DHT, form a WebRTC channel, exchange a signed position update, verify.
- Load a saved world, walk the character, save, quit, reload, verify identical state.
- Build a shelter, save, reload, verify the shelter persists with all its materials and owner.
- Full Closed Alpha onboarding first-hour path, end to end, with AI time-acceleration.

### Layer 3: Scenario Tests

**What:** Full gameplay scenarios the real game must handle. These are the expensive ones. Hours of gameplay simulated in minutes via time-acceleration, but real systems running.

**Who writes:** Crew + TJ approvals on new scenarios.

**Runner:** PlaywrightMultiTest in a long-session mode, or a dedicated scenario runner.

**Count target:** Dozens.

**Budget:** Whole scenario pack runs in under 4 hours. Individual scenarios under 30 minutes each.

**Examples:**
- Wake-up-to-first-night full onboarding with zero dev intervention.
- Solo survival day 1-7, verify progression systems all tick.
- Two players meet, trust, build a base together, get attacked by a Bone-Bear, defend, verify all log events occur.
- Full Hilltop Station quest from discovery to resolution.

### Layer 4: Load Tests

**What:** Scale tests. How many players? How many cryptids? How many chunks? How many packets per second?

**Who writes:** Crew + TJ.

**Runner:** Orchestrated fleet of headless browsers (or Node-based WebRTC clients that speak our protocol), driven by a test harness.

**Count target:** A dozen named load profiles.

**Budget:** Each profile runs under 30 minutes. Full pack runs overnight.

**Examples:**
- Squad (8 players) co-located in a 1km radius for 10 minutes.
- Town event (32 players converging on one POI).
- Full-shard stress (64 players, scattered).
- City event (100 players in a city, likely worst case).
- Cryptid horde (50 cryptids active simultaneously).
- P2P mesh churn (players joining/leaving every 10 seconds).

### Layer 5: Chaos Tests

**What:** What happens when things go wrong. Packet loss, NAT drops, peer disconnects, clock skew, tampered packets.

**Who writes:** Crew under Security/Research officer (Tuvok typically).

**Runner:** Specialized harness that injects faults into a running scenario.

**Count target:** Fault types x scenarios matrix. Grows organically.

**Budget:** Full chaos pack runs weekly, not per-PR.

**Examples:**
- Random peer drops 20% of packets.
- A peer sends packets with bad signatures, swarm reputation system catches them.
- Clock skew injected by 30 seconds, verify game logic handles it.
- Malicious actor broadcasts false chunk authority claim, quorum rejects.
- DHT partition: half the peers can see each other, other half can see each other, can the two halves reconcile on reconnect?

## CI Pipeline

### Per-Commit (under 15 min)

[COMMIT] Every push runs:
- Unit tests on Desktop-CPU backend (baseline, always available).
- Lint, format, em-dash check (feedback_no_emdash.md).
- Build succeeds on all SpawnDev library reference backends.
- Asset pipeline sanity (build 1% of assets, verify no manifest errors).

### Per-PR (under 60 min)

[COMMIT] Every PR runs:
- Full unit test suite on all 6 ILGPU backends (WebGPU, WebGL, Wasm, CUDA, OpenCL, CPU).
- Full integration test suite on at least Desktop-High and Desktop-Mid.
- PLAN-Performance-Targets CI benchmarks. Any regression above 5% blocks merge.
- Security scan (no committed secrets, no dependency vulnerabilities above threshold).

### Nightly (4-8 hours)

[COMMIT] Every night:
- Scenario tests across all platform tiers.
- Load test profiles (squad + town at minimum; city + horde on weekly rotation).
- Chaos fault matrix subset (full matrix weekly).
- Asset pipeline full rebuild from scratch on reference hardware. Compare output bytes - deterministic check.
- Save/load compatibility test: load a save from every prior release, verify upgrade succeeds.

### Weekly (8+ hours, or staged)

[COMMIT] Every weekend:
- Full load test pack.
- Full chaos pack.
- Full cross-backend regression (any subtle drift between backends surfaces here).
- Memory leak soak (8-hour session, verify stable memory footprint).
- VR comfort soak (human-in-the-loop, Quest 3S, 30+ min sessions).

### On Release (varies)

[COMMIT] Every build that could ship to players:
- Everything above, all green.
- Manual smoke test by at least two crew members on real hardware.
- Tester-group dogfood window before public rollout (see Tester Programs).

## Test Infrastructure

[COMMIT] **PlaywrightMultiTest as primary runner.** feedback_playwright_test_runner.md - never manually start servers, always use the runner.

[COMMIT] **Test cleanup discipline.** feedback_kill_testhost_only.md, feedback_never_kill_all_testhost.md. Only kill testhost processes we started. Identify by exe path (feedback_identify_testhost_by_path.md). Post to DevComms before long runs.

[COMMIT] **Tests produce artifacts:** screenshots on failure, logs, network captures, GPU traces. Artifacts are uploaded on CI failure for inspection.

[COMMIT] **ShaderDebugService produces dumps** (reference_shader_debug_service.md) on every GPU failure so we can post-mortem the shader.

[COMMIT] **Tests use real backends on real hardware.** No simulated GPU runs. CI has CUDA, OpenCL, and Wasm machines. WebGPU is exercised on a machine with a compliant browser.

## What We Test Per Plan

This matrix reconciles every plan document with its testing requirements. Ship-gate for each phase requires the relevant rows to be green.

| Plan | Key Tests |
| --- | --- |
| PLAN-Vision | Playable first-hour loop end-to-end |
| PLAN-P2P-Reputation-System | Reputation score drift, Sybil resistance, score propagation |
| PLAN-Terrain-Carving | Deterministic carve, RLE roundtrip, chunk quorum |
| PLAN-Crafting | Recipe completeness, item round-trip through save/load, crafting timers |
| PLAN-Clothing-Storage | Layer stacking, inventory overflow, container ownership |
| PLAN-Player-Progression | Stat growth over scenario, skill unlocks, progression persists save/load |
| PLAN-Dynamic-World-Events | Event triggers fire, cooldowns honored, player-attributed rewards correct |
| PLAN-Environment-Hazards | Fire spread, cold/heat math, radiation model |
| PLAN-Base-Building | Build/destroy cycle, material cost, structural stability model |
| PLAN-Economy | Loot table distributions, crafting cost balance, inflation guardrails |
| PLAN-Vehicles | Drive/crash/repair cycle, fuel math, multi-passenger sync |
| PLAN-Combat | Hit registration, co-signature resolution, weapon class balance |
| PLAN-Survival-Needs | Hunger/thirst/temp drain rates, death conditions, recovery |
| PLAN-Medical | Injury model, disease progression, treatment effects |
| PLAN-Infected-AI | State machine transitions, pack behavior, symmetric sensing |
| PLAN-Radio-Comms | Voice latency, proximity falloff, channel fidelity |
| PLAN-Audio-Design | Audio mix under load, spatial audio accuracy, no clipping |
| PLAN-Death-Corpse-Respawn | Corpse lifetime, looting rules, respawn cooldown |
| PLAN-Factions-Squads | Squad creation/dissolve, chat routing, shared objectives |
| PLAN-Day-Night-Cycle | Time sync across peers, lighting transitions, sleep fast-forward |
| PLAN-Quests-Storyline | Quest state persists save/load, branches fire correctly |
| PLAN-World-Biomes-Regions | Biome boundaries, spawn tables per biome, transitions |
| PLAN-UI-HUD | UI responsive under all tier FPS, accessibility modes |
| PLAN-Lore-History | Lore items findable, journal updates, no dangling references |
| PLAN-Animal-Wildlife-Hunting-Fishing | Spawn ecology, hunting mechanics, fishing loop |
| PLAN-Weather | Weather transitions, fog/rain/snow effect on gameplay |
| PLAN-Accessibility | Every toggle tested by someone who benefits from it |
| PLAN-Networking-Multiplayer | NAT traversal matrix, signed SDP, quorum handoff |
| PLAN-VR-Controls | Every locomotion mode, every grip interaction, motion sickness soak |
| PLAN-Performance-Targets | Benchmark scene FPS per tier, never regress above 5% |
| PLAN-Cryptid-Biology | Per-cryptid behavior tree, kill/loot/respawn cycle |
| PLAN-Stealth-Detection | Vision/sound/scent detection thresholds, AI alerting |
| PLAN-Water-Rivers-Sea | Swimming mechanics, boat mechanics, tide schedule |
| PLAN-Onboarding-First-Hour | First hour end-to-end scenario test (the most load-bearing) |
| PLAN-Modding-Plugin-System | Mod load/unload, sandbox escape attempts, signature verify |
| PLAN-Asset-Pipeline | Deterministic builds, compression round-trips, manifest correctness |
| PLAN-Release-Phases | Ship-gate automation (green CI = eligible to ship) |

## Tester Programs

### Closed Alpha Testers

[COMMIT] 50-200 invited testers. Sign-up form, curated selection.

**Selection criteria:**
- Known SpawnDev contributors or community members.
- Demonstrable feedback quality (e.g., has filed thoughtful GitHub issues on other projects).
- Diverse hardware/platform coverage.
- Geographic diversity (for network testing).
- Mix of playtesters, devs, streamers.

**Onboarding:**
- Welcome email with what-works/what-doesn't doc.
- Discord invite with a dedicated tester channel.
- How-to-file-a-bug guide with examples of good and bad bug reports.
- An in-game feedback button capturing state/screenshot/log.
- Two-week NDA on streaming, then open.

**Feedback cadence:**
- Weekly build notes.
- Bi-weekly survey (5-10 questions).
- Optional 1-on-1 call with a crew member for power testers.

### Open Alpha / Beta Testers

Public. Anyone who opts in. No NDA.

**Channels:**
- Public Discord (different channel from Closed Alpha).
- GitHub Issues for bugs.
- In-game feedback button.
- Monthly community call.

**Incentives:**
- Credit in the game for notable contributors (bug finders, translators, accessibility consultants).
- Cosmetic keepsakes (non-gameplay) for tester participation.
- No paid rewards, no loot, no gameplay advantage. feedback_ai_credit.md spirit - recognition is genuine, not transactional.

### Accessibility Consultants

[COMMIT] Paid accessibility testers at Closed Beta minimum. Not a volunteer ask.

**Categories covered (at least):**
- Screen reader users.
- Low-vision users.
- Deaf/hard-of-hearing users.
- Motor impairment users.
- Colorblind users (multiple types).
- Photosensitivity-risk users.
- Neurodivergent testers (ADHD, autism, dyslexia).
- VR-motion-sensitive users.

**Output:**
- Per-category report on what works and what blocks.
- Blocks are P0 for 1.0 ship gate.

## Bug Triage

### Severity Levels

[COMMIT] Four severity levels.

| Level | Definition | Response Time |
| --- | --- | --- |
| P0 (Critical) | Data loss, security, unable to play | Drop-everything fix |
| P1 (High) | Major feature broken for many users | Within current milestone |
| P2 (Medium) | Annoying, workaround exists | Within phase |
| P3 (Low) | Cosmetic, edge case | Post-launch backlog |

### Triage Ownership

- Crew rotates triage duty weekly. One person per week is first-responder for all new issues.
- Triage decisions posted in DevComms or GitHub comments.
- Captain (TJ) re-triages any P0/P1 to confirm within 24 hours.

### Reproduction Requirement

[COMMIT] feedback_cant_reproduce_not_their_bug.md and CLAUDE.md rule 4b. Every bug gets a reproduction case. If we cannot reproduce, we do not guess - we instrument and get logs from the reporter. "Works on my machine" is not a close.

[COMMIT] Every fix gets a regression test. The test that would have caught the bug is added.

### Security Bugs

[COMMIT] **Private disclosure channel.** SECURITY.md in the repo root with PGP key and contact. Security bugs get P0 response and coordinated disclosure.

[COMMIT] Security bugs have a 90-day maximum disclosure window. If a bug touches crypto/identity/auth, and we cannot fix in 90 days, we publish the vulnerability along with workarounds.

## Crash Telemetry (Opt-In Only)

[COMMIT] **Telemetry is opt-in, never opt-out.** See PLAN-Privacy-Telemetry for details.

[COMMIT] **Minimum data when opted in:**
- Crash stack trace.
- Browser + OS + GPU.
- Lost Spawns version + build hash.
- Time of crash.
- Anonymous session ID (not linked to player identity).

[COMMIT] **Never collected:**
- Player identity, IP address, physical location.
- Chat content.
- Save data.
- Any content the player produced.
- Third-party data (other peers they were playing with).

[COMMIT] **Storage:** Private to dev crew. 90-day retention. Deletable by user on request (referenced by session ID).

[COMMIT] **Aggregate data published:** Crash rate per version, top 10 crash signatures (anonymized), backend distribution. We publish trends, never raw events.

## Performance Regression Detection

[COMMIT] **Every CI run captures PLAN-Performance-Targets benchmark scene FPS.**

[COMMIT] **Regression gate: 5%.** If a PR regresses frame time by 5% or more on any target tier, it cannot merge without Captain sign-off.

[COMMIT] **Regression dashboard public.** Publish CI benchmark history so anyone can see performance trends over time. Builds community trust and catches slow-creep regressions.

[COMMIT] **Reference hardware.** CI runs benchmarks on a fixed hardware profile per backend. We document what that is so the community can reproduce.

[COMMIT] **Player-tier benchmarks.** We also collect (opt-in) benchmarks from tester hardware. Wider coverage than CI can afford.

## Cross-Backend Parity

[COMMIT] **The same scenario on the same input produces the same output on every ILGPU backend.** Deterministic where we can, within-tolerance where floating-point demands it.

[COMMIT] **Cross-backend diff tests.** Run the same scenario on WebGPU and CUDA, compare outputs pixel-level for rendering and state-level for simulation. Any drift above epsilon is a bug.

[COMMIT] **WebGL and Wasm as first-class backends.** feedback_no_cpu_fallback.md. We do not write CPU fallbacks when ILGPU runs on every backend. Every backend is tested.

## Accessibility Regression Prevention

[COMMIT] **Automated checks:**
- Color contrast on all UI elements meets WCAG AA.
- All interactive elements are keyboard-reachable.
- All images have alt text or are marked decorative.
- All audio cues have visual equivalents.
- All visual cues have audio equivalents (for deaf players).

[COMMIT] **Manual checks:** Accessibility consultants run each phase.

[COMMIT] **Accessibility is a release gate at Beta and later.** Cannot 1.0-launch with any accessibility regression vs Closed Beta baseline.

## Localization Testing

[COMMIT] **Pseudolocalization in CI.** Automatic string-bracket check that every UI string is wrapped in translator hooks and fits in constrained spaces even with expanded pseudo-text.

[COMMIT] **Real translator pass before 1.0.** Paid translators for EN, ES, DE at minimum. FR, JA, ZH-Hans stretch.

[COMMIT] **Translator dev environment.** Translators can preview their translations in the running game before submission.

## Test Documentation

[COMMIT] **README in every test project** explaining:
- What this suite covers.
- How to run it.
- How to add a new test.
- Known flakes and their tracking issues.

[COMMIT] **No test is flaky for long.** Any test that flakes more than once per week gets either fixed or quarantined with an open tracking issue. Flaky tests that never get fixed are deleted (they were bad tests).

[COMMIT] **Test naming convention.** `Should_[expected]_When_[condition]` or similar. Readable from the failure output. No `Test1`, `TestFoo`.

## Demo-vs-Test Discipline

[COMMIT] CLAUDE.md rule 5. feedback_test_before_demo.md. Debug happens in unit tests. The demo is the showroom. When a demo bug is reported, the first action is to write or update a unit test that reproduces it.

[COMMIT] **Demo always reflects shippable state.** The Lost Spawns demo URL shows the latest stable build. Experimental work goes on a separate URL (e.g., `alpha-next.lostspawns.spawndev.com`) clearly labeled.

## Test-First Workflow For New Features

The standard workflow for any new feature:

1. Write the feature's public API and ship contract.
2. Write failing tests against the contract (unit, integration, scenario as appropriate).
3. Implement the feature until tests pass.
4. Run all relevant backend tests, get green.
5. Run the feature in the demo, verify correctness by hand.
6. Surface to Captain for confirmation.
7. On green confirmation, commit + push to shared.
8. Write a DevComms note describing what shipped.

[COMMIT] Step 4 must be done before step 5. CLAUDE.md rule 5.

## Test Anti-Patterns to Reject

[COMMIT] feedback_fake_test_audit_checklist.md applies. Specifically reject:

1. Tests that only assert object is not null.
2. Tests that use identity parameters (scale=1, offset=0) that bypass the feature.
3. Tests that do not have a CPU reference or known-correct expected output.
4. Tests that only check "does not throw."
5. Tests that exit early on any error rather than failing.
6. Tests named after the function they test instead of the behavior they verify.
7. Tests that are essentially implementation copies of the feature.
8. Tests that mock the thing they are testing.

When an author writes these, the reviewer reject-to-rewrite. Not a suggestion, a block.

## Ship-Gate Per Phase

Recap from PLAN-Release-Phases, expressed as test criteria.

### Phase 0 (Tech Demo)
- Unit tests green on CPU backend.
- Tech demo runs without crashes 30 min.

### Phase 1 (Closed Alpha)
- Unit tests green on all 6 backends.
- Integration tests green on Desktop-High + Desktop-Mid.
- Onboarding-first-hour scenario test green.
- CI benchmarks baseline set.

### Phase 2 (Open Alpha)
- All Closed Alpha criteria hold.
- Integration tests green on Mobile-Low + VR-Low.
- Load tests green up to 64-player shard.
- Chaos tests green for baseline fault set.

### Phase 3 (Closed Beta)
- All Open Alpha criteria hold.
- Full scenario pack green.
- Full load test pack green.
- Full chaos matrix green.
- Accessibility consultant pass green.

### Phase 4 (Open Beta)
- All Closed Beta criteria hold.
- 5x launch-load sustained for a weekend.
- Crash-free session rate above 99.5%.
- All P1 bugs resolved.

### Phase 5 (1.0)
- All Open Beta criteria hold for the final build unchanged.
- Rollback rehearsed.
- Localization verified by native speakers.

## Non-Goals

- No "testing is done when we are tired of writing tests." Rule 1 trumps exhaustion.
- No "we will add tests next sprint." Next sprint does not come.
- No proprietary test frameworks. xUnit + Playwright are enough.
- No test infrastructure that requires a proprietary cloud. CI on public runners where possible.
- No A/B testing on players without consent.

## Open Questions

[UNDECIDED] **Budget for accessibility consultants.** Depends on donation revenue. Floor: we pay what we can afford. Ceiling: we fundraise if needed.

[UNDECIDED] **Live ops monitoring.** Do we run a Grafana dashboard for live shards? Leaning yes, budget-permitting. See reference for the SpawnDev ecosystem's existing monitoring patterns.

[UNDECIDED] **Test-data generation.** Do we hand-craft test worlds or seed-based procedural? Leaning seed-based with explicit golden seeds for stability.

[DEFER] **Fuzz testing for protocols.** Would be great for network packet fuzzing. Post-1.0 unless a P0 protocol bug shows up that fuzzing would have caught.

## Interlocks With Other Plans

- **PLAN-Performance-Targets** provides the benchmark scenes and thresholds.
- **PLAN-Networking-Multiplayer** provides the load and chaos scenarios.
- **PLAN-Accessibility** provides the per-category consultant requirements.
- **PLAN-Release-Phases** provides the ship gates this plan automates.
- **PLAN-Privacy-Telemetry** governs opt-in crash telemetry rules.
- **PLAN-Asset-Pipeline** provides deterministic build checks.
- **PLAN-Modding-Plugin-System** provides mod sandbox escape tests.
