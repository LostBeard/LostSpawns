# Economy - Brainstorm and Plan

**Status:** Living brainstorm. Decisions get locked as features mature.
**Owner:** Captain (TJ)
**Consulted:** Tuvok (research/planning), Data (VoxelEngine editor)
**Last updated:** 2026-04-16

---

## Status markers

- **[COMMIT]** - committed to v1.0, active work or next in queue
- **[LIKELY]** - strong fit, assumed yes unless something knocks it out
- **[UNDECIDED]** - interesting, uncertain value/cost tradeoff, revisit before touching
- **[DEFER]** - post-v1.0 or beyond scope
- **[REJECT]** - considered and ruled out (with reason)

---

## Vision

**No universal currency. Barter rules.** Governments collapsed, fiat is paper. What trades hands: **ammo, batteries, bandages, clean water, cigarettes.** De-facto cash. Players set their own prices. Vendors set theirs. Markets emerge.

Inspired by DayZ player-run economies + Fallout 76 C.A.M.P. vending machines + STALKER trader rep + Tarkov barter-tree progression. A post-collapse world prices what keeps you alive.

**Design goals:**

1. **No gold coin / universal credit.** Pricing is by utility - a box of 7.62 buys bandages because both have real use.
2. **Player-driven pricing via vending.** Set your price, put goods in a machine, leave camp. Other players come shop.
3. **NPC traders with personality.** Faction vendors, black-market fixers, wandering merchants - each with rotating stock and relationship arcs.
4. **Reputation is priced in.** Good rep = discounts and rare stock. Infamy = refused service and bounty contracts placed on you.
5. **Information is a good.** Intel (coord, crash-site loot tips, base blueprints) trades alongside physical items.

---

## Foundation (what exists today)

**Nothing yet.** Greenfield. Depends on:

- **Entity system** (VoxelEngine Phase 12) - NPC vendors, vending machines, trade stall UI
- **Persistence** (Phase 8 OPFS) - vendor stock, price lists, rep values, transaction log
- **Networking / P2P** - async vendor visits (buyer offline from seller), stock sync
- **Crafting + clothing stacks** - goods that change hands need shared item registry
- **Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))** - Social skill + trade perks affect pricing

---

## Currency model

### [COMMIT] Barter-first, utility-priced

- No single unit of account
- Goods are priced against each other through practical use
- Bandages rare locally = price spikes. Scav-town flooded with ammo = ammo price drops.

### [LIKELY] De-facto currencies (informal)

Items so universally useful they become shorthand currency in practice:

- **Ammunition** - 5.56, 7.62, 9mm, 12g. Priced per round. Biggest de-facto currency.
- **Batteries** - AA/C/D/car. Powers radios, detectors, night vision, flashlights.
- **Clean water bottles** - stable, stackable, pure utility.
- **Bandages** - compact, lifesaving.
- **Cigarettes / tobacco** - lightweight, social, universally desired.
- **Pre-war canned food** - calorie density + shelf life.

### [UNDECIDED] Rare collector items as high-denomination

- Gold teeth, silver coins, pre-war cash, rare stamps
- Compact high-value store of wealth for bulk trades
- Risk: feels videogame-gamey. Lean [LIKELY] for a small curated list.

### [REJECT] Universal credit / in-game bucks

- Breaks the post-collapse premise
- Kills emergent economy - players stop valuing objects once a meta-currency exists

---

## Player-to-player commerce

### [COMMIT] Vending machines (F76-style)

- Placeable at your camp / base (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md))
- Owner stocks inventory + sets prices (in one currency item of their choice per slot)
- Browsing player physically visits - opens vending UI, sees stock + prices, completes trade
- Offline-friendly: owner doesn't need to be online, goods accumulate in pickup bin
- Owner picks up proceeds next time they log in
- Multiple machines per base (different goods, specialty vendors)

### [LIKELY] Trade stall variant

- Simpler staffed-only version - owner stands behind counter, live haggle
- Slower transaction but allows negotiation, inspection, reputation interaction
- Good for personal-touch traders who want to build rep

### [LIKELY] Auction boards / message boards

