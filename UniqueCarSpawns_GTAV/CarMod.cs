using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;

public static class CarMod
{
    private static Random random = new Random();

    /// Applies Max Performance upgrades (Engine, Brakes, Turbo, etc).
    public static void ApplyPerformance(Vehicle v)
    {
        v.CanTiresBurst = false;
        v.Mods[VehicleModType.Engine].Index = 3;
        v.Mods[VehicleModType.Brakes].Index = 2;
        v.Mods[VehicleModType.Transmission].Index = 2;
        v.Mods[VehicleModType.Suspension].Index = 3; // Global default: Lowered
        v.Mods[VehicleModType.Armor].Index = 4;
        v.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
    }

    /// MUST be called AFTER ApplyPerformance, as it may override Turbo/Suspension settings.
    public static void ApplySpecs(Vehicle v, string modelName)
    {
        // Global Default for Hero Cars: Xenon ON (Specific cases can disable it below)
        v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;

        switch (modelName.ToLower())
        {
            // --- SPEED RACER (Mach 5) ---
            case "scramjet":
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods.WheelType = VehicleWheelType.Track;
                v.Mods[VehicleModType.FrontWheel].Index = 17;
                v.Mods[VehicleModType.Suspension].Index = -1; // Stock Height

                if (random.Next(0, 2) == 0)
                {
                    // VARIANT A: Classic White
                    SetColors(v, 111, 111, 111, 12);
                    v.Mods[VehicleModType.Livery].Index = 4;
                }
                else
                {
                    // VARIANT B: Red Racer
                    SetColors(v, 43, 111, 135, 12);
                    v.Mods[VehicleModType.Livery].Index = 2;
                }
                break;

            // --- BATMOBILE ---
            case "vigilante":
                v.Mods[VehicleModType.Roof].Index = 0;
                SetColors(v, 12, 12, 0, 12);
                break;

            // --- JAMES BOND ---
            case "jb7002":
                v.Mods[VehicleModType.Roof].Index = 0;
                SetColors(v, 5, 5, 5, 5);
                break;

            // --- DELOREAN ---
            case "deluxo":
                v.Mods[VehicleModType.Roof].Index = 0;
                SetColors(v, 17, 18, 5, 0);
                break;

            case "stromberg":
                SetColors(v, 111, 111, 111, 0);
                break;

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
                break;

            // --- OPPRESSOR MKII ---
            //
            case "oppressor2":
                SetColors(v, 0, 118, 10, 112); // Black, BlackSteel, GunMetal, White Rim
                v.Mods[VehicleModType.Roof].Index = 1;      // MKII rockets

                // DISABLES (Per XML)
                v.Mods[VehicleToggleModType.Turbo].IsInstalled = false;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;
                v.Mods[VehicleModType.Suspension].Index = -1;
                break;

            // --- INSURGENT REGULAR ---
            //
            case "insurgent2":
                SetColors(v, 154, 153, 0, 0); // Tan, TanVariant, Black
                SetWheels(v, VehicleWheelType.SUV, -1);
                v.Mods[VehicleToggleModType.Turbo].IsInstalled = false;
                break;

            // --- INSURGENT PICKUP CUSTOM ---
            //
            case "insurgent3":
                SetColors(v, 154, 154, 3, 0); // Tan, Grey Pearl
                SetWheels(v, VehicleWheelType.Offroad, 22);

                v.Mods[VehicleModType.RightFender].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleModType.Livery].Index = 12;

                EnableExtras(v, 1);
                v.Mods[VehicleModType.Suspension].Index = -1;
                break;

            // --- THRUSTER ---
            //
            case "thruster":
                SetColors(v, 2, 12, 2, 158); // Black, MatteBlack, Gold Rim
                v.Mods[VehicleModType.Exhaust].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 1;
                EnableExtras(v, 1, 2, 15, 16);
                v.Mods[VehicleModType.Suspension].Index = -1;
                break;

            // --- NIGHTSHARK ---
            //
            case "nightshark":
                SetColors(v, 154, 154, 0, 0);
                SetWheels(v, VehicleWheelType.SUV, -1);
                v.Mods[VehicleModType.Exhaust].Index = 1;
                v.Mods[VehicleModType.Grille].Index = 2;
                v.Mods[VehicleModType.Hood].Index = 7;
                v.Mods[VehicleModType.Fender].Index = 0;
                v.Mods[VehicleModType.Livery].Index = 12;
                break;

            // --- PYRO ---
            //
            case "pyro":
                v.Mods[VehicleModType.Roof].Index = 0;
                if (random.Next(0, 2) == 0)
                    SetColors(v, 5, 4, 112, 0);
                else
                {
                    SetColors(v, 60, 117, 0, 111);
                    v.Mods[VehicleModType.Livery].Index = 7;
                }
                break;

            // --- MOGUL ---
            //
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
                break;

            // --- MOLOTOK ---
            case "molotok":
                SetColors(v, 111, 111, 3, 154);
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleModType.SideSkirt].Index = 0;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;

                if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
                else v.Mods[VehicleModType.Livery].Index = 1;
                break;

            // --- STARLING ---
            //
            case "starling":
                v.Mods[VehicleModType.Exhaust].Index = 0;
                v.Mods[VehicleModType.RightFender].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;

                if (random.Next(0, 2) == 0)
                {
                    // VARIANT A: Military Olive
                    SetColors(v, 152, 25, 3, 154);
                    v.Mods[VehicleModType.Livery].Index = 4;
                }
                else
                {
                    // VARIANT B: Off-White
                    SetColors(v, 121, 25, 7, 111);
                    v.Mods[VehicleModType.Livery].Index = 0;
                }
                break;

            // --- NOKOTA ---
            //
            case "nokota":
                SetColors(v, 4, 43, 111, 119);
                v.Mods[VehicleModType.SideSkirt].Index = 0;
                v.Mods[VehicleModType.Roof].Index = 0;
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = false;

                if (random.Next(0, 2) == 0) v.Mods[VehicleModType.Livery].Index = -1;
                else v.Mods[VehicleModType.Livery].Index = 3;
                break;
        }
    }

    /// <summary>
    /// Applies global fixes for specific models (Spoilers, etc)
    /// </summary>
    public static void ApplyModelFixes(Vehicle v, string modelName)
    {
        // Fix for Turismo2 and Banshee3 spawning without spoilers
        if (modelName == "turismo2" || modelName == "banshee3")
        {
            v.Mods[VehicleModType.Spoilers].Index = 3;
        }
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
        if (v.ClassType == VehicleClass.Motorcycles)
        {
            v.Mods[VehicleModType.RearWheel].Index = index;
        }
    }

    private static void EnableExtras(Vehicle v, params int[] extraIds)
    {
        foreach (int id in extraIds)
        {
            if (v.ExtraExists(id)) v.ToggleExtra(id, true);
        }
    }
}