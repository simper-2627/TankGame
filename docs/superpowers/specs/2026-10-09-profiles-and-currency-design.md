# Profiles and in-game currency: design

Branch: `profiles-and-currency` (from `main`).

## Goal

Give players a persistent profile (editable display name) and an in-game currency earned from play. Cash has no uses yet; it is stored on the profile for future features. There is no database yet, so storage sits behind an interface and is trivial to swap.

## Decisions

- **Identity: anonymous device profile.** The browser keeps a random profile ID in `localStorage`. No login, no signup. A real login can replace the ID later without touching profile or currency code.
- **Not built on `origin/jidapa`** (tank colors/textures, unmerged). The profile gets an `Appearance` slot once that branch lands. Until then it stores only name and money.
- **Kills:** `Tank.Kills` is added using the same name as `origin/endscreen` to keep the eventual merge conflict in `Combat.cs` small. A conflict there is expected whichever branch lands second.
- **Known limitation:** the in-memory store resets on every server restart or deploy.

## Components (`GameLogic/Profiles/`)

### Profile and store
- `Profile { Id, DisplayName, Cash, SecondsPlayed, HitsLanded, Kills }`.
- `IProfileStore` (async): `GetAsync(id)`, `CreateAsync()`, `RenameAsync(id, name)`, `AwardAsync(id, Earnings)`.
  - `AwardAsync` applies an atomic delta (cash += x, counters += y), mapping directly to a SQL `UPDATE` later.
- `InMemoryProfileStore`: `ConcurrentDictionary`-backed; the only implementation. Registered as a singleton in `GameApi/Program.cs`; swapping the DB means one new class and one changed line.
- Names are trimmed, 1 to 20 characters, not required to be unique. Invalid names are rejected.

### Earnings
- `Earnings { Cash, Seconds, Hits, Kills }`.
- `CurrencyRates`: one record holding every number. Placeholder defaults: 1 cash per 10 seconds, 2 per hit, 25 per tank destroyed.
- `EarningsTracker` (pure; no SignalR or clock): fed events per human tank (tick, hit, kill) and returns earnings.
  - Time counts only while the match is in progress.
  - Self-hits do not count (consistent with `HitsLanded`).
  - Bot kills pay the same as player kills.
  - `Flush(tankId)` returns only the earnings since the last flush, so no double payouts.

## Wiring

### Server
- `Tank` gains `Kills`, incremented in `Combat.ApplyHit` when the hit destroys the target (shooter != target).
- `Game` keeps a **private** tank-to-profile map and an `EarningsTracker`. The profile ID is bearer-secret-like and is never placed in `GameState` or sent to other clients.
- Payout timing: flush all human tanks when the match ends; flush one player when their connection disconnects. Each flush calls `IProfileStore.AwardAsync`.
- `LobbyHub` new methods: `GetOrCreateProfile(profileId?)`, `RenameProfile(profileId, name)`. `JoinGame` and `CreateGame` gain a `profileId` argument (SignalR needs every argument sent). A profile's name overrides the random name.

### Client
- `ProfileService` stores the ID in `localStorage` via JS interop and talks to the hub.
- The "Your name" panel on `Home.razor` becomes a profile panel: name field, Save button, cash balance. The balance is refreshed after a payout.
- No per-match "earned" display yet (the end screen is not on `main`).

## Testing (written first)
- `EarningsTracker`: time, hit, kill, self-hit ignored, no double flush, no time earned outside an active match.
- `InMemoryProfileStore`: create, get, rename, name validation, concurrent awards.
- Game-level: a match ends and the profile balance rises; the profile ID never appears in `GameState`.

## Out of scope
Spending cash, real authentication, a database, appearance storage, end-screen earnings display.
