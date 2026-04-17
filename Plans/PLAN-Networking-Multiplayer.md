# Lost Spawns: Networking & Multiplayer Architecture

## Status Legend
- **[COMMIT]** settled design decisions that later plans can rely on
- **[LIKELY]** strong preference, expect to commit unless a blocker surfaces
- **[UNDECIDED]** genuinely open, needs discussion
- **[DEFER]** out of scope for 1.0, revisit after ship
- **[REJECT]** explicitly not doing, and why

---

## Premise

Lost Spawns is a browser game. That single constraint reshapes every networking decision. We cannot assume UDP. We cannot assume open ports. We cannot assume the player has any software installed beyond a browser. The stack we ship has to work when the player is on hotel wifi behind a captive portal, on a phone hotspot, on a school Chromebook, and on a gigabit fiber home LAN - and give them a good experience on all five.

DayZ ships dedicated servers on UDP port 2302 and expects the player to launch a client, join via IP, and trust a central hive. We cannot copy that model. We have to build something that feels as good but works under stricter constraints - and then use those constraints as an advantage, because a player's identity, base, and reputation survive any server going away.

**[COMMIT]** Lost Spawns is playable solo, on LAN, on internet P2P, and on dedicated servers. All four modes are first-class. We do not treat any of them as "the real way to play."

---

## Topology Options

### 1. Pure P2P Mesh
Every player connects to every other player. N players = N*(N-1)/2 connections. Great for small groups, terrible at scale.

- **Good:** No server, no cost, no single point of failure
- **Bad:** Bandwidth scales quadratically, browser caps on peer connections (~256 per origin)
- **Use case:** Small squad sessions (2-8 players)

### 2. Hub-and-Spoke (One Host)
One player is the "host." Everyone else connects only to them. Host relays all traffic.

- **Good:** Simple, familiar model (Minecraft LAN, Left 4 Dead)
- **Bad:** Host leaving kills the session, host's upload is bottleneck, host has authority (trust problem)
- **Use case:** Friends-only games, co-op raids

### 3. Dedicated Server
One always-on server. Everyone connects to it. Server is authoritative.

- **Good:** Strong anti-cheat, persistent world, no host migration pain
- **Bad:** Costs money, single point of failure, central trust
- **Use case:** Official persistent servers, community servers, clan servers

### 4. Hybrid Swarm (Our Model)
A world is a DHT entry. Players discover peers via swarm. A rotating quorum of peers holds authoritative state. Anyone can join. No single host.

- **Good:** Scales horizontally, no SPOF, works in browser, leverages existing SpawnDev.WebTorrent + SpawnDev.ILGPU.P2P
- **Bad:** Complex authority resolution, novel anti-cheat model, must prove it works
- **Use case:** Persistent open worlds, 50-500 players

**[LIKELY]** All four topologies ship. Solo is #0 (no network). LAN is #2 with the LAN peer acting as ephemeral host. Internet casual is #2 or #1 for small groups. Persistent worlds are #4 or #3.

**[LIKELY]** Dedicated server is a thin C# ASP.NET app. A dedicated server is just a peer that never leaves and is pinned as a quorum member. Same protocol, same code path, same rules - it just runs on a server box with a public IP instead of in a browser.

---

## Transport Layer

### Data Channels

**[COMMIT]** Primary transport is WebRTC data channels. Every peer-to-peer byte moves over a DataChannel. This is the one transport the browser guarantees us for arbitrary binary data with low latency.

- Reliable-ordered: world edits, chat, trade receipts, signed events
- Unreliable-unordered: position updates, voice chat frames, projectile telemetry
- Reliable-unordered: chunk streams (can arrive out of order, must all arrive)

**[COMMIT]** WebSocket is a fallback when WebRTC is blocked (some corporate firewalls block STUN/TURN). WebSocket routes through our rendezvous service or a dedicated server. Slower, higher latency, but a player can still play.

**[REJECT]** WebTransport. Chromium-only, not universally available, not worth the fragmentation in 2026.

### Binary Format

**[COMMIT]** All network messages are binary CBOR with a 1-byte type tag prefix. No JSON in the hot path. We have `SpawnDev.Serialization.Cbor` producing compact payloads; use it.

