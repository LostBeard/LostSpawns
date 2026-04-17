# Lost Spawns: P2P Reputation & Identity System

## Vision

In DayZ, your 200-hour character lives on a central hive server. Server goes down, character gone. Account hacked, character gone. Server admin bans you, character gone.

In Lost Spawns, your identity IS your cryptographic key. Your reputation is what other players say about you, published to a distributed hash table that nobody owns. Nobody can take it away. Nobody can forge it. You own your identity the same way you own your house keys.

This is what player identity looks like when you build it right.

---

## Core Architecture

### Identity = Ed25519 Key Pair

Every player has an Ed25519 key pair generated on first launch:
- **Public key** = your player identity (32 bytes, hex or base64 for display)
- **Private key** = stored in browser IndexedDB (encrypted), never transmitted
- **Display name** = self-published, not unique (like Discord - name + discriminator from pubkey suffix)

Your public key is your address in the game world. Other players look you up by your key. No email, no password, no account server.

**Hardware key option:** YubiKey or Trezor holds the master signing key. Browser generates a session key, hardware key signs a delegation certificate. Reputation vouches signed by hardware key carry more weight (proof of dedicated physical device = harder to create Sybil accounts).

### Stats Publishing via BEP46

Player stats are published to the BitTorrent DHT as BEP46 mutable items:

```
Key: player_public_key
Salt: "stats"
Sequence: auto-incrementing (prevents replay)
Value: CBOR-encoded stats object (up to 1000 bytes)
Signature: Ed25519 signature over (salt + sequence + value)
```

Any peer on the DHT can read your stats by knowing your public key. The signature proves YOU published them. The sequence number prevents rollback.

**Salt channels (different data per key):**
| Salt | Content | Updated |
|------|---------|---------|
| `stats` | Core gameplay stats | Every session end |
| `profile` | Display name, avatar config, bio | On change |
| `rep` | Reputation summary (computed from vouches) | Periodically |
| `clan` | Clan membership, role | On change |
| `bounties` | Active bounties placed by this player | On change |
| `achievements` | Unlocked achievements | On unlock |

### What Stats Look Like

```json
{
  "v": 1,
  "name": "Survivor_7f3a",
  "hours": 847.3,
  "alive": true,
  "longestLife": 142.5,
  "deaths": 23,
  "kills": { "players": 8, "zombies": 1247, "animals": 89 },
  "crafted": 3412,
  "built": { "walls": 247, "doors": 18, "fires": 89 },
  "distance": 892.4,
  "trades": 34,
  "vouches_received": 12,
  "vouches_given": 8,
  "firstSeen": 1745000000,
  "lastSeen": 1745200000
}
```

---

## Reputation: Web of Trust

### The Core Insight

Your reputation is NOT what you say about yourself. It's what OTHER players say about you, cryptographically signed and published to the DHT.

### Vouch System

A vouch is a signed attestation from one player about another:

```
Key: voucher_public_key
Salt: "vouch:" + target_public_key_hex
Value: {
  "type": "vouch",
  "target": "target_public_key_hex",
  "rating": 1,        // 1 = positive, -1 = negative
  "category": "trader", // trader, fighter, builder, medic, leader
  "comment": "Fair trader, good prices",
  "timestamp": 1745100000
}
Signature: Ed25519 by voucher
```

**Anyone can read it.** When you encounter a player, you query the DHT for vouches about their public key. Each vouch is independently verifiable - signed by a different player.

### Reputation Score

A player's reputation score is computed client-side from collected vouches:

```
reputation = sum(vouch.rating * weight(voucher))
```

Where `weight(voucher)` is:
- **Base weight:** 1.0
- **Hardware key bonus:** x1.5 (voucher uses YubiKey/Trezor)
- **Reputation scaling:** x(1 + voucher_reputation * 0.1) - more reputable vouchers carry more weight
- **Recency decay:** vouches older than 30 days lose 10% per week
- **Category matching:** vouches in the relevant category count more

