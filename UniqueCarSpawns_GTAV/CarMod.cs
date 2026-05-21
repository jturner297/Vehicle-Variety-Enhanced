using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;

public enum SpawnBehavior
{
    Stock,      // No visual or performance mods, factory look.
    Spec,       // Max Performance. Visuals are either Clean, Hero-Specific, or List-Specific (Higgins/Armoured).
    RandomSpec,  // Max Performance. Visuals are Randomized (or Cult-Specific if in Cult list).
    Tuner,      // STREET RACER: 80% chance. Body kits, Spoilers, Liveries.
    VIP,        // LUXURY: Clean look. Rims, Low Suspension, Tint. NO Body kits.
    Muscle,      // DRAG/POWER: Blowers (Hoods), Exhausts, Muscle Wheels. NO GT Spoilers.
    Beater
}

public static class CarMod
{
    private static Random random = new Random();

    private static readonly Dictionary<string, Dictionary<VehicleModType, HashSet<int>>> ModBlacklist = new Dictionary<string, Dictionary<VehicleModType, HashSet<int>>>(StringComparer.OrdinalIgnoreCase)
{
    // --- WEAPON / PHYSICAL PART BLACKLISTS ---
    { "comet4", new Dictionary<VehicleModType, HashSet<int>> {
        { VehicleModType.Roof, new HashSet<int> { 0 } }     
    }},

    // --- LIVERY BLACKLISTS ---
    { "eudora", new Dictionary<VehicleModType, HashSet<int>> {
        { VehicleModType.Livery, new HashSet<int> { 10 } }
    }},
    { "monstrociti", new Dictionary<VehicleModType, HashSet<int>> {
        { VehicleModType.Livery, new HashSet<int> { 10, 11 } }
    }},
    { "arbitergt", new Dictionary<VehicleModType, HashSet<int>> {
        { VehicleModType.Livery, new HashSet<int> { 10, 11 } }
    }}
};

