using GTA;
using GTA.Math;
using GTA.Native;
﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

public class SpawnMP : Script
{
    ScriptSettings config;
    int vehicles_spawned;
    private int doors_config = 0;
    private int blip_config = 0;
    private int[] mode_type = new int[5];
    private float[] angle = new float[1];
    private GTA.Vehicle car;
    private int blip_color;
    private int mod_plate;
    private int plate_id = -1;
    private Vehicle[] veh = new Vehicle[200];
    private Vehicle[] street_veh = new Vehicle[200];
    private List<Blip> marker = new List<Blip>();
    private int debugging = 0;
    private int _canSpawn = 1;
   
    private int nextSpawnCheck = 0;

    private string mod_version = "1.72";
    private static Random random = new Random();
    // Change these definitions at the top of your class
    Dictionary<SpawnSpot, Vehicle> vehDict = new Dictionary<SpawnSpot, Vehicle>();
    Dictionary<SpawnSpot, Blip> markerDict = new Dictionary<SpawnSpot, Blip>();
    // Tracks a "Bag" of upcoming cars for each spot
    Dictionary<List<string>, Queue<string>> spawnQueues = new Dictionary<List<string>, Queue<string>>();

    private List<SpawnSpot> AllSpawns = new List<SpawnSpot>();
    Dictionary<List<string>, string> lastSpawnedDict = new Dictionary<List<string>, string>();







