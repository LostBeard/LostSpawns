# PLAN-Privacy-Telemetry

Status markers: [COMMIT] = locked decision, [LIKELY] = leaning strongly, [UNDECIDED] = open, [DEFER] = punt to later phase, [REJECT] = ruled out.

## Purpose

Lost Spawns runs in a browser, in a peer-to-peer swarm, on hardware we do not control. We handle player identity, voice chat, positional data, save games, and crash logs. This plan defines exactly what we collect, exactly what we do not collect, how long we keep it, who can see it, and how a player can take all of it back.

This plan is the privacy promise. Everything else follows from it.

## Guiding Principles

[COMMIT] **No data is collected by default.** Telemetry is opt-in. Analytics are opt-in. Crash reporting is opt-in. The default experience is fully private.

[COMMIT] **Minimum collection, maximum retention limits.** If we can answer our question with less data, we collect less. If we can answer it with data that expires in 30 days, we do not keep it 90.

[COMMIT] **No third-party trackers.** No Google Analytics. No Meta pixel. No advertising networks. No third-party anything in the client.

[COMMIT] **No ads.** Ever. Never. The business model is donations, not attention harvesting.

[COMMIT] **Local-first.** Data lives on the player's device. When data leaves the device, the player knows and consented.

[COMMIT] **Transparent data flow.** Every piece of data that leaves the device is documented publicly. Source code is public. Player can inspect.

[COMMIT] **Portable.** Players can export all their data in a standard format. Save files, identity keys, chat logs, block lists, progression. Any time. One click.

[COMMIT] **Right to delete.** Players can delete all their data from our servers at any time. We comply within 30 days of request, faster where possible.

[COMMIT] **Compliance exceeds the bar.** GDPR, CCPA, COPPA, Washington's My Health My Data, Delete Act - we intend to satisfy all of them and exceed them. Not because we have to, because we should.

[COMMIT] **Security is privacy's floor.** The strongest privacy policy is irrelevant if we leak data through bad engineering. Every data path is reviewed for security.

## Data We Collect (And Do Not)

### What we collect on the device (OPFS, encrypted at rest)

- Player identity keypair (Ed25519).
- Save games (solo worlds, character progression, cosmetics).
- Block list.
- Friend list.
- Settings (graphics, audio, accessibility, controls).
- Chat logs (local only, last 30 days, configurable).
- Session history (what shards, when, how long - for "recent servers" UI).

This is **stored locally**. It never leaves the device unless the player opts in or initiates an action that requires it (e.g., syncing with another of their own devices).

### What we collect at the P2P protocol level (ephemeral, unavoidable)

- Other peers' public keys (required for identity verification).
- Other peers' signed position updates (required for rendering other players).
- Chunk authority claims (required for world sync).

This data is ephemeral. It is the game state of a live session. It does not get stored beyond the session unless explicitly requested (e.g., save game snapshot).

[COMMIT] This data is not forwarded to us. It stays in the peer mesh.

### What we collect on official infrastructure (opt-in)

**Tracker relays** (tracker.spawndev.com or similar):
- Peer public key + shard ID (required to pair peers for session).
- Duration: held only while session is live. Purged within hours.

**Matchmaking service** (if it exists for official shards):
- Shard capacity, per-shard player count.
- Aggregate, not per-player.

**Crash reporting service** (opt-in):
- Stack trace.
- Lost Spawns version + build hash.
- Browser + OS + GPU info.
- Anonymous session ID (random UUID, new each session).
- No identifying information about the player.
- Retention: 90 days.

**Gameplay telemetry** (opt-in):
- Aggregate: how many players, what platforms, what playtime distribution, what retention patterns.
- Never per-player identifiable.
- Retention: 30 days raw, rolled up to aggregate monthly.

**Bug reports** (explicit action):
- Contents of the report (text, screenshot, log excerpt, voice clip with explicit per-report consent).
- Reporter's identity (if they choose to include it for follow-up).
- Retention: duration of case + 90 days.

