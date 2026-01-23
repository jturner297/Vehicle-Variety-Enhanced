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

    private static readonly Dictionary<string, HashSet<int>> LiveryBlacklist = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase)
    {
     //   { "monstrociti", new HashSet<int> { 5, 10, 11 } },
       { "eudora", new HashSet<int> { 10 } },
       { "monstrociti", new HashSet<int> { 10, 11 } },
        // Add more here: { "modelname", new HashSet<int> { 1, 2, 3 } },
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
                    ApplyRandomVisuals(v);
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
                        ApplyRandomVisuals(v);
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
                ApplyTunerVisuals(v);
                RandomizeLivery(v, modelName);
                break;

            case SpawnBehavior.VIP:
                // LUXURY / STANCE
                ApplyVIPVisuals(v);
                v.Mods[VehicleModType.Livery].Index = -1;
                /* if (random.Next(0, 2) == 0)
               {
                   RandomizeLivery(v, modelName, 20);
               }
               else
               {
                   v.Mods[VehicleModType.Livery].Index = -1;
               }*/
                break;

            case SpawnBehavior.Muscle:
                // DRAG / CLASSIC
                ApplyMuscleVisuals(v);
                RandomizeLivery(v, modelName); // Stripes allowed
                break;
            case SpawnBehavior.Beater:
                // DRAG / CLASSIC
                v.Mods.Livery = -1; // Force Clean
                ApplyBeaterVisuals(v);
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
        v.Mods[VehicleModType.Suspension].Index = 3;
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

    public static void ApplyRandomVisuals(Vehicle v)
    {
        var types = new HashSet<VehicleModType> { VehicleModType.FrontBumper, VehicleModType.RearBumper, VehicleModType.SideSkirt, VehicleModType.Spoilers, VehicleModType.Hood, VehicleModType.Exhaust };
        foreach (var type in types)
        {
            ApplySmartMod(v, type, 100, 100); // 100% chance, Keep 100% of list
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
        int count = v.Mods.LiveryCount; //
        if (count > 0)
        {
            // 1. Calculate the Percentage Limit (SmartMod Logic)
            // e.g. If count is 10 and keepPct is 20, maxIndex becomes 2.
            int maxIndex = (count * keepPct) / 100;

            // Safety: Always check at least 1 livery if the car has them, 
            // but don't exceed the actual count.
            if (maxIndex < 1) maxIndex = 1;
            if (maxIndex > count) maxIndex = count;

            // 2. Filter Valid Liveries (Blacklist Logic)
            List<int> validLiveries = new List<int>();

            HashSet<int> bannedIndices = null;
            if (LiveryBlacklist.ContainsKey(modelName))
            {
                bannedIndices = LiveryBlacklist[modelName];
            }

            // Loop ONLY up to the calculated percentage (maxIndex)
            for (int i = 0; i < maxIndex; i++)
            {
                // Skip if this specific livery ID is blacklisted
                if (bannedIndices != null && bannedIndices.Contains(i))
                {
                    continue;
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

    private static void ApplySmartMod(Vehicle v, VehicleModType type, int chance, int keepPct)
    {
        int count = v.Mods[type].Count;

        // Safety check: Does the car have parts? Did we roll the lucky number?
        if (count > 0 && random.Next(0, 100) < chance)
        {
            // NEW LOGIC: Use integer math (e.g. 20 * 90 / 100 = 18)
            int maxIndex = (count * keepPct) / 100;

            // Safety: Ensure we have at least 1 option if percentage is tiny
            if (maxIndex < 1) maxIndex = 1;
            if (maxIndex > count) maxIndex = count;

            v.Mods[type].Index = random.Next(0, maxIndex);
        }
        else
        {
            // Clean Logic: Force stock if the check failed
            v.Mods[type].Index = -1;
        }
    }


    public static void ApplyTunerVisuals(Vehicle v)
    {


        // 1. Core Body Mods (80% chance, Keep 100% of parts)
        ApplySmartMod(v, VehicleModType.FrontBumper, 80, 100);
        ApplySmartMod(v, VehicleModType.RearBumper, 80, 100);
        ApplySmartMod(v, VehicleModType.SideSkirt, 80, 100);
        ApplySmartMod(v, VehicleModType.Hood, 80, 100);
        ApplySmartMod(v, VehicleModType.Exhaust, 80, 100);

        // 2. Spoilers (80% chance, Keep 70% - Bans the top 30% huge wings)
        ApplySmartMod(v, VehicleModType.Spoilers, 80, 70);
    }

    public static void ApplyVIPVisuals(Vehicle v)
    {
        // 1. Suspension (Low)
        v.Mods[VehicleModType.Suspension].Index = 3;

        // 2. Window Tint (Dark Smoke)
        v.Mods.WindowTint = VehicleWindowTint.DarkSmoke;

        // 3. Wheels (High End or Sport)
        if (v.ClassType == VehicleClass.Super || v.ClassType == VehicleClass.Sports)
        {
            v.Mods.RimColor = (VehicleColor)0;
          /*  if (random.Next(0, 2) == 0)
            {
                v.Mods.WheelType = VehicleWheelType.HighEnd;
                v.Mods[VehicleModType.FrontWheel].Index = random.Next(0, 20);
            }
            else
            {
                v.Mods.WheelType = VehicleWheelType.Sport;
                v.Mods[VehicleModType.FrontWheel].Index = random.Next(0, 20);
            }*/
        }

        // 4. Subtle Spoilers ONLY
        // 50% Chance, Keep 20% (Lip Spoilers only).
        ApplySmartMod(v, VehicleModType.Spoilers, 50, 40);
    }

    public static void ApplyMuscleVisuals(Vehicle v)
    {
        // 1. Wheels (Muscle)
        v.Mods.WheelType = VehicleWheelType.Muscle;
        v.Mods[VehicleModType.FrontWheel].Index = random.Next(0, 18);

        // 2. Hoods (Blowers - 60% chance, Keep 100% of types)
        ApplySmartMod(v, VehicleModType.Hood, 60, 100);

        // 3. Exhausts (Loud - 70% chance, Keep 100%)
        ApplySmartMod(v, VehicleModType.Exhaust, 70, 100);

        // 4. Spoilers (Ducktails ONLY)
        // 60% Chance, Keep 35% (Bans GT Wings).
        ApplySmartMod(v, VehicleModType.Spoilers, 60, 40);
    }

    public static void ApplyBeaterVisuals(Vehicle v)
    {

        // --- WHEELS (The Foundation) ---


        // --- KEY BEATER PARTS (High Priority) ---
        ApplySmartMod(v, VehicleModType.Roof, 80, 100); // Roof Racks / Luggage (Essential for the look)
        ApplySmartMod(v, VehicleModType.Grille, 60, 100); // Broken or Custom Grilles
        ApplySmartMod(v, VehicleModType.Exhaust, 50, 30);  // Small/Rusty tips (Limit to first 30% to avoid giant canons)
        ApplySmartMod(v, VehicleModType.Horns, 40, 100); // Truck horns / Joke horns

        // --- BODY KITS (Conservative) ---
        ApplySmartMod(v, VehicleModType.FrontBumper, 50, 40);  // Limit to 40% to avoid Carbon Splitters
        ApplySmartMod(v, VehicleModType.RearBumper, 50, 40);
        ApplySmartMod(v, VehicleModType.SideSkirt, 40, 30);  // Skirts often look too "racy", keep low limit

        // --- DETAIL PARTS (The "Junk" Look) ---
        ApplySmartMod(v, VehicleModType.Hood, 40, 50);  // Rusted hoods / removed hoods
        ApplySmartMod(v, VehicleModType.Fender, 40, 100); // Vents / Arches


        // --- ACCESSORIES (Trucks & Classics) ---
        ApplySmartMod(v, VehicleModType.Trim, 50, 100); // Exterior Trim / Chrome
        ApplySmartMod(v, VehicleModType.Aerials, 70, 100); // Antennas (Great for rural vibe)



        // --- THE DANGER ZONE (Race Parts) ---
        // If you enable Spoilers, keep the limit VERY low (10-20%) to only catch "Lip" spoilers.
        // Anything higher usually hits "GT Wing" territory.
        ApplySmartMod(v, VehicleModType.Spoilers, 10, 15);
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
}