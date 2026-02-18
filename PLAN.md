# PanicAtDawn — Plan

## Bugs

### Pylon NPC check not working

Pylons count as safe zones even when they don't have 2+ nearby villagers. The code calls `TeleportPylonsSystem.DoesPositionHaveEnoughNPCs(2, pylon.PositionInTiles)` but it appears to always return true, or the check isn't running where expected.

**Investigate:**
- Does `DoesPositionHaveEnoughNPCs` work on the server? On the client? NPC housing data may not be available in all contexts.
- Is the pylon check running on the right netmode? `UpdateLinkSanity` runs server-side, dawn check runs client-side -- both call `IsNearActivePylon`.
- Test: place a pylon with no NPCs nearby, confirm it should NOT be a safe zone.

**Location:** `Shelter.cs:50` -- `TeleportPylonsSystem.DoesPositionHaveEnoughNPCs`

### Wormhole potion consumption on block

Fixed in v0.1.16 by detouring `HasUnityPotion` instead of only `UnityTeleport`. Needs multiplayer testing to confirm the potion is no longer consumed when blocked.

## Future Work

- Configurable dawn warning timing (currently hardcoded to last in-game hour)