**Donations** (if any):
- Crypto transactions are public by nature of blockchain. We do not collect additional PII.
- No credit card, PayPal, or identity-linked payment options at 1.0 (see feedback_no_proton.md spirit).

### What we never collect

[COMMIT] **Personal information we refuse to ask for:**
- Real name.
- Physical address.
- Phone number.
- Email address (except voluntarily for crash reports or bug reports, never mandatory).
- Age (except self-declared for age-gating toggles; never verified).
- Government ID.
- Payment methods (donations are crypto, public-ledger transparent).

[COMMIT] **Behavioral data we refuse to collect:**
- Chat content (no content-level chat monitoring).
- Voice audio (no server-side voice recording).
- Save game contents (no cloud-reading of player saves).
- Play session video.
- Keystrokes, mouse movements, or input patterns.
- Screen content.

[COMMIT] **Cross-context data we refuse to collect:**
- Other apps on the player's device.
- Other tabs in the browser.
- Fingerprinting data (canvas, audio, font, WebGL fingerprints).

[COMMIT] **Inferred data we refuse to derive:**
- Demographic inference (age, gender, ethnicity).
- Behavioral scores for marketing.
- Risk / creditworthiness scores.
- Anything resembling profiling for commercial sale.

## Opt-In Flows

[COMMIT] **First launch dialog.** Player lands in the main menu. The only modal is a clear, plain-language privacy box:

> Lost Spawns does not collect any data by default. You can opt in to help us improve the game. All options can be changed any time in Settings > Privacy.
>
> [ ] Send anonymous crash reports when the game crashes.
> [ ] Send anonymous gameplay statistics (no identifying info).
> [ ] Receive optional news about Lost Spawns releases.

[COMMIT] **All three default OFF.**

[COMMIT] **Plain-language copy.** No "to improve your user experience." Clear statements of what we collect and why.

[COMMIT] **Linked policy.** Full privacy policy accessible from the dialog.

[COMMIT] **Per-session reminder.** Not pestering, but on build-update (version bump) the dialog re-appears to confirm settings still apply. Default: keep current setting. Player does not have to re-answer.

### Gameplay Telemetry (if opted in)

[COMMIT] **What goes out per session:**
- Session duration bucket (0-15m, 15-60m, 1-4h, 4+h).
- Platform tier (Mobile-Low, Desktop-High, etc.).
- Backend in use (WebGPU, WebGL, Wasm).
- Random anonymous session ID (new every session).
- Did-the-session-end-in-crash boolean.
- First-hour completion boolean.

[COMMIT] **Explicitly NOT in gameplay telemetry:**
- Which shards they joined.
- Which cryptids they killed.
- Who they played with.
- Chat / voice data.
- Location in the world.
- Character name or identity.

### Crash Reports (if opted in)

[COMMIT] **Attached data:**
- Stack trace.
- Client log (last 500 lines).
- Lost Spawns version, build hash, UTC timestamp.
- Browser, OS, GPU (from navigator.userAgent-style strings, sanitized).
- Anonymous session ID (same new-per-session UUID).

[COMMIT] **Not attached:**
- Identity key or public key.
- Save file contents.
- Chat logs.
- Screenshots (unless reporter explicitly adds one via bug report flow).
- IP address (we intentionally do not log IPs; see below).

[COMMIT] **IP logging at crash endpoint.** We accept inbound connections to submit crashes. TLS termination logs IP automatically. We set our log retention to 24 hours and strip IP from the crash record. The IP never joins the crash data.

## Identity and Pseudonymity

[COMMIT] **Player identity is a public key.** No username required. No email required. Ed25519 keypair generated on first launch, stored in OPFS.

[COMMIT] **Display name is chosen by player.** Does not have to be unique across the swarm. Verification-of-real-human is not a 1.0 feature.

[COMMIT] **Alt keys are allowed.** Players can create additional identities for alt characters, privacy separation, or testing. The swarm may track key lineage (optionally) to prevent ban evasion but does not share that data with other players by default.

