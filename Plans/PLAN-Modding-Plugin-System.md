# Lost Spawns: Modding & Plugin System

## Status Legend
- **[COMMIT]** settled design decisions
- **[LIKELY]** strong preference, expect to commit
- **[UNDECIDED]** open
- **[DEFER]** post-1.0
- **[REJECT]** explicitly not doing

---

## Premise

The longest-lived survival games - DayZ, Minecraft, Garry's Mod, Ark - are kept alive by their modding communities long after the studio stops shipping content. A modding ecosystem is not a "nice to have" for a game like Lost Spawns. It is how the game stays relevant for ten years instead of two.

Two constraints shape our approach:

1. **Browser security model**: Lost Spawns runs in a browser. We cannot load arbitrary native code. We cannot let a mod read the player's disk. We cannot let a server admin ship a plugin that runs in a player's browser without the player understanding what they consented to.

2. **Cross-platform reality**: The same mod should (ideally) work on Quest VR, desktop, mobile. A mod that only runs on desktop Windows is a platform fragmentation we cannot accept.

These constraints are also opportunities. Browser-native mods are *safer* than native-binary mods: there are no memory-corruption exploits, no kernel drivers, no DLL hijacks. A well-designed in-browser plugin ecosystem could be the safest modding environment ever shipped for a survival game.

---

## Design Principles

### 1. Modding Is a First-Party Feature

**[COMMIT]** Modding is designed in from day one, not bolted on after 1.0. The APIs we expose to gameplay code ARE the mod APIs. If we cannot mod our own game with our own APIs, they are not sufficient.

**[COMMIT]** The official game shipped by the SpawnDev team consists of core + base content. Base content is built on the same plugin system mods use. Our own content is not privileged over mod content except by virtue of being shipped by default.

### 2. Explicit Trust

**[COMMIT]** Players explicitly opt into mods per-server. Server browser shows mods required for a given server before the player joins. Player can read a mod's signed manifest (author, permissions requested, description) and accept or cancel.

**[COMMIT]** No silent mod loading. Ever. A server cannot push mods to a player without consent.

**[COMMIT]** Mod code runs in a sandboxed execution environment with permissions declared up front. A mod that says "I only add new recipes" cannot access networking, OPFS, or other mods' state.

### 3. Permissions Are Granular

**[COMMIT]** Mods declare permissions in their manifest:
- `content:add` - add items, recipes, NPCs, blocks (safe)
- `content:modify` - change existing items' stats (gameplay impact, visible to player at opt-in)
- `world:read` - read world state (e.g., for a minimap mod)
- `world:write` - place blocks, spawn entities (base-building automation mods)
- `network:peer` - communicate with other mod instances (for multiplayer mod features)
- `network:external` - make outbound network requests (strictly restricted, whitelisted domains only for 1.0)
- `ui:overlay` - render UI elements (separated from base UI permission)
- `input:capture` - read player input beyond game actions (custom keybinds)
- `storage:read` - read mod storage (per-mod quota)
- `storage:write` - write mod storage
- `persist:account` - data that travels with player identity (very restricted)

**[COMMIT]** Players see the permissions list before accepting. A mod requesting `network:external` gets extra scrutiny (and a warning icon).

### 4. Deterministic Execution

**[LIKELY]** Mods that affect gameplay (not cosmetic mods) must be deterministic. Given same inputs + same world state, same output. Enforced by:
- No access to non-deterministic APIs (wall clock, random without seeded RNG, etc.)
- Mod code is evaluated by the game's simulation, not free-running
- Cross-peer verification in P2P mode: a mod that produces different state on different peers flags as non-compliant

**[LIKELY]** Cosmetic-only mods (texture packs, UI overlays, HUD customization) have no determinism requirement. They only affect local rendering.

### 5. Cross-Platform by Default

**[COMMIT]** Mod code is pure C# (compiled to DLL or IL) OR a web-native runtime (JavaScript/TypeScript with our mod API as a library). No native dependencies.

**[COMMIT]** Mod assets (textures, audio, voxel models) use the same pipeline as first-party assets. Same compression, same streaming, same CDN path. See PLAN-Asset-Pipeline (not yet written).

**[COMMIT]** A mod that works on desktop works on Quest, mobile, Chromebook. No platform splits.

---

## Runtime Models

### Server-Side Plugins

