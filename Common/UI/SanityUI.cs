using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using PanicAtDawn.Common.Config;
using PanicAtDawn.Common.Players;

namespace PanicAtDawn.Common.UI;

public sealed class SanityUI : ModSystem
{
    private const int SegmentCount = 10;
    private const int FillSize = 8;
    private const int SocketSize = 10;
    private const int Spacing = 2;
    private const float Scale = 2f;
    
    private const float ShowThreshold = 0.90f;
    private const float HideThreshold = 1.00f;

    private static bool _isVisible = false;
    
    private static Asset<Texture2D> _socketGrayTex;
    private static Asset<Texture2D> _socketGoldTex;
    private static Asset<Texture2D> _socketRedTex;
    private static Asset<Texture2D> _socketGreenTex;
    private static Asset<Texture2D> _socketAmberTex;
    private static Asset<Texture2D> _grayTex;
    private static Asset<Texture2D> _goldTex;
    private static Asset<Texture2D> _redTex;
    private static Asset<Texture2D> _greenTex;
    private static Asset<Texture2D> _amberTex;

    public override void Load()
    {
        _socketGrayTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanitySocketGray");
        _socketGoldTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanitySocketGold");
        _socketRedTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanitySocketRed");
        _socketGreenTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanitySocketGreen");
        _socketAmberTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanitySocketAmber");
        _grayTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanityGray");
        _goldTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanityGold");
        _redTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanityRed");
        _greenTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanityGreen");
        _amberTex = ModContent.Request<Texture2D>("PanicAtDawn/Assets/UI/SanityAmber");
    }

    public override void Unload()
    {
        _socketGrayTex = null;
        _socketGoldTex = null;
        _socketRedTex = null;
        _socketGreenTex = null;
        _socketAmberTex = null;
        _grayTex = null;
        _goldTex = null;
        _redTex = null;
        _greenTex = null;
        _amberTex = null;
    }

    public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
    {
        int idx = layers.FindIndex(l => l.Name == "Vanilla: Entity Health Bars");
        if (idx < 0)
            idx = 0;

        layers.Insert(idx + 1, new LegacyGameInterfaceLayer(
            "PanicAtDawn: Sanity",
            Draw,
            InterfaceScaleType.UI
        ));
    }

    private static bool Draw()
    {
        if (_socketGrayTex == null || _socketGoldTex == null || _socketRedTex == null || _socketGreenTex == null || _socketAmberTex == null
            || _grayTex == null || _goldTex == null || _redTex == null || _greenTex == null || _amberTex == null)
            return true;

        var cfg = ModContent.GetInstance<PanicAtDawnConfig>();
        if (!cfg.EnableLinkSanity)
            return true;

        Player p = Main.LocalPlayer;
        if (p == null || !p.active || p.dead)
            return true;

        var mp = p.GetModPlayer<PanicAtDawnPlayer>();
        float sanity = mp.Sanity;
        int max = cfg.SanityMax;

        if (max <= 0)
            return true;

        float sanityPercent = sanity / max;

        // Dawn approaching: last in-game hour of night, player is not sheltered.
        // Night lasts 32400 ticks; one hour = 3600 ticks; warning at >= 28800.
        bool dawnApproaching = !Main.dayTime && Main.time >= 28800.0
            && cfg.EnableDawnShelterRule && !mp.IsSheltered;

        if (dawnApproaching)
            _isVisible = true;
        else if (sanityPercent <= ShowThreshold)
            _isVisible = true;
        else if (sanityPercent >= HideThreshold)
            _isVisible = false;

        if (!_isVisible)
            return true;

        // Pick textures by priority: green (sheltered), gold (teammate), red (critical drain),
        // amber (dawn warning), gray (default)
        Texture2D socketTex;
        Texture2D fillTex;
        if (mp.IsSheltered)
        {
            socketTex = _socketGreenTex.Value;
            fillTex = _greenTex.Value;
        }
        else if (mp.IsSanityRecovering)
        {
            socketTex = _socketGoldTex.Value;
            fillTex = _goldTex.Value;
        }
        else if (sanityPercent <= 0.10f)
        {
            socketTex = _socketRedTex.Value;
            fillTex = _redTex.Value;
            _isVisible = true; // Always show when dying
        }
        else if (dawnApproaching)
        {
            socketTex = _socketAmberTex.Value;
            fillTex = _amberTex.Value;
        }
        else
        {
            socketTex = _socketGrayTex.Value;
            fillTex = _grayTex.Value;
        }

        // Restart spritebatch with point filtering for crisp pixels
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullCounterClockwise,
            null,
            Main.UIScaleMatrix);

        // Calculate total width at scaled size (based on socket size)
        // Use integer sizes to ensure pixel-perfect alignment
        int scaledSocketSize = (int)(SocketSize * Scale);
        int scaledSpacing = (int)(Spacing * Scale);
        int totalWidth = SegmentCount * scaledSocketSize + (SegmentCount - 1) * scaledSpacing;
        
        // Offset to center fill inside socket
        int fillOffset = (int)((SocketSize - FillSize) / 2f * Scale);

        // Fixed screen position: center horizontally, above center vertically
        // Round to integers for pixel-perfect rendering
        int barStartX = (Main.screenWidth - totalWidth) / 2;
        int barStartY = Main.screenHeight / 2 - 100;

        float sanityPerSegment = (float)max / SegmentCount;

        for (int i = 0; i < SegmentCount; i++)
        {
            float fill = MathHelper.Clamp((sanity - (i * sanityPerSegment)) / sanityPerSegment, 0f, 1f);

            int x = barStartX + i * (scaledSocketSize + scaledSpacing);
            int y = barStartY;
            var socketPos = new Vector2(x, y);
            var fillPos = new Vector2(x + fillOffset, y + fillOffset);

            // Draw socket background
            Main.spriteBatch.Draw(socketTex, socketPos, null, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);

            // Draw fill with opacity based on segment fill
            if (fill > 0f)
            {
                Color tint = Color.White * fill;
                Main.spriteBatch.Draw(fillTex, fillPos, null, tint, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }
        }

        // Restore default spritebatch state
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullCounterClockwise,
            null,
            Main.UIScaleMatrix);

        return true;
    }
}