    // ==========================================
    //           HERO CONFIGURATIONS
    // ==========================================
    // This Dictionary replaces the old "Switch" statement.
    // Logic: "Key" (Car Name) -> "Value" (Action to perform)
    private static readonly Dictionary<string, Action<Vehicle>> HeroConfigs = new Dictionary<string, Action<Vehicle>>(StringComparer.OrdinalIgnoreCase)
    {
        // --- MODEL FIXES ---
        { "turismo2", v => v.Mods[VehicleModType.Spoilers].Index = 3 },
        { "banshee3", v => v.Mods[VehicleModType.Spoilers].Index = 3 },
        { "infernus2", v => v.Mods[VehicleModType.Spoilers].Index = 0 },
        { "kuruma2", v =>   SetColors(v, 12, 12, 0, 12) },
        { "ardent", v => {
       
            // Body Mods from XML
            v.Mods[VehicleModType.Spoilers].Index = 0;
            v.Mods[VehicleModType.FrontBumper].Index = 1;
            v.Mods[VehicleModType.Hood].Index = 4;
            
            // Clean up
            v.Mods[VehicleModType.Livery].Index = -1;
        }},
        // --- SPEED RACER (Mach 5) ---
        { "scramjet", v => {
            v.Mods[VehicleModType.Roof].Index = 0;
            v.Mods.WheelType = VehicleWheelType.Track;
            v.Mods[VehicleModType.FrontWheel].Index = 17;
            v.Mods[VehicleModType.Suspension].Index = -1;
            if (random.Next(0, 2) == 0) {
                SetColors(v, 111, 111, 111, 12);
                v.Mods[VehicleModType.Livery].Index = 4;
            } else {
                SetColors(v, 43, 111, 135, 12);
                v.Mods[VehicleModType.Livery].Index = 2;
            }
        }},

        // --- WEAPONIZED TAMPA (User Custom) ---
        { "tampa3", v => {
            SetColors(v, 93, 0, 4, 0); // Primary: Olive, Sec/Rim: Black, Pearl: Silver
            SetWheels(v, VehicleWheelType.Offroad, 1); // Specific Offroad Wheel
            
            // Body Mods from XML
            v.Mods[VehicleModType.FrontBumper].Index = 0;
            v.Mods[VehicleModType.RearBumper].Index = 0;
            v.Mods[VehicleModType.Frame].Index = 2; // Chassis Upgrade
            v.Mods[VehicleModType.Hood].Index = 2;
            v.Mods[VehicleModType.Roof].Index = 0; // Weapon/Roof Mount
            
            // Clean up
            v.Mods[VehicleModType.Livery].Index = -1;
            v.Mods.WindowTint = VehicleWindowTint.None;
        }},

        // --- BATMOBILE ---
        { "vigilante", v => {
            v.Mods[VehicleModType.Roof].Index = 0;
            SetColors(v, 12, 12, 0, 12);
        }},

        // --- JAMES BOND ---
        { "jb7002", v => {
            v.Mods[VehicleModType.Roof].Index = 0;
            SetColors(v, 5, 5, 5, 5);
        }},

        // --- DELOREAN ---
        { "deluxo", v => {
            v.Mods[VehicleModType.Roof].Index = 0;
            SetColors(v, 18, 17, 5, 0);
        }},

        // --- SUBMARINE CAR ---
        { "stromberg", v => SetColors(v, 111, 111, 111, 0) },

        // --- STREET HAWK (Oppressor Mk1) ---
        { "oppressor", v => {
            v.Mods[VehicleModType.Roof].Index = 0;
            SetColors(v, 0, 117, 10, 27);
            SetWheels(v, VehicleWheelType.BikeWheels, 31);
            v.Mods[VehicleModType.Spoilers].Index = 0;
            v.Mods[VehicleModType.FrontBumper].Index = 0;
            v.Mods[VehicleModType.RearBumper].Index = 0;
            v.Mods[VehicleModType.Frame].Index = 0;
            v.Mods[VehicleModType.Hood].Index = 0;
            v.Mods[VehicleModType.Fender].Index = 0;
            v.Mods[VehicleModType.Suspension].Index = -1;
        }},

        // --- OPPRESSOR MKII ---
        { "oppressor2", v => {
            SetColors(v, 0, 118, 10, 112);
            v.Mods[VehicleModType.Roof].Index = 1;
            v.Mods[VehicleToggleModType.Turbo].IsInstalled = false;
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
            v.Mods[VehicleModType.Suspension].Index = -1;
        }},

        // --- INSURGENT REGULAR ---
        { "insurgent2", v => {
            SetColors(v, 154, 153, 0, 0);
            SetWheels(v, VehicleWheelType.SUV, -1);
            v.Mods[VehicleToggleModType.Turbo].IsInstalled = false;
        }},

        // --- INSURGENT PICKUP CUSTOM ---
        { "insurgent3", v => {
            SetColors(v, 154, 154, 3, 0);
            SetWheels(v, VehicleWheelType.Offroad, 22);
            v.Mods[VehicleModType.RightFender].Index = 0;
            v.Mods[VehicleModType.Roof].Index = 0;
            v.Mods[VehicleModType.Livery].Index = 12;
            EnableExtras(v, 1);
            v.Mods[VehicleModType.Suspension].Index = -1;
        }},

        // --- THRUSTER ---
        { "thruster", v => {
            SetColors(v, 2, 12, 2, 158);
            v.Mods[VehicleModType.Exhaust].Index = 0;
            v.Mods[VehicleModType.Roof].Index = 1;
            EnableExtras(v, 1, 2, 15, 16);
            v.Mods[VehicleModType.Suspension].Index = -1;
        }},

        // --- NIGHTSHARK ---
        { "nightshark", v => {
            SetColors(v, 154, 154, 0, 0);
            SetWheels(v, VehicleWheelType.SUV, -1);
            v.Mods[VehicleModType.Exhaust].Index = 1;
            v.Mods[VehicleModType.Grille].Index = 2;
            v.Mods[VehicleModType.Hood].Index = 7;
            v.Mods[VehicleModType.Fender].Index = 0;
            v.Mods[VehicleModType.Livery].Index = 12;
        }},

        // --- MILITARY PLANES ---
        { "pyro", v => {
            v.Mods[VehicleModType.Roof].Index = 0;
            if (random.Next(0, 2) == 0) SetColors(v, 5, 4, 112, 0);
            else { SetColors(v, 60, 117, 0, 111); v.Mods[VehicleModType.Livery].Index = 7; }
        }},

        { "mogul", v => {
            SetColors(v, 5, 4, 3, 154);
            v.Mods[VehicleModType.Exhaust].Index = 0;
            v.Mods[VehicleModType.Frame].Index = 0;
            v.Mods[VehicleModType.RightFender].Index = 0;
            v.Mods[VehicleModType.Roof].Index = 0;
            v.Mods[VehicleModType.Suspension].Index = -1;
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
            if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
            else v.Mods[VehicleModType.Livery].Index = 3;
        }},

        { "molotok", v => {
            SetColors(v, 111, 111, 3, 154);
            v.Mods[VehicleModType.Roof].Index = 0;
            v.Mods[VehicleModType.SideSkirt].Index = 0;
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
            if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
            else v.Mods[VehicleModType.Livery].Index = 1;
        }},

        { "starling", v => {
            v.Mods[VehicleModType.Exhaust].Index = 0;
            v.Mods[VehicleModType.RightFender].Index = 0;
            v.Mods[VehicleModType.Roof].Index = 0;
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
            if (random.Next(0, 2) == 0) { SetColors(v, 152, 25, 3, 154); v.Mods[VehicleModType.Livery].Index = 4; }
            else { SetColors(v, 121, 25, 7, 111); v.Mods[VehicleModType.Livery].Index = 0; }
        }},

        { "nokota", v => {
            SetColors(v, 4, 43, 111, 119);
            v.Mods[VehicleModType.SideSkirt].Index = 0;
            v.Mods[VehicleModType.Roof].Index = 0;
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
            if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
            else v.Mods[VehicleModType.Livery].Index = 3;
        }}
    };

