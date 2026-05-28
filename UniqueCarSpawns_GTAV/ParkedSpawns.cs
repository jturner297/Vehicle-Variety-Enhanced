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

    private bool _isInMissionMode = false;

    private Dictionary<SpawnSpot, Vehicle> vehDict = new Dictionary<SpawnSpot, Vehicle>();
    private Dictionary<SpawnSpot, Blip> markerDict = new Dictionary<SpawnSpot, Blip>();

    // Time & Distance Cooldown Tracking
    private Dictionary<SpawnSpot, int> spotCooldowns = new Dictionary<SpawnSpot, int>();
    private const int CooldownDuration = 180000; // 3 minutes real-time
    private const float CooldownResetDistance = 1500f; // "Super far away" distance threshold

    // Player State Tracking for Resets
    private int lastPlayerHandle = 0;
    private bool wasPlayerDead = false;

    private List<SpawnSpot> AllSpawns = new List<SpawnSpot>();

    public SpawnParked()
    {
        ModSettings.Load();
        AllSpawns = SpawnDatabase.GetSpawns();

        Tick += OnTick;
        Aborted += OnAborted;
    }

    private void OnTick(object sender, EventArgs e)
    {
        var player = Game.Player.Character;
        if (player == null || !player.Exists()) return;

        var playerPos = player.Position;

        // --- 0. EVENT RESETS (Death & Character Switch) ---
        if (ModSettings.EnableSpotCooldowns) // Only process if toggle is on
        {
            int currentHandle = player.Handle;
            if (lastPlayerHandle != 0 && currentHandle != lastPlayerHandle)
            {
                spotCooldowns.Clear(); // Switched Characters
            }
            lastPlayerHandle = currentHandle;

            bool isDead = player.IsDead;
            if (!isDead && wasPlayerDead)
            {
                spotCooldowns.Clear(); // Respawned
            }
            wasPlayerDead = isDead;
        }

        // --- MISSION HANDLING ---
        if (ModUtilities.IsMissionOrCutsceneActive())
        {
            if (!_isInMissionMode)
            {
                ReleaseAllToGame();
                _isInMissionMode = true;
            }
            return;
        }
        else
        {
            if (_isInMissionMode)
            {
                _isInMissionMode = false;
                nextSpawnCheck = Game.GameTime + 5000;
            }
        }

        // --- 1. SPAWN & DESPAWN LOGIC ---
        if (Game.GameTime > nextSpawnCheck)
        {
            foreach (var spot in AllSpawns)
            {
                float distance = Vector3.Distance(spot.Position, playerPos);

                float activeSpawnDist = (spot.CustomSpawnRange > 0) ? spot.CustomSpawnRange : ModSettings.SpotSpawnDistance;
                float activeBuffer = (spot.CustomDespawnBuffer > 0) ? spot.CustomDespawnBuffer : ModSettings.SpotDefaultDespawnBuffer;
                float activeDespawnDist = activeSpawnDist + activeBuffer;

                // A. COOLDOWN MANAGEMENT
                if (ModSettings.EnableSpotCooldowns)
                {
                    if (spotCooldowns.ContainsKey(spot))
                    {
                        // 1. Time-based reset
                        if (Game.GameTime > spotCooldowns[spot])
                        {
                            spotCooldowns.Remove(spot);
                        }
                        // 2. Distance-based reset (Driven "super far away")
                        else if (distance > CooldownResetDistance)
                        {
                            spotCooldowns.Remove(spot);
                        }
                    }
                }
                else if (spotCooldowns.Count > 0)
                {
                    // Clean up memory if the user toggled it off mid-game
                    spotCooldowns.Clear();
                }

                // B. SPAWN CHECK
                bool isCoolingDown = ModSettings.EnableSpotCooldowns && spotCooldowns.ContainsKey(spot);

                if (distance < activeSpawnDist && distance > ModSettings.SpotSpawnDistMin && !vehDict.ContainsKey(spot) && !isCoolingDown)
                {
                    string modelName = GetUniqueModel(spot);

                    if (modelName != null)
                    {
                        var vehicle = CreateNewVehicle(modelName, spot.Position, spot.Heading, spot);
                        if (vehicle != null)
                        {
                            vehDict[spot] = vehicle;
                            if (ModSettings.ParkedShowBlips) CreateBlip(vehicle, spot);

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

                        // Apply cooldown when naturally despawning off-screen
                        if (ModSettings.EnableSpotCooldowns)
                        {
                            spotCooldowns[spot] = Game.GameTime + CooldownDuration;
                        }
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

                // Apply cooldown when stolen or destroyed
                if (ModSettings.EnableSpotCooldowns)
                {
                    spotCooldowns[spot] = Game.GameTime + CooldownDuration;
                }

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

        if (spot.RareList != null && spot.RareList.Count > 0)
        {
            if (random.Next(0, 100) < spot.RareChance)
            {
                targetList = spot.RareList;
            }
        }

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

        bool isFreeRide = spot.Id.Contains("Arena") || spot.Id.Contains("Casino");

        if (ModSettings.LockDoors && !isFreeRide)
        {
            Function.Call(Hash.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER, car, false);
            Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, car, 7);
            Function.Call(Hash.SET_VEHICLE_ALARM, car, true);
            Function.Call(Hash.SET_VEHICLE_NEEDS_TO_BE_HOTWIRED, car, true);
        }

        car.Opacity = 0;

        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, car);
        if (comboCount > 0)
        {
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, car, random.Next(0, comboCount));
        }
        return car;
    }

    private void CreateBlip(Vehicle v, SpawnSpot spot)
    {
        Blip b = ModUtilities.CreateVehicleBlip(v, BlipColor.Blue);
        markerDict[spot] = b;
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
        spotCooldowns.Clear();
    }

    private void ReleaseAllToGame()
    {
        foreach (var blip in markerDict.Values)
        {
            if (blip != null && blip.Exists()) blip.Delete();
        }
        markerDict.Clear();

        foreach (var vehicle in vehDict.Values)
        {
            if (vehicle != null && vehicle.Exists())
            {
                vehicle.MarkAsNoLongerNeeded();
            }
        }
        vehDict.Clear();
        spotCooldowns.Clear();
    }
    private void OnAborted(object sender, EventArgs e) => CleanupAll();
}

public class SpawnSpot
{
    public string Id { get; set; }
    public Vector3 Position { get; set; }
    public float Heading { get; set; }
    public SpawnBehavior Behavior { get; set; }
    public HashSet<string> ModelList { get; set; }
    public HashSet<string> RareList { get; set; }
    public int RareChance { get; set; }
    public float CustomSpawnRange { get; set; }
    public float CustomDespawnBuffer { get; set; }

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