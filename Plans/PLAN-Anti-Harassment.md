# PLAN-Anti-Harassment

Status markers: [COMMIT] = locked decision, [LIKELY] = leaning strongly, [UNDECIDED] = open, [DEFER] = punt to later phase, [REJECT] = ruled out.

## Purpose

Lost Spawns is a shared world with voice chat, text chat, radio comms, player squads, PvP, and moddable content. That means players will encounter other players who are hostile, rude, bigoted, predatory, or dangerous. This plan defines the tools every player gets to protect themselves, the tools server admins get to protect their community, and the safety rules we build into the engine so that harassment is hard to inflict and easy to report.

This plan is specifically about player-vs-player safety. In-game predators like cryptids are combat design, not harassment. Combat consent and opt-in PvP rules are here because they are safety tools, not PvP balance tools.

## Guiding Principles

[COMMIT] **Safety is a player right, not a privilege.** Every player has access to mute, block, and report at any time. These are never gated behind level, reputation, or in-game currency.

[COMMIT] **Safety tools are instant.** Mute/block takes effect immediately. No queue. No "your report is under review for 72 hours" before a mute works. The mute works the moment the button is pressed.

[COMMIT] **Safety tools cannot be weaponized against the person using them.** Using mute/block/report never signals the harasser. They do not get notified they were blocked.

[COMMIT] **Default to safe.** New players start with conservative defaults: proximity voice only from trusted contacts, friend requests off, direct messages from strangers muted. Opt-in to looser settings.

[COMMIT] **Server operators are accountable.** Running a community shard is a responsibility. Our Mod Hub and Server Browser surface safety-relevant server settings so players know what they are joining.

[COMMIT] **Minors get extra protection.** Default settings for verified-young accounts are stricter. Unverified accounts (most players) get the same strict defaults until they opt in to adult-style defaults.

[COMMIT] **We do not moderate opinion.** We moderate behavior. Disagreement about the game is fine. Targeted harassment of another human is not.

## What Counts As Harassment

[COMMIT] **Zero-tolerance categories (server-agnostic):**

- **Targeted racism, sexism, homophobia, transphobia, ableism, or other identity-based hate.** The content and intent both matter.
- **Sexual content involving minors.** Instant ban, law-enforcement report where required.
- **Non-consensual sexual content or solicitation.** Including in-game actions, not just messages.
- **Threats of real-world violence, doxing, or swatting.**
- **Stalking** including cross-server pursuit patterns our systems can detect.
- **Coordinated harassment.** A group of players ganging up on a single target.
- **Impersonation of real people without consent** (crew members, known players, public figures).

[COMMIT] **Server-configurable categories:**

- Rough language (servers can be strict, lax, or somewhere between).
- PvP rules (full PvP, consent-based PvP, PvE only).
- Ownership/raiding rules.
- Voice chat culture (casual, radio-protocol serious, family-friendly).

[COMMIT] **We never ban:**

- Roleplay within consent.
- In-game theft/betrayal where PvP/looting is enabled.
- Losing.
- Being bad at the game.
- Disagreeing with the dev crew.

## Player-Level Tools

### Mute

[COMMIT] **Instant voice + text mute.** Click the mute button on any player's nameplate, card in the friends list, or contextual radial. They stop speaking to you, instantly.

[COMMIT] **One-way.** They do not know. They can still hear themselves. To them, nothing changed.

[COMMIT] **Persistent across sessions.** Mute outlives the current shard/session. If you mute someone today, they stay muted tomorrow.

[COMMIT] **Scope:** per-person. You can mute one specific player without muting proximity voice in general.

[COMMIT] **Mute-all-except-friends toggle.** For players who want ambient quiet, a single setting that mutes everyone not on their friends list.

[COMMIT] **Mute preserves game audio cues.** You still hear footsteps, gunshots, other gameplay sounds. Only voice chat from muted players is suppressed.

### Block

[COMMIT] **Block is mute plus non-interaction.** Blocked players cannot DM you, send you trade requests, invite you to squads, or see your profile.

[COMMIT] **Optional hiding.** Toggle: "Hide blocked players in the world (replace with generic silhouette, muted footsteps)." For players who do not want visual presence of someone they blocked.

