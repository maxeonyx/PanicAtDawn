using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;

namespace PanicAtDawn.Common.GlobalItems;

public sealed class RecallConversionGlobalItem : GlobalItem
{
    public override void OnSpawn(Item item, Terraria.DataStructures.IEntitySource source)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.ConvertDroppedRecallToWormhole)
            return;

        if (item.type != ItemID.RecallPotion)
            return;

        // Recall is far too common. Replace any stack with a single Wormhole Potion.
        item.SetDefaults(ItemID.WormholePotion);
        item.stack = 1;
    }
}
