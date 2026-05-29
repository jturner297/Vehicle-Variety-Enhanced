using GTA;
using System;

public static class ModSettings
{
    private static bool _isLoaded = false;

    // ==========================================
    //              PARKED SETTINGS
    // ==========================================
    public static bool ParkedShowBlips { get; private set; } = true;
    public static bool LockDoors { get; private set; } = true;
    public static float SpotSpawnDistance { get; private set; } = 150f;
    public static float SpotSpawnDistMin { get; private set; } = 80f;
    public static float SpotDefaultDespawnBuffer { get; private set; } = 50f;

    // ==========================================
    //              TRAFFIC SETTINGS
    // ==========================================
    public static int MinSwapCooldown { get; private set; } = 30000;
    public static int MaxSwapCooldown { get; private set; } = 75000;
    public static int CheckInterval { get; private set; } = 250;
    public static int MaxActiveSwaps { get; private set; } = 1;
    public static float MaxSwapDist { get; private set; } = 250f;
    public static int _historyCapacity { get; private set; } = 10;
    public static float SwapFOV { get; private set; } = 60f;
    public static float ScoreThreshold { get; private set; } = 400f;
    public static float ScoreVisible { get; private set; } = 50f;
    public static float ScoreSameRoad { get; private set; } = 150f;
    public static float ScoreDeadAhead { get; private set; } = 200f;
    public static bool TrafficShowBlips { get; private set; } = true;

   
    public static bool IgnoreEmergencyTraffic { get; private set; } = true;
    public static bool IgnoreServiceTraffic { get; private set; } = true;
    public static bool IgnoreBigTraffic { get; private set; } = true;




    // ==========================================
    //              BLIPS SETTINGS
    // ==========================================
    public static bool ShowVehicleNameOnBlips { get; private set; } = false;
    public static float BlipSize { get; private set; } = 1.0f;

    // ==========================================
    //              IMMERSION SETTINGS
    // ==========================================

    public static bool EnableSpotCooldowns = true; // Set to false to disable immersion cooldowns
    public static int SpotCooldown { get; private set; } = 50000;
    public static float CooldownResetDistance { get; private set; } = 1000f;
    public static bool RngSpots { get; private set; } = false;
    public static int SpotRngChancePercent { get; private set; } = 60;

    public static void Load()
    {
        if (_isLoaded) return; // Prevent loading multiple times

        string iniPath = "scripts\\UniqueVehiclesSP.ini";
        ScriptSettings settings = ScriptSettings.Load(iniPath);

        // ParkedSpawns
        ParkedShowBlips = settings.GetValue("PARKED", "ShowBlips", ParkedShowBlips);
        LockDoors = settings.GetValue("PARKED", "LockDoors", LockDoors);
        SpotSpawnDistance = settings.GetValue("PARKED", "SpotSpawnDistance", SpotSpawnDistance);
        SpotSpawnDistMin = settings.GetValue("PARKED", "SpotSpawnDistMin", SpotSpawnDistMin);
        SpotDefaultDespawnBuffer = settings.GetValue("PARKED", "SpotDefaultDespawnBuffer", SpotDefaultDespawnBuffer);

     

        // TrafficSwap
        MinSwapCooldown = settings.GetValue("TRAFFIC", "MinSwapCooldown", MinSwapCooldown);
        MaxSwapCooldown = settings.GetValue("TRAFFIC", "MaxSwapCooldown", MaxSwapCooldown);
        CheckInterval = settings.GetValue("TRAFFIC", "CheckInterval", CheckInterval);
        MaxActiveSwaps = settings.GetValue("TRAFFIC", "MaxActiveSwaps", MaxActiveSwaps);
        MaxSwapDist = settings.GetValue("TRAFFIC", "MaxSwapDist", MaxSwapDist);
        SwapFOV = settings.GetValue("TRAFFIC", "SwapFOV", SwapFOV);
        ScoreThreshold = settings.GetValue("TRAFFIC", "ScoreThreshold", ScoreThreshold);
        ScoreVisible = settings.GetValue("TRAFFIC", "ScoreVisible", ScoreVisible);
        ScoreSameRoad = settings.GetValue("TRAFFIC", "ScoreSameRoad", ScoreSameRoad);
        ScoreDeadAhead = settings.GetValue("TRAFFIC", "ScoreDeadAhead", ScoreDeadAhead);
        TrafficShowBlips = settings.GetValue("TRAFFIC", "ShowBlips", TrafficShowBlips);
        IgnoreEmergencyTraffic = settings.GetValue("TRAFFIC", "IgnoreEmergencyTraffic", IgnoreEmergencyTraffic);
        IgnoreServiceTraffic = settings.GetValue("TRAFFIC", "IgnoreServiceTraffic", IgnoreServiceTraffic);
        IgnoreBigTraffic = settings.GetValue("TRAFFIC", "IgnoreBigTraffic", IgnoreBigTraffic);
        _historyCapacity = settings.GetValue("TRAFFIC", "HistoryCapacity", _historyCapacity);

        // Blips
        ShowVehicleNameOnBlips = settings.GetValue("BLIPS", "ShowVehicleNameOnBlips", ShowVehicleNameOnBlips);
        BlipSize = settings.GetValue("BLIPS", "BlipSize", BlipSize);
        
        // Immersion
        EnableSpotCooldowns = settings.GetValue("IMMERSION", "EnableSpotCooldowns", EnableSpotCooldowns);
        SpotCooldown = settings.GetValue("IMMERSION", "SpotCooldown", SpotCooldown);
        CooldownResetDistance = settings.GetValue("IMMERSION", "CooldownResetDistance", CooldownResetDistance);
        RngSpots = settings.GetValue("IMMERSION", "RngSpots", RngSpots);
        SpotRngChancePercent = settings.GetValue("IMMERSION", "SpotRngChancePercent", SpotRngChancePercent);
       
        _isLoaded = true;
    }
}