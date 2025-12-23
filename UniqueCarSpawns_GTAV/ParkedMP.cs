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
    private string mod_version = "1.72";
    private static Random random = new Random();
    Dictionary<SpawnId, Vehicle> vehDict = new Dictionary<SpawnId, Vehicle>();
    Dictionary<SpawnId, Blip> markerDict = new Dictionary<SpawnId, Blip>();

  


    struct SpawnPoint
    {
        public Vector3 Position;
        public float Heading;

        public SpawnPoint(Vector3 pos, float heading)
        {
            Position = pos;
            Heading = heading;
        }
    }

    enum SpawnId
    {
        ArenaHotring1,
        ArenaHotring2,
        ArenaSpeed1,
        ArenaSpeed2,
        ArenaOffroad1,
        ArenaOffroad2,
        Cinema,
        Cult,
        Higgins_Heli,
        Helicopter,
        Lowrider_1,
        Lowrider_2,
        Lowrider_3,
        Lowrider_4,
        Lowrider_5,
        Lowrider_6,
        Lowrider_7,
        Openwheel,
        Planes,
        RetroSports1,
        RetroSports2,
        RetroSports3,
        RetroSports4,
        RetroSports5,
        RetroSports6,
        RetroSports7,
        RetroSports8,
        RetroSports9,
        RetroSports10,
        RetroSports11,
        RetroSports12,
        Super1,
        Super2,
        Super3,
        Super4,
        Super5,
        Super6,
        Super7,
        Super8,
        Super9,
        Super10,
        Super11,
        Super12,
        Super13,


    }
    Dictionary<SpawnId, SpawnPoint> Spawns = new Dictionary<SpawnId, SpawnPoint>()
    {
        [SpawnId.ArenaHotring1] = new SpawnPoint(
            new Vector3(-206.046f, -1988.758f, 26.96269f),
        90.24596f
        ),
        [SpawnId.ArenaHotring2] = new SpawnPoint(
            new Vector3(1189.208f, 304.6935f, 81.48812f),
            146.9079f
        ),
        [SpawnId.ArenaSpeed1] = new SpawnPoint(
            new Vector3(-176.5869f, -2019.529f, 27.14398f),
             75.33392f
        ),
        [SpawnId.ArenaSpeed2] = new SpawnPoint(
            new Vector3(1117.519f, 257.8285f, 80.31487f),
          -122.0743f
        ),
        [SpawnId.ArenaOffroad1] = new SpawnPoint(
            new Vector3(-192.949f, -1928.497f, 27.20675f),
            -151.2955f
        ),
        [SpawnId.ArenaOffroad2] = new SpawnPoint(
            new Vector3(1151.233f, 183.6329f, 80.23096f),
           -53.71473f
        ),
        [SpawnId.Cinema] = new SpawnPoint(
          new Vector3(-1084.873f, -477.591f, 36.2069f), 
          27.92156f
        ),
        [SpawnId.Cult] = new SpawnPoint(
            new Vector3(-719.9119f, 79.29325f, 55.13408f),
           25.0975f
        ),
        [SpawnId.Higgins_Heli] = new SpawnPoint(
           new Vector3(-746.4702f, -1469.937f, 6.87726f),
            140.3646f
        ),
        [SpawnId.Helicopter] = new SpawnPoint(
           new Vector3(-979.378f, -2996.868f, 13.945f),
          331.180f
        ),
        [SpawnId.Lowrider_1] = new SpawnPoint(
          new Vector3(-229.587f, -1483.44f, 30.352f),
          146.244f
      ),
        [SpawnId.Lowrider_2] = new SpawnPoint(
          new Vector3(-22.296f, -1851.58f, 24.108f),
          141.262f
      ),
        [SpawnId.Lowrider_3] = new SpawnPoint(
          new Vector3(321.798f, -1948.14f, 23.627f),
          47.597f
      ),
        [SpawnId.Lowrider_4] = new SpawnPoint(
          new Vector3(455.602f, -1695.26f, 28.289f),
          138.808f
      ),
        [SpawnId.Lowrider_5] = new SpawnPoint(
          new Vector3(1228.548f, -1605.65f, 50.736f),
          33.185f
      ),
        [SpawnId.Lowrider_6] = new SpawnPoint(
          new Vector3(298.2452f, -1241.624f, 28.75226f),
          -179.7188f
      ),
        [SpawnId.Lowrider_7] = new SpawnPoint(
          new Vector3(264.0245f, -1512.3302f, 28.7877f),
          268.3357f
      ),
        [SpawnId.Openwheel] = new SpawnPoint(
         new Vector3(1135.19f, 39.81987f, 80.34249f),
          16.77524f
        ),
        [SpawnId.Planes] = new SpawnPoint(
           new Vector3(-961.005f, -2963.593f, 13.945f),
           147.589f
        ),
       
        [SpawnId.RetroSports1] = new SpawnPoint(
          new Vector3(-1114.1f, 479.205f, 81.161f),
          169.13f
        ),
        [SpawnId.RetroSports2] = new SpawnPoint(
            new Vector3(-1405.12f, 81.983f, 52.099f), //golfyard
            58.178f
        ),
        [SpawnId.RetroSports3] = new SpawnPoint(
          new Vector3(-1334.63f, -1008.97f, 6.867f), //beach house
          126.968f
        ),
        [SpawnId.RetroSports4] = new SpawnPoint(
         new Vector3(-1886.25f, 2016.572f, 139.951f), //vineyard
         160.257f
        ),
        [SpawnId.RetroSports5] = new SpawnPoint(
         new Vector3(-817.325f, -1201.59f, 5.935f), //vespucci hotel
         318.133f
        ),
        [SpawnId.RetroSports6] = new SpawnPoint(
         new Vector3(-1407.751f, -589.1447f, 29.65687f),  //bahama mamas
         298.6727f
        ),
        [SpawnId.RetroSports7] = new SpawnPoint(
        new Vector3(-552.673f, 309.154f, 82.191f),  //tequilala
         260.340f
        ),
        [SpawnId.RetroSports8] = new SpawnPoint(
        new Vector3(339.481f, 159.143f, 102.146f),  //vinewood cinema
         71.345f
        ),
        [SpawnId.RetroSports9] = new SpawnPoint(
            new Vector3(-3036.57f, 105.31f, 10.593f), //chumash yacht club
        141.262f
        ),
        [SpawnId.RetroSports10] = new SpawnPoint(
             new Vector3(-205.516f, 281.035f, 91.818f), //eclipse blvd 
       165.351f
        ),
        [SpawnId.RetroSports11] = new SpawnPoint(
            new Vector3(-972.578f, -1464.27f, 4.013f), //marina
            294.730f
        ),
        [SpawnId.RetroSports12] = new SpawnPoint(
            new Vector3(1309.942f, -530.154f, 70.312f), //mirror park suburbs 
 341.133f
        ),



        [SpawnId.Super1] = new SpawnPoint(
          new Vector3(-1873.6f, -343.933f, 48.26f),//north vespucci hotel
          225.300f
        ),
        [SpawnId.Super2] = new SpawnPoint(
          new Vector3(-1297.2f, 252.495f, 61.813f), //rockford hotel
            3.0345f
        ),
        [SpawnId.Super3] = new SpawnPoint(
         new Vector3(-345.267f, 662.299f, 168.587f),    //vinewood hills mansion
         171.211f
        ),
        [SpawnId.Super4] = new SpawnPoint(
        new Vector3(-72.605f, 902.579f, 234.631f), //vinewood hills lake mansion
         291.351f
        ),
        [SpawnId.Super5] = new SpawnPoint(
        new Vector3(-1451.92f, 533.495f, 118.177f), //west vinewood hills mansion
         73.674f
        ),

        [SpawnId.Super6] = new SpawnPoint(
        new Vector3(443.542f, 253.197f, 102.21f), //east vinewood hotel
         245.845f
        ),
       
        [SpawnId.Super7] = new SpawnPoint(
         new Vector3(-397.528046f, 210.366409f, 82.7887802f),  //eclipse strip club
         91.1357f
        ),
       
        [SpawnId.Super8] = new SpawnPoint(
     new Vector3(-220.102f, -590.273f, 33.264f), //arcadius business center
        341.667f
        ),
        [SpawnId.Super9] = new SpawnPoint(
        new Vector3(-1535.044f, 890.5871f, 181.3348f), //rehab
         19.50508f
        ),
        [SpawnId.Super10] = new SpawnPoint(
          new Vector3(-718.510742f, -74.6843796f, 36.9155846f), //chumash beach house
         62.2418f
        ),
        [SpawnId.Super11] = new SpawnPoint(
          new Vector3(-1126.722f, -318.281f, 37.21f), //chumash beach house
         -95.129f
        ),
        [SpawnId.Super12] = new SpawnPoint(
          new Vector3(-801.566f, -1313.92f, 4.0f), //vespucci marina 
        169.408f
        ),
        [SpawnId.Super13] = new SpawnPoint(
          new Vector3(-504.323f, 424.21f, 96.287f), //vespucci marina 
       313.167f
        ),

    };


    

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


   


        
      
        Tick += OnTick;
        Aborted += OnAborded;
    }




    string GenerateVehicleModelName(SpawnId spawnId, int type)
    {
        string model_name = null;
        bool isEmpty;


        // Ensure key exists
        if (!vehDict.ContainsKey(spawnId))
            vehDict[spawnId] = null;

        switch (spawnId)
        {
            case SpawnId.ArenaHotring1:
            case SpawnId.ArenaHotring2:
                isEmpty = !VehList.models_arena_hotring.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_arena_hotring[random.Next(VehList.models_arena_hotring.Count)];
                break;

            case SpawnId.ArenaSpeed1:
            case SpawnId.ArenaSpeed2:
                isEmpty = !VehList.models_arena_speed.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_arena_speed[random.Next(VehList.models_arena_speed.Count)];
                break;

            case SpawnId.ArenaOffroad1:
            case SpawnId.ArenaOffroad2:
                isEmpty = !VehList.models_arena_offroad.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_arena_offroad[random.Next(VehList.models_arena_offroad.Count)];
                break;
            case SpawnId.Cinema:
                isEmpty = !VehList.models_cinema.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_cinema[random.Next(VehList.models_cinema.Count)];
                break;
            case SpawnId.Cult:
                isEmpty = !VehList.models_cult.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_cult[random.Next(VehList.models_cult.Count)];
                break;
            case SpawnId.Higgins_Heli:
                isEmpty = !VehList.models_higgins.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_higgins[random.Next(VehList.models_higgins.Count)];
                break;
            case SpawnId.Helicopter:
                isEmpty = !VehList.models_helicopter.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_helicopter[random.Next(VehList.models_helicopter.Count)];
                break;
            case SpawnId.Lowrider_1:
            case SpawnId.Lowrider_2:
            case SpawnId.Lowrider_3:
            case SpawnId.Lowrider_4:
            case SpawnId.Lowrider_5:
            case SpawnId.Lowrider_6:
            case SpawnId.Lowrider_7:
                isEmpty = !VehList.models_ghetto.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_ghetto[random.Next(VehList.models_ghetto.Count)];
                break;
            case SpawnId.Openwheel:
                isEmpty = !VehList.models_openwheel.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_openwheel[random.Next(VehList.models_openwheel.Count)];
                break;
            case SpawnId.Planes:
                isEmpty = !VehList.models_planes.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_planes[random.Next(VehList.models_planes.Count)];
                break;
            case SpawnId.RetroSports1:
            case SpawnId.RetroSports2:
            case SpawnId.RetroSports3:
            case SpawnId.RetroSports4:
            case SpawnId.RetroSports5:
            case SpawnId.RetroSports6:
            case SpawnId.RetroSports7:
            case SpawnId.RetroSports8:
            case SpawnId.RetroSports9:
            case SpawnId.RetroSports10:
                case SpawnId.RetroSports11:
                case SpawnId.RetroSports12:
                isEmpty = !VehList.models_classics.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_classics[random.Next(VehList.models_classics.Count)];
                break;
            case SpawnId.Super1:
            case SpawnId.Super2:
            case SpawnId.Super3:
            case SpawnId.Super4:
            case SpawnId.Super5:
            case SpawnId.Super6:
            case SpawnId.Super7:
            case SpawnId.Super8:
            case SpawnId.Super9:
            case SpawnId.Super10:
            case SpawnId.Super11:
            case SpawnId.Super12:
                case SpawnId.Super13:
                isEmpty = !VehList.models_supers.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_supers[random.Next(VehList.models_supers.Count)];
                break;
        }

        return model_name;
    }

    void SetNumberPlate(Vehicle car, int mode, int index)
    {
        if (mode == 1 && plate_id != -1)
        {
            Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, car, index);
        }
    }


    Vehicle CreateNewVehicle(string hash, Vector3 pos, float heading, SpawnId spawnId)
    {
        var veh_model = new Model(hash);
        veh_model.Request(500);
        if (!veh_model.IsValid) return null;

        while (!veh_model.IsLoaded) Script.Wait(100);
        Vehicle car = World.CreateVehicle(veh_model, pos, heading);
        veh_model.MarkAsNoLongerNeeded();

        if (doors_config == 1)
            Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, car, 7);
        // 3. GLOBAL COLOR LOGIC (Applies to everything)
        // This native checks if the car has developer-defined color presets (carcols.meta)
       
        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, car);

        if (comboCount > 0)
        {
         
            int randomCombo = random.Next(0, comboCount);
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, car, randomCombo);
        }


        return car;
    }

    Blip CreateMarkerAboveCar(Vehicle car, SpawnId spawnId)
    {
        Blip mark = Function.Call<Blip>(GTA.Native.Hash.ADD_BLIP_FOR_ENTITY, car);
        Function.Call(GTA.Native.Hash.SET_BLIP_SPRITE, mark, 1);
        Function.Call(GTA.Native.Hash.SET_BLIP_COLOUR, mark, blip_color);
        Function.Call(GTA.Native.Hash.FLASH_MINIMAP_DISPLAY);
        mark.Name = "Unique vehicle";
        markerDict[spawnId] = mark; // markerDict: Dictionary<SpawnId, Blip>
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
        if (vehicles_spawned == 1 && isMissionActive)
        {
            // Force delete everything if a mission starts
            foreach (var spawnId in vehDict.Keys.ToList())
            {
                var car = vehDict[spawnId];
                if (car != null && car.Exists()) car.Delete();

                // Also clean up blips using the Dictionary
                if (markerDict.ContainsKey(spawnId))
                {
                    if (markerDict[spawnId] != null && markerDict[spawnId].Exists())
                        markerDict[spawnId].Delete();
                    markerDict[spawnId] = null;
                }

                vehDict[spawnId] = null;
            }
            vehicles_spawned = 0;
        }

        // --- 2. SPAWN LOGIC (Only if no mission) ---
        if (!isMissionActive)
        {
            foreach (var kvp in Spawns)
            {
                SpawnId spawnId = kvp.Key;
                SpawnPoint spawn = kvp.Value;

                // Check if player is close enough to spawn point
                if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                    spawn.Position.X, spawn.Position.Y, spawn.Position.Z,
                    playerPos.X, playerPos.Y, playerPos.Z, 0) < 300)
                {
                    // Try to get a model name (checks internally if spot is empty)
                    string model_name = GenerateVehicleModelName(spawnId, 0);

                    if (model_name != null)
                    {
                        vehDict[spawnId] = CreateNewVehicle(model_name, spawn.Position, spawn.Heading, spawnId);
                        var vehicle = vehDict[spawnId];

                        if (vehicle != null)
                        {
                            SetNumberPlate(vehicle, mod_plate, plate_id);
                            plate_id = -1;

                            if (blip_config == 1)
                            {
                                // CreateMarkerAboveCar already adds it to markerDict
                                CreateMarkerAboveCar(vehicle, spawnId);
                            }
                            ApplyVehicleMods(vehicle, spawnId, model_name);

                            // Mark that we have vehicles spawned
                            vehicles_spawned = 1;
                        }
                    }
                }
            }
        }

        // --- 3. PLAYER INTERACTION (Entered Vehicle) ---
        // We iterate over a copy of keys to safely modify the dictionary if needed
        foreach (var spawnId in vehDict.Keys.ToList())
        {
            Vehicle car = vehDict[spawnId];

            // If player is in a tracked vehicle, remove the blip but keep tracking the car 
            // until they leave the area
            if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
            {
                if (markerDict.ContainsKey(spawnId))
                {
                    if (markerDict[spawnId] != null && markerDict[spawnId].Exists())
                        markerDict[spawnId].Delete();

                    markerDict[spawnId] = null; // Remove blip reference
                }

                car.MarkAsNoLongerNeeded(); // Let the game engine handle memory
            }
        }

        // --- 4. DISTANCE CLEANUP (The Fix) ---
        foreach (var kvp in Spawns)
        {
            SpawnId spawnId = kvp.Key;
            SpawnPoint spawn = kvp.Value;

            // Check distance between Player and the SPAWN POINT
            if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                spawn.Position.X, spawn.Position.Y, spawn.Position.Z,
                playerPos.X, playerPos.Y, playerPos.Z, 0) > 300)
            {
                // Check if we are currently tracking a vehicle here
                if (vehDict.TryGetValue(spawnId, out var vehicle))
                {
                    // Handle the blip first
                    if (markerDict.ContainsKey(spawnId))
                    {
                        if (markerDict[spawnId] != null && markerDict[spawnId].Exists())
                            markerDict[spawnId].Delete();
                        markerDict[spawnId] = null;
                    }

                    if (vehicle != null && vehicle.Exists())
                    {
                        // IF player is inside: Do NOT delete, just stop tracking.
                        // IF player is NOT inside: Delete it.
                        if (!Function.Call<bool>(Hash.IS_PED_SITTING_IN_VEHICLE, Game.Player.Character, vehicle))
                        {
                            vehicle.Delete();
                        }
                        else
                        {
                            // Player took the car away. We release control.
                            vehicle.MarkAsNoLongerNeeded();
                        }
                    }

                    // CRITICAL FIX: Always null the dictionary slot so a new car can spawn 
                    // when we return to this spot.
                    vehDict[spawnId] = null;
                }
            }
        }
    }
    private void ApplyVehicleMods(Vehicle v, SpawnId id, string modelName)
    {
    

        // -----------------------------------------------------------
        // 1. GLOBAL INITIALIZATION
        // -----------------------------------------------------------
        v.Mods.InstallModKit(); // Required for everything below

        // -----------------------------------------------------------
        // 2. MANDATORY PERFORMANCE (Replicated from your snippet)
        // -----------------------------------------------------------
        v.Mods[VehicleModType.Engine].Index = 3;       // Lvl 4
        v.Mods[VehicleModType.Brakes].Index = 2;       // Race Brakes
        v.Mods[VehicleModType.Transmission].Index = 2; // Race Trans
        v.Mods[VehicleModType.Suspension].Index = 3;   // Competition
        v.Mods[VehicleModType.Armor].Index = 4;        // 100% Armor
        v.Mods[VehicleToggleModType.Turbo].IsInstalled = true;
        v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;

        // -----------------------------------------------------------
        // 3. RANDOM VISUALS (Replicated from your snippet)
        // -----------------------------------------------------------
    
        bool skipVisuals = false;
        switch (id) //super and retro cars do not get random visuals
        {
            case SpawnId.RetroSports1:
            case SpawnId.RetroSports2:
            case SpawnId.RetroSports3:
            case SpawnId.RetroSports4:
            case SpawnId.RetroSports5:
            case SpawnId.RetroSports6:
            case SpawnId.RetroSports7:
            case SpawnId.RetroSports8:
            case SpawnId.RetroSports9:
            case SpawnId.RetroSports10:
            case SpawnId.RetroSports11:
            case SpawnId.RetroSports12:
            case SpawnId.Super1:
            case SpawnId.Super2:
            case SpawnId.Super3:
            case SpawnId.Super4:
            case SpawnId.Super5:
            case SpawnId.Super6:
            case SpawnId.Super7:
            case SpawnId.Super8:
            case SpawnId.Super9:
            case SpawnId.Super10:
            case SpawnId.Super11:
            case SpawnId.Super12:
            case SpawnId.Super13:
                skipVisuals = true; // Set flag to TRUE for these groups
                break;
        }
        // We loop through every possible mod type defined in the game

        if (!skipVisuals)
        {
            foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
            {
                // Skip Livery here (handled in Group Logic) and Performance mods (handled above)
                if (modType == VehicleModType.Livery ||
                    modType == VehicleModType.Engine ||
                    modType == VehicleModType.Brakes ||
                    modType == VehicleModType.Transmission ||
                    modType == VehicleModType.Suspension ||
                    modType == VehicleModType.Armor ||
                    modType == VehicleModType.RearWheel ||
                    modType == VehicleModType.FrontWheel) continue;

                int count = v.Mods[modType].Count;
                if (count > 0)
                {
                    // Randomize between Stock (-1) and Max Index
                    v.Mods[modType].Index = random.Next(-1, count);
                }
            }
        }
           

        // -----------------------------------------------------------
        // 4. GROUP SPECIFIC LOGIC (The core replication)
        // -----------------------------------------------------------
        switch (id)
        {
            // --- ARENA / GHETTO / STANDARD GROUPS ---
            case SpawnId.ArenaHotring1:
            case SpawnId.ArenaHotring2:
            case SpawnId.ArenaSpeed1:
            case SpawnId.ArenaSpeed2:
            case SpawnId.ArenaOffroad1:
            case SpawnId.ArenaOffroad2:
            case SpawnId.Lowrider_1: 
            case SpawnId.Lowrider_2:               
            case SpawnId.Lowrider_3:                  
            case SpawnId.Lowrider_4:                     
            case SpawnId.Lowrider_5:
            case SpawnId.Lowrider_6:
            case SpawnId.Lowrider_7:
            case SpawnId.Openwheel:
                    // Random Livery if available
                    int liveryCount = v.Mods[VehicleModType.Livery].Count;
                    if (liveryCount > 0) v.Mods[VehicleModType.Livery].Index = random.Next(0, liveryCount);
                
                break;

            // --- CULT GROUP ---
            case SpawnId.Cult:
                // Fixed Epsilon Blue
                v.Mods.PrimaryColor = (VehicleColor)157;
                v.Mods.SecondaryColor = (VehicleColor)157;
                v.Mods.PearlescentColor = (VehicleColor)1;
                v.Mods[VehicleModType.Livery].Index = -1; // Tacky for cult members
                break;

            // --- HIGGINS HELICOPTER ---
            case SpawnId.Higgins_Heli:
                if (modelName.ToLower() == "conada")
                {
                    v.Mods.PrimaryColor = (VehicleColor)89;
                    v.Mods.SecondaryColor = (VehicleColor)6;
                    v.Mods.PearlescentColor = (VehicleColor)1;
                    v.Mods[VehicleModType.Livery].Index = 9; // Higgins livery

             
                }
                break;

            // --- HELICOPTERS & PLANES ---
            case SpawnId.Helicopter:
            case SpawnId.Planes:
                int airLiveryCount = v.Mods[VehicleModType.Livery].Count;
                if (airLiveryCount > 0) v.Mods[VehicleModType.Livery].Index = random.Next(0, airLiveryCount);
                break;
        }

        // -----------------------------------------------------------
        // 5. SPECIFIC MODEL OVERRIDES (Brickade2 logic)
        // -----------------------------------------------------------
        if (modelName.ToLower() == "brickade2")
        {
            v.Mods.CustomPrimaryColor = Color.Black;
            v.Mods.CustomSecondaryColor = Color.Black;
            v.Mods[VehicleModType.Livery].Index = 5;
        }
        // -----------------------------------------------------------
        // 6. GLOBAL HELICOPTER FIX (The New Logic)
        // -----------------------------------------------------------
        // This checks if the vehicle is strictly a helicopter class (ID 15)
        if (v.ClassType == VehicleClass.Helicopters)
        {
            Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
        }
    }

}