    public SpawnMP()
    {
        string onlineVersion = Function.Call<string>(Hash.GET_ONLINE_VERSION);
        if (onlineVersion != mod_version)
        {
            GTA.UI.Notification.PostTicker($"~r~WARNING:\n~s~Your version of the game is out of date. ~g~All MP Vehicles in SP ~s~will not be able to load vehicles from new updates.\n\nRequired Game Version:\n{mod_version}\nYour Game Version: {onlineVersion}", true);
        }

        // 2. Load Config (Only keep what you need)
        config = ScriptSettings.Load("Scripts\\UniqueCarSpawns.ini");
        _canSpawn = config.GetValue<int>("MAIN", "parking_lots_spawn", 1);
        doors_config = config.GetValue<int>("MAIN", "doors", 1);
        blip_config = config.GetValue<int>("MAIN", "blips", 1);
        mod_plate = config.GetValue<int>("MAIN", "new_license_plates", 1); // Set default to 1 if you want plates
        blip_color = config.GetValue<int>("MAIN", "blip_color", 3);
        debugging = config.GetValue<int>("MAIN", "show_errors", 0);

     

        config.Save();


        AllSpawns = new List<SpawnSpot>()
    {
        // Format: ID, Position, Heading, Vehicle List, Behavior Type
        
     // --- Arena War Group ---
    new SpawnSpot("ArenaHotring1", new Vector3(-206.046f, -1988.758f, 26.96269f), 90.246f, VehList.models_arena_hotring, SpawnBehavior.Standard),
    new SpawnSpot("ArenaHotring2", new Vector3(1189.208f, 304.6935f, 81.48812f), 146.908f, VehList.models_arena_hotring, SpawnBehavior.Standard),

    new SpawnSpot("ArenaSpeed1", new Vector3(-176.5869f, -2019.529f, 27.14398f), 75.334f, VehList.models_arena_speed, SpawnBehavior.Standard),
    new SpawnSpot("ArenaSpeed2", new Vector3(1117.519f, 257.8285f, 80.31487f), -122.074f, VehList.models_arena_speed, SpawnBehavior.Standard),

    new SpawnSpot("ArenaOffroad1", new Vector3(-192.949f, -1928.497f, 27.20675f), -151.296f, VehList.models_arena_offroad, SpawnBehavior.Standard),
    new SpawnSpot("ArenaOffroad2", new Vector3(1151.233f, 183.6329f, 80.23096f), -53.715f, VehList.models_arena_offroad, SpawnBehavior.Standard),




    new SpawnSpot("Boats", new Vector3(-926.119f, -1478.350f, -0.474f), 12.163f, VehList.models_boats, SpawnBehavior.Stock),
    new SpawnSpot("Tuggy", new Vector3(-3092.066f, 3465.729f, -0.474f), 47.552f, VehList.models_weaponboats, SpawnBehavior.Stock),

   // --- Special Locations ---
    new SpawnSpot("Cemetery ", new Vector3(-1640.42f, -202.879f, 54.146f), 338.279f, VehList.models_cemetery, SpawnBehavior.NoVisuals),

    new SpawnSpot("Cinema", new Vector3(-1084.873f, -477.591f, 36.2069f), 27.922f, VehList.models_cinema, SpawnBehavior.NoVisuals),
    new SpawnSpot("Cult", new Vector3(-719.9119f, 79.29325f, 55.13408f), 25.098f, VehList.models_cult, SpawnBehavior.Cult),
    new SpawnSpot("Openwheel", new Vector3(1135.19f, 39.81987f, 80.34249f), 58.875f, VehList.models_openwheel, SpawnBehavior.Standard),
    new SpawnSpot("Sandy", new Vector3(1546.591f, 3781.791f, 33.06f), 26.557f, VehList.models_cheburek, SpawnBehavior.NoVisuals),
       new SpawnSpot("Marriage", new Vector3(-762.865f, -38.192f, 37.687f), 115.427f, VehList.models_valentine, SpawnBehavior.NoVisuals),
           new SpawnSpot("Beach_Karts", new Vector3(-1530.63f, -993.47f, 12.017f), 254.258f, VehList.models_karting, SpawnBehavior.Standard),

    // --- Aircraft ---
    // Note: Higgins usually implies specific livery/colors, ensure you have logic for SpawnBehavior.Higgins or switch to Helicopter
    new SpawnSpot("Higgins_Heli", new Vector3(-746.4702f, -1469.937f, 6.87726f), 140.365f, VehList.models_higgins, SpawnBehavior.Higgins),
    new SpawnSpot("Helicopter", new Vector3(-979.378f, -2996.868f, 13.945f), 331.180f, VehList.models_helicopter, SpawnBehavior.Helicopter),
    new SpawnSpot("Planes", new Vector3(-961.005f, -2963.593f, 13.945f), 147.589f, VehList.models_planes, SpawnBehavior.Standard),

    // --- Lowriders ---
    // Mapped to models_ghetto based on your VehList content (Faction, Chino, etc.)
    new SpawnSpot("Lowrider_1", new Vector3(-229.587f, -1483.44f, 30.352f), 146.244f, VehList.models_ghetto, SpawnBehavior.Standard),
    new SpawnSpot("Lowrider_2", new Vector3(-22.296f, -1851.58f, 24.108f), 141.262f, VehList.models_ghetto, SpawnBehavior.Standard),
    new SpawnSpot("Lowrider_3", new Vector3(321.798f, -1948.14f, 23.627f), 47.597f, VehList.models_ghetto, SpawnBehavior.Standard),
    new SpawnSpot("Lowrider_4", new Vector3(455.602f, -1695.26f, 28.289f), 138.808f, VehList.models_ghetto, SpawnBehavior.Standard),
    new SpawnSpot("Lowrider_5", new Vector3(1228.548f, -1605.65f, 50.736f), 33.185f, VehList.models_ghetto, SpawnBehavior.Standard),
    new SpawnSpot("Lowrider_6", new Vector3(298.2452f, -1241.624f, 28.75226f), -179.719f, VehList.models_ghetto, SpawnBehavior.Standard),
    new SpawnSpot("Lowrider_7", new Vector3(264.0245f, -1512.3302f, 28.7877f), 268.336f, VehList.models_ghetto, SpawnBehavior.Standard),

    // --- Retro Sports (Classics) ---
    // Mapped to models_classics (Stinger, Monroe, etc.) using NoVisuals (Performance only)
    new SpawnSpot("RetroSports1", new Vector3(-1114.1f, 479.205f, 81.161f), 169.13f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports2", new Vector3(-1405.12f, 81.983f, 52.099f), 58.178f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports3", new Vector3(-1334.63f, -1008.97f, 6.867f), 126.968f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports4", new Vector3(-1886.25f, 2016.572f, 139.951f), 160.257f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports5", new Vector3(-817.325f, -1201.59f, 5.935f), 318.133f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports6", new Vector3(-1407.751f, -589.1447f, 29.65687f), 298.673f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports7", new Vector3(-552.673f, 309.154f, 82.191f), 260.340f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports8", new Vector3(339.481f, 159.143f, 102.146f), 71.345f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports9", new Vector3(-3036.57f, 105.31f, 10.593f), 141.262f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports10", new Vector3(-205.516f, 281.035f, 91.818f), 165.351f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports11", new Vector3(-972.578f, -1464.27f, 4.013f), 294.730f, VehList.models_classics, SpawnBehavior.NoVisuals),
    new SpawnSpot("RetroSports12", new Vector3(1309.942f, -530.154f, 70.312f), 341.133f, VehList.models_classics, SpawnBehavior.NoVisuals),

    // --- Super Cars ---
    // Using NoVisuals to keep them clean/stock looking but with performance upgrades
    new SpawnSpot("Super1", new Vector3(-1873.6f, -343.933f, 48.26f), 225.300f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super2", new Vector3(-1297.2f, 252.495f, 61.813f), 3.035f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super3", new Vector3(-345.267f, 662.299f, 168.587f), 171.211f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super4", new Vector3(-72.605f, 902.579f, 234.631f), 291.351f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super5", new Vector3(-1451.92f, 533.495f, 118.177f), 73.674f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super6", new Vector3(443.542f, 253.197f, 102.21f), 245.845f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super7", new Vector3(-397.528f, 210.366f, 82.789f), 91.136f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super8", new Vector3(-220.102f, -590.273f, 33.264f), 341.667f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super9", new Vector3(-1535.044f, 890.5871f, 181.3348f), 19.505f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super10", new Vector3(-718.511f, -74.684f, 36.916f), 62.242f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super11", new Vector3(-1126.722f, -318.281f, 37.21f), -95.129f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super12", new Vector3(-801.566f, -1313.92f, 4.0f), 169.408f, VehList.models_supers, SpawnBehavior.NoVisuals),
    new SpawnSpot("Super13", new Vector3(-504.323f, 424.21f, 96.287f), 313.167f, VehList.models_supers, SpawnBehavior.NoVisuals),
    };

        // ... rest of init ...




        Tick += OnTick;
        Aborted += OnAborded;
    }




    string GenerateVehicleModelName(SpawnSpot spot, int type)
    {
        // The list is already attached to the spot!
        if (spot.ModelList == null || !spot.ModelList.Any()) return null;

        // Check if we already have a car here
        if (vehDict.ContainsKey(spot) && vehDict[spot] != null && type == 0) return null;

        // Use your existing GetUniqueModel helper
        return GetUniqueModel(spot.ModelList, spot);
    }

    void SetNumberPlate(Vehicle car, int mode, int index)
    {
        if (mode == 1 && plate_id != -1)
        {
            Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, car, index);
        }
    }


