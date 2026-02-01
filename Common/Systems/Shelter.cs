using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

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

    public static void DropInventory(Player p)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        // Only drop the main inventory (including coins/ammo) to avoid nuking loadouts.
        for (int i = 0; i < p.inventory.Length; i++)
        {
            var item = p.inventory[i];
            if (item is null || item.IsAir)
                continue;

            Item.NewItem(p.GetSource_Death(), p.getRect(), item.type, item.stack);
            item.TurnToAir();
        }
    }
}