[COMMIT] **Not a wall-hack.** Blocking does not make the blocked player unable to interact with the world or with others. It just removes their ability to reach you.

[COMMIT] **Symmetric-ish.** Blocked players cannot interact with you, but this is not used as a PvP weapon - if a player blocks you mid-combat, your ongoing attacks still resolve under normal rules. We will not reward griefing via block abuse.

[COMMIT] **Block history.** Your blocked list is visible to you, unblockable by anyone else (including admins).

### Report

[COMMIT] **Always-available button.** Nameplate context menu, chat log, any player card. "Report player."

[COMMIT] **Categories:**
- Slurs / hate speech
- Sexual content / solicitation
- Threats / doxing
- Cheating / exploits
- Impersonation
- Griefing (context-sensitive to server rules)
- Other

[COMMIT] **Required evidence:**
- Reporter notes (free text).
- Last 30 seconds of proximity voice (if available, local-captured not server-captured, with reporter consent).
- Last 30 seconds of chat log context.
- Screenshot at time of report.
- Timestamp + shard ID + approximate in-world location.

[COMMIT] **Reports go to:**
- The server admin of the shard.
- The Lost Spawns moderation team (official shards only, or opt-in for community shards).
- If zero-tolerance category: escalated automatically to the Lost Spawns team regardless of shard.

[COMMIT] **Reporter gets a receipt.** Confirmation that the report was filed. Case ID. Estimated response time for that category.

[COMMIT] **Reporter gets a resolution.** When the report is closed, they get the outcome: action taken, no action taken, referred elsewhere. Even "no action" gets communicated so reporters know the system works.

### Direct Messages

[COMMIT] **Off-by-default from strangers.** Default setting: DMs from friends only.

[COMMIT] **Opt-in per tier:**
- Friends only (default).
- Friends-of-friends.
- Squadmates (current + recent).
- Everyone.

[COMMIT] **Message filtering.** Optional keyword filters applied locally. Player can set their own. Game ships with a strong default filter for slurs.

[COMMIT] **No gif/image DMs at all** for 1.0. Only text. Defers image-based harassment entirely. Revisit post-1.0 if demand is loud, with robust blurring + opt-in.

### Friend Requests

[COMMIT] **Off-by-default from strangers.** Default setting: friend requests must include a mutual squad or shared-shard context.

[COMMIT] **Rate limits.** A player cannot send more than N friend requests per hour. Prevents spam-invite harassment.

[COMMIT] **Mutual cancellation.** Rejecting a friend request does not notify the sender directly; they see "pending" indefinitely, which fades to "expired" after a week.

### Voice Chat Settings

[COMMIT] **Per-player volume slider.** You can dial down a loud teammate without muting them.

[COMMIT] **Push-to-talk default ON.** Voice activation is opt-in, not default. Reduces ambient harassment from players who do not realize their mic is hot.

[COMMIT] **Mic gate recommendation.** Default voice pipeline includes noise suppression + gate, configurable.

[COMMIT] **Voice-to-text local accessibility.** For accessibility, voice chat can be transcribed locally in real time. The transcriber is also a harassment filter - if a slur is detected, the audio is dropped before it plays. Per-player setting.

### Proximity and Distance

[COMMIT] **Proximity voice has a distance falloff.** A harasser cannot shout at you from across the map; voice is local by default. Radio chat is channel-based (PLAN-Radio-Comms), and channels have admins.

[COMMIT] **Fast-distancing.** If you mute someone and then walk 50m away, the system drops them from your proximity voice pool entirely until you are close again. You do not pay the latency cost of "mute this specific person" every time.

## Server-Level Tools

### Admin Roles

[COMMIT] **Three default roles on every shard:**
- **Owner:** Full control. One per shard. Can delegate.
- **Admin:** Kick, ban, mute, warn, access logs.
- **Moderator:** Warn, mute, escalate to admin. Cannot ban directly.

[COMMIT] **Customizable roles** via server config. Owners can define additional roles with granular permissions.

[COMMIT] **Audit log** for every admin action. Who did what to whom when. Auto-posted to a channel the owner designates.

[COMMIT] **Two-strike rule** for moderators. A mod who gets two complaints reviewed and upheld is automatically demoted pending owner review. Prevents admin-abuse.

### Moderation Actions