    Vehicle CreateNewVehicle(string hash, Vector3 pos, float heading, SpawnSpot spot)
    {
        var veh_model = new Model(hash);
        veh_model.Request(500);
        if (!veh_model.IsValid) return null;

        while (!veh_model.IsLoaded) Script.Wait(100);
        Vehicle car = World.CreateVehicle(veh_model, pos, heading);
        veh_model.MarkAsNoLongerNeeded();

        if (doors_config == 1)
            Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, car, 7);

        // Global Color Logic
        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, car);
        if (comboCount > 0)
        {
            int randomCombo = random.Next(0, comboCount);
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, car, randomCombo);
        }
        return car;
    }

    Blip CreateMarkerAboveCar(Vehicle car, SpawnSpot spot)
    {
        Blip mark = Function.Call<Blip>(GTA.Native.Hash.ADD_BLIP_FOR_ENTITY, car);
        Function.Call(GTA.Native.Hash.SET_BLIP_SPRITE, mark, 1);
        Function.Call(GTA.Native.Hash.SET_BLIP_COLOUR, mark, blip_color);
        Function.Call(GTA.Native.Hash.FLASH_MINIMAP_DISPLAY);
        mark.Name = "Unique vehicle";
        markerDict[spot] = mark;
        return mark;
    }

    void OnAborded(object sender, EventArgs e)
    {
        // 1. Clean up Blips
        foreach (var blip in markerDict.Values)
        {
            if (blip != null && blip.Exists())
            {
                blip.Delete();
            }
        }

        // 2. Clean up Vehicles
        foreach (var vehicle in vehDict.Values)
        {
            if (vehicle != null && vehicle.Exists())
            {
                vehicle.Delete();
            }
        }

        // 3. Clear collections (Safe to do after loops are done)
        markerDict.Clear();
        vehDict.Clear();
    }



    void OnTick(object sender, EventArgs e)
    {
        if (_canSpawn == 0) return;

        var playerPos = Game.Player.Character.Position;
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);

        // --- 1. FORCE CLEANUP (Mission/Cutscene) ---
        // Run this every frame to ensure instant cleanup
        if (vehicles_spawned == 1 && isMissionActive)
        {
            foreach (var spot in vehDict.Keys.ToList())
            {
                var car = vehDict[spot];
                if (car != null && car.Exists()) car.Delete();

                if (markerDict.ContainsKey(spot) && markerDict[spot] != null && markerDict[spot].Exists())
                {
                    markerDict[spot].Delete();
                    markerDict[spot] = null;
                }
                vehDict[spot] = null;
            }
            vehicles_spawned = 0;
        }

        if (!isMissionActive)
        {
            // --- 2. OPTIMIZED SPAWN LOOP (Runs twice per second) ---
            if (Game.GameTime > nextSpawnCheck)
            {
                foreach (var spot in AllSpawns)
                {
                    // Use C# Vector3.Distance (Much faster than Function.Call)
                    if (Vector3.Distance(spot.Position, playerPos) < 300f)
                    {
                        string model_name = GenerateVehicleModelName(spot, 0);

                        if (model_name != null)
                        {
                            vehDict[spot] = CreateNewVehicle(model_name, spot.Position, spot.Heading, spot);
                            var vehicle = vehDict[spot];

                            if (vehicle != null)
                            {
                                SetNumberPlate(vehicle, mod_plate, plate_id);
                                plate_id = -1;

                                if (blip_config == 1) CreateMarkerAboveCar(vehicle, spot);

                                ApplyVehicleMods(vehicle, spot, model_name);
                                vehicles_spawned = 1;
                            }
                        }
                    }
                }
                // Reset the timer to wait 500ms before checking spawns again
                nextSpawnCheck = Game.GameTime + 500;
            }

            // --- 3. CLEANUP LOOP (Can also run on the timer, or keep here for responsiveness) ---
            // We'll keep this separate so cleanup feels instant when driving away
            foreach (var spot in AllSpawns)
            {
                // Simple optimization: Don't check distance if we know we don't have a car there
                if (!vehDict.ContainsKey(spot) || vehDict[spot] == null) continue;

                if (Vector3.Distance(spot.Position, playerPos) > 300f)
                {
                    if (vehDict.TryGetValue(spot, out var vehicle))
                    {
                        if (markerDict.ContainsKey(spot) && markerDict[spot] != null && markerDict[spot].Exists())
                        {
                            markerDict[spot].Delete();
                            markerDict[spot] = null;
                        }

                        if (vehicle != null && vehicle.Exists())
                        {
                            if (!Function.Call<bool>(Hash.IS_PED_SITTING_IN_VEHICLE, Game.Player.Character, vehicle))
                            {
                                vehicle.Delete();
                            }
                            else
                            {
                                vehicle.MarkAsNoLongerNeeded();
                            }
                        }
                        vehDict[spot] = null;
                    }
                }
            }
        }

        // --- 4. PLAYER INTERACTION ---
        // Safe to run every frame
        foreach (var spot in vehDict.Keys.ToList())
        {
            Vehicle car = vehDict[spot];
            if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
            {
                if (markerDict.ContainsKey(spot) && markerDict[spot] != null && markerDict[spot].Exists())
                {
                    markerDict[spot].Delete();
                    markerDict[spot] = null;
                }
                car.MarkAsNoLongerNeeded();
                // Remove from tracking so we don't delete it while driving
                vehDict[spot] = null;
            }
        }
    }
    private void ApplyVehicleMods(Vehicle v, SpawnSpot spot, string modelName)
    {
        v.Mods.InstallModKit();
        // ... (Your mandatory performance mods here: Engine, Brakes, etc.) ...
        // -----------------------------------------------------------
        // 1. GLOBAL INITIALIZATION
        // -----------------------------------------------------------
        v.Mods.InstallModKit(); // Required for everything below

        // -----------------------------------------------------------
        // 2. TWEAK PERFORMANCE (Replicated from your snippet)
        // -----------------------------------------------------------
        if(spot.Behavior != SpawnBehavior.Stock)
        {
            v.Mods[VehicleModType.Engine].Index = 3;       // Lvl 4
            v.Mods[VehicleModType.Brakes].Index = 2;       // Race Brakes
            v.Mods[VehicleModType.Transmission].Index = 2; // Race Trans
            v.Mods[VehicleModType.Suspension].Index = 3;   // Competition
            v.Mods[VehicleModType.Armor].Index = 4;        // 100% Armor
            v.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
        }


        if (v.ClassType == VehicleClass.Super || v.ClassType == VehicleClass.Sports) //apply xenon lights to Super only
        {
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;

        }

        // Behavior Switch
        switch (spot.Behavior)
        {
            case SpawnBehavior.Standard:
                ApplyRandomVisuals(v);
                RandomizeLivery(v);
                break;

            case SpawnBehavior.NoVisuals:
            case SpawnBehavior.Stock:
                // We do NOTHING. 
                // By doing nothing, the car keeps the factory parts it spawned with.
                break;

            case SpawnBehavior.Cult:
                v.Mods.PrimaryColor = (VehicleColor)157; // Epsilon Blue
                v.Mods.SecondaryColor = (VehicleColor)157;
                v.Mods.PearlescentColor = (VehicleColor)1;
                v.Mods[VehicleModType.Livery].Index = -1;

     
                break;

            case SpawnBehavior.Helicopter:
                Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
                RandomizeLivery(v);
                break;

            case SpawnBehavior.Higgins:
               if(modelName == "conada")
                {
                    //  for Higgins behavior if you want specific colors
                    v.Mods.PrimaryColor = (VehicleColor)89;
                    v.Mods.SecondaryColor = (VehicleColor)6;

                    v.Mods[VehicleModType.Livery].Index = 9; // Higgins livery
                }
                v.Mods.PearlescentColor = VehicleColor.MetallicMidnightSilver;
                Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
                break;
        }
    }
    string GetUniqueModel(List<string> list, SpawnSpot spot)
    {
        if (list == null || list.Count == 0) return null;

        // Use the LIST as the key. This enables "Shared Decks."
        if (!spawnQueues.ContainsKey(list) || spawnQueues[list].Count == 0)
        {
            // 1. Create a fresh copy
            List<string> freshBatch = new List<string>(list);

            // 2. Shuffle it
            freshBatch.Shuffle();

            // 3. Bridge Protection (Prevents the new batch starting with the old batch's last car)
            if (lastSpawnedDict.ContainsKey(list) && freshBatch.Count > 1)
            {
                if (freshBatch[0] == lastSpawnedDict[list])
                {
                    // Swap first and last to break the pattern
                    string temp = freshBatch[0];
                    freshBatch[0] = freshBatch[freshBatch.Count - 1];
                    freshBatch[freshBatch.Count - 1] = temp;
                }
            }

            spawnQueues[list] = new Queue<string>(freshBatch);
        }

        // Pull from the shared bag
        string selection = spawnQueues[list].Dequeue();

        // Record history for the group
        lastSpawnedDict[list] = selection;

        return selection;
    }

    private void RandomizeLivery(Vehicle v)
    {
        int liveryCount = v.Mods.LiveryCount;
        if (liveryCount > 0)
        {
            v.Mods.Livery = random.Next(0, liveryCount);
        }
    }

    private void ApplyRandomVisuals(Vehicle v)
    {
        // List of performance types we want to SKIP here (because we already maxed them)
        var performanceTypes = new List<VehicleModType>
    {
        VehicleModType.Engine,
        VehicleModType.Brakes,
        VehicleModType.Transmission,
        VehicleModType.Suspension,
        VehicleModType.Armor
    };

        // Iterate through all possible mod slots
        foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
        {
            // Skip performance mods (don't randomize them, keep them maxed)
            if (performanceTypes.Contains(modType)) continue;

            // Skip Livery (handled separately) and Horns
            if (modType == VehicleModType.Livery || modType == VehicleModType.Horns) continue;

            int count = v.Mods[modType].Count;

            // If the car has mods for this slot
            if (count > 0)
            {
                // 50% chance to modify this part
             
                    // Pick a random part (0 to count-1)
                    int rndIndex = random.Next(0, count);
                    v.Mods[modType].Index = rndIndex;
                
            }
        }
    }

}

public class SpawnSpot
{
    // A unique name for logs/debugging (replaces the Enum)
    public string Id { get; set; }

    // Position Data
    public Vector3 Position { get; set; }
    public float Heading { get; set; }

    // Logic Data: Pass the ACTUAL list from VehList here
    public List<string> ModelList { get; set; }

    // Behavior Group (replaces the complex switch cases in ApplyVehicleMods)
    public SpawnBehavior Behavior { get; set; }

    public SpawnSpot(string id, Vector3 pos, float head, List<string> list, SpawnBehavior behavior)
    {
        Id = id;
        Position = pos;
        Heading = head;
        ModelList = list;
        Behavior = behavior;
    }
}

// Define behaviors here so you don't need a unique case for every single car
public enum SpawnBehavior
{
    Standard,   // Visuals + Livery
    Cult,       // Epsilon Blue
    Higgins,    // Specific colors
    NoVisuals,  // Super/Retro cars (Performance only)
    Helicopter, // Special collision logic
    Stock,       //Stock tuning 
    Brickade    // Specific override
}

public static class ListExtensions
{
    private static Random rng = new Random();

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}