**[COMMIT]** Position updates use fixed-point i16 deltas for X/Z and i8 for Y quantized to 5cm buckets. A full position update for 10 players per tick fits in ~100 bytes.

### Compression

**[LIKELY]** zstd dictionary compression for chunk streams. Pre-train a dictionary on typical voxel patterns. Ship the dictionary with the client. Chunks compress 4-8x better with a tuned dictionary than generic compression.

**[UNDECIDED]** Compress voice? Opus handles this internally via WebRTC. Probably not worth a separate pass.

### Encryption

**[COMMIT]** WebRTC gives us DTLS for free. Every data channel is encrypted end-to-end between peers. We do not bolt additional crypto on top except for signed payloads (trade receipts, vouches - see PLAN-P2P-Reputation-System).

**[COMMIT]** Peer identity is pinned to the Ed25519 public key from PLAN-P2P-Reputation-System. The WebRTC SDP offer includes a signature from the identity key over the transport fingerprint. A man-in-the-middle cannot impersonate a known peer.

---

## Rendezvous & Peer Discovery

How does peer A find peer B when they have never met?

### Discovery Methods

**[COMMIT]** BitTorrent DHT via SpawnDev.WebTorrent. A world is identified by a 32-byte key (derived from the world seed + owner signature). Peers publish their presence as DHT items under that key. New peers query the DHT for the world key and get a list of currently-connected peers to dial.

**[COMMIT]** Tracker servers. We run a small pool of public trackers (BitTorrent-compatible) for faster discovery than raw DHT. Community servers can add their own tracker. If all our trackers are down, DHT still works.

**[LIKELY]** Local network discovery via mDNS-in-browser (or equivalent). For "LAN with friends" mode, zero-config connect. Browsers do not expose mDNS directly, so we use a small helper: the host opens a well-known WebSocket port and broadcasts a QR code; friends scan the QR to join. Not true mDNS but achieves the same outcome.

**[LIKELY]** Invite link. A 32-character URL fragment encodes (world key + entry peer ID + optional password hash). Paste the link, land in the world. Same model as Discord invites or Multi-Party video calls.

**[UNDECIDED]** Friend list with presence. "Your friend TJ is online and in world X - join?" Requires a friends list system, which is its own design problem (trust, block, abuse). For 1.0 we can ship without it and lean on invite links.

### Rendezvous Service

**[COMMIT]** We run a small rendezvous service using SpawnDev.WebTorrent + existing Rally/Rendezvous patterns. Its job:

1. STUN/TURN relay for NAT traversal (we pay for TURN bandwidth; it is a known cost)
2. Tracker endpoint for world discovery
3. Bootstrap peer list (a few always-on peers to seed the DHT)
4. Optional: hosted dedicated servers

**[COMMIT]** The service is not authoritative for game state. It can be down and existing peers keep playing; only new peers joining their first world are blocked. This is a hard design constraint - no central service must be load-bearing for gameplay.

### TURN Relay Budget

**[LIKELY]** We cap TURN relay per-peer at ~64 KB/s egress, ~64 KB/s ingress. If a peer needs more, they need a direct connection (port forward, IPv6, or better NAT). We publish this in the UI so players understand why their connection quality might be limited.

**[UNDECIDED]** Community-run TURN servers. Clan leaders could sponsor a TURN instance for their faction. Adds federation to the infrastructure. DEFER to after 1.0.

---

## Session Modes

### Solo

**[COMMIT]** Fully offline. No network calls. World state stored in OPFS. Can be played on a plane. Can be saved and exported.

**[COMMIT]** Solo saves can be "upgraded" to multiplayer worlds later by publishing them. The world key stays stable; joining peers can verify the world is unforked.

### LAN Co-op

**[COMMIT]** One player hosts. Up to 8 peers join. Host can close the session at any time; peers get a chance to export their character state before disconnect. No persistent state other than what peers carry with them.

**[LIKELY]** Voice chat enabled by default on LAN (low latency, no relay cost).

### Internet Casual (Small Group)

**[COMMIT]** Invite-link-based. Same topology as LAN but over internet. Host-or-quorum model. 2-16 peers.