- Posted text offers at trade hubs ("WTB surgical kit - pay 200x 7.62")
- Physical bulletin boards, message cards, not a global UI feed
- Expiration timer, owner picks up replies at the hub
- Cross-ref reputation (see below) - posted offers by infamous players get flagged

### [UNDECIDED] Direct-trade window (two-player live)

- Secure inventory-window trade between two present players
- Feels modern-game-y but useful to avoid drop-and-steal
- Lean [LIKELY] for v1.0 at safe zones only

### [LIKELY] Drop-at-coords dead-drop trades

- Old-school dead-drop: seller buries item at coords, buyer digs it up
- Cross-ref [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md) buried stashes
- Trust-based, risk of double-cross, ideal for secret exchanges

### [REJECT] Global marketplace with instant delivery

- Breaks world cohesion, trivializes geography
- Every trade should require someone to physically go somewhere

---

## NPC trader system

### [COMMIT] Faction vendors

- Each major survivor faction runs a shop (military salvagers, medical collective, farmer's market, smugglers' guild)
- Stock reflects faction identity (military = ammo/guns, medical = pharma/surgical, smugglers = contraband)
- Prices scale by player reputation with that faction
- Stock rotates (weekly refresh + rare-stock chance per visit)

### [LIKELY] Wandering merchants

- NPC travelers who move between zones on a schedule
- Smaller inventory, higher prices than fixed vendors, but reach remote corners of the map
- Rare stock variants (seasonal specials)

### [LIKELY] Black-market fixer

- Rare appearance (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) black-market pop-up event)
- Sells contraband: illegal mods, restricted pharma, military-grade gear, prototype weapons
- Buys without questions - no rep penalty for selling stolen goods here
- Protected by mercenary entourage; hostile behavior ends the visit permanently

### [LIKELY] Refugee camp stalls

- Cheap basic goods (water, cloth, basic food)
- Hospitable to low-rep players - good starter-friendly commerce
- Rep gain for buying regularly (supporting the refugees)

### [UNDECIDED] Quest-chain vendors

- NPCs who gate rare goods behind multi-stage contracts
- "Bring me 3 cryptid hides + a broadcast-tower key, I'll give you a Tier 4 recipe"
- Nice depth, moderate scope - maybe [LIKELY] if quest framework exists

---

## Trade goods taxonomy

### [COMMIT] Physical consumables

- Ammo (by caliber)
- Medical (bandages, pharma, antirad, antibiotics)
- Food + water (canned, fresh, purified, contaminated)
- Crafting materials (fiber, metal, plastic, chemicals, electronics)

### [COMMIT] Physical durables

- Weapons (condition-tiered, cross-ref [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) condition)
- Clothing (condition-tiered)
- Armor plates
- Tools (gathering, construction, detection)
- Base building materials (walls, furniture, blueprints)

### [LIKELY] Knowledge goods

- **Recipes / schematics** - unlock a new craftable item permanently (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))
- **Skill books** - one-time XP bump in a specialty (cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md))
- **Base blueprints** - architectural designs for camp deployment (cross-ref [PLAN-Base-Building.md](PLAN-Base-Building.md))
- **Map fragments** - reveal unexplored zones, mark POIs
- **Radio frequencies** - access encrypted channels, decrypted beacons

### [LIKELY] Mods (weapon / armor)

- Scopes, barrels, stocks, grips, ammo converters (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md) mod system)
- Reusable if extracted from ruined items via Rank 3 station

### [LIKELY] Perk cards

- Cross-ref [PLAN-Player-Progression.md](PLAN-Player-Progression.md) perk system
- Duplicates tradeable as premium goods
- Rare boss-drop perks are top-tier barter fuel

### [LIKELY] Information / intel

- Crash-site coords (fresh crash event location)
- Convoy schedules
- Known base locations (rival or neutral)
- Cryptid spawn zones
- Safe-passage routes through hazard zones
- Black-market fixer frequencies

### [UNDECIDED] Contracts / IOU paper

- Written promises (craft X by next visit, deliver Y to Z)
- Honor-based economy layer
- Fun but hard to enforce, lean [DEFER] unless natural use emerges

---

## Reputation and faction economy

### [COMMIT] Faction reputation

