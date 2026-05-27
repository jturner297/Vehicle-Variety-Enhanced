using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;

public class SpawnParked : Script
{


    private int nextSpawnCheck = 0;
    private static Random random = new Random();

    private bool _isInMissionMode = false; // State Flag to prevent loop spam

    private Dictionary<SpawnSpot, Vehicle> vehDict = new Dictionary<SpawnSpot, Vehicle>();
    private Dictionary<SpawnSpot, Blip> markerDict = new Dictionary<SpawnSpot, Blip>();
    private HashSet<SpawnSpot> cooldownSpots = new HashSet<SpawnSpot>();



    private List<SpawnSpot> AllSpawns = new List<SpawnSpot>();

    public SpawnParked()
    {

       ModSettings.Load(); // Initialize Settings First
        // INITIALIZE SPAWNS
        // REMINDER: SpawnBehavior.Spec handles "NoVisuals", "Hero Specs", "Higgins", and "Armoured" internally via CarMod.
        // REMINDER: SpawnBehavior.RandomSpec handles "Standard" and "Cult" (Epsilon check) internally.

        AllSpawns = SpawnDatabase.GetSpawns();

        Tick += OnTick;
        Aborted += OnAborted;
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
                //    float activeSpawnDist = (spot.CustomSpawnRange > 0) ? spot.CustomSpawnRange : ModSettings.ParkedSpawnDistance;
                //       float activeDespawnDist = activeSpawnDist + 150f;

                // 1. Determine Spawn Distance
                // If the spot has a custom range (Military/Arena), use it. Otherwise use default (250).
                float activeSpawnDist = (spot.CustomSpawnRange > 0) ? spot.CustomSpawnRange : ModSettings.SpotSpawnDistance;

                // 2. THE Despawn buffer 
                // If a custom buffer is defined (> 0), use it. Otherwise, use the global ModSettings.SpotDefaultDespawnBuffer.
                float activeBuffer = (spot.CustomDespawnBuffer > 0) ? spot.CustomDespawnBuffer : ModSettings.SpotDefaultDespawnBuffer;
                float activeDespawnDist = activeSpawnDist + activeBuffer;

                // A. COOLDOWN CHECK
                if (distance > activeDespawnDist && cooldownSpots.Contains(spot))
                {
                    cooldownSpots.Remove(spot);
                }

                // B. SPAWN CHECK
                if (distance < activeSpawnDist && distance > ModSettings.SpotSpawnDistMin && !vehDict.ContainsKey(spot) && !cooldownSpots.Contains(spot))
                {
                    string modelName = GetUniqueModel(spot);

                    if (modelName != null)
                    {
                        var vehicle = CreateNewVehicle(modelName, spot.Position, spot.Heading, spot);
                        if (vehicle != null)
                        {
                            vehDict[spot] = vehicle;
                            if (ModSettings.ParkedShowBlips) CreateMarkerAboveCar(vehicle, spot);

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

            if (player.IsInVehicle(car) || car.IsDead)
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

        if (ModSettings.LockDoors && !isFreeRide)
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
        //  mark.Scale = 0.7f;
        if (ModSettings.ShowVehicleNameOnBlips)
        {
            mark.Name = Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, car.Model.Hash));
        }
        else
        {
            mark.Name = "Vehicle";
        }
    
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
    public float CustomDespawnBuffer { get; set; } // NEW: The specific buffer for this spot
    public SpawnSpot(string id, Vector3 pos, float head, HashSet<string> list, SpawnBehavior behavior, HashSet<string> rareList = null, int rareChance = 0, float customRange = -1f, float customBuffer = 50f)
    {
        Id = id;
        Position = pos;
        Heading = head;
        ModelList = list;
        Behavior = behavior;
        RareList = rareList;
        RareChance = rareChance;
        CustomSpawnRange = customRange;
        CustomDespawnBuffer = customBuffer;
    }
}
