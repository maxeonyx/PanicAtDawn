using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;

namespace PanicAtDawn.Common.Systems;

public sealed class LootSystem : ModSystem
{
    public override void OnWorldLoad()
    {
        ReplaceRecallInAllChests();
    }

    public override void PostWorldGen()
    {
        ReplaceRecallInAllChests();
    }

    private static void ReplaceRecallInAllChests()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.ReplaceRecallWithWormhole)
            return;

        // Chests store items directly; GlobalItem.OnSpawn won't run for these.
        for (int i = 0; i < Main.maxChests; i++)
        {
            Chest chest = Main.chest[i];
            if (chest == null)
                continue;

            for (int s = 0; s < chest.item.Length; s++)
            {
                Item it = chest.item[s];
                if (it is null || it.IsAir)
                    continue;

                if (it.type != ItemID.RecallPotion)
                    continue;

                it.SetDefaults(ItemID.WormholePotion);
                it.stack = 1;
            }
        }
    }
}