**[COMMIT]** Dedicated servers (see PLAN-Networking-Multiplayer) can load server-side plugins. These:
- Run in the server's .NET process
- Have broader access (disk, network, admin) with server operator's explicit consent
- Can be heavy logic: custom game modes, custom progression, custom NPCs
- Are signature-verified before loading
- Can be reloaded without restarting the server (if the plugin supports hot-reload)

**[COMMIT]** Server plugins are a C# class library (.dll) that the server loads at startup. Plugin entry point registers event handlers and new systems.

**[LIKELY]** Server plugins can optionally ship client-side components. When a player joins a server using such a plugin, the player downloads the client-side portion and opts into it (see "Explicit Trust" above).

**[LIKELY]** Server-only plugins (no client-side) are common and preferred: custom spawn rules, admin tools, server-side analytics, chat filters, rate limiters. They do not require player opt-in because they never touch the player's browser.

### Client-Side Plugins

**[COMMIT]** Client-side plugins run in the Blazor WASM runtime. They:
- Are pure WebAssembly or JavaScript
- Sandboxed by the browser + our execution model
- Limited by per-mod resource quota (CPU, memory, storage)
- Can be pure-cosmetic (no gameplay effect) or gameplay-affecting (must be deterministic)
- Signature-verified

**[LIKELY]** Client plugins come in flavors:
- **Cosmetic**: texture packs, UI themes, HUD layouts, music replacement
- **Tool**: minimap, crafting helper, inventory filter, log viewer
- **Gameplay**: new items, recipes, buffs - requires server-side authority agreement
- **Extension**: new behaviors that need both client + server components

### Asset Packs (No Code)

**[COMMIT]** Asset packs are the simplest form: zero code, just new textures/models/sounds that override defaults. Safe by construction.

**[COMMIT]** Asset packs install instantly, no permissions beyond `ui:overlay` equivalent. Players can freely swap asset packs.

**[LIKELY]** Asset packs can be stacked. "Retro pack" applies on top of "autumn pack" applies on top of default.

---

## Plugin API Surface

### Core Concepts

**[COMMIT]** Plugins hook into the game via an event-based API. Events: player actions, world events, NPC lifecycle, inventory changes, etc.

**[COMMIT]** Plugins register handlers; handlers receive event data and can return modifications or trigger actions.

**[LIKELY]** The API is intentionally narrow at 1.0. A small, high-quality surface beats a sprawling low-quality one. We expand based on modder requests, not speculation.

### API Categories

**[LIKELY]** 1.0 API includes roughly:

**Items & Inventory**
- Register new item definitions (stats, icon, category)
- Register recipes (crafting, cooking, smelting)
- Modify existing item properties (with server agreement)
- Read/write player inventory (with permission)

**World Generation**
- Register new biomes (world-gen plugins)
- Register new structures (dungeons, bases, POIs)
- Modify terrain gen parameters

**Entities & AI**
- Register new NPC types
- Register new animal types (wildlife)
- Attach behavior trees to entities
- Hook combat events (damage, death)

**UI & HUD**
- Register new UI panels
- Register new HUD elements
- Custom menus (within sandbox bounds)

**Chat & Communication**
- Register slash commands
- Register chat filters / transforms (server-side)
- Register chat channels

**World Events**
- Hook dynamic world events
- Register custom events (weather, cryptid spawns, caravan arrivals)

**Persistence**
- Read/write per-mod storage (quota limited)
- Attach data to player / base / entity (with permissions)

**Utilities**
- Logging (visible in dev console, not production)
- Localization (register strings with i18n)
- Analytics (opt-in telemetry)

### API Surface Discipline

**[COMMIT]** Every public API is documented and has an acceptance test shipped with the game. Breaking changes to API surface follow semver; post-1.0 we minimize breaking changes.