    public static void ApplyStyle(Vehicle v, SpawnBehavior behavior, string modelName)
    {
        // 1. STOCK BEHAVIOR
        if (behavior == SpawnBehavior.Stock) return;

        v.Mods.InstallModKit();

        // 2. APPLY PERFORMANCE (Maintained your specific static indexes)
        ApplyPerformance(v);
        ClearNeon(v);
        // 3. GLOBAL DEFAULTS
        if (v.ClassType == VehicleClass.Super || v.ClassType == VehicleClass.Sports)
        {
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;
        }

        switch (behavior)
        {
            case SpawnBehavior.RandomSpec:
                // TWEAK: CULT Logic (Epsilon Blue)
                if (VehList.models_cult.Contains(modelName))
                {
                    SetColors(v, 157, 157, 1, 0);
                    v.Mods[VehicleModType.Livery].Index = -1;
                    ApplyRandomVisuals(v, modelName);
                }
                else
                {
                    // SAFETY CHECK: Air Vehicles (No Body Mods = No Weapons)
                    if (v.ClassType == VehicleClass.Helicopters || v.ClassType == VehicleClass.Planes)
                    {
                        RandomizeLivery(v, modelName);
                        // Note: If you want the Conada fix we discussed, insert it here.
                        // Currently keeping it EXACTLY as your upload.
                    }
                    else // Cars/Bikes/Boats
                    {
                        ApplyRandomVisuals(v, modelName);
                        RandomizeLivery(v, modelName);
                    }
                }
                break;
            case SpawnBehavior.Spec:
                // A. Check Hero Dictionary (The new efficient way)
                // Replaces the old "ApplyHeroSpecs" switch statement
                bool foundSpecific = ApplyHeroSpecs(v, modelName);

                // B. If no specific config found, check the Group Lists
                if (!foundSpecific)
                {
                    // HIGGINS
                    if (VehList.models_higgins.Contains(modelName))
                    {
                        if (modelName == "conada")
                        {
                            v.Mods.PrimaryColor = (VehicleColor)89;
                            v.Mods.SecondaryColor = (VehicleColor)6;
                            v.Mods[VehicleModType.Livery].Index = 9;
                        }
                        v.Mods.PearlescentColor = VehicleColor.MetallicMidnightSilver;
                    }
                    // ARMOURED
                    else if (VehList.models_armoured.Contains(modelName))
                    {
                        SetColors(v, 12, 12, 0, 12); // matte black
                    }
                    // HELICOPTERS (Generic)
                    else if (VehList.models_helicopter.Contains(modelName))
                    {
                        RandomizeLivery(v, modelName);
                    }
                }
                break;
            case SpawnBehavior.Tuner:
                // JDM / STREET RACER
                ApplyTunerVisuals(v, modelName);
                RandomizeLivery(v, modelName);
                break;

            case SpawnBehavior.VIP:
                // LUXURY / STANCE
                ApplyVIPVisuals(v, modelName);


                break;

            case SpawnBehavior.Muscle:
                // DRAG / CLASSIC
                ApplyMuscleVisuals(v, modelName);
                RandomizeLivery(v, modelName); // Stripes allowed
                break;
            case SpawnBehavior.Beater:
                // DRAG / CLASSIC
                v.Mods.Livery = -1; // Force Clean
                ApplyBeaterVisuals(v, modelName);
                break;

        }


        // Ensure collision loading for heavier edits
        Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
        Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
    }

