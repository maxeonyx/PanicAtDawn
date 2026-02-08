using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Systems;

namespace PanicAtDawn.Common.Players;

public sealed class PanicAtDawnPlayer : ModPlayer
{
    public float Sanity;
    public int DawnGraceTicks;
    public bool IsSanityRecovering; // True when sanity is regenerating (near spawn OR near teammate) - for UI gold bar
    public bool IsSuffocating;      // True when sanity is at zero and player is taking DOT - for UI red bar
    public bool ClientNearSpawn;    // Set by client via SyncNearSpawn packet; server reads this for sanity regen
    private int _denyUseTextCooldown;
    private int _netSyncTimer;      // Throttle server -> client sanity sync
    private int _nearSpawnSyncTimer; // Throttle client -> server near-spawn sync

    public override void Initialize()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        Sanity = cfg.SanityMax;
        DawnGraceTicks = 0;
        _denyUseTextCooldown = 0;
        _netSyncTimer = 0;
        _nearSpawnSyncTimer = 0;
        ClientNearSpawn = false;
        IsSuffocating = false;
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        // Send sanity state when player joins or needs sync
        ModPacket packet = Mod.GetPacket();
        packet.Write((byte)PanicAtDawn.MessageType.SyncSanity);
        packet.Write((byte)Player.whoAmI);
        packet.Write(Sanity);
        packet.Write(IsSanityRecovering);
        packet.Write(IsSuffocating);
        packet.Send(toWho, fromWho);
    }

    public override void OnRespawn()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        Sanity = cfg.SanityMax;
        IsSuffocating = false;
    }

    public override void OnEnterWorld()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        DawnGraceTicks = Math.Max(0, cfg.DawnJoinGraceSeconds) * 60;
    }

    public override void PostUpdate()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();

        if (DawnGraceTicks > 0)
            DawnGraceTicks--;

        if (_denyUseTextCooldown > 0)
            _denyUseTextCooldown--;

        // Sanity logic runs on server (or singleplayer) only - server is authoritative
        bool isServerOrSingleplayer = Main.netMode != NetmodeID.MultiplayerClient;

        if (cfg.EnableLinkSanity && isServerOrSingleplayer)
        {
            UpdateLinkSanity(cfg);
            
            // Sync sanity to clients periodically (every 6 ticks = 10Hz)
            if (Main.netMode == NetmodeID.Server)
            {
                _netSyncTimer++;
                if (_netSyncTimer >= 6)
                {
                    _netSyncTimer = 0;
                    SyncPlayer(-1, -1, false);
                }
            }
        }

        // Client: report near-spawn status to server periodically (every 6 ticks = 10Hz)
        // SpawnX/SpawnY aren't reliably synced to the server, so the client must check locally.
        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer && cfg.EnableLinkSanity)
        {
            _nearSpawnSyncTimer++;
            if (_nearSpawnSyncTimer >= 6)
            {
                _nearSpawnSyncTimer = 0;
                bool nearSpawn = Shelter.IsNearSpawnPoint(Player, cfg.SpawnSafeRadiusTiles);
                ModPacket packet = Mod.GetPacket();
                packet.Write((byte)PanicAtDawn.MessageType.SyncNearSpawn);
                packet.Write((byte)Player.whoAmI);
                packet.Write(nearSpawn);
                packet.Send();
            }
        }

        // Client-side suffocation DOT (like drowning — bypasses armor, scales to max HP).
        // Applied by the client that owns this player, since health is client-authoritative.
        // Server syncs Sanity and IsSuffocating; client applies the actual damage.
        if (cfg.EnableLinkSanity && Player.whoAmI == Main.myPlayer && !Player.dead && IsSuffocating)
        {
            // Deal statLifeMax2 / (60s * 60tps) per tick = death in 60 seconds
            float damagePerTick = Player.statLifeMax2 / 3600f;
            Player.statLife -= (int)Math.Max(Math.Ceiling(damagePerTick), 1);
            if (Player.statLife <= 0)
            {
                Player.statLife = 0;
                Player.KillMe(
                    Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                        NetworkText.FromLiteral($"{Player.name} had a panic attack.")),
                    1.0, 0);
            }
        }

    }

    public override bool CanUseItem(Item item)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        
        // Check recall/mirror restrictions
        if (cfg.DisableRecallAndMirrors)
        {
            if (item.type is ItemID.RecallPotion or ItemID.MagicMirror or ItemID.IceMirror or ItemID.CellPhone or ItemID.Shellphone)
            {
                if (Main.myPlayer == Player.whoAmI && _denyUseTextCooldown <= 0)
                {
                    Main.NewText("Recall/mirrors are disabled. Use Wormhole Potions to regroup.");
                    _denyUseTextCooldown = 90;
                }
                return false;
            }
        }

        return base.CanUseItem(item);
    }

    private void UpdateLinkSanity(PanicAtDawnConfig cfg)
    {
        // Check if player is near their spawn point (bed).
        // In multiplayer, SpawnX/SpawnY aren't reliably synced to the server,
        // so we use the client-reported ClientNearSpawn flag instead.
        bool nearSpawn = Main.netMode == NetmodeID.Server
            ? ClientNearSpawn
            : Shelter.IsNearSpawnPoint(Player, cfg.SpawnSafeRadiusTiles);

        if (nearSpawn)
        {
            // Safe near spawn: regen sanity.
            Sanity = Math.Clamp(Sanity + (cfg.SanityRegenPerSecond / 60f), 0f, cfg.SanityMax);
            IsSanityRecovering = true;
            IsSuffocating = false;
            return;
        }

        int teammate = FindClosestLinkedTeammate();
        
        // Debug: log teammate detection
        if (Main.GameUpdateCount % 120 == 0) // Every 2 seconds
        {
            Mod.Logger.Info($"[Sanity] Player {Player.name}: teammate={teammate}, sanity={Sanity:F1}, netMode={Main.netMode}");
        }

        // Calculate drain rate.
        float drainRate = cfg.SanityDrainPerSecond;
        
        // Double drain in darkness.
        if (IsPlayerInDarkness())
            drainRate *= cfg.SanityDarknessDrainMultiplier;
        
        // Quarter drain near sunflowers (Happy! buff).
        if (Player.HasBuff(BuffID.Sunflower))
            drainRate *= 0.25f;

        if (teammate < 0)
        {
            // Singleplayer / no linked teammate: sanity drains continuously.
            Sanity = Math.Clamp(Sanity - (drainRate / 60f), 0f, cfg.SanityMax);
            IsSanityRecovering = false;
            IsSuffocating = Sanity <= 0f;
            return;
        }

        var other = Main.player[teammate];
        float dist = Vector2.Distance(Player.Center, other.Center);
        float radiusPx = cfg.LinkRadiusTiles * 16f;

        float delta = 0f;
        if (dist <= radiusPx)
        {
            // Shared regen pool: 2× regen rate distributed by inverse sanity (lower player gets more)
            // If A has half the sanity of B, A gets 2/3 of the pool, B gets 1/3
            var otherMod = other.GetModPlayer<PanicAtDawnPlayer>();
            float sharedPool = cfg.SanityRegenPerSecond * 2f;
            
            // Weight by inverse of current sanity (lower sanity = higher weight)
            // Clamp to avoid division by zero
            float weightMe = 1f / Math.Max(Sanity, 0.01f);
            float weightOther = 1f / Math.Max(otherMod.Sanity, 0.01f);
            float totalWeight = weightMe + weightOther;
            
            float myShare = (weightMe / totalWeight) * sharedPool;
            
            delta = myShare;
            IsSanityRecovering = true; // Near teammate = recovering (gold bar)
        }
        else
        {
            delta = -drainRate;
            IsSanityRecovering = false;
        }

        Sanity = Math.Clamp(Sanity + (delta / 60f), 0f, cfg.SanityMax);
        IsSuffocating = Sanity <= 0f;
    }

    private int FindClosestLinkedTeammate()
    {
        // Uses Terraria's team mechanic. If both players are on team 0 (no team), treat it as linked anyway.
        int best = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            var other = Main.player[i];
            if (other is null || !other.active || other.dead || other.whoAmI == Player.whoAmI)
                continue;

            bool linked = (Player.team == other.team) || (Player.team == 0 && other.team == 0);
            if (!linked)
                continue;

            float d = Vector2.DistanceSquared(Player.Center, other.Center);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

    private int CountItems(int itemType)
    {
        int count = 0;
        for (int i = 0; i < Player.inventory.Length; i++)
        {
            var it = Player.inventory[i];
            if (it is not null && !it.IsAir && it.type == itemType)
                count += it.stack;
        }
        return count;
    }

    private bool IsPlayerInDarkness()
    {
        // Check if player has the Darkness debuff.
        if (Player.HasBuff(BuffID.Darkness) || Player.HasBuff(BuffID.Blackout))
            return true;

        // Lighting check only works on clients (servers don't calculate lighting).
        // On dedicated servers, darkness drain bonus only applies with debuffs.
        // For "host and play" (listen servers), lighting works normally.
        if (Main.netMode == NetmodeID.Server && Main.dedServ)
            return false;

        // Check tile lighting at player position.
        Point tile = Player.Center.ToTileCoordinates();
        if (!WorldGen.InWorld(tile.X, tile.Y, 10))
            return false;

        // Lighting.GetColor returns the light color at the tile; we check brightness.
        Color light = Lighting.GetColor(tile.X, tile.Y);
        float brightness = (light.R + light.G + light.B) / (255f * 3f);

        // Consider "darkness" if brightness is below 10%.
        return brightness < 0.10f;
    }
}