[COMMIT] **Warning.** Sends a private message to the offending player, logs the incident, counts toward auto-escalation thresholds.

[COMMIT] **Mute (server-side).** Silences the player across voice and text on this shard. Time-bounded.

[COMMIT] **Kick.** Removes the player from the shard. They can rejoin unless banned.

[COMMIT] **Ban.** Prevents the player's identity from rejoining. Time-bounded or permanent.

[COMMIT] **IP / address banning REJECTED.** We use identity-based bans (Ed25519 key, see PLAN-P2P-Reputation-System). IP bans are ineffective and punish people who share networks.

[COMMIT] **Shadow ban REJECTED.** We do not ship shadow-ban tools. Bans are transparent. The banned player gets a notification with reason + appeal path.

### Server Settings That Affect Safety

[COMMIT] **Server owners declare PvP posture** (PvE-only, consent-based PvP, full PvP) in their shard config. Surfaced in the Server Browser so players know what they are joining.

[COMMIT] **Server owners declare tone** (family-friendly, casual, mature) in their shard config. Surfaced in the Server Browser.

[COMMIT] **Server owners declare moderation activity level** (active mod team, owner-only, unmoderated).

[COMMIT] **Unmoderated shards are flagged.** "This shard has no active moderators" banner on the join screen.

[COMMIT] **Server owners can require identity verification to join.** See PLAN-P2P-Reputation-System for identity model. Verified-only shards can be set up by communities that want a higher-trust environment.

### Region Moderation

[COMMIT] **Per-region chat channels** (PLAN-Radio-Comms). Server admins can appoint channel moderators with limited scope.

[COMMIT] **Safe zones.** Server admins can designate areas as safe zones (PvP-off, verbal harassment reports auto-escalated).

[COMMIT] **New-player shelters.** Every shard has a new-player shelter at spawn that is strictly moderated and PvP-off, even on full-PvP servers. First 30 minutes of gameplay are in-shelter by default with an opt-out to leave early.

## Appeal Path

[COMMIT] **Every ban has an appeal link.** The banned player gets a URL they can use to file an appeal, visible in the ban message.

[COMMIT] **Appeals go to:**
- The shard owner, for community shard bans.
- The Lost Spawns team, for official shard bans and zero-tolerance escalations.

[COMMIT] **SLA:**
- Appeals reviewed within 7 days (target).
- Auto-closed after 30 days if reviewer does not act (with notice).
- Urgent appeals (e.g., wrongful ban during a competitive event) get a 48-hour express lane.

[COMMIT] **Appeal outcomes:**
- Ban overturned (player returns).
- Ban reduced (e.g., permanent to 30-day).
- Ban upheld.
- Ban clarified (same duration, different stated reason).

[COMMIT] **Ban reasons are documented.** Banned player gets the specific reason, example evidence, and the policy it violated. No "because we said so."

## Zero-Tolerance Handling

[COMMIT] **Automatic escalation.** Reports tagged as CSAM, real-world threats, or doxing skip local moderation and go directly to the Lost Spawns team.

[COMMIT] **Immediate server-side action.** Target shard gets an automated mute of the reported player while review is underway.

[COMMIT] **Law enforcement cooperation.** CSAM reports are forwarded to the appropriate legal body (NCMEC in the US, equivalent elsewhere) per law. We will publish our cooperation policy transparently.

[COMMIT] **Transparency report.** Annual publication of aggregate numbers: reports received, actions taken, legal disclosures. No individual case details.

## Identity and Reputation Safety

[COMMIT] **One player, one primary identity** (Ed25519 keypair, PLAN-P2P-Reputation-System). You can have alt keys but the swarm knows they are separate identities.

[COMMIT] **Reputation is earned, not bought.** Time on shard, completed trades, helpful actions, positive player tags - these build reputation. Reputation gates some interactions (e.g., only trusted-reputation players can run for squad captain in verified-only shards).

[COMMIT] **Reputation is resettable only by consent.** Banned players cannot "just create a new account" as easily - the swarm tracks key lineage and fresh keys start at low trust. Not a blanket block on new players, but grief is harder.

[COMMIT] **Whisper networks.** A community can choose to share block lists. Opt-in feature. One trusted streamer's block list can seed a newcomer's list. Abuse is possible, so lists are visible and revocable.

