using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;

public class SpawnMP : Script
{
    // ==========================================
    //              QUICK SETTINGS
    // ==========================================
    private bool ShowBlips = true;      // Set 'false' to hide map markers
    private bool LockDoors = true;      // Set 'false' to leave cars unlocked
    private float SpawnDistMax = 700f;  // Spawn/Cleanup range
    private float SpawnDistMin = 200f;  // Minimum distance (prevents pop-in)
    // ==========================================

    private int nextSpawnCheck = 0;
    private string mod_version = "1.72";
    private static Random random = new Random();

    // Core Tracking
    private Dictionary<SpawnSpot, Vehicle> vehDict = new Dictionary<SpawnSpot, Vehicle>();
    private Dictionary<SpawnSpot, Blip> markerDict = new Dictionary<SpawnSpot, Blip>();

    // Shuffle Logic
    private Dictionary<List<string>, Queue<string>> spawnQueues = new Dictionary<List<string>, Queue<string>>();
    private Dictionary<List<string>, string> lastSpawnedDict = new Dictionary<List<string>, string>();

    private List<SpawnSpot> AllSpawns = new List<SpawnSpot>();

    public SpawnMP()
    {
        // 1. Version Check (Kept purely for safety, can be removed if annoying)
        string onlineVersion = Function.Call<string>(Hash.GET_ONLINE_VERSION);
        if (onlineVersion != mod_version)
        {
            // You can comment this out if you don't want the warning
            GTA.UI.Notification.PostTicker($"~r~WARNING: Game Version Mismatch.\nRequired: {mod_version}", true);
        }

        // 2. Initialize Spawn Locations
        AllSpawns = new List<SpawnSpot>()
        {
            // Racing
            new SpawnSpot("ArenaHotring1", new Vector3(-206.046f, -1988.758f, 26.96269f), 90.246f, VehList.models_arena_hotring, SpawnBehavior.Standard),
            new SpawnSpot("ArenaHotring2", new Vector3(1189.208f, 304.6935f, 81.48812f), 146.908f, VehList.models_arena_hotring, SpawnBehavior.Standard),
            new SpawnSpot("ArenaSpeed1", new Vector3(-176.5869f, -2019.529f, 27.14398f), 75.334f, VehList.models_arena_speed, SpawnBehavior.Standard),
            new SpawnSpot("ArenaSpeed2", new Vector3(1117.519f, 257.8285f, 80.31487f), -122.074f, VehList.models_arena_speed, SpawnBehavior.Standard),
            new SpawnSpot("ArenaOffroad1", new Vector3(-192.949f, -1928.497f, 27.20675f), -151.296f, VehList.models_arena_offroad, SpawnBehavior.Standard),
            new SpawnSpot("ArenaOffroad2", new Vector3(1151.233f, 183.6329f, 80.23096f), -53.715f, VehList.models_arena_offroad, SpawnBehavior.Standard),
            new SpawnSpot("Openwheel", new Vector3(1135.19f, 39.81987f, 80.34249f), 58.875f, VehList.models_openwheel, SpawnBehavior.Standard),

            // Special
            new SpawnSpot("Tuggy", new Vector3(-3092.066f, 3465.729f, -0.474f), 47.552f, VehList.models_weaponboats, SpawnBehavior.Stock),
            new SpawnSpot("Cemetery", new Vector3(-1640.42f, -202.879f, 54.146f), 338.279f, VehList.models_cemetery, SpawnBehavior.NoVisuals),
            new SpawnSpot("Cinema", new Vector3(-1084.873f, -477.591f, 36.2069f), 27.922f, VehList.models_cinema, SpawnBehavior.Studio),
            new SpawnSpot("Cult", new Vector3(-719.9119f, 79.29325f, 55.13408f), 25.098f, VehList.models_cult, SpawnBehavior.Cult),
            new SpawnSpot("Sandy", new Vector3(1546.591f, 3781.791f, 33.06f), 26.557f, VehList.models_cheburek, SpawnBehavior.NoVisuals),
            new SpawnSpot("Marriage", new Vector3(-762.865f, -38.192f, 37.687f), 115.427f, VehList.models_valentine, SpawnBehavior.NoVisuals),
            new SpawnSpot("Beach_Karts1", new Vector3(-1530.63f, -993.47f, 12.017f), 254.258f, VehList.models_karting, SpawnBehavior.Standard),
            new SpawnSpot("Beach_Karts2", new Vector3(-1235.388f, -1647.45f, 3.512795f), 124.5176f, VehList.models_karting, SpawnBehavior.Standard),

            // Aircraft
            new SpawnSpot("Higgins_Heli", new Vector3(-746.4702f, -1469.937f, 6.87726f), 140.365f, VehList.models_higgins, SpawnBehavior.Higgins),
            new SpawnSpot("Helicopter", new Vector3(-979.378f, -2996.868f, 13.945f), 331.180f, VehList.models_helicopter, SpawnBehavior.Helicopter),
            new SpawnSpot("Planes", new Vector3(-961.005f, -2963.593f, 13.945f), 147.589f, VehList.models_planes, SpawnBehavior.Standard),

            // Lowriders
            new SpawnSpot("Lowrider_1", new Vector3(-229.587f, -1483.44f, 30.352f), 146.244f, VehList.models_lowriders, SpawnBehavior.Standard),
            new SpawnSpot("Lowrider_2", new Vector3(-22.296f, -1851.58f, 24.108f), 141.262f, VehList.models_lowriders, SpawnBehavior.Standard),
            new SpawnSpot("Lowrider_3", new Vector3(321.798f, -1948.14f, 23.627f), 47.597f, VehList.models_lowriders, SpawnBehavior.Standard),
            new SpawnSpot("Lowrider_4", new Vector3(455.602f, -1695.26f, 28.289f), 138.808f, VehList.models_lowriders, SpawnBehavior.Standard),
            new SpawnSpot("Lowrider_5", new Vector3(1228.548f, -1605.65f, 50.736f), 33.185f, VehList.models_lowriders, SpawnBehavior.Standard),
            new SpawnSpot("Lowrider_6", new Vector3(298.2452f, -1241.624f, 28.75226f), -179.719f, VehList.models_lowriders, SpawnBehavior.Standard),
            new SpawnSpot("Lowrider_7", new Vector3(264.0245f, -1512.3302f, 28.7877f), 268.336f, VehList.models_lowriders, SpawnBehavior.Standard),

            // Classics
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
            new SpawnSpot("RetroSports12", new Vector3(-489.2397f, -596.5908f, 30.56949f), 358.1453f, VehList.models_classics, SpawnBehavior.NoVisuals),

            // Supers
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
            new SpawnSpot("Super14", new Vector3(-1979.25f, 586.078f, 116.479f), 185.087f, VehList.models_supers, SpawnBehavior.NoVisuals),
            new SpawnSpot("Super15", new Vector3(-2340.907f, 295.8933f, 169.1187f), 294.0081f, VehList.models_supers, SpawnBehavior.NoVisuals),

            // Armoured
            new SpawnSpot("Armoured1", new Vector3(110.261f, -714.605f, 32.133f), 341.667f, VehList.models_armoured, SpawnBehavior.Armoured),
            new SpawnSpot("Armoured2", new Vector3(-340.161f, -876.799f, 30.90968f), 347.7794f, VehList.models_armoured, SpawnBehavior.Armoured),
            new SpawnSpot("Armoured3", new Vector3(-329.9433f, -700.7843f, 32.33982f), 88.68892f, VehList.models_armoured, SpawnBehavior.Armoured),

            // Old School
            new SpawnSpot("OldSchool_1", new Vector3(124.0182f, -1472.58f, 28.6794f), 321.0109f, VehList.models_old_school, SpawnBehavior.Stock),
            new SpawnSpot("OldSchool_2", new Vector3(393.4623f, -649.7198f, 27.92926f), 90.89349f, VehList.models_old_school, SpawnBehavior.Stock),
            new SpawnSpot("OldSchool_3", new Vector3(31.46499f, -1706.062f, 28.6591f), 23.36283f, VehList.models_old_school, SpawnBehavior.Stock),
            new SpawnSpot("OldSchool_4", new Vector3(1136.156f, -773.997f, 56.632f), 269.604f, VehList.models_old_school, SpawnBehavior.Stock),
            new SpawnSpot("OldSchool_5", new Vector3(1309.942f, -530.154f, 70.312f), 341.133f, VehList.models_old_school, SpawnBehavior.Stock),
            new SpawnSpot("OldSchool_6", new Vector3(-1528.733f, -427.0032f, 35.01511f), 48.3741f, VehList.models_old_school, SpawnBehavior.Stock),
      
          // Military
            new SpawnSpot("M_Planes_1", new Vector3(-1892.247f, 3082.933f, 32.810f), 147.141f, VehList.models_military_planes, SpawnBehavior.NoVisuals),
            new SpawnSpot("M_Planes_2", new Vector3(-1934.867f, 3109.608f, 32.810f), 150.073f, VehList.models_military_planes, SpawnBehavior.NoVisuals),
            new SpawnSpot("M_Helis", new Vector3(-1965.212f, 3101.532f, 32.810f), 236.324f, VehList.models_military_helicopters, SpawnBehavior.NoVisuals),

            //Desert
              new SpawnSpot("Wacky1", new Vector3(140.945f, 6606.513f, 30.845f), 0.239f, VehList.models_wacky, SpawnBehavior.Standard),
              new SpawnSpot("Wacky2", new Vector3(1205.454f, 2658.357f, 36.824f), 223.627f, VehList.models_wacky, SpawnBehavior.Standard),
        };

        Tick += OnTick;
        Aborted += OnAborted;
    }

