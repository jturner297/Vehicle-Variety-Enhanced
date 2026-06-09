using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Runtime.CompilerServices;

public class SpawnParked : Script
{
    // ==========================================
    // DYNAMIC BLIP TUNING
    // ==========================================
    private float _blipRevealDist = 130f;  // Must get this close to discover the car and trigger the blip
    private float _blipHideDist = 150f;   // Must back up this far for the blip to fade away

    private int nextSpawnCheck = 0;
    private static Random random = new Random();

    private bool _isInMissionMode = false;

    private Dictionary<SpawnSpot, Vehicle> vehDict = new Dictionary<SpawnSpot, Vehicle>();
    private Dictionary<SpawnSpot, Blip> markerDict = new Dictionary<SpawnSpot, Blip>();

    // Time & Distance Cooldown Tracking
    private Dictionary<SpawnSpot, int> spotCooldowns = new Dictionary<SpawnSpot, int>();

    // Player State Tracking for Resets
    private int lastPlayerHandle = 0;
    private bool wasPlayerDead = false;

    // RNG spawn tracking: track whether we've attempted the RNG roll for the current player entry into a spot
    private HashSet<SpawnSpot> attemptedRng = new HashSet<SpawnSpot>();

    // NEW: Tracks stolen cars to prevent instant respawns when immersion is off
    private HashSet<SpawnSpot> stolenPendingReset = new HashSet<SpawnSpot>();

    // NEW: Tracks if the player crossed the UI Reveal threshold to validate a "real" encounter
    private HashSet<SpawnSpot> _discoveredSpots = new HashSet<SpawnSpot>();

    private List<SpawnSpot> AllSpawns = new List<SpawnSpot>();

    public SpawnParked()
    {
        ModSettings.Load();
        AllSpawns = SpawnDatabase.GetSpawns();

        Tick += OnTick;
        Aborted += OnAborted;
    }

