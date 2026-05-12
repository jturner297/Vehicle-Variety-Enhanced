using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;

public class SwapParked : Script
{
    // ==========================================
    //              QUICK SETTINGS
    // ==========================================
    private bool ShowBlips = false;
    private bool LockDoors = true;
    private float SpawnDistance = 200f;
    private float SpawnDistMin = 150f;
    // ==========================================

    private int nextSpawnCheck = 0;
    private string mod_version = "1.72";
    private static Random random = new Random();

    private bool _isInMissionMode = false; // State Flag to prevent loop spam

    private Dictionary<SpawnSpot, Vehicle> vehDict = new Dictionary<SpawnSpot, Vehicle>();
    private Dictionary<SpawnSpot, Blip> markerDict = new Dictionary<SpawnSpot, Blip>();
    private HashSet<SpawnSpot> cooldownSpots = new HashSet<SpawnSpot>();



    private List<SpawnSpot> AllSpawns = new List<SpawnSpot>();

    public SwapParked()
    {
        string onlineVersion = Function.Call<string>(Hash.GET_ONLINE_VERSION);
        if (onlineVersion != mod_version)
        {
            GTA.UI.Notification.PostTicker($"~r~WARNING: Game Version Mismatch.\nRequired: {mod_version}", true);
        }

        // INITIALIZE SPAWNS
        // REMINDER: SpawnBehavior.Spec handles "NoVisuals", "Hero Specs", "Higgins", and "Armoured" internally via CarMod.
        // REMINDER: SpawnBehavior.RandomSpec handles "Standard" and "Cult" (Epsilon check) internally.

        AllSpawns = SpawnDatabase.GetSpawns();

        Tick += OnTick;
        Aborted += OnAborted;
        KeyDown += OnKeyDown;
    }

    private void OnTick(object sender, EventArgs e)
    {
        var player = Game.Player.Character;
        var playerPos = player.Position;
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);

        /* if (isMissionActive)
         {
             CleanupAll();
             return;
         }*/

        // --- SMART MISSION LOGIC ---
        if (isMissionActive)
        {
            // Only run this ONCE when the mission first starts
            if (!_isInMissionMode)
            {
                ReleaseAllToGame(); // Handoff cars to game
                _isInMissionMode = true; // Lock the door
            }
            return; // Stop script logic during mission
        }
        else
        {
            // Mission is over, reset the flag so we can spawn again
            if (_isInMissionMode)
            {
                _isInMissionMode = false;
                // Optional: Force a small cooldown so we don't spawn instantly on top of the old cars
                nextSpawnCheck = Game.GameTime + 5000;
            }
        }


        // --- 1. SPAWN & DESPAWN LOGIC ---
        if (Game.GameTime > nextSpawnCheck)
        {
            foreach (var spot in AllSpawns)
            {
                float distance = Vector3.Distance(spot.Position, playerPos);
            //    float activeSpawnDist = (spot.CustomSpawnRange > 0) ? spot.CustomSpawnRange : SpawnDistance;
         //       float activeDespawnDist = activeSpawnDist + 150f;

                // 1. Determine Spawn Distance
                // If the spot has a custom range (Military/Arena), use it. Otherwise use default (250).
                float activeSpawnDist = (spot.CustomSpawnRange > 0) ? spot.CustomSpawnRange : SpawnDistance;

                // 2. Determine Buffer (The Fix)
                // If it's a "Custom Large Spot" (Range > 0), give it a HUGE 400f buffer.
                // If it's a normal city spot, give it a TINY 50f buffer.
                float buffer = (spot.CustomSpawnRange > 0) ? 300f : 200f;

                float activeDespawnDist = activeSpawnDist + buffer;

                // A. COOLDOWN CHECK
                if (distance > activeDespawnDist && cooldownSpots.Contains(spot))
                {
                    cooldownSpots.Remove(spot);
                }

                // B. SPAWN CHECK
                if (distance < activeSpawnDist && distance > SpawnDistMin && !vehDict.ContainsKey(spot) && !cooldownSpots.Contains(spot))
                {
                    string modelName = GetUniqueModel(spot);

                    if (modelName != null)
                    {
                        var vehicle = CreateNewVehicle(modelName, spot.Position, spot.Heading, spot);
                        if (vehicle != null)
                        {
                            vehDict[spot] = vehicle;
                            if (ShowBlips) CreateMarkerAboveCar(vehicle, spot);

                            // UPDATED: Now calls Unified CarMod
                            CarMod.ApplyStyle(vehicle, spot.Behavior, modelName);
                        }
                    }
                }

                // C. DESPAWN CHECK
                if (vehDict.ContainsKey(spot))
                {
                    if (distance > activeDespawnDist)
                    {
                        DeleteSpotResources(spot);
                    }
                }
            }
            nextSpawnCheck = Game.GameTime + 500;
        }