- Each faction tracks player rep independently
- Rep earned by: trading regularly, completing faction events/contracts, defending faction assets, killing faction enemies
- Rep lost by: killing faction members/guards, robbing faction convoys, being caught in faction territory as infamous

### [LIKELY] Price scaling with rep

- Friendly faction: -10% to -30% prices, access to rare stock
- Neutral: baseline prices
- Hostile: refused service, vendor won't engage, guards escalate
- Bounty contracts: high infamy = NPC bounty on player's head (cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) for NPC hunters)

### [LIKELY] Stock gating by rep

- Tier 3-4 gear only sold at faction-friendly levels
- Rare schematics / perk cards require faction tokens (earned via long-term service)
- Creates pick-your-faction meta: committing to one set of vendors shapes available gear

### [UNDECIDED] Faction-exclusive trades

- Only medical collective sells surgery kits
- Only smugglers sell silencers
- Lean: yes, forces either cross-faction trade (via player middle-men) or faction commitment

### [LIKELY] Faction warfare economy effects

- Factions occasionally feud - hostile-vs-hostile faction buys goods stolen from its enemies at premium
- Players can become "double agents" - sell to both sides, walk a rep knife-edge

### [REJECT] Single global reputation score

- Too crude - player is "good" or "bad" regardless of who they dealt with
- Per-faction rep lets player be hero to one group, villain to another (DayZ-style moral nuance)

---

## Player-driven market dynamics

### [COMMIT] Price discovery via observed trades

- Vending machine prices are public - other players see what you charge
- Trade hub bulletin boards show offered rates
- Regional price trends emerge naturally (ammo dumps at war zones cheaper, medicine cheaper near refugee camps)

### [LIKELY] Scarcity events

- Cross-ref [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) - events shift supply (horde siege → bandage demand spike, crashed transport → ammo surplus)
- Smart traders profit from reading the world

### [LIKELY] Scrap economy

- Near-universal fallback: ruined gear still breaks down to scrap (cross-ref [PLAN-Crafting.md](PLAN-Crafting.md))
- Scrap trades cheaply but steadily - bottom of every currency pyramid
- Lets players offload gear that's too damaged to repair

### [UNDECIDED] Player-set taxes / tolls

- At owned bases, owner can charge entry/use fee for services
- Interesting territorial tax gameplay, mild complexity
- Lean: [DEFER] until base claim + squad mechanics are solid

### [UNDECIDED] Inflation / deflation over server lifetime

- If scrap accumulates over months, prices drift
- Events (big horde clear, merchant depopulation) could reset markets
- Monitor during playtests, intervene only if exploits emerge

---

## Trust, scams, and enforcement

### [COMMIT] Safe-zone trade

- Designated safe zones (trader hubs) enforce no-PvP rules
- Trades completed here are guaranteed - no robbery, no backstab
- Travel to the safe zone is the risk, not the trade itself

### [LIKELY] Outside-safe-zone trade = caveat emptor

- Meeting a stranger in the woods to trade? Risk is real.
- Players can rob, scam, double-cross - and earn infamy for it
- Reputation system is enforcement: repeat scammers hit max infamy fast

### [LIKELY] Escrow via NPC broker

- Trusted NPC at trade hub holds both items, releases on confirmation
- Small fee (5-10% in trade goods)
- Slow but 100% safe for high-value deals

### [UNDECIDED] Reputation marks visible on player

- Cosmetic tag (color-coded rep, badge) visible to other players nearby
- Social-cost transparency for scammers
- Lean [LIKELY] at safe zones only - outside zones, rep is word-of-mouth

---

## Economy interactions with other plans

### Crafting (see [PLAN-Crafting.md](PLAN-Crafting.md))

- Schematics + mods are premium trade goods
- Recipe discovery via purchase is one path (alongside XP + NPC teaching)
- Scrap economy gives scrap-tier progression a monetary outlet

### Player progression (see [PLAN-Player-Progression.md](PLAN-Player-Progression.md))

- Perk cards are tradeable
- Social skill + Quartermaster perks boost vendor prices + stock access
- Skill books sold as knowledge goods

### Base building (see [PLAN-Base-Building.md](PLAN-Base-Building.md))