### Why This Works

**Can players lie about their own stats?** Yes. But:
- Your stats page shows "self-reported" for stats only you published
- Your reputation score is from OTHER players' vouches - you can't fake those
- A player claiming 1000 kills with zero vouches is suspicious
- A player with 50 positive trader vouches is trustworthy for trading

**Can players create fake vouch accounts?** Yes, but:
- New accounts with no play time have near-zero weight
- Hardware key vouches are expensive to mass-produce
- The weight function means 100 zero-reputation vouches < 5 high-reputation vouches
- Sybil resistance comes from the cost of building real reputation

**Can players grief the reputation system?** Somewhat, but:
- Negative vouches from low-reputation accounts carry little weight
- If someone consistently gives unfair negative vouches, their own reputation drops
- The system converges on honest behavior because reputation is valuable

---

## Gameplay Integration

### Meeting a Stranger

You're looting a town. Someone walks around the corner. In DayZ, you have nothing to go on. Shoot or don't.

In Lost Spawns:
1. You see their display name floating above their head
2. Press Tab to "inspect" - queries their public key from DHT
3. See: **Reputation: 47** (positive), **Category: Medic/Trader**, **Hours: 312**, **Vouches: 18 positive, 2 negative**
4. Their most recent vouch: "Saved my life at the hospital, shared medical supplies" - signed by a player with reputation 65
5. Decision: this person is probably trustworthy. Lower your weapon.

### After a Positive Interaction

You traded with them. Fair deal, good prices, no tricks.

1. Open the interaction menu (UIRadialMenu!)
2. Select "Vouch"
3. Choose category: "Trader"
4. Optional comment: "Fair prices, honest trade"
5. Your vouch is signed by your key and published to DHT
6. Their reputation increases by your weight

### Bounty System

Player "DarkReaper_a3f2" has been KOS'ing everyone at the airfield. Multiple negative vouches.

1. Open the bounty board (UIList in GameUI!)
2. Post a bounty: 500 scrap metal for proof of kill against DarkReaper_a3f2's public key
3. Bounty published to DHT via BEP46 (salt: "bounty:" + target_key)
4. Any player who kills DarkReaper and both parties publish the event can claim the bounty
5. Bounty is verified by checking both kill reports match

### Clan/Faction System

A clan is a group of public keys with a shared clan key:

1. Clan founder generates a clan Ed25519 key pair
2. Founder publishes clan info to DHT (name, rules, requirements)
3. Members are added by the founder signing a "membership" vouch for their key
4. Clan reputation = average of member reputations
5. Minimum reputation requirement to join (e.g., rep >= 20)
6. Clan leader transfer = sign a delegation to a new key

### Trade System

Trustless peer-to-peer trading:

1. Player A offers items, Player B offers items
2. Both review the offer in the trade UI (UIGrid showing item slots!)
3. Both "accept" - each signs a trade receipt containing both players' items
4. Trade executes atomically in game logic
5. Trade receipt published to DHT - proof that the trade happened
6. Both players can vouch for each other as fair traders
7. If either player backs out or scams, the other publishes evidence

---

## Technical Implementation

### Data Flow

```
Player Action
  -> Game Client signs data with Ed25519 private key
  -> Publish to DHT via BEP46 (SpawnDev.WebTorrent)
  -> Other players discover via swarm (SpawnDev.ILGPU.P2P)
  -> Lookup by public key + salt
  -> Verify Ed25519 signature (SpawnDev.BlazorJS.Cryptography)
  -> Display in GameUI (UIList, UIProgressBar, UITooltip)
```

### BEP46 Data Budget