    // Helper: horizontal distance (2D) between two vectors
    private float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private void OnTick(object sender, EventArgs e)
    {
        var player = Game.Player.Character;
        if (player == null || !player.Exists()) return;

        int currentHandle = player.Handle;
        bool isDead = player.IsDead;

        // --- 0. EVENT RESETS & STARTUP DELAY ---
        // Must run BEFORE IsModReady() so it can catch the switch/respawn instantly
        if (lastPlayerHandle == 0 || currentHandle != lastPlayerHandle || (!isDead && wasPlayerDead))
        {
            ReleaseAllToGame(); // Instantly wipe the old parked blips

            // Wipe memory for the new character / respawn
            if (ModSettings.EnableSpotCooldowns) spotCooldowns.Clear();
            stolenPendingReset.Clear();
            attemptedRng.Clear();
            _discoveredSpots.Clear();

            // Tell the mod to pause
            ModUtilities.TriggerGlobalDelay(ModSettings.StartupDelay);

            // STAGGERED WAKE-UP
            nextSpawnCheck = Game.GameTime + ModSettings.StartupDelay + 500;
        }

        lastPlayerHandle = currentHandle;
        wasPlayerDead = isDead;

        // Gatekeeper Check (Pauses the rest of the script if timer is active)
        if (!ModUtilities.IsModReady()) return;

        var playerPos = player.Position;

        // IMMERSION SETTING: Check if the player is actively wanted to pause new spawns
        bool isWanted = ModSettings.DisableWhenWanted && Game.Player.Wanted.WantedLevel > 0;

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
                nextSpawnCheck = Game.GameTime + ModSettings.MissionEndDelay;
            }
        }

        // --- 1. SPAWN & DESPAWN LOGIC ---
        if (Game.GameTime > nextSpawnCheck)
        {
            // Cache camera data once per check cycle for performance
            Vector3 camPos = GameplayCamera.Position;
            Vector3 camDir = GameplayCamera.Direction;

            foreach (var spot in AllSpawns)
            {
                // Use horizontal distance for most spawn/despawn/cooldown checks so vertical flight
                // doesn't constantly toggle/reset spots. Keep vertical difference for edge checks.
                float horizDistance = GetHorizontalDistance(spot.Position, playerPos);
                float verticalDiff = Math.Abs(spot.Position.Z - playerPos.Z);

                float activeSpawnDist = (spot.CustomSpawnRange > 0) ? spot.CustomSpawnRange : ModSettings.SpotSpawnDistance;
                float activeBuffer = (spot.CustomDespawnBuffer > 0) ? spot.CustomDespawnBuffer : ModSettings.SpotDefaultDespawnBuffer;
                float activeDespawnDist = activeSpawnDist + activeBuffer;

                // --- BASELINE STOLEN DISTANCE RESET ---
                // Even with immersion off, you must leave the area before a stolen spot respawns
                if (stolenPendingReset.Contains(spot))
                {
                    if (horizDistance > activeDespawnDist)
                    {
                        stolenPendingReset.Remove(spot);
                    }
                }

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
                        // 2. Distance-based reset (Driven "super far away") - use horizontal distance
                        else if (horizDistance > ModSettings.CooldownResetDistance)
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

                // Added stolenPendingReset check to block instant respawns
                // Only consider spawning when horizontally within range and not extremely high/low compared to the spot
                const float MaxVerticalSpawnOffset = 80f; // prevents spawning directly beneath/above while flying

                // NEW: Added !isWanted check so no new cars generate during a chase
                if (!isWanted && horizDistance < activeSpawnDist && horizDistance > ModSettings.SpotSpawnDistMin && verticalDiff < MaxVerticalSpawnOffset && !vehDict.ContainsKey(spot) && !isCoolingDown && !stolenPendingReset.Contains(spot))
                {
                    // --- FORWARD HEMISPHERE CHECK ---
                    // Prevent cars from spawning behind the camera to stop awkward blip pop-ins
                    Vector3 camDirHor = new Vector3(camDir.X, camDir.Y, 0f).Normalized;
                    Vector3 toSpotDirHor = new Vector3(spot.Position.X - camPos.X, spot.Position.Y - camPos.Y, 0f).Normalized;

                    // A 30-degree angle creates a 180-degree half-circle strictly in front of the camera
                    if (Vector3.Angle(camDirHor, toSpotDirHor) > 60f)
                    {
                        continue; // Spot is behind the player's view plane, skip the spawn
                    }
                    // ----

                    // If RNG spawns enabled, perform a single chance roll the first time the player enters the radius
                    if (ModSettings.RngSpots)
                    {
                        if (attemptedRng.Contains(spot))
                        {
                            // already attempted while inside this radius; skip further attempts until player leaves and re-enters
                            continue;
                        }

                        attemptedRng.Add(spot);

                        // DETERMINE ACTIVE RNG (Override vs Global)
                        int activeRngChance = (spot.CustomRngChance >= 0) ? spot.CustomRngChance : ModSettings.SpotRngChancePercent;

                        int roll = random.Next(0, 100);
                        if (roll >= activeRngChance)
                        {
                            // FAILED RNG: Do not apply a cooldown! 
                            // The player never saw it, so let them try again next time they re-enter the 200m radius.
                            continue;
                        }
                        // else allow spawn to proceed
                    }

                    string modelName = GetUniqueModel(spot);

                    if (modelName != null)
                    {
                        var vehicle = CreateNewVehicle(modelName, spot.Position, spot.Heading, spot);
                        if (vehicle != null)
                        {
                            vehDict[spot] = vehicle;

                            // BLIP CREATION REMOVED HERE: Handled dynamically in the Despawn Check below

                            CarMod.ApplyStyle(vehicle, spot.Behavior, modelName);
                        }
                    }
                }

                // If player moved out of spawn radius, reset RNG attempt flag so a new roll can occur on re-entry
                if (horizDistance >= activeSpawnDist && attemptedRng.Contains(spot))
                {
                    attemptedRng.Remove(spot);
                }

                // C. DESPAWN CHECK & DYNAMIC BLIP LOGIC
                if (vehDict.ContainsKey(spot))
                {
                    Vehicle car = vehDict[spot];
                    bool hasBlip = markerDict.ContainsKey(spot);

                    // --- 1. THE UI REVEAL TIER ---
                    // A. Reveal the static blip when you get close
                    if (horizDistance <= _blipRevealDist)
                    {
                        _discoveredSpots.Add(spot); // Mark as a valid encounter

                        if (!hasBlip && ModSettings.ParkedShowBlips && spot.ShowBlip)
                        {
                            CreateBlip(car, spot);
                        }
                    }
                    // B. Delete the blip when you back away
                    else if (hasBlip && horizDistance > _blipHideDist)
                    {
                        DeleteBlipForSpot(spot);
                    }

                    // --- 2. THE ULTIMATE PHYSICAL DESPAWN TIER ---
                    const float MaxVerticalDespawnOffset = 400f;

                    if (horizDistance > activeDespawnDist || verticalDiff > MaxVerticalDespawnOffset)
                    {
                        DeleteSpotResources(spot);

                        // ONLY apply the standard cooldown if the player actually discovered the car!
                        if (ModSettings.EnableSpotCooldowns && _discoveredSpots.Contains(spot))
                        {
                            spotCooldowns[spot] = Game.GameTime + ModSettings.SpotCooldown;
                        }

                        // Wipe the memory for the next organic spawn cycle
                        _discoveredSpots.Remove(spot);
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

                stolenPendingReset.Add(spot); // Adds the spot to the baseline reset distance tracker

                // Apply DOUBLED cooldown when stolen or destroyed
                if (ModSettings.EnableSpotCooldowns)
                {
                    spotCooldowns[spot] = Game.GameTime + (ModSettings.SpotCooldown * 2);
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
        stolenPendingReset.Clear();
        _discoveredSpots.Clear();
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
        stolenPendingReset.Clear();
        _discoveredSpots.Clear();
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
    public bool ShowBlip { get; set; }
    public int CustomRngChance { get; set; }

    public SpawnSpot(string id, Vector3 pos, float head, HashSet<string> list, SpawnBehavior behavior, HashSet<string> rareList = null, int rareChance = 0, float customRange = -1f, float customBuffer = 50f, bool showBlip = true, int customRng = -1)
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
        ShowBlip = showBlip;
        CustomRngChance = customRng;
    }
}