    public static void ApplyPerformance(Vehicle v)
    {
        // Maintained your EXACT static indexes (No HSW logic)
        v.CanTiresBurst = false;
        v.Mods[VehicleModType.Engine].Index = 3;
        v.Mods[VehicleModType.Brakes].Index = 2;
        v.Mods[VehicleModType.Transmission].Index = 2;
        //v.Mods[VehicleModType.Suspension].Index = 3;
        v.Mods[VehicleModType.Armor].Index = 4;
        v.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
    }

    private static bool ApplyHeroSpecs(Vehicle v, string modelName)
    {
        if (HeroConfigs.ContainsKey(modelName))
        {
            HeroConfigs[modelName](v);
            return true;
        }
        return false;
    }

    public static void ApplyRandomVisuals(Vehicle v, string modelName)
    {
        var types = new HashSet<VehicleModType> { VehicleModType.FrontBumper, VehicleModType.RearBumper, VehicleModType.SideSkirt, VehicleModType.Spoilers, VehicleModType.Hood, VehicleModType.Exhaust };
        foreach (var type in types)
        {
            ApplySmartMod(v, type, 100, 100, modelName);
        }
    }

    public static void ApplyBalancedVisuals(Vehicle v)
    {
        var performanceTypes = new HashSet<VehicleModType> { VehicleModType.Engine, VehicleModType.Brakes, VehicleModType.Transmission, VehicleModType.Suspension, VehicleModType.Armor };

        foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
        {
            // BANS: Performance, Liveries, Horns, AND ROOFS
            if (performanceTypes.Contains(modType) || modType == VehicleModType.Livery || modType == VehicleModType.Horns || modType == VehicleModType.Roof) continue;

            int count = v.Mods[modType].Count;
            if (count > 0)
            {
                // 50% CHANCE: Flip a coin (0 or 1). Only change if 0.
                if (random.Next(0, 2) == 0)
                {
                    v.Mods[modType].Index = random.Next(0, count);
                }
                // Else: Do nothing (leave as stock)
            }
        }
    }