[COMMIT] **No real-name requirement.** Never. Not at 1.0, not at 10.0.

[COMMIT] **Identity portability.** Player can export their keypair and import it on another device. Private key never leaves the device unless the player does the export.

[COMMIT] **Identity deletion.** Player deletes their local storage and the identity is gone for them. Peers in the swarm cannot be force-deleted (they hold the public key; it will age out of indexes as the player stops using it). Transparency-report commitment: we surface the non-deletable half of identity (the distributed half) clearly in the privacy policy.

## Save Data Privacy

[COMMIT] **Saves are local by default.** OPFS-backed, encrypted with a device-local key.

[COMMIT] **Optional cloud save (post-1.0).** If we ship cloud save, it is opt-in, end-to-end encrypted (player holds the key), and we cannot read it. If the player loses their key, they lose the save.

[COMMIT] **Solo vs multiplayer saves.** Solo saves stay on the device. Multiplayer worlds are swarm state - the player's character travels with them but the world state is shared. See PLAN-Save-Persistence.

[COMMIT] **Export in standard format.** Saves can be exported as a `.lostspawns-save` file. Specification is documented and stable.

## Chat Log Privacy

[COMMIT] **Voice chat is ephemeral.** Not recorded by default.

[COMMIT] **Text chat is local-only by default.** Stored in OPFS on the player's device. 30-day rolling window, configurable down to zero.

[COMMIT] **Server-side chat logging opt-in at server level.** Server owners may enable chat logging for their shard for moderation. Players are told before joining.

[COMMIT] **Chat log from reports.** When a player files a report, chat excerpts are attached voluntarily. Consent required per report.

## Voice Chat Privacy

[COMMIT] **Peer-to-peer voice.** Voice goes directly between players. No server records.

[COMMIT] **No voice processing at servers.** Relay servers (if used for NAT traversal) forward encrypted audio packets. They cannot decrypt or analyze.

[COMMIT] **Local voice recording for report only.** When a player files a voice report, their client captures the last 30 seconds of incoming audio with consent, attaches to the report. Otherwise nothing is saved.

[COMMIT] **No voice-to-text server-side.** Any voice-to-text happens on the reporter's device and is discarded unless attached to a report.

## Location / Positional Data

[COMMIT] **In-game position shared with peers (required).** Other players need to see where you are to play with you.

[COMMIT] **In-game position NOT stored centrally.** We do not have a heatmap service showing where everyone has been.

[COMMIT] **Real-world location never collected.** No browser geolocation API access. No IP geolocation inference for gameplay features.

[COMMIT] **Shard-choice preference.** If we offer latency-based shard suggestions, the latency measurement happens client-side; we do not collect the player's region.

## Children and Minors

[COMMIT] **We do not target children for marketing.** Period. Full stop. No sponsored content, no kid-targeted offerings, no child-oriented data collection.

[COMMIT] **COPPA compliance.** For unverified accounts (everyone by default), we assume the player might be under 13 and apply COPPA-level restrictions. No personal information collected.

[COMMIT] **Strict mode.** Players who self-identify as under 13 (or whose parents do on their behalf) get the strictest defaults, cannot opt in to any telemetry, and voice chat is opt-in-by-adult rather than by player.

[COMMIT] **No persistent cookies or local storage IDs used for tracking.** OPFS is for game data, not ad tracking.

[COMMIT] **No school/classroom-specific features that create obligation.** If we ever want to pitch Lost Spawns in education, we will add features then, under proper COPPA/FERPA compliance. 1.0 is not for schools.

## Modding Privacy

[COMMIT] **Mods run in a sandbox** (PLAN-Modding-Plugin-System). Sandboxed mods cannot access:
- Identity keys.
- Chat logs (unless scoped).
- Other mods' data.
- System clipboard.
- Browser fingerprint APIs.
- Arbitrary network endpoints.

[COMMIT] **Mods declare permissions.** Similar to browser extensions. "This mod wants: network access to X, read save data, record position." Player approves or rejects before install.

