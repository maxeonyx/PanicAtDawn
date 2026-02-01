using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Systems;

namespace PanicAtDawn.Common.GlobalNPCs;

public sealed class BossHexGlobalNPC : GlobalNPC
{
    public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableBossHex)
            return;

        if (!npc.boss)
            return;

        // Only roll at the start of a boss presence.
        if (PanicAtDawnState.CurrentBossHex != BossHex.None)
            return;

        PanicAtDawnState.CurrentBossHex = (BossHex)Main.rand.Next(1, 6);

        if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral($"Boss Hex: {PanicAtDawnState.CurrentBossHex}"), Color.Orange);
        else
            Main.NewText($"Boss Hex: {PanicAtDawnState.CurrentBossHex}");
    }
}