## Chat Content Moderation

[COMMIT] **Slur filter client-side default ON.** Configurable list + language-aware defaults. Local rendering substitutes asterisks. Can be turned off in settings.

[COMMIT] **No server-side chat logging of all messages.** Respects privacy. Message logs are only kept for the duration of an open report, and only for messages near the reported event. PLAN-Privacy-Telemetry for details.

[COMMIT] **ASCII art and creative spelling filter.** We acknowledge this is an arms race. The filter is layered (literal matching + common leetspeak). When filters fail, reports handle the gap.

[COMMIT] **Language-specific filters.** Slur filters are per-language. We work with native speakers to build them correctly, not Google Translate.

## Voice Content Moderation

[COMMIT] **No server-side voice logging.** Voice is peer-to-peer. Servers do not hear the voice unless they are a relay, and relay voice is ephemeral.

[COMMIT] **Client-side voice log on report only.** When a player files a voice report, their local client preserves the last 30 seconds of audio they heard. Consent is required - they acknowledge that the audio will be attached to the report and may be reviewed by the server admin or Lost Spawns team.

[COMMIT] **Real-time local filter (optional).** Per player's settings. Transcribes incoming proximity voice locally, drops audio with flagged content. Latency cost: ~200ms. Not on by default (latency hit), but available for players who want it.

## Voice Consent

[COMMIT] **Mic is push-to-talk by default.** Open-mic is opt-in.

[COMMIT] **New-player mic coaching.** First time a new player's voice is heard by others, the game confirms they know their mic is live. One-time onboarding check.

[COMMIT] **Mic indicator always visible.** Your own mic state (on/off, who can hear you) is surfaced in the HUD.

## Squad and Group Safety

[COMMIT] **Squad invites can be declined silently.** Rejecter is not notified to inviter.

[COMMIT] **Squad chat is scoped to squad.** Admins of the squad (captain + delegates) can mute members.

[COMMIT] **Leaving a squad is instant.** No cooldown, no "the captain must approve your departure." Player agency wins.

[COMMIT] **Captain toxicity escalation.** If a squad captain is repeatedly reported, admin tools can split the squad or revoke the captain role.

## Anti-Grooming Safeguards

[COMMIT] **Account-age-based communication limits.** New accounts have limited communication until they pass a minimum play-time threshold. Reduces throwaway account harassment.

[COMMIT] **Voice chat age-gated on unverified accounts.** Voice chat is off for the first hour for unverified accounts. The player explicitly turns it on when they are ready.

[COMMIT] **Resource-sharing limits for new accounts.** Trading, resource-gifting, and base-invitations are rate-limited for new accounts. Reduces "groomer gives new player gifts" vector.

[COMMIT] **No private meet-up systems.** The game does not facilitate off-platform contact. No "click to Discord" from in-game DMs.

[COMMIT] **Family-friendly shard type.** Server owners can flag a shard as family-friendly, which tightens multiple defaults: stricter language filter, consent-PvP off, mic age-gating extended, minors default to friends-only communication.

## Technical Implementation

### Mute/Block Storage

[COMMIT] **Per-player, stored locally (OPFS) + synced via the P2P identity layer.**

[COMMIT] **Encrypted at rest.** Your block list is yours. Even a compromised shard admin cannot read your full block list.

[COMMIT] **Portable across shards.** Mute and block follow the player, not the server.

### Report Pipeline

[COMMIT] **Report payload is a signed package:**
- Reporter's signed attestation.
- Evidence (screenshot, log excerpt, audio clip with consent).
- Category.
- Timestamp + shard ID.

[COMMIT] **Immutable once sent.** Reports cannot be retracted (but can be annotated with "I no longer wish to pursue this" by the reporter).

[COMMIT] **Deliverable offline.** If the target shard is unreachable, the report queues locally and sends when reachable.

### Admin Tools

[COMMIT] **Built into the client.** No separate admin app. Admins on a shard see admin-tier context menus in the live client.

[COMMIT] **Admin actions are two-step** (click + confirm) for ban and kick. Prevents accidents.

[COMMIT] **Admin identity is proven.** Admin role is a claim signed by the shard owner; the client verifies signatures on every privileged action.

