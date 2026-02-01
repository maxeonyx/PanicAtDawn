using System;
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
    public bool IsInSafeZone; // Exposed for UI to show recovery color
    private int _wormholeDripTicks;
    private int _suffocateTick;
    private int _denyUseTextCooldown;

    public override void Initialize()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        Sanity = cfg.SanityMax;
        DawnGraceTicks = 0;
        _wormholeDripTicks = 0;
        _suffocateTick = 0;
        _denyUseTextCooldown = 0;
    }

    public override void OnRespawn()
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        Sanity = cfg.SanityMax;
        _suffocateTick = 0;
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

        if (cfg.EnableLinkSanity)
            UpdateLinkSanity(cfg);

        if (cfg.EnableWormholeDrip)
            UpdateWormholeDrip(cfg);

        if (cfg.EnableBossHex)
            ApplyBossHex(PanicAtDawnState.CurrentBossHex);
    }

    public override bool CanUseItem(Item item)
    {
        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.DisableRecallAndMirrors)
            return base.CanUseItem(item);

        if (item.type is ItemID.RecallPotion or ItemID.MagicMirror or ItemID.IceMirror or ItemID.CellPhone or ItemID.Shellphone)
        {
            if (Main.myPlayer == Player.whoAmI && _denyUseTextCooldown <= 0)
            {
                Main.NewText("Recall/mirrors are disabled. Use Wormhole Potions to regroup.");
                _denyUseTextCooldown = 90;
            }
            return false;
        }

        return base.CanUseItem(item);
    }

    private void UpdateLinkSanity(PanicAtDawnConfig cfg)
    {
        // Check if player is near their spawn point (bed).
        bool nearSpawn = Shelter.IsNearSpawnPoint(Player, cfg.SpawnSafeRadiusTiles);
        IsInSafeZone = nearSpawn;

        if (nearSpawn)
        {
            // Safe near spawn: regen sanity.
            Sanity = Math.Clamp(Sanity + (cfg.SanityRegenPerSecond / 60f), 0f, cfg.SanityMax);
            _suffocateTick = 0;
            return;
        }

        int teammate = FindClosestLinkedTeammate();

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

            if (Sanity > 0f)
            {
                _suffocateTick = 0;
                return;
            }

            ApplySuffocation(cfg);
            return;
        }

        var other = Main.player[teammate];
        float dist = Vector2.Distance(Player.Center, other.Center);
        float radiusPx = cfg.LinkRadiusTiles * 16f;

        float delta = 0f;
        if (dist <= radiusPx)
            delta = cfg.SanityRegenPerSecond;
        else
            delta = -drainRate;

        Sanity = Math.Clamp(Sanity + (delta / 60f), 0f, cfg.SanityMax);

        // Shared pool for a duo: snap both to the lower sanity, so separation hurts both.
        // (This stays stable for >2 players as well: it tends toward the group's minimum.)
        var otherMod = other.GetModPlayer<PanicAtDawnPlayer>();
        float shared = Math.Min(Sanity, otherMod.Sanity);
        Sanity = shared;
        otherMod.Sanity = shared;

        if (Sanity > 0f)
        {
            _suffocateTick = 0;
            return;
        }

        ApplySuffocation(cfg);
    }

    private void ApplySuffocation(PanicAtDawnConfig cfg)
    {
        // Apply damage as a steady DOT. Server is authoritative.
        _suffocateTick++;
        if (_suffocateTick < 60)
            return;
        _suffocateTick = 0;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (Player.dead)
            return;

        Player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral($"{Player.name} had a panic attack.")), cfg.SuffocationDamagePerSecond, 0);
    }

    private void UpdateWormholeDrip(PanicAtDawnConfig cfg)
    {
        // Very low, deterministic rate. If you already have enough, it pauses.
        if (cfg.WormholeDripStackCap <= 0)
            return;

        int current = CountItems(ItemID.WormholePotion);
        if (current >= cfg.WormholeDripStackCap)
            return;

        _wormholeDripTicks++;
        int intervalTicks = Math.Max(1, cfg.WormholeDripSeconds) * 60;
        if (_wormholeDripTicks < intervalTicks)
            return;
        _wormholeDripTicks = 0;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Player.QuickSpawnItem(Player.GetSource_Misc("PanicAtDawn:WormholeDrip"), ItemID.WormholePotion, 1);
    }

    private void ApplyBossHex(BossHex hex)
    {
        if (hex == BossHex.None)
            return;

        bool anyBoss = false;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var n = Main.npc[i];
            if (n.active && n.boss)
            {
                anyBoss = true;
                break;
            }
        }
        if (!anyBoss)
            return;

        switch (hex)
        {
            case BossHex.Darkness:
                Player.AddBuff(BuffID.Darkness, 2);
                break;
            case BossHex.Weak:
                Player.AddBuff(BuffID.Weak, 2);
                break;
            case BossHex.Slow:
                Player.AddBuff(BuffID.Slow, 2);
                break;
            case BossHex.WingClip:
                Player.wingTime = 0f;
                break;
            case BossHex.Frail:
                Player.statLifeMax2 = (int)(Player.statLifeMax2 * 0.8f);
                break;
        }
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
        // Check if player has the Darkness debuff, or if local lighting is very low.
        if (Player.HasBuff(BuffID.Darkness) || Player.HasBuff(BuffID.Blackout))
            return true;

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