BEP46 mutable items are limited to ~1000 bytes per item. Strategy:
- Core stats fit in one item (~500 bytes CBOR-encoded)
- Profile in one item (~200 bytes)
- Each vouch is a separate item (voucher's key + salt = target key)
- For large data (achievement details, trade history): publish a torrent info hash via BEP46, actual data in the torrent

### Key Management

**Generation:** First launch generates Ed25519 key pair. Stored in IndexedDB encrypted with a user-chosen passphrase.

**Backup:** Export key as encrypted file. Import on another device. Or use hardware key (YubiKey) as the master - no export needed, key never leaves the device.

**Recovery:** If you lose your key, you lose your identity. That's the trade-off of true ownership. Mitigation:
- Social recovery: designate 3 trusted friends. Any 2 of 3 can sign a "key migration" attestation linking your old key to a new key. Your reputation transfers.
- Hardware key backup: Trezor supports multiple key slots
- Cloud backup: encrypted key file in OPFS or user's cloud storage

**Multiple devices:** Same key on multiple devices via encrypted export/import. Or hardware key plugs into any device.

### Integration with Existing SpawnDev Stack

| Component | Role |
|-----------|------|
| SpawnDev.BlazorJS.Cryptography | Ed25519 key generation, signing, verification |
| SpawnDev.WebTorrent (BEP44/46) | DHT publish/read for stats, vouches, profiles |
| SpawnDev.ILGPU.P2P | Swarm discovery, find nearby players |
| SpawnDev.GameUI | UI for reputation display, vouch UI, trade UI, bounty board |
| SpawnDev.VoxelEngine | Game world rendering |
| WebAuthn (YubiKey) | Hardware-backed key for high-trust vouches |

Everything is already built. This is wiring, not building.

---

## Anti-Cheat Considerations

### What this system CAN'T prevent
- Modified game clients reporting false stats
- Collusion between friends to inflate each other's reputation
- A skilled social engineer building trust then betraying

### What this system DOES prevent
- Identity theft (keys are cryptographic)
- Reputation forgery (vouches are signed by independent parties)
- Stat rollback (BEP46 sequence numbers)
- Central authority abuse (no admin can delete your reputation)
- Server shutdown (DHT is distributed, no single point of failure)

### What this system DISCOURAGES
- Serial betrayal (negative vouches accumulate, reputation tanks)
- Throwaway griefing accounts (new accounts have zero reputation, excluded from high-rep interactions)
- KOS culture (knowing someone's reputation changes the shoot/don't-shoot calculus)

### The DayZ Lesson
DayZ's biggest problem isn't zombies. It's that every stranger is equally dangerous because you have zero information about them. Reputation gives you information. Information enables trust. Trust enables cooperation. Cooperation makes the game worth playing.

---

## Phase Plan

### Phase 1: Identity (integrate during P2P work)
- [ ] Generate Ed25519 key pair on first launch
- [ ] Store in IndexedDB (encrypted)
- [ ] Display name publishing via BEP46
- [ ] Key export/import
- [ ] "Inspect player" UI showing public key + basic info

### Phase 2: Stats Publishing
- [ ] Session stats tracking (play time, kills, deaths, distance)
- [ ] Auto-publish to DHT on session end
- [ ] Stats query by public key
- [ ] Stats display in player inspection UI

### Phase 3: Reputation
- [ ] Vouch publishing (positive/negative, categorized)
- [ ] Vouch collection and score computation
- [ ] Reputation display in player nameplate
- [ ] Reputation-gated interactions (minimum rep for clan join, trade)

### Phase 4: Advanced
- [ ] Bounty system
- [ ] Clan/faction keys
- [ ] Trade receipts
- [ ] Social recovery (key migration)
- [ ] Hardware key (YubiKey/Trezor) integration
- [ ] Reputation leaderboard (top reputed players on the server)

---

## The Pitch

"In Lost Spawns, you own your identity. Your reputation is earned, verified, and permanent. No server can take it from you. No admin can reset it. When you meet a stranger in the wasteland, you'll know whether to lower your weapon - because their reputation speaks louder than their words."