- Vending machine + trade stall placement
- Base location affects foot traffic - high-traffic stall > remote stall
- Blueprints tradeable between players

### Clothing / storage (see [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md))

- Condition-tiered items price by condition (ruined = scrap value, pristine = premium)
- Hidden pockets enable smuggling contraband past safe-zone customs checks
- Carry weight affects how much merch player can haul per run

### Dynamic world events (see [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md))

- Events shift supply and demand
- Black-market pop-up is rare rotating vendor
- Refugee rescue = rep gain with refugee faction, improved prices

### Hazards (see [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md))

- Cures + NBC gear are premium goods at faction medical vendors
- Hazard-zone loot (anomaly materials) is exotic currency

### Terrain carving (see [PLAN-Terrain-Carving.md](PLAN-Terrain-Carving.md))

- Buried stash dead-drops for secret trades
- Stash maps sold as information goods

---

## Gameplay verbs economy enables

- Stock your vending machines with scav-run surplus before logging off, log in next day to find your pickup bin loaded with 7.62 rounds
- Haggle with a farmer faction vendor for a discount because you've been selling them infected kill-counts
- Spot a bulletin-board post offering a rare scope for "any pristine field-medic kit", spend the week crafting the kit to claim the scope
- Buy a rare boss-drop perk card at the black-market fixer, trade it a week later at 3x price to a collector
- Sell crash-site coords to a squad of raiders for a premium, watch them stampede while you slip away with pocket cash
- Post a dead-drop trade: bury 5 stim packs at (x,y,z), buyer leaves ammo in return slot, vanish before they arrive
- Build rep with smugglers by buying every bootleg mod they stock, unlock the hidden Tier-4 contraband shelf
- Earn max infamy for robbing a refugee column, discover every NPC bounty hunter knows your face, spend the winter hiding in the mountains
- Run scrap-cycling: scavenge ruined gear, break down at Rank 3 bench, trade scrap-bulk for consumables
- Find a pristine prewar book, identify it as a rare skill book, resell to a faction elder for a unique schematic
- Accept a quest-chain contract from a medical faction vendor, complete 3 escalating deliveries, unlock a Tier 4 surgery schematic
- Offer safe-passage escort for a fee through a contaminated zone - your NBC gear is the service, their relief is the pay

---

## Open questions

1. **Default currency bias** - should vending machines accept any item or require one-currency-per-slot? (Simpler: one-per-slot.)
2. **Global trade hub cap** - how many trade hub safe zones per server? Too many = fragmented, too few = bottleneck.
3. **Rep decay** - does faction rep erode over time without interaction, or does it stick?
4. **Scammer tracking** - how much is visible to other players? Reputation public, transaction history private?
5. **Black-market discovery** - how does a low-rep player ever find the black-market fixer? Pure RNG event, or NPC tip-chain?
6. **Vendor stock refresh** - fixed weekly timer or event-driven? (Event-driven feels more alive but more work.)
7. **Information commodification** - should intel expire automatically, or stay tradeable forever (risk of stale data flooding market)?

---

## Dependencies

| Feature | Depends on |
|---------|------------|
| Item registry | Crafting + clothing + weapons unified item IDs |
| Vending machine | Entity system + UI + async stock persistence |
| Reputation system | Per-faction score storage + event hooks |
| NPC vendors | AI + dialogue + rotating-stock templates |
| Bulletin boards | Text/message persistence + expiration |
| Black-market fixer | Dynamic world events + encrypted channels |
| Safe-zone enforcement | PvP flag + zone definitions |
| Escrow broker | NPC + atomic swap logic |

---

## Next actions

1. Define item registry (unified IDs across all plans, quality tiers, stackability)
2. Prototype vending machine end-to-end (stock UI, buyer browse, transaction, pickup bin, offline persistence)
3. Reputation schema (per-faction scores, price multiplier table, stock gating rules)
4. NPC vendor templates (stock pool, price base, rotation rules)
5. Trade hub safe-zone design (locations, rules enforcement, escrow broker NPC placement)

---

*Make it so.* 🖖

-- Brainstorm by Tuvok (Claude CLI #3, Research/Planning), for Captain's decisions