[COMMIT] **Mod metadata is public.** Who wrote it, what permissions it requests, license. No "secretly spyware" mods.

[COMMIT] **Mod Hub curation.** Mods that misrepresent their permissions are delisted and revocation signals are broadcast.

## Third-Party Hosting

[COMMIT] **Official shards hosted on our own infrastructure or known providers.** Providers disclosed in the privacy policy.

[COMMIT] **Community shards are the player's choice.** We do not control what community shard operators do. They declare their moderation posture, tone, and logging policy in their shard config; players see it before joining.

[COMMIT] **Trust chains.** If a player trusts Shard Operator X, they trust X's data handling. We surface the operator's claimed practices but cannot enforce them remotely.

## Right to Access / Portability / Deletion

[COMMIT] **Access:** Player can view all data stored about them. Request path: privacy@lostspawns.spawndev.com (or wherever). Response within 30 days (target: 7 days).

[COMMIT] **Portability:** Export all data in a machine-readable format. Includes identity, saves, settings, block/friend lists, session history. Free.

[COMMIT] **Deletion:** Player can request erasure. We delete all opt-in data associated with their session IDs / identity. Some data cannot be deleted (swarm-distributed public keys, on-chain donations if any); we disclose this at deletion time.

[COMMIT] **Bulk self-service tools.** Where technically possible, deletion happens in-app. Settings > Privacy > Delete All Data. Confirm. Gone.

## Infrastructure Security

[COMMIT] **TLS everywhere.** No cleartext connections.

[COMMIT] **Minimum encryption.** AES-256-GCM at rest, TLS 1.3 in transit.

[COMMIT] **Identity keys in OPFS with device-local encryption.** Private key never transmitted.

[COMMIT] **Crypto via SpawnDev.BlazorJS.Cryptography.** Always `IPortableCrypto`. No direct crypto class instantiation (CLAUDE.md SpawnDev libraries section).

[COMMIT] **Regular security review.** Pre-1.0 third-party audit. Post-1.0 annual review.

[COMMIT] **Responsible disclosure.** SECURITY.md in repo, PGP key, 90-day standard disclosure timeline.

[COMMIT] **Public incident reports.** If we experience a breach, we disclose publicly within 72 hours of discovery. What happened, what was exposed, what we did, what players should do.

## Logging and Metrics

[COMMIT] **Minimum server logging.** Our own infrastructure logs only what is operationally necessary: connection counts, error rates, latency buckets. No per-player logs unless a report is active.

[COMMIT] **Log retention:**
- Operational logs: 30 days.
- Security logs: 90 days.
- Legal-hold logs: duration of legal matter + 30 days.

[COMMIT] **No data sold.** To anyone. For any reason. Data we collect is used to operate the game, fix bugs, and improve performance. Nothing else.

## Compliance Posture

### GDPR

[COMMIT] Lawful basis: consent (telemetry), contract (service delivery), legitimate interest (security).
[COMMIT] Data Protection Officer: TJ (LostBeard) during small-team phase; named officer post-scale.
[COMMIT] Data subject rights supported: access, rectification, erasure, portability, restriction, objection.
[COMMIT] DPA available on request for server operators in the EU.

### CCPA / CPRA

[COMMIT] Do Not Sell My Personal Information: trivially satisfied because we do not sell.
[COMMIT] California-specific rights enumerated in the privacy policy.

### COPPA

[COMMIT] Strict mode defaults cover COPPA.
[COMMIT] Verifiable parental consent flow for under-13 telemetry opt-in: we refuse that flow (no telemetry collected from declared-minors, period).

### Other jurisdictions

[COMMIT] Brazil (LGPD), UK (UK-GDPR), Canada (PIPEDA), Japan (APPI), Australia (Privacy Act), India (DPDP), South Korea (PIPA), Singapore (PDPA) - monitor and comply.

## Privacy Policy

[COMMIT] Published at `lostspawns.spawndev.com/privacy` in plain language.