## Streamer and Content Creator Safety

See PLAN-Streamer-Mode for deeper detail. Quick summary:

[COMMIT] **Hide nameplates / handles.** Streamers can hide all player nameplates to avoid reveal-stream-harassment.

[COMMIT] **Voice-safe mode.** Automatically mutes chat from players not on a trusted list while streaming.

[COMMIT] **Chat scrubber.** Streamers can replace all nameplates with pseudo-handles during capture.

## Minors and Age Verification

[COMMIT] **We do not verify age at the platform level.** No ID upload, no credit card handshake. Privacy matters.

[COMMIT] **Default settings assume minors may be present.** Strict defaults apply to all unverified accounts.

[COMMIT] **Platform-verified adult mode.** A player can opt in to adult defaults. No age-verification mechanism is perfect, so opt-in is a statement the player makes, not a proof.

[COMMIT] **COPPA / GDPR-K compliance.** We follow child-protection legal frameworks. If a player self-declares as under the threshold, we tighten further.

[COMMIT] **No targeted data collection for marketing.** Minors especially. See PLAN-Privacy-Telemetry.

## Dispute Resolution Between Players

[COMMIT] **Rubber-hose mediation.** Most disputes do not need moderator intervention. The block button is the first tool. Walking away is the second.

[COMMIT] **Server-level mediation.** Shard owners may offer mediation channels for ongoing disputes (contested bases, trade disagreements). Voluntary.

[COMMIT] **No reputation-by-admin.** We do not let admins spike a player's reputation. Reputation is computed from observable actions and peer ratings, not admin decree.

## Transparency Commitments

[COMMIT] **Annual transparency report.** Aggregate numbers of reports, actions, appeals.

[COMMIT] **Policy changelog.** Every change to the harassment policy is published, dated, and rationalized.

[COMMIT] **Open source moderation tools.** Anti-harassment code is in the public repo. Anyone can audit what we do and do not do.

[COMMIT] **Community feedback on policy.** Major policy changes have a community comment period before rollout.

## Non-Goals

- No shadow-banning. Transparency over stealth.
- No AI-driven automatic bans. Automation flags; humans decide.
- No "community voting" on bans at scale. Democracy is a great principle for small communities but an awful moderation tool at scale.
- No "trust score" used for gameplay. Reputation affects moderation and access to trust-gated shards, never combat or loot.
- No ad-targeting based on harassment data.
- No "pay to unban" ever.

## Open Questions

[UNDECIDED] **Global ban list visibility.** Do we publish a list of accounts permanently banned for zero-tolerance? Transparency argument yes; harassment argument (naming-and-shaming could become its own harassment) no. Default conservative: no public list, but aggregate numbers in transparency report.

[UNDECIDED] **Third-party moderation tooling.** Twitch-like bots for trusted streamers that auto-ban known bad actors. Useful but also risky. Defer to post-launch.

[UNDECIDED] **Cross-shard reputation transfer.** A player's rep on Shard A when they join Shard B: follow, reset, or admin-choice? Leaning admin-choice as server setting.

[UNDECIDED] **Restorative justice programs.** For non-zero-tolerance offenses, do we offer a "work it off" path (e.g., ban reduced if offender completes a mandatory reflection)? Research shows this works in some communities. Defer, pilot post-launch.

[DEFER] **Voice spoofing / impersonation detection.** Deepfake voices impersonating other players. Real problem, no clean solution yet. Monitor research, revisit post-launch.

## Interlocks With Other Plans

- **PLAN-P2P-Reputation-System** provides the identity and reputation layer these tools sit on.
- **PLAN-Radio-Comms** defines voice and radio channels that are subject to moderation.
- **PLAN-Networking-Multiplayer** handles the signed-packet layer that gives reports their evidence.
- **PLAN-Privacy-Telemetry** governs what we can log for moderation and how long.
- **PLAN-Streamer-Mode** provides streamer-specific safety tools.
- **PLAN-Modding-Plugin-System** must ensure mods cannot bypass safety tools (e.g., a mod that overrides the mute filter is rejected at signature verification).
- **PLAN-Onboarding-First-Hour** introduces safety tools in the tutorial.
- **PLAN-UI-HUD** provides the surfaces for mute/block/report in every context.