**[COMMIT]** The API is in a SpawnDev.LostSpawns.ModAPI package. Modders install it as a NuGet dependency (for C#) or import it as a JS library. Same versioning as the base game.

**[LIKELY]** API deprecation cycle: mark deprecated in version N, remove no sooner than version N+2 (usually years of warning).

---

## Plugin Distribution

### The Mod Hub

**[LIKELY]** We host a Mod Hub (mods.spawndev.com or similar). Modders upload:
- Plugin manifest (name, author, permissions, category, description, screenshots)
- Plugin package (DLL + assets + metadata)
- Signed with the modder's Ed25519 key

**[LIKELY]** Hub features:
- Browse by category, popularity, recency
- Per-mod ratings + reviews
- Per-mod comment threads / support
- Version history
- Compatibility badges (which game versions + which other mods known to work)

**[COMMIT]** Hub is federation-friendly. Anyone can run a mirror. Community moderators elsewhere can host their own hubs for their communities. The official Hub is one option, not the only option.

**[COMMIT]** No paid mods in 1.0. Mods are free. Modders can accept voluntary donations via their own channels (linked from their Hub profile). We do not take a cut of anything.

**[LIKELY]** Optional "featured mods" program: curated by SpawnDev team (not algorithmic). Showcases high-quality mods. Featured mods get priority on the main menu.

### Installation Flow

**[COMMIT]** From the Hub:
1. Click "Install" on a mod page
2. Prompt appears in the game showing mod name, author, permissions
3. Player reviews, clicks Accept
4. Mod downloads (usually from the Hub CDN, or torrent-distributed for popular mods)
5. Mod is available to enable/disable from the mod menu

**[COMMIT]** From a server:
1. Server broadcasts required mods to player attempting to join
2. Game shows list of required + optional mods
3. Player reviews + accepts
4. Mods download
5. Player joins server

**[LIKELY]** Offline mod install: for LAN / isolated play, mods can be installed from local files. Same permission flow. Signed manifests still required (or "unsigned - accept at your own risk" warning).

### Update Model

**[LIKELY]** Mods auto-update by default unless the player pins a specific version. Update includes permission diff - if a new version requests new permissions, player is prompted to re-consent.

**[LIKELY]** Mod authors can mark versions as "security update" which auto-installs even if auto-update is off (opt-out globally).

---

## Security Model

### Sandboxing

**[COMMIT]** Client-side mod execution is sandboxed:
- Separate V8 isolate (via browser worker) for JS mods
- Restricted AOT-compiled C# context with no dangerous APIs
- Memory quota: 64MB per mod default, scalable up to 256MB with user consent
- CPU quota: time-sliced execution, >N ms spent in a frame triggers mod slowdown warning and eventual suspension
- Storage quota: 50MB per mod default, scalable

**[COMMIT]** No filesystem access. No direct network. No access to other mods' data. No DOM manipulation outside the game's rendered UI context.

**[COMMIT]** Attempts to escape sandbox trigger automatic mod disable + report to player. Bad-actor mods get flagged on the Hub (via signature + community reports).

### Signing

**[COMMIT]** Mods are signed with the author's Ed25519 key. Signature covers the mod manifest + the mod content. Modification after signing invalidates the signature.

**[COMMIT]** Our mod Hub publishes modder public keys. Players can verify any mod against any hub's modder directory.

**[LIKELY]** Hardware-key signing option for modders (YubiKey / Trezor). Modders who sign with a hardware key get a visual trust indicator. Adds Sybil resistance to the modder ecosystem.

### Malicious Mod Response

**[LIKELY]** If a malicious mod is identified:
1. Hub flags the mod (visible warning on its page)
2. Hub revokes the signing key from the trusted directory
3. Players with auto-update enabled receive a "MOD REVOKED" notification and the mod is disabled
4. Players can still manually re-enable if they choose (at their own risk)

**[COMMIT]** We do not remotely execute code on players' machines to remove mods. We only notify; the player decides. Respect player agency.

**[LIKELY]** Malicious-mod reports are themselves signed. False reports are tracked; persistent false-reporters lose reporting weight.

### Anti-Cheat Intersection

**[COMMIT]** Mods that affect gameplay (permissions touching `world:write`, `content:modify`, `persist:account`) are visible to other players on the same server. Your name-plate shows "+Mod: Advanced Sniper Optics" or similar.

**[COMMIT]** Servers can refuse specific mods. A hardcore server might say "no UI mods, no crafting helpers, pure vanilla only."

**[COMMIT]** Servers can require specific mods ("must have 'Winterized' pack").

**[COMMIT]** P2P worlds negotiate mod compatibility among connecting peers. Everyone must have a compatible set; missing-mod peers cannot join.

---

## Developer Experience

### Mod SDK

**[COMMIT]** We ship a Mod SDK as a separate download:
- `.NET 10 SDK` for C# mods
- Template projects for common mod types (new item, new biome, new UI, texture pack)
- Documentation with examples
- Local test harness (run your mod against a local test world)
- Signing utility (sign your mod before publishing)

**[LIKELY]** Hot-reload in dev mode: save a file, mod reloads, effect visible in game without restart. Blazor's build system makes this feasible.

### JavaScript/TypeScript Track

**[LIKELY]** JS/TS modders get a separate SDK:
- `npm` package for the mod API
- TypeScript type definitions
- Same template types (item, biome, UI, texture)
- Bundling to target our runtime format

**[UNDECIDED]** Do we support both C# and JS for the same mod types, or specialize? Leaning toward both. C# for deep gameplay, JS for quick UI tweaks and texture packs.

### Documentation

**[COMMIT]** Documentation first-class. Every API has examples. Every mod type has a tutorial. Documentation is at docs.spawndev.com/lost-spawns/modding.

**[LIKELY]** Tutorials guide modders through real, buildable-and-shippable mods in 30-minute progressive lessons. "Make a new sword" -> "Make a new biome" -> "Make a new NPC faction."

**[LIKELY]** Community-contributed tutorials credited and featured.

---

## Revenue & Sustainability

**[COMMIT]** Modders keep 100% of any revenue they raise through their own channels (Patreon, donations, etc.). SpawnDev takes no cut.

**[COMMIT]** The Mod Hub hosting costs are on us. We absorb them as part of the game's operating cost.

**[UNDECIDED]** Could there ever be a "sponsor a modder" mechanic within the game? Unlikely in 1.0; complex to do fairly.

**[REJECT]** Paid mods, mod storefronts with revenue share, mod DRM, closed ecosystem. None of these. Free, open, author-controlled.

**[REJECT]** NFTs, tokens, blockchain modding. Not happening.

---

## Governance

**[LIKELY]** Community moderation of the Mod Hub:
- Volunteer moderators with reputation-gated permissions (high-rep community members can be invited to moderate)
- Transparent moderation logs (what was removed and why)
- Appeals process
- Author-side ability to respond to false claims

**[COMMIT]** SpawnDev team does not unilaterally remove mods except for clear security/legal issues (malware, unambiguous copyright violation). Gameplay decisions are community-moderated.

**[LIKELY]** Code of Conduct for the Hub. Reasonable standards: no hate, no harassment, no malware. Enforced without being absurd.

---

## Examples of Mods We Expect

To concretize what this system enables, a sampling of mods we foresee (or would build ourselves in post-launch):

- **Seasons+**: extra seasonal weather effects, deeper winter mechanics
- **Bigger Base**: increases base building size limits
- **Trader Caravans**: roaming NPC trader caravans with schedules
- **Advanced Medicine**: deeper disease model, surgical interventions
- **Cryptid Hunter**: cryptid tracking mini-game, reward mods
- **Winter Reborn**: total conversion, snow-only world gen
- **Retro Textures**: Minecraft-reminiscent pixel art texture pack
- **Dark Forest**: horror-focused atmosphere pack
- **Vehicle Overhaul**: deeper driving mechanics, more vehicle variety
- **Peaceful Mode**: no combat, focus on crafting + exploration + social
- **PvP Extreme**: faster damage, shorter time-to-kill, no-NPC
- **Rendition Pack**: alternative art style (stylized, cartoonish)
- **Fishing Paradise**: deeper fishing mechanics, dozens of new fish
- **Cryptid Parley Plus**: expands Shadelark dialog trees significantly
- **Guardians of the Peninsula**: total conversion to a hero/villain story
- **Doomsday Clock**: dynamic-world-event mod adding massive events
- **Co-op Storytime**: deeper co-op quest mechanics

Many of these will be built by the community; some by us. All are possible with the API we plan.

---

## Total Conversions

**[LIKELY]** Total conversions are a first-class mod type. A total conversion can:
- Replace all world-gen (different peninsula, different geography)
- Replace all content (new items, recipes, NPCs, cryptids)
- Replace all narrative (different lore, different Cascade, different factions)
- Reuse the engine, systems, and multiplayer

**[LIKELY]** Total-conversion mods still require SpawnDev runtime (you cannot ship a binary). They ship as very large mods, typically distributed via torrent.

**[LIKELY]** Total conversion workflow: a single-file manifest marks the mod as TC. Installing switches the game to TC mode; normal play resumes by disabling.

---

## Relationship to Base Game Content

**[LIKELY]** All our base content is structured as mods internally. The "Lost Spawns core pack" is a mod that ships bundled by default. This means:
- Every API we use is public
- Community can study our implementations as reference
- Post-launch content updates ship as updated "Lost Spawns core pack" versions
- A mod can cleanly replace a base content item (e.g., a mod's "advanced knife" can replace the base "kitchen knife" cleanly)

**[COMMIT]** No secret APIs. If we can use something, modders can too. Period.

---

## Performance Considerations

**[COMMIT]** Per-frame mod overhead budget: 1-2ms. A slow mod that spends more is flagged and the player is warned.

**[LIKELY]** Mods that exceed budget are asked for confirmation to continue running or are automatically throttled. Player can always disable.

**[COMMIT]** Mods cannot block the main thread. Heavy mod work must happen in workers or over multiple frames (cooperative multitasking).

**[LIKELY]** Networking from mods is bandwidth-budgeted per-mod. A mod that tries to send too much data per second is throttled. This prevents a bad mod from ruining a player's connection.

---

## Testing Mod Compat

**[COMMIT]** Mod CI: a modder can submit their mod to run against our automated compat suite. Tests that the mod loads, registers, passes basic sanity checks, does not trigger sandbox violations. Gives modders confidence before publishing.

**[LIKELY]** Community CI: players can run "mod stress test" scenes against their installed mods to find incompatibilities.

**[LIKELY]** Mod regression tracking: when the game updates, known mods are tested against the new version. Breaking changes are disclosed in release notes (which mods break, how modders should update).

---

## Deliverables for 1.0

1. Mod manifest format + signing
2. Client-side sandbox execution for C# + JS
3. Permissions system with player consent UI
4. Mod installation flow (from Hub, from file, from server)
5. Server-side plugin loading for dedicated servers
6. Core API categories (items, recipes, world-gen, NPCs, UI, chat)
7. Asset pack support (zero-code texture/audio replacement)
8. Mod SDK with C# and JS tracks
9. Mod documentation site
10. Mod Hub (launch version - modest feature set, grow from there)
11. Hot-reload in dev mode
12. Mod disable / enable per world / per session
13. Mod auto-update with permission-diff prompts
14. Server-side mod requirements negotiation
15. Malicious mod flagging + revocation flow

---

## Open Questions

**[UNDECIDED]** How much of the API do we freeze before 1.0? Too little = modders cannot build persistent content; too much = we cannot refactor our own engine. Probably: core gameplay API is frozen at 1.0 with minor additions; UI + chrome APIs can evolve.

**[UNDECIDED]** Mod marketplaces vs. Hub: if someone runs an alternative mod hosting service (in the spirit of federation), do we integrate with it or ignore it? Likely integrate - treat any federated source as equal to our Hub if signed properly.

**[UNDECIDED]** Mod dependency management. Mod A requires Mod B. How do we express + resolve? Standard package-manager patterns apply; need to be careful with circular dependencies and version conflicts.

**[UNDECIDED]** Mods that modify core protocol (new event types in networking). High-risk; gives modders massive power. Probably not in 1.0; might in 2.0 with careful gating.

**[UNDECIDED]** Scripting language beyond C# and JS? Lua is the default for game modding for a reason (simple, sandbox-friendly, universal). Could add later as an opinionated choice. Not 1.0.

**[UNDECIDED]** Monetization detection: mods that look like they are pushing players toward paid-mod stores elsewhere. Moderation-by-policy probably enough; no special tech detection.

---

## Relationship to Other Plans

- **PLAN-Networking-Multiplayer** - server-side plugins, server mod requirements, distributed mod content
- **PLAN-Performance-Targets** - mod performance budgets
- **PLAN-Asset-Pipeline** (not yet written) - mod assets flow through same pipeline
- **PLAN-Accessibility** - accessibility mods are first-class (large text, color-blind overlays, input adapters)
- **PLAN-P2P-Reputation-System** - modder hardware-key signing, author trust indicators
- **PLAN-UI-HUD** - mod UI extensions, HUD customization
- **PLAN-Localization-I18n** (not yet written) - mods can register localized strings
- **PLAN-Release-Phases** (not yet written) - when mod API freezes for 1.0
