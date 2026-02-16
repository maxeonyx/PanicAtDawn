using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;

namespace PanicAtDawn.Common.Systems;

public static class Shelter
{
    /// <summary>
    /// Checks if the player is within a given tile radius of their bed spawn point.
    /// Only counts if player has set a bed spawn - world spawn does NOT count.
    /// </summary>
    public static bool IsNearSpawnPoint(Player p, int radiusTiles)
    {
        // SpawnX/SpawnY are -1 if no bed spawn is set.
        // World spawn does NOT count as safe - only bed spawns.
        if (p.SpawnX < 0 || p.SpawnY < 0)
            return false;

        Point playerTile = p.Center.ToTileCoordinates();
        float distX = playerTile.X - p.SpawnX;
        float distY = playerTile.Y - p.SpawnY;
        float distTiles = (float)System.Math.Sqrt(distX * distX + distY * distY);

        return distTiles <= radiusTiles;
    }

    /// <summary>
    /// Checks if the player is within a given tile radius of any active pylon.
    /// A pylon is "active" if it has at least 2 nearby housed NPCs (or is the Victory pylon).
    /// Pylon positions are world state, available on both client and server.
    /// </summary>
    public static bool IsNearActivePylon(Player p, int radiusTiles)
    {
        Point playerTile = p.Center.ToTileCoordinates();

        foreach (TeleportPylonInfo pylon in Main.PylonSystem.Pylons)
        {
            float dx = playerTile.X - pylon.PositionInTiles.X;
            float dy = playerTile.Y - pylon.PositionInTiles.Y;
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);

            if (dist > radiusTiles)
                continue;

            // Victory (universal) pylons don't need NPCs
            if (pylon.TypeOfPylon == TeleportPylonType.Victory)
                return true;

            if (TeleportPylonsSystem.DoesPositionHaveEnoughNPCs(2, pylon.PositionInTiles))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the player is "sheltered" - near bed spawn OR near an active pylon.
    /// </summary>
    public static bool IsSheltered(Player p, int radiusTiles, bool pylonsEnabled)
    {
        if (IsNearSpawnPoint(p, radiusTiles))
            return true;

        if (pylonsEnabled && IsNearActivePylon(p, radiusTiles))
            return true;

        return false;
    }
}