        // --- 2. OWNERSHIP TRANSFER ---
        foreach (var spot in vehDict.Keys.ToList())
        {
            Vehicle car = vehDict[spot];
            if (car == null || !car.Exists())
            {
                vehDict.Remove(spot);
                continue;
            }

            if (player.IsInVehicle(car))
            {
                DeleteBlipForSpot(spot);
                car.MarkAsNoLongerNeeded();
                vehDict.Remove(spot);
                cooldownSpots.Add(spot);
                car.Opacity = 255;
                Function.Call(Hash.RESET_ENTITY_ALPHA, car);
            }
        }

        // --- 3. FADING LOGIC ---
        foreach (var vehicle in vehDict.Values)
        {
            if (vehicle != null && vehicle.Exists())
            {
                if (vehicle.Opacity < 255)
                {
                    int newAlpha = vehicle.Opacity + 10;
                    if (newAlpha >= 255)
                    {
                        vehicle.Opacity = 255;
                        Function.Call(Hash.RESET_ENTITY_ALPHA, vehicle);
                    }
                    else vehicle.Opacity = newAlpha;
                }
            }
        }
    }

    private string GetUniqueModel(SpawnSpot spot)
    {
        HashSet<string> targetList = spot.ModelList;

        // 1. Decide which list to use (Rare vs Common)
        if (spot.RareList != null && spot.RareList.Count > 0)
        {
            if (random.Next(0, 100) < spot.RareChance)
            {
                targetList = spot.RareList;
            }
        }

        // 2. Ask VehicleSelector for the next car
        // (Pass null for exclusions because ParkedMP doesn't use a blacklist)
        return VehicleSelector.GetNext(targetList, null);
    }

    private Vehicle CreateNewVehicle(string hash, Vector3 pos, float heading, SpawnSpot spot)
    {
        var model = new Model(hash);
        model.Request(500);
        if (!model.IsValid) return null;

        while (!model.IsLoaded) Script.Wait(100);
        Vehicle car = World.CreateVehicle(model, pos, heading);
        model.MarkAsNoLongerNeeded();

        // CHECK: Is this a "Free Ride" spot? (Arena/Casino/Openwheel)
        bool isFreeRide = spot.Id.Contains("Arena") || spot.Id.Contains("Casino");

        if (LockDoors && !isFreeRide)
        {

            Function.Call(Hash.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER, car, false);
            Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, car, 7); // 7 = LockedCanBeBroken
            Function.Call(Hash.SET_VEHICLE_ALARM, car, true);
            Function.Call(Hash.SET_VEHICLE_NEEDS_TO_BE_HOTWIRED, car, true);
        }
        // Start Invisible for fading logic
        car.Opacity = 0;
        
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
        Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
        markerDict[spot] = mark;
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
        cooldownSpots.Clear();
    }
    // NEW METHOD: Releases cars to the game engine instead of deleting them.
    // Use this when a mission starts so cars don't vanish in front of the player.
    private void ReleaseAllToGame()
    {
        // 1. Delete Blips (Clean the map UI immediately)
        foreach (var blip in markerDict.Values)
        {
            if (blip != null && blip.Exists()) blip.Delete();
        }
        markerDict.Clear();

        // 2. Release Vehicles (Don't delete! Just let the game manage them)
        foreach (var vehicle in vehDict.Values)
        {
            if (vehicle != null && vehicle.Exists())
            {
                // This makes the car non-persistent. 
                // It stays visible now, but deletes naturally when you drive away.
                vehicle.MarkAsNoLongerNeeded();
            }
        }
        vehDict.Clear();
        cooldownSpots.Clear();
    }
    private void OnAborted(object sender, EventArgs e) => CleanupAll();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // F10: ADVANCED COORDINATE LOGGER
       /* if (e.KeyCode == Keys.F10)
        {
            Ped player = Game.Player.Character;
            Vehicle currentCar = player.CurrentVehicle;
            Vector3 pos;
            float heading;
            string refModel = "OnFoot";
            string suggestedList = "VehList.models_city_rich";

            // 1. Get Position & Reference Info
            if (player.IsInVehicle())
            {
                pos = currentCar.Position;
                heading = currentCar.Heading;
                refModel = currentCar.DisplayName;
            }
            else
            {
                pos = player.Position;
                heading = player.Heading;
            }

            // 2. Get Location Data (Zone & Street)
            // Get Short Code (e.g., "AIRP")
            string zoneShort = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);

            // Get Full Name (e.g., "Los Santos International Airport")
            // FIX: Using Raw Hash 0x7B5280EBA9840C72 for GET_LABEL_TEXT to avoid Enum errors
            string zoneName = Function.Call<string>((Hash)0x7B5280EBA9840C72, zoneShort);

            // Fallback if null
            if (string.IsNullOrEmpty(zoneName)) zoneName = zoneShort;

            string streetName = World.GetStreetName(pos);

            // 3. Format Data
            string x = pos.X.ToString("F3") + "f";
            string y = pos.Y.ToString("F3") + "f";
            string z = pos.Z.ToString("F3") + "f";
            string h = heading.ToString("F3") + "f";

            // Clean up zone name for ID generation (Remove spaces/quotes)
            string cleanZone = zoneName.Replace(" ", "").Replace("'", "");
            string autoId = $"{cleanZone}_{DateTime.Now.ToString("HHmmss")}";

            string line = $"new SpawnSpot(\"{autoId}\", new Vector3({x}, {y}, {z}), {h}, {suggestedList}, SpawnBehavior.RandomSpec), // {zoneName} - {streetName} - Ref: {refModel}";

            // 4. Save to File
            try
            {
                File.AppendAllText("NewSpawns.txt", line + Environment.NewLine);
                // FIX: PostTicker
                GTA.UI.Notification.PostTicker($"~g~Saved: {autoId}\n~w~{refModel} @ {streetName}", true);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Error: {ex.Message}", true);
            }
        } */

        // F12: FORCE REFRESH
        if (e.KeyCode == Keys.F12)
        {
            CleanupAll();
            cooldownSpots.Clear();
            nextSpawnCheck = 0;
            GTA.UI.Notification.PostTicker("~y~Spawns Force Cycled", true);
        }
    }
}

// ==================================================
//               UPDATED SPAWNSPOT CLASS
// ==================================================
public class SpawnSpot
{
    public string Id { get; set; }
    public Vector3 Position { get; set; }
    public float Heading { get; set; }

    // Updated: Now uses the Shared Enum
    public SpawnBehavior Behavior { get; set; }

    public HashSet<string> ModelList { get; set; }
    public HashSet<string> RareList { get; set; }
    public int RareChance { get; set; }
    public float CustomSpawnRange { get; set; }

    public SpawnSpot(string id, Vector3 pos, float head, HashSet<string> list, SpawnBehavior behavior, HashSet<string> rareList = null, int rareChance = 0, float customRange = -1f)
    {
        Id = id;
        Position = pos;
        Heading = head;
        ModelList = list;
        Behavior = behavior;
        RareList = rareList;
        RareChance = rareChance;
        CustomSpawnRange = customRange;
    }
}