    public static void RandomizeLivery(Vehicle v, string modelName, int keepPct = 100)
    {
        int count = v.Mods.LiveryCount;
        if (count > 0)
        {
            int maxIndex = (count * keepPct) / 100;

            if (maxIndex < 1) maxIndex = 1;
            if (maxIndex > count) maxIndex = count;

            List<int> validLiveries = new List<int>();
            HashSet<int> bannedIndices = null;

            // --- NEW UNIFIED BLACKLIST LOOKUP ---
            if (ModBlacklist.ContainsKey(modelName) && ModBlacklist[modelName].ContainsKey(VehicleModType.Livery))
            {
                bannedIndices = ModBlacklist[modelName][VehicleModType.Livery];
            }
           
            // Loop ONLY up to the calculated percentage (maxIndex)
            for (int i = 0; i < maxIndex; i++)
            {
                if (bannedIndices != null && bannedIndices.Contains(i))
                {
                    continue; // Skip blacklisted livery
                }
                validLiveries.Add(i);
            }
            // 3. Apply a random livery from the valid list
            if (validLiveries.Count > 0)
            {
                v.Mods.Livery = validLiveries[random.Next(0, validLiveries.Count)];
            }
        }
    }

    private static void ApplySmartMod(Vehicle v, VehicleModType type, int chance, int keepPct, string modelName)
    {
        int count = v.Mods[type].Count;

        if (count > 0 && random.Next(0, 100) < chance)
        {
            int maxIndex = (count * keepPct) / 100;
            if (maxIndex < 1) maxIndex = 1;
            if (maxIndex > count) maxIndex = count;

            List<int> validIndices = new List<int>();
            HashSet<int> bannedIndices = null;

            // Check if this specific car and mod type are in the blacklist
            if (ModBlacklist.ContainsKey(modelName) && ModBlacklist[modelName].ContainsKey(type))
            {
                bannedIndices = ModBlacklist[modelName][type];
            }

            // Only add non-blacklisted parts to the valid pool
            for (int i = 0; i < maxIndex; i++)
            {
                if (bannedIndices != null && bannedIndices.Contains(i)) continue;
                validIndices.Add(i);
            }

            if (validIndices.Count > 0)
            {
                v.Mods[type].Index = validIndices[random.Next(0, validIndices.Count)];
            }
            else
            {
                v.Mods[type].Index = -1; // Clean fallback if all options are banned
            }
        }
        else
        {
            v.Mods[type].Index = -1;
        }
    }

    public static void ApplyTunerVisuals(Vehicle v, string modelName)
    {
        ApplySmartMod(v, VehicleModType.FrontBumper, 80, 100, modelName);
        ApplySmartMod(v, VehicleModType.RearBumper, 80, 100, modelName);
        ApplySmartMod(v, VehicleModType.SideSkirt, 80, 100, modelName);
        ApplySmartMod(v, VehicleModType.Hood, 80, 100, modelName);
        ApplySmartMod(v, VehicleModType.Exhaust, 80, 100, modelName);
        ApplySmartMod(v, VehicleModType.Spoilers, 80, 70, modelName);
    }