**[COMMIT]** Host migration: if the host leaves, one of the remaining peers is elected as new host by Ed25519 key ordering (deterministic). Session continues with minimal disruption.

### Persistent World (Our Flagship Model)

**[COMMIT]** A world has a seed, an owner key, and a manifest. The manifest lists the initial quorum peers (who can be changed via signed manifest updates). Any peer can connect, fetch the world state, and start playing. World state lives in the swarm.

**[LIKELY]** Persistent worlds require at least one of the following to stay alive:
- 1+ dedicated server pinned as quorum member (operator's choice)
- 3+ always-on community peers (community-hosted worlds)
- Periodic snapshots stored in SpawnDev.WebTorrent swarm by seeding peers

**[LIKELY]** If a world drops below minimum quorum, it enters "sleep" mode - no new edits accepted, existing players can still explore the last-known state. Waking it up requires the owner (or delegated quorum) to rejoin.

### Dedicated Server

**[COMMIT]** Downloadable C# ASP.NET Core app. Runs on Windows, Linux, macOS. Single binary. Reads a config YAML for world settings, quorum members, admin keys. Joins the swarm on startup.

**[LIKELY]** Dedicated servers can be run by anyone. We publish the binary on GitHub Releases. Community servers are first-class - they get server browser listings if the operator opts in.

**[COMMIT]** Dedicated server hosting is free software. We do not sell hosting. If we ever run hosted dedicated servers ourselves, it is at-cost or with a subscription tier.

---

## Authority Model

Who decides what is true?

### The Three Kinds of State

1. **Player-owned state** - what this specific player has, where they are, what they are holding. Player is authoritative. Signed by their Ed25519 key.
2. **World state** - voxel changes, placed bases, dropped items, NPC positions. Quorum or dedicated server is authoritative.
3. **Interaction state** - combat resolution, trades, damage. Witnessed and signed by both participants.

### Player Authority

**[COMMIT]** A player's position, inventory contents, health, and intent are signed by the player's key. Other peers receive these as claims, not facts. Peers validate the claims against observed behavior.

**[COMMIT]** Claims that violate physics (teleporting 100m in one tick, gaining health without a medical item, firing a weapon with no ammo) are rejected by receivers and flagged. Repeated violations = anti-cheat signal.

### World Authority (Quorum Model)

**[LIKELY]** Each world chunk has an authoritative peer or small peer set (quorum of 3 for important chunks, single owner for dormant chunks). Chunk authority rotates based on who has been online longest with the lowest latency to interested observers.

**[LIKELY]** Authority changes require a signed handoff. The outgoing authority signs "I pass chunk (x,z) to peer P at tick T." Incoming authority co-signs. Handoff is published to swarm. Anyone can verify the chain of authority.

**[UNDECIDED]** Conflict resolution when two peers claim authority over the same chunk. Likely CRDT-style merge for low-stakes edits (placed blocks) and timestamp-plus-signature precedence for high-stakes edits (base ownership transfer).

### Interaction Authority (Co-Signing)

**[COMMIT]** When player A shoots player B:
1. A signs "I fired at B at tick T with weapon W" and broadcasts
2. B receives, simulates the shot from B's perspective, decides if hit registers
3. B signs "I acknowledge hit from A at tick T, damage D applied" and broadcasts
4. The hit is canonical only when both signatures are observed
5. If B refuses to co-sign a hit that A's simulation says should hit, A flags it - anti-cheat signal accumulates on B

**[COMMIT]** This is lag-compensation-friendly: B has final say on B's body, so B never dies to a hit they never saw coming. The trade-off is ghost shots - sometimes A hits and B dodges because B's simulation is different. We make this explicit via tracer rounds and damage telemetry in the HUD.

**[LIKELY]** High-trust rooms (clan servers with WebAuthn-gated membership) can opt into server-authoritative hit resolution to eliminate ghost shots. It is a per-world setting, not a per-mode setting.

---

## State Synchronization

### Chunks

**[COMMIT]** World is divided into 32x32x32 voxel chunks. Each chunk has a 64-bit version counter. Changes to a chunk generate a delta (list of voxel edits since last version). Peers subscribe to nearby chunks and receive deltas as they happen.

**[COMMIT]** Cold chunks (no edits for 10+ minutes) are pulled on-demand from the swarm as chunks-with-version snapshots. Hot chunks stream deltas.

**[LIKELY]** Chunk snapshots are stored as SpawnDev.WebTorrent info hashes. The world manifest includes a Merkle root of all chunk info hashes. Peers verify chunk content against the Merkle root to detect tampering.

**[LIKELY]** Streaming priority follows camera frustum: chunks in the player's immediate FOV stream first, then adjacent, then distant. See PLAN-Performance-Targets for the exact draw distance numbers we are hitting.

### Entities

**[COMMIT]** Entities (players, NPCs, dropped items, projectiles, vehicles) are networked via state replication. Each entity has an owner peer who sends position/state deltas at a tick rate.

**[COMMIT]** Tick rates:
- Player position: 30 Hz local, 15 Hz to distant peers (>50m), 5 Hz to very distant (>150m)
- NPC position: 10 Hz in combat, 2 Hz ambient
- Projectile: 30 Hz for tracked shots, pre-simulated for hitscan
- Dropped items: event-based only (drop, pickup, despawn)
- Vehicles: 30 Hz driver tick, interpolated for passengers

**[COMMIT]** Interpolation and extrapolation use the proven game-networking pattern: render peer positions 100ms behind the latest packet, interpolate between known states. Extrapolate only for hitscan shot resolution.

### NPCs

**[LIKELY]** NPC simulation runs on chunk authority peers. A zombie pack near a town is simulated by whoever owns that town chunk. Other peers receive NPC state updates as they enter interest radius.

**[LIKELY]** NPC ownership changes when the current owner leaves. Handoff is seamless - NPC state vector includes enough to continue the behavior tree from the same state.

### Voxel Edits (Base Building)

**[COMMIT]** Voxel edits are ordered by signature timestamp, de-duplicated by peer key. Each edit is a signed message: "peer P places block B at (x,y,z) at tick T." Edits to the same voxel collapse to the latest-timestamped one.

**[COMMIT]** A "base" is a cluster of voxels owned by a peer or clan key. Non-owners cannot edit unless they have permission or successfully raid (see PLAN-Base-Building). Raid is a signed event that transfers ownership or destroys voxels.

---

## Bandwidth Budget

**[LIKELY]** Per-peer targets (egress + ingress combined):

| Peer count | Expected bandwidth | Cap |
|---|---|---|
| 2-8 (squad) | 30-50 KB/s | 100 KB/s |
| 8-32 (town) | 80-150 KB/s | 300 KB/s |
| 32-100 (region) | 150-400 KB/s | 1 MB/s |
| 100+ (city event) | 500 KB/s - 2 MB/s | 4 MB/s |

**[COMMIT]** Players on slow connections are given interest-culling dials: lower tick rate on distant peers, reduced chunk stream distance, skip detailed NPC animation updates. They should be able to play over a 100 KB/s connection even if their experience of faraway content is lower quality.

**[COMMIT]** Voice chat is capped at 32 KB/s per active speaker and is relay-aware. If you are on cellular, voice downgrades to 16 KB/s narrowband.

---

## Anti-Cheat

### What We Can Do

**[COMMIT]** Signature validation. Every action is signed. Tampered packets are rejected.

**[COMMIT]** Plausibility checks. Movement, fire rate, inventory changes are bounded. Violations flag the peer.

**[COMMIT]** Witnessed events. Combat outcomes require co-signatures. A peer who refuses to co-sign legitimate hits raises their fraud score.

**[COMMIT]** Replay of disputed events. If a peer contests a hit, the last N ticks of observed state can be replayed from signed logs held by witnesses. Audit trail is cryptographic.

**[COMMIT]** Reputation-weighted trust. Fresh accounts have low trust, gate into high-rep interactions. See PLAN-P2P-Reputation-System.

**[COMMIT]** Client integrity checks. The client publishes a hash of its running code (via Service Worker introspection on Blazor WASM). Mismatches against published releases flag the peer.

### What We Cannot Do

**[COMMIT]** We cannot detect a modified client that behaves within plausibility bounds. A tuned aimbot that fires exactly like a legit pro player is undetectable by protocol. We rely on reputation, clan gating, and community reporting to sandbox these players.

**[COMMIT]** We cannot detect wallhacks in a P2P model where peers receive chunk data for their interest region. A client rendering occluded geometry differently is a client-side choice. Mitigation: chunk occlusion culling happens server-side for dedicated mode; reputation catches repeat offenders in P2P.

### Cheat Categories & Responses

| Cheat | Detectable? | Response |
|---|---|---|
| Speed hack | Yes (movement cap) | Reject packets, flag peer, auto-kick after threshold |
| Teleport | Yes | Same |
| Infinite ammo | Yes (inventory signed) | Reject fire events without ammo, flag |
| Aim bot | Partial (stats) | Reputation hit, clan review, community report system |
| Wallhack | No (in P2P) | Reputation, clan gating, dedicated servers for ranked |
| Item dupe | Yes (inventory merkle tree) | Reject malformed inventory, flag |
| Vote manipulation | Partial (vouch analysis) | Weight function penalizes sybil vouches |
| Griefing (KOS, chat abuse) | Behavioral | Negative vouches, block list, filter |

**[LIKELY]** We ship a "cheat report" flow. Any peer can flag an interaction as suspicious. Reports with co-witnesses carry weight. Persistent flags lead to a community jury (randomly selected high-rep players) reviewing the evidence and voting. This is a soft-governance layer, not a technical one.

---

## Persistence

### World State

**[COMMIT]** World state lives in the SpawnDev.WebTorrent swarm. A world is, physically, a torrent - the manifest references chunk snapshots, entity ledgers, and event logs as files within the torrent.

**[LIKELY]** Snapshots roll up every N minutes (N tuned; likely 10 minutes for hot worlds, 1 hour for cold worlds). Older snapshots are pruned from the swarm unless someone opts to seed them (archive mode).

**[LIKELY]** Peers download the latest snapshot + recent event log when joining. Event log is replayed to current state. Past that, they are subscribed to live deltas.

### Player Character State

**[COMMIT]** Character state (inventory, position, stats, progression) is stored locally in OPFS signed by the player's key. Character is tied to identity key, not to a specific world.

**[COMMIT]** Character is portable - you can take your character from server A to server B if server B allows imports. Server operators can set rules: hardcore servers require fresh characters, casual servers allow imports.

**[LIKELY]** Character state is backed up to the swarm as an encrypted blob. If a player loses their device, their hardware key (YubiKey/Trezor) can decrypt their last character backup on a new device. No cloud account needed.

### Base Ownership

**[COMMIT]** Base ownership is recorded on the world's event log as signed claim events. If a world resets, base ownership claims are invalidated (unless the world is forked - see below).

**[LIKELY]** Base ownership can transfer via signed deeds - trade, gift, or bequeath. Enables player-driven real estate markets without trust.

### World Forking

**[LIKELY]** If a world goes dormant or the community disagrees with an admin decision, anyone can fork the world at a signed snapshot. Fork = new manifest, new world key, starting state copied from the chosen snapshot. Players with characters in the original world can join the fork with their current state. Reputation and base ownership carry over; edits after the fork point do not.

This is git-for-game-worlds. Controversial but in the spirit of "no central authority can take your stuff."

---

## Matchmaking & World Discovery

### Server Browser

**[COMMIT]** The main menu has a server browser. It lists:
- Your recent worlds
- Invited worlds (from friends)
- Featured worlds (curated by us - no pay-to-feature)
- Public worlds (opted-in by their owners, searchable)

**[LIKELY]** Filters: player count, PvP/PvE, has-cryptids, region (latency), playstyle tags.

**[LIKELY]** Ping indicator per world. Worlds test-ping on hover so players can pick low-latency options.

### Quick Play

**[LIKELY]** A "Drop in anywhere" option picks a suitable public world based on player count, ping, and recent activity. Good for players who do not want to pick a specific community.

### Joining

**[COMMIT]** Joining a world requires:
1. Download manifest (via DHT or tracker lookup)
2. Verify manifest signature against world owner key
3. Download latest snapshot (via torrent - resume from local cache if previously played)
4. Connect to quorum peers (WebRTC)
5. Spawn into world at appropriate spawn point (see PLAN-Death-Corpse-Respawn)

**[COMMIT]** Join time target: under 15 seconds for a world the player has joined before, under 60 seconds for a fresh world (full snapshot download).

---

## Voice Chat

**[COMMIT]** Integrated voice chat. Uses WebRTC audio tracks alongside data channels. Opus codec. Proximity-based volume falloff. See PLAN-Audio-Design.

**[COMMIT]** Push-to-talk default for internet, open-mic option for LAN. Mute/block per peer. Accessibility: live transcription to text subtitles (see PLAN-Accessibility).

**[LIKELY]** Radio system (in-world items) allows out-of-proximity voice on specific frequencies. Radio is a separate audio channel, can be scanned by other radios on the same frequency. Adds gameplay layer - radio intercept, radio silence tactics.

**[REJECT]** Voice is not stored or replayed server-side. Voice is ephemeral, peer-to-peer, never hits our infrastructure except TURN-relay bytes (and those are not decrypted).

---

## Observability & Debugging

**[LIKELY]** Built-in net graph overlay: latency, packet loss, bandwidth usage, per-peer tick rate, authoritative peers for current chunks. Toggle with F3 (or equivalent in VR/mobile).

**[LIKELY]** Session replay: optional local recording of signed event log for the last 30 minutes. Player can replay what happened, export a clip, or submit as evidence in a cheat report. Replay is local-only unless the player explicitly shares it.

**[LIKELY]** Dev mode: a lower-level net diagnostics view that shows peer connection states, DHT queries, TURN relay traffic. Hidden behind a dev flag.

---

## Failure Modes & Recovery

### Peer Disconnect

**[COMMIT]** If a peer disconnects mid-action (shooting, mid-trade, mid-base-edit), their in-flight claims time out after a grace window. Authority for any chunks they held is handed off to the next-eligible peer.

**[COMMIT]** Players disconnecting mid-combat cannot "log out to safety" - their character remains in the world for 30 seconds after disconnect. If killed during that window, the kill is canonical when they reconnect.

### Swarm Partition

**[LIKELY]** If the swarm partitions (say, a regional internet outage splits peers into two groups), each partition continues playing independently. When the partition heals, conflicting edits are merged by timestamp + priority rules. Players notice briefly - "the last 5 minutes merged with another partition, some events may have moved." We show this transparently in the HUD.

### Dedicated Server Crash

**[COMMIT]** Dedicated server restarts replay from the last signed snapshot. Between the snapshot and crash, up to N minutes of events may be lost. Server operators can configure snapshot interval.

### Client Crash

**[COMMIT]** Client state is saved continuously to OPFS. On relaunch, the client resumes from the last-saved state and reconciles with the swarm (downloading any missed deltas).

---

## Dedicated Server Specifics

**[COMMIT]** Binary is self-contained .NET 10 with SpawnDev.WebTorrent + SpawnDev.ILGPU.P2P. Runs on any platform that supports .NET 10. No Blazor WASM dependency on the server side; all C# runs native.

**[LIKELY]** Admin API: REST + SignalR for real-time. Admins can view connected peers, ban by pubkey, rollback by snapshot, freeze the world, adjust world rules. Admin auth uses WebAuthn.

**[LIKELY]** Observability: Prometheus metrics endpoint. Grafana dashboard template shipped. Admins can see chunk authority load, per-peer bandwidth, anti-cheat flags, etc.

**[LIKELY]** Backup: server takes rolling snapshots to configurable storage (local disk, S3-compatible, Backblaze B2). Shareable as torrent info hashes.

**[LIKELY]** Mod/plugin support: server-side plugins in C# (IL loaded from DLLs after signature verification). Plugins can register event handlers, new recipes, new NPC AIs. See PLAN-Modding-Plugin-System (not yet written - Tuvok TODO).

**[COMMIT]** Server operators own their data. We do not phone home. We do not collect telemetry from dedicated servers by default. Operators can opt into anonymized usage reporting if they want to share.

---

## Cross-Platform Interop

**[COMMIT]** Browser, desktop (Blazor Desktop + MAUI), Quest VR, mobile - all play on the same worlds, same protocol. A Quest VR player and a mobile touch player can be in the same world with no feature partition.

**[COMMIT]** Client-specific rendering/input differences are invisible to other peers. Everyone sees everyone else's humanoid avatar with correct pose. VR player throwing a grenade looks the same to a mobile player as a desktop player throwing it.

**[COMMIT]** No "VR-only servers" or "mobile-only servers" by default. Operators can set mode restrictions for competitive servers if they choose (e.g., no aim-assist peers on a hardcore server).

---

## Testing Strategy

**[COMMIT]** Net layer has unit tests that exercise the real SpawnDev.WebTorrent stack, real WebRTC, real Ed25519 signing. No mock peers. The PlaywrightMultiTest infrastructure from AubsCraft and SpawnDev.WebTorrent is our model.

**[COMMIT]** A test harness can spin up N headless browser peers in the same world and run interaction scenarios (join, combat, trade, base raid, disconnect) end-to-end. Scenarios are scripted in C#.

**[LIKELY]** Soak test: 50 peers for 24 hours, random behavior, monitored for regressions, crashes, memory leaks, desync events. Runs nightly once the server browser is live.

**[LIKELY]** Adversarial test suite: scripted malicious peers attempting known cheats (speed, teleport, dupe, aimbot-like fire rate). Verifies anti-cheat signals fire.

---

## Open Questions

**[UNDECIDED]** Do we run a central user directory (pubkey -> display name lookup) for search? Pro: findable friends. Con: central trust, spam vector. Leaning toward self-published profiles only, no central directory.

**[UNDECIDED]** Who pays for TURN bandwidth at scale? Self-funded up to a point, then clan-sponsored TURN, then possibly a Supporter tier that unlocks priority TURN. Needs more thought.

**[UNDECIDED]** IPv6 preference. IPv6 peers can often direct-connect without TURN. Do we incentivize IPv6-capable peers somehow, or just let the protocol prefer IPv6 silently?

**[UNDECIDED]** E2E latency target for PvP. 80ms sounds right. 120ms is acceptable. 200ms+ starts hurting. Target matters for where we place TURN nodes geographically.

**[UNDECIDED]** Bot/NPC density cap per world. More NPCs = more simulation load on whichever peer owns their chunks. Need per-chunk NPC cap to avoid tanking a peer.

---

## Deliverables for 1.0

1. Solo mode working end-to-end
2. LAN hub-and-spoke with QR-code invite, 2-8 peers
3. Internet invite-link-based session, 2-16 peers
4. Persistent world with quorum authority, 50+ peers
5. Dedicated server binary, downloadable, documented
6. Server browser with filters, recent, invited, featured, public
7. Anti-cheat v1: plausibility, signatures, reputation-weighted trust
8. Voice chat with proximity falloff + radio item
9. Session replay + local event log
10. Net graph overlay
11. Adversarial test suite passing

---

## Dependencies

- **SpawnDev.WebTorrent** - BEP44/46 DHT, trackers, swarm, torrent snapshots
- **SpawnDev.ILGPU.P2P** - peer discovery, swarm membership
- **SpawnDev.BlazorJS.Cryptography** - Ed25519 signing/verification
- **SpawnDev.BlazorJS** - WebRTC + WebSocket wrappers, OPFS storage
- **SpawnDev.RTLink** (proven SipSorcery WebRTC patterns for desktop peers)
- **SpawnDev.BackgroundServices** - async service orchestration

All of these exist. This plan is about how to wire them together, not about building new libraries.

---

## Relationship to Other Plans

- **PLAN-P2P-Reputation-System** - identity and reputation are the trust foundation this plan builds on
- **PLAN-Combat** - co-signature hit resolution is described at a high level here, detailed there
- **PLAN-Base-Building** - base ownership is enforced by the authority model defined here
- **PLAN-Radio-Comms** - radio is a gameplay layer on top of the voice channel defined here
- **PLAN-Death-Corpse-Respawn** - spawn points and character persistence intersect with the join flow here
- **PLAN-Performance-Targets** (not yet written) - bandwidth and tick rate budgets feed into overall perf budget
- **PLAN-Modding-Plugin-System** (not yet written) - server-side plugin model is referenced here
- **PLAN-Accessibility** - voice transcription, alternative input, colorblind-safe net indicators
