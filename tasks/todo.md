# Top-down tank + mouse-aimed turret

Arrow keys drive the hull (unchanged). Mouse position aims the turret. Mouse click fires along the turret.

## 0. Assets (Ben)
- [x] Get two top-down PNGs with transparent backgrounds: **hull** (no barrel) and **turret** (turret + barrel)
- [x] Both drawn pointing **right** (angle 0 = +X in `Tank.cs`), or tell me which way they point and I'll offset
- [x] Put them in `GameFrontend/wwwroot/images/`

## 1. Shared model: add turret aim
- [x] `PlayerInputRequest`: add `AimX`, `AimY` (board coords of the mouse); not `required`, so existing tests still compile
- [x] `Tank`: add `AimX`, `AimY`, `TurretAngle`
- [x] `Tank.ProcessTankMovement`: recompute `TurretAngle = atan2(AimY - y, AimX - x)` every tick, so the turret stays locked on the cursor while the hull moves
- [x] `TankState` + `Game.GetGameState`: send `TurretAngle` to clients
- [x] `Game.ReceiveUserInput`: copy aim into the tank

## 2. Firing
- [ ] Bullet `Angle` = `TurretAngle` (not hull `Angle`)
- [ ] Spawn at the barrel tip (tank center + barrel length along `TurretAngle`)
- [ ] Fire on the rising edge of `Shoot` (false → true) plus a short cooldown. Otherwise, with aim in the input, every mouse move while the button is held sends a new message and spawns a bullet

## 3. Frontend input (`PlayerControls.razor`)
- [x] `@onmousemove`: store `e.OffsetX/OffsetY` as the aim point
- [ ] `@onmousedown` / `@onmouseup`: set/clear shoot (and clear it in `handleBlur`)
- [x] `pointer-events: none` on tanks and bullets so `OffsetX/Y` is always relative to the board, not the child under the cursor
- [x] Update the "control with the arrow keys" hint text

## 4. Frontend render (`TankComponent.razor`)
- [x] Replace the inline side-view SVG with a hull `<img>` rotated by `Angle`
- [x] Stack the turret `<img>` on top, rotated by `TurretAngle`, `transform-origin` at the turret's pivot
- [x] Treat `PositionX/Y` as the tank's **center** (`translate(-50%, -50%)`) so aim math, bullet spawn, and drawing agree
- [x] Decide on per-player color (currently a GUID-based SVG fill; PNGs can't use that directly)

## 5. Verify
- [x] Unit tests: turret angle (GameTest/TurretTests.cs)
- [ ] Unit tests: bullet uses turret angle; holding fire doesn't spray
- [x] Existing tests still pass (BulletMotion was already failing on main; that's for the shooting step)
- [x] Run the app: drive with arrows, aim with mouse (verified in Edge)
- [ ] Two tabs + click to fire (after shooting step)
