using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;


public enum SpawnBehavior
{
    Stock,      // No visual or performance mods, factory look.
    Spec,       // Max Performance. Visuals are either Clean, Hero-Specific, or List-Specific (Higgins/Armoured).
    RandomSpec  // Max Performance. Visuals are Randomized (or Cult-Specific if in Cult list).
}

public static class CarMod
{
    private static Random random = new Random();

    /// <summary>
    /// MASTER STYLING FUNCTION
    /// </summary>
    public static void ApplyStyle(Vehicle v, SpawnBehavior behavior, string modelName)
    {
        // 1. STOCK BEHAVIOR
        // Return immediately (Factory condition)
        if (behavior == SpawnBehavior.Stock)
        {
            return;
        }
        v.Mods.InstallModKit();

        // 2. APPLY PERFORMANCE (For Spec and RandomSpec)
        ApplyPerformance(v);

        // 3. GLOBAL DEFAULTS
        if (v.ClassType == VehicleClass.Super || v.ClassType == VehicleClass.Sports)
        {
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;
        }

        // 4. RANDOMSPEC LOGIC
        if (behavior == SpawnBehavior.RandomSpec)
        {
            // TWEAK: CULT Logic (Epsilon Blue)
            if (VehList.models_cult.Contains(modelName))
            {
                SetColors(v, 157, 157, 1, 0);
                v.Mods[VehicleModType.Livery].Index = -1;
                ApplyRandomVisuals(v);
            }
            else
            {
                ApplyRandomVisuals(v);
                RandomizeLivery(v);
            }
        }
        // 5. SPEC LOGIC (Hard Defined or Group Themes)
        else if (behavior == SpawnBehavior.Spec)
        {
            // A. Try Hard-Defined Rules (Hero Cars / Fixes / Military)
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
                    SetColors(v, 12, 12, 0, 12); //matteblack
                }
                // HELICOPTERS (Generic)
                else if (VehList.models_helicopter.Contains(modelName))
                {
                    RandomizeLivery(v);
                }
            }
        }

        // Ensure collision loading for heavier edits
        Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
        Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
    }

    public static void ApplyPerformance(Vehicle v)
    {
        v.CanTiresBurst = false;
        v.Mods[VehicleModType.Engine].Index = 3;
        v.Mods[VehicleModType.Brakes].Index = 2;
        v.Mods[VehicleModType.Transmission].Index = 2;
        v.Mods[VehicleModType.Suspension].Index = 3;
        v.Mods[VehicleModType.Armor].Index = 4;
        v.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
    }

    /// <summary>
    /// Applies specific hard-coded configs. Returns TRUE if a config was applied.
    /// </summary>
    private static bool ApplyHeroSpecs(Vehicle v, string modelName)
    {
        switch (modelName.ToLower())
        {
            // --- MODEL FIXES (Moved from ApplyModelFixes) ---
            case "turismo2":
            case "banshee3":
                v.Mods[VehicleModType.Spoilers].Index = 3;
                return true;

            // --- SPEED RACER (Mach 5) ---
            case "scramjet":
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods.WheelType = VehicleWheelType.Track;
                v.Mods[VehicleModType.FrontWheel].Index = 17;
                v.Mods[VehicleModType.Suspension].Index = -1;

                if (random.Next(0, 2) == 0)
                {
                    SetColors(v, 111, 111, 111, 12);
                    v.Mods[VehicleModType.Livery].Index = 4;
                }
                else
                {
                    SetColors(v, 43, 111, 135, 12);
                    v.Mods[VehicleModType.Livery].Index = 2;
                }
                return true;

            // --- BATMOBILE ---
            case "vigilante":
                v.Mods[VehicleModType.Roof].Index = 0;
                SetColors(v, 12, 12, 0, 12);
                return true;

            // --- JAMES BOND ---
            case "jb7002":
                v.Mods[VehicleModType.Roof].Index = 0;
                SetColors(v, 5, 5, 5, 5);
                return true;

            // --- DELOREAN ---
            case "deluxo":
                v.Mods[VehicleModType.Roof].Index = 0;
                SetColors(v, 17, 18, 5, 0);
                return true;

            case "stromberg":
                SetColors(v, 111, 111, 111, 0);
                return true;

            // --- STREET HAWK (Oppressor Mk1) ---
            case "oppressor":
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
                return true;

            // --- OPPRESSOR MKII ---
            case "oppressor2":
                SetColors(v, 0, 118, 10, 112);
                v.Mods[VehicleModType.Roof].Index = 1;
                v.Mods[VehicleToggleModType.Turbo].IsInstalled = false;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
                v.Mods[VehicleModType.Suspension].Index = -1;
                return true;

            // --- INSURGENT REGULAR ---
            case "insurgent2":
                SetColors(v, 154, 153, 0, 0);
                SetWheels(v, VehicleWheelType.SUV, -1);
                v.Mods[VehicleToggleModType.Turbo].IsInstalled = false;
                return true;

            // --- INSURGENT PICKUP CUSTOM ---
            case "insurgent3":
                SetColors(v, 154, 154, 3, 0);
                SetWheels(v, VehicleWheelType.Offroad, 22);
                v.Mods[VehicleModType.RightFender].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleModType.Livery].Index = 12;
                EnableExtras(v, 1);
                v.Mods[VehicleModType.Suspension].Index = -1;
                return true;

            // --- THRUSTER ---
            case "thruster":
                SetColors(v, 2, 12, 2, 158);
                v.Mods[VehicleModType.Exhaust].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 1;
                EnableExtras(v, 1, 2, 15, 16);
                v.Mods[VehicleModType.Suspension].Index = -1;
                return true;

            // --- NIGHTSHARK ---
            case "nightshark":
                SetColors(v, 154, 154, 0, 0);
                SetWheels(v, VehicleWheelType.SUV, -1);
                v.Mods[VehicleModType.Exhaust].Index = 1;
                v.Mods[VehicleModType.Grille].Index = 2;
                v.Mods[VehicleModType.Hood].Index = 7;
                v.Mods[VehicleModType.Fender].Index = 0;
                v.Mods[VehicleModType.Livery].Index = 12;
                return true;

            // --- MILITARY PLANES ---
            case "pyro":
                v.Mods[VehicleModType.Roof].Index = 0;
                if (random.Next(0, 2) == 0) SetColors(v, 5, 4, 112, 0);
                else { SetColors(v, 60, 117, 0, 111); v.Mods[VehicleModType.Livery].Index = 7; }
                return true;

            case "mogul":
                SetColors(v, 5, 4, 3, 154);
                v.Mods[VehicleModType.Exhaust].Index = 0;
                v.Mods[VehicleModType.Frame].Index = 0;
                v.Mods[VehicleModType.RightFender].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleModType.Suspension].Index = -1;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
                if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
                else v.Mods[VehicleModType.Livery].Index = 3;
                return true;

            case "molotok":
                SetColors(v, 111, 111, 3, 154);
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleModType.SideSkirt].Index = 0;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
                if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
                else v.Mods[VehicleModType.Livery].Index = 1;
                return true;

            case "starling":
                v.Mods[VehicleModType.Exhaust].Index = 0;
                v.Mods[VehicleModType.RightFender].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
                if (random.Next(0, 2) == 0) { SetColors(v, 152, 25, 3, 154); v.Mods[VehicleModType.Livery].Index = 4; }
                else { SetColors(v, 121, 25, 7, 111); v.Mods[VehicleModType.Livery].Index = 0; }
                return true;

            case "nokota":
                SetColors(v, 4, 43, 111, 119);
                v.Mods[VehicleModType.SideSkirt].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
                if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
                else v.Mods[VehicleModType.Livery].Index = 3;
                return true;
        }

        return false;
    }

    public static void ApplyRandomVisuals(Vehicle v)
    {
        var performanceTypes = new List<VehicleModType> { VehicleModType.Engine, VehicleModType.Brakes, VehicleModType.Transmission, VehicleModType.Suspension, VehicleModType.Armor };

        foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
        {
            if (performanceTypes.Contains(modType) || modType == VehicleModType.Livery || modType == VehicleModType.Horns) continue;

            int count = v.Mods[modType].Count;
            if (count > 0)
            {
                v.Mods[modType].Index = random.Next(0, count);
            }
        }
    }

    public static void RandomizeLivery(Vehicle v, int limit = -1)
    {
        int count = v.Mods.LiveryCount;
        if (count > 0)
        {
            int max = (limit > 0) ? Math.Min(count, limit) : count;
            v.Mods.Livery = random.Next(0, max);
        }
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