    public static void ApplyVIPVisuals(Vehicle v, string modelName)
    {
        // 1. Suspension (Low)
       // v.Mods[VehicleModType.Suspension].Index = 3;

        // 2. Window Tint (Dark Smoke)
        v.Mods.WindowTint = VehicleWindowTint.DarkSmoke;

        // 3. Wheels (High End or Sport)
        if (v.ClassType == VehicleClass.Super || v.ClassType == VehicleClass.Sports)
        {
            v.Mods.RimColor = (VehicleColor)0;

        }


        ApplySmartMod(v, VehicleModType.FrontBumper, 100, 50, modelName);
        ApplySmartMod(v, VehicleModType.RearBumper, 100, 50, modelName);
        ApplySmartMod(v, VehicleModType.SideSkirt, 100, 50, modelName);
        ApplySmartMod(v, VehicleModType.Hood, 50, 30, modelName);
        ApplySmartMod(v, VehicleModType.Roof, 30, 20, modelName);
        ApplySmartMod(v, VehicleModType.Exhaust, 100, 100, modelName);
        // 50% Chance, Keep 20% (Lip Spoilers only).
        ApplySmartMod(v, VehicleModType.Spoilers, 100, 40, modelName);


       
        // Livery
        if (random.Next(0, 2) == 0)
        {
            RandomizeLivery(v, modelName, 20);
        }
        else
        {
            v.Mods[VehicleModType.Livery].Index = -1;
        }

    }

    public static void ApplyMuscleVisuals(Vehicle v, string modelName)
    {
        // 1. Wheels (Muscle)
        v.Mods.WheelType = VehicleWheelType.Muscle;
        v.Mods[VehicleModType.FrontWheel].Index = random.Next(0, 18);

        ApplySmartMod(v, VehicleModType.Hood, 60, 100, modelName);
        ApplySmartMod(v, VehicleModType.Exhaust, 70, 100, modelName);
        ApplySmartMod(v, VehicleModType.Spoilers, 60, 40, modelName);
    }

    public static void ApplyBeaterVisuals(Vehicle v, string modelName)
    {
        ApplySmartMod(v, VehicleModType.Roof, 80, 100, modelName);
        ApplySmartMod(v, VehicleModType.Grille, 60, 100, modelName);
        ApplySmartMod(v, VehicleModType.Exhaust, 50, 30, modelName);
        ApplySmartMod(v, VehicleModType.Horns, 40, 100, modelName);
        ApplySmartMod(v, VehicleModType.FrontBumper, 50, 40, modelName);
        ApplySmartMod(v, VehicleModType.RearBumper, 50, 40, modelName);
        ApplySmartMod(v, VehicleModType.SideSkirt, 40, 30, modelName);
        ApplySmartMod(v, VehicleModType.Hood, 40, 50, modelName);
        ApplySmartMod(v, VehicleModType.Fender, 40, 100, modelName);
        ApplySmartMod(v, VehicleModType.Trim, 50, 100, modelName);
        ApplySmartMod(v, VehicleModType.Aerials, 70, 100, modelName);
        ApplySmartMod(v, VehicleModType.Spoilers, 10, 15, modelName);
    }
    // ==========================================
    //              HELPER METHODS
    // ==========================================
    private static void SetColors(Vehicle v, int pri, int sec, int pearl, int rim)
    {
        v.Mods.PrimaryColor = (VehicleColor)pri;
        v.Mods.SecondaryColor = (VehicleColor)sec;
        v.Mods.PearlescentColor = (VehicleColor)pearl;
        v.Mods.RimColor = (VehicleColor)rim;
    }

    private static void SetWheels(Vehicle v, VehicleWheelType type, int index)
    {
        v.Mods.WheelType = type;
        v.Mods[VehicleModType.FrontWheel].Index = index;
        if (v.ClassType == VehicleClass.Motorcycles) v.Mods[VehicleModType.RearWheel].Index = index;
    }

    private static void EnableExtras(Vehicle v, params int[] extraIds)
    {
        foreach (int id in extraIds) if (v.ExtraExists(id)) v.ToggleExtra(id, true);
    }
    private static void ClearNeon(Vehicle v)
    {
        // Explicitly turn off all 4 neon tubes
        v.Mods.SetNeonLightsOn(VehicleNeonLight.Left, false);
        v.Mods.SetNeonLightsOn(VehicleNeonLight.Right, false);
        v.Mods.SetNeonLightsOn(VehicleNeonLight.Front, false);
        v.Mods.SetNeonLightsOn(VehicleNeonLight.Back, false);
    }
}