    private void OnTick(object sender, EventArgs e)
    {
        var playerPos = Game.Player.Character.Position;
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);

        // --- 1. MISSION CLEANUP ---
        if (isMissionActive)
        {
            CleanupAll();
            return;
        }

        // --- 2. SPAWN LOGIC ---
        if (Game.GameTime > nextSpawnCheck)
        {
            foreach (var spot in AllSpawns)
            {
                float distance = Vector3.Distance(spot.Position, playerPos);

                if (distance < SpawnDistMax && distance > SpawnDistMin && !vehDict.ContainsKey(spot))
                {
                    string modelName = GetUniqueModel(spot.ModelList, spot);
                    if (modelName != null)
                    {
                        var vehicle = CreateNewVehicle(modelName, spot.Position, spot.Heading);
                        if (vehicle != null)
                        {
                            vehDict[spot] = vehicle;
                            if (ShowBlips) CreateMarkerAboveCar(vehicle, spot);
                            ApplyVehicleMods(vehicle, spot, modelName);
                        }
                    }
                }
            }
            nextSpawnCheck = Game.GameTime + 500;
        }

        // --- 3. PROXIMITY CLEANUP ---
        foreach (var spot in vehDict.Keys.ToList())
        {
            var car = vehDict[spot];
            if (car == null || !car.Exists())
            {
                vehDict.Remove(spot);
                continue;
            }

            if (Vector3.Distance(spot.Position, playerPos) > SpawnDistMax)
            {
                // If player is sitting in it, we do not delete
                if (!Function.Call<bool>(Hash.IS_PED_SITTING_IN_VEHICLE, Game.Player.Character, car))
                {
                    DeleteSpotResources(spot);
                }
                else
                {
                    car.MarkAsNoLongerNeeded();
                    DeleteBlipForSpot(spot);
                    vehDict.Remove(spot);
                }
            }
        }

