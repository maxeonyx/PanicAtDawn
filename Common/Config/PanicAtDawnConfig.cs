using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace PanicAtDawn.Common.Config;

public sealed class PanicAtDawnConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [DefaultValue(true)]
    public bool EnableLinkSanity;

    [DefaultValue(90f)]
    [Range(20f, 300f)]
    public float LinkRadiusTiles;

    [DefaultValue(200)]
    [Range(20, 500)]
    public int SanityMax;

    [DefaultValue(3.33f)]
    [Range(0.1f, 50f)]
    public float SanityDrainPerSecond;

    [DefaultValue(2f)]
    [Range(1f, 10f)]
    public float SanityDarknessDrainMultiplier;

    [DefaultValue(4f)]
    [Range(0.1f, 50f)]
    public float SanityRegenPerSecond;

    [DefaultValue(true)]
    public bool DisableRecallAndMirrors;

    [DefaultValue(true)]
    public bool ReplaceRecallWithWormhole;

    [DefaultValue(true)]
    public bool RequireFullHealthForWormhole;

    [DefaultValue(true)]
    public bool EnableDawnShelterRule;

    [DefaultValue(60)]
    [Range(0, 600)]
    public int DawnJoinGraceSeconds;

    [DefaultValue(50)]
    [Range(20, 300)]
    public int SpawnSafeRadiusTiles;

    [DefaultValue(true)]
    public bool EnablePylonSafeZones;
}