[COMMIT] Machine-readable summary in JSON for privacy assistant tools.

[COMMIT] Version history accessible. Every change dated, with a diff against the prior version.

[COMMIT] Email contact for privacy-specific requests.

[COMMIT] Not written by lawyers to confuse. Written to inform. Legal review after, not before.

## Transparency Report

[COMMIT] **Published annually.** Key metrics:
- Total crash reports received.
- Total bug reports received.
- Total harassment reports received and actioned.
- Total legal requests for data (even if we had nothing to disclose).
- Total account deletion requests fulfilled.
- Any breaches during the reporting year.

[COMMIT] **Aggregate, not individual.** No player-identifying data in the transparency report.

## Data Processing Agreements for Server Operators

[COMMIT] Community shard operators are technically controllers of their shard's data. We provide a template DPA so operators can understand their obligations.

[COMMIT] Operators running shards in the EU should adopt privacy practices compatible with their jurisdiction. We publish guidance.

[COMMIT] We do not offload our responsibilities to operators. Our engine provides privacy-safe defaults so most operators inherit good practice without reading the DPA.

## Analytics Alternative: Self-Service Stats

[COMMIT] Players can see their own stats locally. Playtime, kills, distance traveled, bases built. These are local. We do not collect them.

[COMMIT] Servers can surface their own shard stats (player count, uptime). They do not share them with us.

[COMMIT] We publish engine-wide stats (total active players, popular platforms) derived only from opt-in telemetry. If opt-in rate is too low to compute meaningful stats, we say so rather than extrapolate.

## Opt-Out Details

[COMMIT] **Settings > Privacy** is the single surface. Every knob. Every switch.

[COMMIT] **"Delete my data" button.** Confirms, deletes, reports success.

[COMMIT] **"Export my data" button.** Downloads a `.lostspawns-data-export.zip`.

[COMMIT] **"Disable all telemetry" button.** Single toggle that turns off every opt-in.

[COMMIT] **"Clear my local data" button.** Wipes OPFS. Confirms understanding this cannot be undone.

## Non-Goals

- No "we have to collect X to give you a better experience." If we can run the game without collecting X, we collect nothing.
- No "trust us, we are the good guys." Code is public, audits are published.
- No "we reserve the right to change the privacy policy at any time without notice." Every change is announced, with a comment period for major changes.
- No dark patterns in settings. Opt-in is obvious. Opt-out is obvious.
- No fingerprinting.
- No device-ID tracking.
- No cross-device tracking by us.

## Open Questions

[UNDECIDED] **Crash reporter hosting.** Self-hosted vs a privacy-respecting third party (e.g., Sentry self-hosted). Leaning self-hosted for full control.

[UNDECIDED] **Metrics aggregation tool.** We need some kind of dashboard. Leaning: Grafana or ClickHouse self-hosted, no third-party analytics vendors.

[UNDECIDED] **Crypto donation processing.** Do we run our own donation endpoint or use an aggregator? Trezor-direct donations (crypto addresses posted) are preferred (reference_trezor_crypto.md, feedback_no_proton.md). No third-party payment processors.

[UNDECIDED] **Email newsletter service.** If we run one at all, self-hosted or a privacy-respecting provider. No Mailchimp tracking pixels.

[DEFER] **Federated identity.** E.g., "sign in with Trezor" or a Web3 identity solution. Useful but adds complexity. Revisit post-1.0.

## Interlocks With Other Plans

- **PLAN-P2P-Reputation-System** defines the identity layer.
- **PLAN-Anti-Harassment** depends on privacy-safe report pipelines.
- **PLAN-Save-Persistence** honors privacy commitments for saves.
- **PLAN-Modding-Plugin-System** enforces mod-sandbox privacy.
- **PLAN-Testing-QA-Strategy** opt-in crash telemetry rules come from here.
- **PLAN-Streamer-Mode** depends on being able to hide identity locally.
- **PLAN-Radio-Comms** defines the ephemeral voice pipeline that this plan requires.