        // --- 4. PLAYER INTERACTION (Remove blip on entry) ---
        foreach (var spot in vehDict.Keys.ToList())
        {
            Vehicle car = vehDict[spot];
            if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
            {
                DeleteBlipForSpot(spot);
                car.MarkAsNoLongerNeeded();
            }
        }
    }

    private Vehicle CreateNewVehicle(string hash, Vector3 pos, float heading)
    {
        var model = new Model(hash);
        model.Request(500);
        if (!model.IsValid) return null;

        while (!model.IsLoaded) Script.Wait(100);
        Vehicle car = World.CreateVehicle(model, pos, heading);
        model.MarkAsNoLongerNeeded();

   

        // Lock Doors if toggle is on
        if (LockDoors) Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, car, 7);

        // Randomize Color Combo (matches original logic)
        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, car);
        if (comboCount > 0)
        {
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, car, random.Next(0, comboCount));
        }
        return car;
    }

    private void CreateMarkerAboveCar(Vehicle car, SpawnSpot spot)
    {
        Blip mark = car.AddBlip();
        mark.Sprite = BlipSprite.Standard;
        mark.Color = BlipColor.Blue;
        mark.Name = "Unique Vehicle";

        // RESTORED: This flashes the radar to alert you
        Function.Call(Hash.FLASH_MINIMAP_DISPLAY);

        markerDict[spot] = mark;
    }

    private void ApplyVehicleMods(Vehicle v, SpawnSpot spot, string modelName)
    {
        v.Mods.InstallModKit();

        // RESTORED: Performance Logic
        if (spot.Behavior != SpawnBehavior.Stock)
        {
            v.Mods[VehicleModType.Engine].Index = 3;
            v.Mods[VehicleModType.Brakes].Index = 2;
            v.Mods[VehicleModType.Transmission].Index = 2;
            v.Mods[VehicleModType.Suspension].Index = 3;
            v.Mods[VehicleModType.Armor].Index = 4;
            v.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
        }

        if (v.ClassType == VehicleClass.Super || v.ClassType == VehicleClass.Sports)
        {
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;
        }

        switch (spot.Behavior)
        {
            case SpawnBehavior.Standard:
                ApplyRandomVisuals(v);
                RandomizeLivery(v);
                break;

            case SpawnBehavior.Cult:
                v.Mods.PrimaryColor = (VehicleColor)157; // Epsilon Blue
                v.Mods.SecondaryColor = (VehicleColor)157;
                v.Mods.PearlescentColor = (VehicleColor)1; 
                v.Mods[VehicleModType.Livery].Index = -1;  
                ApplyRandomVisuals(v);
                break;

            case SpawnBehavior.Helicopter:
                // RESTORED: Critical physics flags to prevent falling through map
                Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
              //  Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
                RandomizeLivery(v);
                break;

            case SpawnBehavior.Higgins:
                if (modelName == "conada")
                {
                    v.Mods.PrimaryColor = (VehicleColor)89;
                    v.Mods.SecondaryColor = (VehicleColor)6;
                    v.Mods[VehicleModType.Livery].Index = 9;
                }
                v.Mods.PearlescentColor = VehicleColor.MetallicMidnightSilver;
                // RESTORED: Physics flags
               // Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
                //Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
                break;

            case SpawnBehavior.Armoured:
                v.Mods.PrimaryColor = VehicleColor.MatteBlack;
                v.Mods.SecondaryColor = VehicleColor.MatteBlack;
                break;

            case SpawnBehavior.Studio:
                v.Mods[VehicleModType.Roof].Index = 0;
                break;
        }

        if (modelName == "turismo2")
        {
            v.Mods[VehicleModType.Spoilers].Index = 3;
        }
        Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
        Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);

    }

    private string GetUniqueModel(List<string> list, SpawnSpot spot)
    {
        if (list == null || list.Count == 0) return null;

        if (!spawnQueues.ContainsKey(list) || spawnQueues[list].Count == 0)
        {
            List<string> freshBatch = new List<string>(list);
            freshBatch.Shuffle();

            if (lastSpawnedDict.ContainsKey(list) && freshBatch.Count > 1 && freshBatch[0] == lastSpawnedDict[list])
            {
                string temp = freshBatch[0];
                freshBatch[0] = freshBatch[freshBatch.Count - 1];
                freshBatch[freshBatch.Count - 1] = temp;
            }
            spawnQueues[list] = new Queue<string>(freshBatch);
        }

        string selection = spawnQueues[list].Dequeue();
        lastSpawnedDict[list] = selection;
        return selection;
    }

    private void ApplyRandomVisuals(Vehicle v)
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

    private void RandomizeLivery(Vehicle v)
    {
        int count = v.Mods.LiveryCount;
        if (count > 0) v.Mods.Livery = random.Next(0, count);
    }

    private void DeleteSpotResources(SpawnSpot spot)
    {
        if (vehDict.ContainsKey(spot))
        {
            if (vehDict[spot] != null && vehDict[spot].Exists()) vehDict[spot].Delete();
            vehDict.Remove(spot);
        }
        DeleteBlipForSpot(spot);
    }

    private void DeleteBlipForSpot(SpawnSpot spot)
    {
        if (markerDict.ContainsKey(spot))
        {
            if (markerDict[spot] != null && markerDict[spot].Exists()) markerDict[spot].Delete();
            markerDict.Remove(spot);
        }
    }

    private void CleanupAll()
    {
        foreach (var blip in markerDict.Values) if (blip != null && blip.Exists()) blip.Delete();
        foreach (var vehicle in vehDict.Values) if (vehicle != null && vehicle.Exists()) vehicle.Delete();
        markerDict.Clear();
        vehDict.Clear();
    }

    private void OnAborted(object sender, EventArgs e) => CleanupAll();
}

public class SpawnSpot
{
    public string Id { get; set; }
    public Vector3 Position { get; set; }
    public float Heading { get; set; }
    public List<string> ModelList { get; set; }
    public SpawnBehavior Behavior { get; set; }

    public SpawnSpot(string id, Vector3 pos, float head, List<string> list, SpawnBehavior behavior)
    {
        Id = id; Position = pos; Heading = head; ModelList = list; Behavior = behavior;
    }
}

public enum SpawnBehavior { Standard, Cult, Higgins, NoVisuals, Helicopter, Stock, Armoured, Studio }

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