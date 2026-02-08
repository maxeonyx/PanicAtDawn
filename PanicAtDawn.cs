using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Players;
using PanicAtDawn.Common.Systems;

namespace PanicAtDawn;

public sealed class PanicAtDawn : Mod
{
    internal enum MessageType : byte
    {
        SyncSanity,
        CheckDawn,        // Server -> Client: tells client to check dawn safety and kill themselves if unsafe
        SyncNearSpawn     // Client -> Server: client reports whether it's near its bed spawn
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var msgType = (MessageType)reader.ReadByte();

        switch (msgType)
        {
            case MessageType.SyncSanity:
                byte playerIndex = reader.ReadByte();
                float sanity = reader.ReadSingle();
                bool isRecovering = reader.ReadBoolean();
                bool isSuffocating = reader.ReadBoolean();

                if (playerIndex < Main.maxPlayers && Main.player[playerIndex].active)
                {
                    var modPlayer = Main.player[playerIndex].GetModPlayer<PanicAtDawnPlayer>();
                    modPlayer.Sanity = sanity;
                    modPlayer.IsSanityRecovering = isRecovering;
                    modPlayer.IsSuffocating = isSuffocating;
                }

                // Server forwards to other clients
                if (Main.netMode == NetmodeID.Server)
                {
                    ModPacket packet = GetPacket();
                    packet.Write((byte)MessageType.SyncSanity);
                    packet.Write(playerIndex);
                    packet.Write(sanity);
                    packet.Write(isRecovering);
                    packet.Write(isSuffocating);
                    packet.Send(-1, whoAmI); // Send to all except sender
                }
                break;

            case MessageType.CheckDawn:
                // Server tells client: dawn has arrived, check if you're safe
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    Player player = Main.LocalPlayer;
                    if (player is null || !player.active || player.dead)
                        break;

                    if (player.GetModPlayer<PanicAtDawnPlayer>().DawnGraceTicks > 0)
                        break;

                    var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
                    bool safe = Shelter.IsNearSpawnPoint(player, cfg.SpawnSafeRadiusTiles);

                    if (!safe)
                    {
                        player.KillMe(
                            Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                                NetworkText.FromLiteral($"{player.name} was caught outside at dawn.")),
                            9999.0,
                            0);
                    }
                }
                break;

            case MessageType.SyncNearSpawn:
                // Client tells server whether it's near its bed spawn
                // (SpawnX/SpawnY aren't reliably synced to the server, so clients must report this)
                byte spawnPlayerIndex = reader.ReadByte();
                bool nearSpawn = reader.ReadBoolean();

                if (Main.netMode == NetmodeID.Server
                    && spawnPlayerIndex < Main.maxPlayers
                    && Main.player[spawnPlayerIndex].active)
                {
                    var modPlayer = Main.player[spawnPlayerIndex].GetModPlayer<PanicAtDawnPlayer>();
                    modPlayer.ClientNearSpawn = nearSpawn;
                }
                break;
        }
    }

    /// <summary>
    /// Broadcasts a dawn check to all clients.
    /// </summary>
    internal static void SendCheckDawn(Mod mod)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        ModPacket packet = mod.GetPacket();
        packet.Write((byte)MessageType.CheckDawn);
        packet.Send(); // Broadcast to all clients
    }
}
