
## Tanks game

- two players can join a game from a lobby
- players can make a new game in a lobby
- players can drive their tank with wasd
- players can shoot by clicking with their mouse
- tank cannon always points to mouse
- bounding box exists that prevent tanks from driving out of bounds
- players have a profile (name and cash) saved in their browser; cash comes from time in a match, hits landed and tanks destroyed
- profiles are kept in memory for now (`InMemoryProfileStore`) and reset when the server restarts; to use a database, implement `IProfileStore` and pass it to `Lobby`
- earning rates live in one place: `CurrencyRates` (`GameLogic/Profiles/Earnings.cs`)
