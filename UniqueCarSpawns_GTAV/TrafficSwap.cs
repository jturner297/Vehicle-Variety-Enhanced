using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

public class TrafficSwap : Script
{
    // =============================================================
    //                 TUNING DASHBOARD
    // =============================================================

    // Randomized Cooldown (15 to 40 seconds)
    private readonly int MinSpawnCooldown = 30000;
    private readonly int MaxSpawnCooldown = 75000;
    private readonly int CheckInterval = 250;

    // LIMITS
    private readonly int MaxActiveSwaps = 1;
    private readonly List<Vehicle> _activeSwaps = new List<Vehicle>();

    // TRACKERS
    private readonly HashSet<int> _lockedVehicles = new HashSet<int>(); // Prevents "Blinking"

    // VARIETY CONTROL
    private readonly List<string> _recentSpawnHistory = new List<string>();
    private readonly int _historyCapacity = 10;

    // SCORING
    private readonly float MaxSpawnDist = 250f; // Fixed: Expanded to allow room for the 250m visible cutoff
    private readonly float SpawnFOV = 60f;
    private readonly float ScoreThreshold = 400f; // Fixed: Raised to properly gate the inflated static bonuses

    // BONUSES
    private readonly float ScoreVisible = 50f;
    private readonly float ScoreSameRoad = 150f;
    private readonly float ScoreDeadAhead = 200f;

    // DEBUG & LOGGING
    private bool ShowBlips = true;
    private bool ShowVehicleNameOnBlips = false;
    private bool EnableFileLogging = false;
    private bool _debugMode = false;

    // =============================================================

    private readonly bool IgnoreEmergency = true;
    private readonly bool IgnoreService = true;
    private readonly bool IgnoreBig = true;

    private const string DECOR_NAME = "TMP_Swap_ID";
    private const string AMB_TAG = "Ambient_Swap_ID";

    private bool _isInMissionMode = false;

    private readonly VehicleDrivingFlags DriveStyle = (VehicleDrivingFlags)786603 | (VehicleDrivingFlags)262144;

    private int _nextCheckTime = 0;
    private int _nextSpawnTime = 0;


    private readonly HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private readonly HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };

    private readonly Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();
    private readonly List<Blip> _activeBlips = new List<Blip>();
    private readonly Random _rnd = new Random();

    private readonly HashSet<string> _urbanZones = new HashSet<string>
    {
        "AIRP", "PBOX", "TEXTI", "SKID", "DOWNT", "LOSPUER", "DELSOL", "KOREAT", "STAD", "LEGSQU",
        "VINE", "WVINE", "DTVINE", "BURTON", "HAWICK", "ALTA", "RGLEN", "CHIL", "BAYTRE", "GALLI", "OBSERV",
        "CHAMH", "DAVIS", "RANCHO", "STRAW", "BANNIN",
        "ROCKF", "RICHM", "MOVIE", "GOLF", "MORN", "VCANA", "VESP", "PBLUFF", "BHAMCA", "CHU", "DELPE",
        "MIRR", "EAST_V",
        "EBURO", "CYPRE", "LMESA", "MURRI", "PALHIGH", "TATAMO", "TERMINA", "ELYSIAN", "ZP_ORT"
    };

    public TrafficSwap()
    {
        Function.Call(Hash.DECOR_REGISTER, DECOR_NAME, 3);
        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);

        InitializeZones();
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (_debugMode) DrawDebugInfo();

        Ped player = Game.Player.Character;

        // --- SMART MISSION LOGIC ---
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);
        if (isMissionActive)
        {
            if (!_isInMissionMode)
            {
                ReleaseAllToGame(); // Handoff active swaps to the game
                _isInMissionMode = true; // Lock the door
            }
            return; // Stop all script logic during the mission
        }
        else
        {
            // Mission is over, reset the flag so we can spawn again
            if (_isInMissionMode)
            {
                _isInMissionMode = false;
                // Force a small cooldown so we don't start swapping immediately upon mission exit
                _nextSpawnTime = Game.GameTime + 5000;
                _nextCheckTime = Game.GameTime + 5000;
            }
        }

        // --- 0. SIGHT TRACKER (Prevent Blinking) ---
        Vehicle[] nearbyVehicles = World.GetNearbyVehicles(player.Position, 150f);
        foreach (Vehicle v in nearbyVehicles)
        {
            if (!v.Exists() || _lockedVehicles.Contains(v.Handle) || IsSwapped(v)) continue;

            // If it is on screen AND not occluded by the map/traffic, lock it in memory.
            if (v.IsOnScreen && !IsVehicleOccluded(v, GameplayCamera.Position))
            {
                _lockedVehicles.Add(v.Handle);
            }
        }

        // --- 1. REGISTRY CLEANUP ---
        for (int i = _activeSwaps.Count - 1; i >= 0; i--)
        {
            Vehicle v = _activeSwaps[i];

            if (!v.Exists() || v.IsDead)
            {
                _lockedVehicles.Remove(v.Handle);
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + _rnd.Next(MinSpawnCooldown, MaxSpawnCooldown);
                continue;
            }

            if (player.IsInVehicle(v))
            {
                if (v.AttachedBlip != null) v.AttachedBlip.Delete();
                v.MarkAsNoLongerNeeded();
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + _rnd.Next(MinSpawnCooldown, MaxSpawnCooldown);
                continue;
            }

            if (v.Position.DistanceTo(player.Position) > 270f)
            {
                if (v.AttachedBlip != null) v.AttachedBlip.Delete();
                //v.MarkAsNoLongerNeeded();

                // Delete all occupants before deleting the car so they don't drop to the road
                foreach (Ped occupant in v.Occupants)
                {
                    if (occupant != null && occupant.Exists())
                    {
                        occupant.Delete();
                    }
                }
                v.Delete();
                _lockedVehicles.Remove(v.Handle);
                _activeSwaps.RemoveAt(i);
                continue;
            }
        }

        if (Game.GameTime < _nextCheckTime) return;

        try
        {
            CleanupBlips();
            RunDirectorAI();
        }
        catch (Exception) { }

        _nextCheckTime = Game.GameTime + CheckInterval;
    }

    private void RunDirectorAI()
    {
        if (Game.GameTime < _nextSpawnTime) return;
        if (_activeSwaps.Count >= MaxActiveSwaps) return;

        Ped player = Game.Player.Character;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;
        Vector3 playerVel = player.Velocity;
        Vector3 playerRight = player.RightVector;

        int playerRoadID = GetVehicleNodeID(player.Position);

        Vehicle[] allVehicles = World.GetAllVehicles();
        Vehicle bestCandidate = null;
        float bestScore = 0f;

        float minimumDistanceBetweenSwaps = 150f;

        foreach (Vehicle v in allVehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer || IsSwapped(v)) continue;
            if (IsExcludedCategory(v)) continue;

            // Spatial Proximity Check
            bool isTooCloseToExistingSwap = false;
            foreach (Vehicle activeSwap in _activeSwaps)
            {
                if (activeSwap.Exists() && v.Position.DistanceTo(activeSwap.Position) < minimumDistanceBetweenSwaps)
                {
                    isTooCloseToExistingSwap = true;
                    break;
                }
            }

            if (isTooCloseToExistingSwap) continue;

            float score = GetCinematicScore(v, camPos, camDir, playerVel, playerRight, playerRoadID);

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = v;
            }
        }

        if (bestCandidate != null && bestScore > ScoreThreshold)
        {
            bool isDirt = IsVehicleOnDirt(bestCandidate);
            if (TransformVehicle(bestCandidate, isDirt))
            {
                // Randomized Cooldown
                _nextSpawnTime = Game.GameTime + _rnd.Next(MinSpawnCooldown, MaxSpawnCooldown);
            }
        }
    }

    private int GetVehicleNodeID(Vector3 pos)
    {
        return Function.Call<int>(Hash.GET_NTH_CLOSEST_VEHICLE_NODE_ID, pos.X, pos.Y, pos.Z, 1, 1, 1073741824, 0);
    }

    // --- MAIN SCORING LOGIC ---
    private float GetCinematicScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerVel, Vector3 playerRight, int playerRoadID)
    {
        float score = 0f;
        Vector3 vPos = v.Position;
        float dist = vPos.DistanceTo(camPos);

        // 1. CHEAP FILTERS
        if (dist < 60f || dist > MaxSpawnDist) return 0f;

        // Height Check 
        float heightDiff = Math.Abs(vPos.Z - camPos.Z);
        if (heightDiff > 15f) return 0f;

        // FOV & Direction Checks
        Vector3 toCarDir = (vPos - camPos).Normalized;
        if (Vector3.Angle(camDir, toCarDir) > SpawnFOV) return 0f;

        int carRoadID = GetVehicleNodeID(vPos);

        // Trajectory Check
        float movementDirection = Vector3.Dot(v.Velocity, toCarDir);
        if (movementDirection > 5f && carRoadID != playerRoadID) return 0f;

        // --- STATIC LATERAL MATH & SCORING ---
        bool isSameRoad = (playerRoadID != 0 && carRoadID == playerRoadID);

        // Calculate lateral distance
        float lateralDist = Math.Abs(Vector3.Dot((vPos - camPos), playerRight));

        // STATIC DEAD AHEAD PRIORITY
        // Widened to 20 meters to capture desirable cars slightly off-center 
        if (lateralDist < 20f)
        {
            score += ScoreDeadAhead;
        }

        if (isSameRoad) score += ScoreSameRoad;

        // 3. THE OCCLUSION DECISION
        bool isHidden = IsVehicleOccluded(v, camPos);

        if (isHidden && !_lockedVehicles.Contains(v.Handle))
        {
            // Massive stealth bonus ONLY if it's on our exact road (blind corners, hills)
            // OR if it's a cross-street directly in front of us (< 40m left/right).
            if (isSameRoad || lateralDist < 40f)
            {
                score += 500f;
            }
            else
            {
                // Hidden, but far off to the side on a parallel street.
                score += 50f;
            }
        }
        else
        {
            // FIXED: Visible cars must be at least 250m away to ensure a safe swap
            if (dist < 200f) return 0f;
            score += ScoreVisible;
        }

        score += (MaxSpawnDist - dist) * 0.5f;

        return score;
    }

    // --- STEALTH SWAP RAYCAST ---
    private bool IsVehicleOccluded(Vehicle v, Vector3 camPos)
    {
        IntersectFlags flags = IntersectFlags.Map | IntersectFlags.Vehicles;
        RaycastResult result = World.Raycast(camPos, v.Position, flags, v);
        return result.DidHit;
    }

    // --- TRANSFORMATION & ZONES ---
    private bool TransformVehicle(Vehicle oldVehicle, bool onDirt)
    {
        if (IsSwapped(oldVehicle)) return false;

        SelectionLayer layer;
        string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, oldVehicle.Position.X, oldVehicle.Position.Y, oldVehicle.Position.Z);
        bool isUrban = _urbanZones.Contains(currentZone);

        if (isUrban && onDirt) return false;

        if (onDirt && _zoneRegistry.ContainsKey("_OVERRIDE_OFFROAD_"))
        {
            layer = _zoneRegistry["_OVERRIDE_OFFROAD_"].PickLayer();
            layer.SourceProfile = "OFFROAD (Dirt Override)";
        }
        else { layer = GetLayerForLocation(oldVehicle.Position); }

        if (layer.List == null || layer.List.Count == 0) return false;

        string modelName = null;
        var candidates = layer.List.Except(_recentSpawnHistory).ToList();

        // --- OPTION #2: HISTORY CHOKE PURGE ---
        // If the history blocked every single car in this zone, 
        // clear the oldest half of the history to breathe life back into the candidates.
        if (candidates.Count == 0 && layer.List.Count > 1)
        {
            _recentSpawnHistory.RemoveRange(0, _recentSpawnHistory.Count / 2);
            candidates = layer.List.Except(_recentSpawnHistory).ToList();
        }
        // --------------------------------------



        if (candidates.Count > 0)
        {
            modelName = candidates[_rnd.Next(candidates.Count)];
        }
        else
        {
            modelName = layer.List.ElementAt(_rnd.Next(layer.List.Count));
        }

        if (modelName == null) return false;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return false;
        model.Request();

        int timeout = Game.GameTime + 1000;
        while (!model.IsLoaded && Game.GameTime < timeout)
        {
            Script.Yield();
            if (!oldVehicle.Exists()) { model.MarkAsNoLongerNeeded(); return false; }
        }
        if (!model.IsLoaded) { model.MarkAsNoLongerNeeded(); return false; }

        if (!oldVehicle.Exists() || IsSwapped(oldVehicle)) { model.MarkAsNoLongerNeeded(); return false; }

        Ped driver = oldVehicle.Driver;
        if (driver == null || !driver.Exists()) { model.MarkAsNoLongerNeeded(); return false; }

        Vector3 oldVelocity = oldVehicle.Velocity;
        float oldSpeed = oldVehicle.Speed;

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);
        Vehicle newVehicle = World.CreateVehicle(model, oldVehicle.Position, oldVehicle.Heading);

        if (newVehicle != null)
        {
            _activeSwaps.Add(newVehicle);
            _recentSpawnHistory.Add(modelName);
            if (_recentSpawnHistory.Count > _historyCapacity) _recentSpawnHistory.RemoveAt(0);

            newVehicle.IsEngineRunning = true;

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));

            Function.Call(Hash.DECOR_SET_INT, newVehicle, DECOR_NAME, 1);

            driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);

            _lockedVehicles.Remove(oldVehicle.Handle);

            // Delete any extra occupants so they don't get stranded on the road
            foreach (Ped occupant in oldVehicle.Occupants)
            {
                if (occupant != null && occupant.Exists() && occupant != driver)
                {
                    occupant.Delete();
                }
            }

            oldVehicle.Delete();

            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, newVehicle, true, 1);
            newVehicle.Velocity = oldVelocity;
            newVehicle.ForwardSpeed = oldSpeed;

            driver.BlockPermanentEvents = false;

            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, (int)DriveStyle);

            if (EnableFileLogging) LogSwap(layer.SourceProfile, modelName, layer.Behavior);
            if (ShowBlips || _debugMode) CreateBlip(newVehicle, modelName);

            //  newVehicle.MarkAsNoLongerNeeded();
            //   driver.MarkAsNoLongerNeeded();
            //   model.MarkAsNoLongerNeeded();

            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    private void InitializeZones()
    {
        ZoneProfile Hippy = new ZoneProfile("HIPSTER", _excludedModels);
        Hippy.AddIngredient("WACKY", VehList.models_wacky, SpawnBehavior.Beater, 2);
        Hippy.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Beater, 2);
        Hippy.AddIngredient("BEATER", VehList.models_beaters, SpawnBehavior.Beater, 3);
        Hippy.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Beater, 1);
        AssignToProfile(Hippy, "MIRR", "EAST_V");

        ZoneProfile Gangster = new ZoneProfile("GHETTO", _excludedModels);
        Gangster.AddIngredient("LOWRIDER", VehList.models_lowriders, SpawnBehavior.RandomSpec, 4);
        Gangster.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle, 3);
        AssignToProfile(Gangster, "CHAMH", "DAVIS", "RANCHO", "STRAW", "STAD");

        ZoneProfile Downtown = new ZoneProfile("DOWNTOWN", _excludedModels);
        Downtown.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 4);
        Downtown.AddIngredient("SUV", VehList.models_armoured, SpawnBehavior.VIP, 3);
        Downtown.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 1);
        Downtown.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Tuner, 3);
        Downtown.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle, 3);
        AssignToProfile(Downtown, "VINE", "PBOX", "TEXTI", "SKID", "DOWNT", "LOSPUER", "DELSOL", "KOREAT", "AIRP");

        ZoneProfile Vinewood = new ZoneProfile("VINEWOOD", _excludedModels);
        Vinewood.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 3);
        Vinewood.AddIngredient("CLASSIC", VehList.models_classics, SpawnBehavior.Spec, 3);
        Vinewood.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 2);
        Vinewood.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Tuner, 2);
        Vinewood.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle, 2);
        AssignToProfile(Vinewood, "WVINE", "DTVINE", "BURTON", "HAWICK", "ALTA");

        ZoneProfile Coastal = new ZoneProfile("COASTAL", _excludedModels);
        Coastal.AddIngredient("CLASSIC", VehList.models_classics, SpawnBehavior.Spec, 3);
        Coastal.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 1);
        AssignToProfile(Coastal, "VCANA", "VESP", "PBLUFF", "BHAMCA", "CHU", "DELPE");

        ZoneProfile Elite = new ZoneProfile("ELITE", _excludedModels);
        Elite.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 3);
        Elite.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 1);
        AssignToProfile(Elite, "ROCKF", "RICHM", "MOVIE", "GOLF", "MORN");

        ZoneProfile VinewoodHills = new ZoneProfile("HILLS", _excludedModels);
        VinewoodHills.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 1);
        VinewoodHills.AddIngredient("CLASSIC", VehList.models_classics, SpawnBehavior.Spec, 1);
        AssignToProfile(VinewoodHills, "RGLEN", "CHIL", "BAYTRE", "GALLI", "OBSERV");

        ZoneProfile Industry = new ZoneProfile("INDUSTRIAL", _excludedModels);
        Industry.AddIngredient("BEATER", VehList.models_beaters, SpawnBehavior.Beater, 3);
        Industry.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Beater, 2);
        Industry.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Beater, 2);
        AssignToProfile(Industry, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO", "TERMINA", "ELYSIAN");

        ZoneProfile CountrySide = new ZoneProfile("COUNTRYSIDE", _excludedModels);
        CountrySide.AddIngredient("BEATER", VehList.models_beaters, SpawnBehavior.Beater, 4);
        CountrySide.AddIngredient("OFFROAD", VehList.models_offroad, SpawnBehavior.Beater, 2);
        CountrySide.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Beater, 3);
        CountrySide.AddIngredient("WACKY", VehList.models_wacky, SpawnBehavior.RandomSpec, 1);
        AssignToProfile(CountrySide, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE", "TONGVAV", "SLAB");

        ZoneProfile offroadProfile = new ZoneProfile("OFFROAD", _excludedModels);
        offroadProfile.AddIngredient("OFFROAD", VehList.models_offroad, SpawnBehavior.Beater, 1);
        _zoneRegistry["_OVERRIDE_OFFROAD_"] = offroadProfile;
    }

    public class ZoneProfile
    {
        public string Name;
        private List<Ingredient> _ingredients = new List<Ingredient>();
        private Queue<Ingredient> _tokenDeck = new Queue<Ingredient>();
        private HashSet<string> _blacklist;
        private readonly Random _rnd = new Random();

        public ZoneProfile(string name, HashSet<string> blacklist) { Name = name; _blacklist = blacklist; }

        public void AddIngredient(string id, HashSet<string> list, SpawnBehavior behavior, int weight)
        {
            if (list == null || list.Count == 0) return;
            HashSet<string> filteredList = new HashSet<string>();
            foreach (string model in list) { if (_blacklist != null && _blacklist.Contains(model)) continue; filteredList.Add(model); }
            if (filteredList.Count > 0) _ingredients.Add(new Ingredient { Id = id, List = filteredList, Behavior = behavior, Weight = weight });
        }

        public SelectionLayer PickLayer()
        {
            if (_ingredients.Count == 0) return new SelectionLayer();
            if (_tokenDeck.Count == 0) RefillDeck();
            Ingredient selected = _tokenDeck.Dequeue();
            return new SelectionLayer { List = selected.List, Behavior = selected.Behavior, SourceProfile = this.Name };
        }

        private void RefillDeck()
        {
            List<Ingredient> freshTokens = new List<Ingredient>();
            foreach (var ing in _ingredients) { for (int i = 0; i < ing.Weight; i++) freshTokens.Add(ing); }
            int n = freshTokens.Count;
            while (n > 1) { n--; int k = _rnd.Next(n + 1); var value = freshTokens[k]; freshTokens[k] = freshTokens[n]; freshTokens[n] = value; }
            _tokenDeck = new Queue<Ingredient>(freshTokens);
        }

        private class Ingredient { public string Id; public HashSet<string> List; public SpawnBehavior Behavior; public int Weight; }
    }

    private void AssignToProfile(ZoneProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }
    private void LogSwap(string zoneName, string carModel, SpawnBehavior behavior) { try { File.AppendAllText("TrafficMP_SwapLog.txt", $"[{DateTime.Now:HH:mm:ss}] {zoneName}: {carModel} ({behavior}){Environment.NewLine}"); } catch { } }
    private SelectionLayer GetLayerForLocation(Vector3 pos) { string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z); if (string.IsNullOrEmpty(zone) || _bannedZones.Contains(zone)) return new SelectionLayer(); return _zoneRegistry.ContainsKey(zone) ? _zoneRegistry[zone].PickLayer() : new SelectionLayer(); }

    private bool IsExcludedCategory(Vehicle v)
    {
        if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane || v.ClassType == VehicleClass.Cycles || v.ClassType == VehicleClass.Motorcycles || v.IsPersistent || Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v) || v.PopulationType == EntityPopulationType.RandomScenario || IsSwapped(v)) return true;
        VehicleClass vc = v.ClassType;
        if (IgnoreEmergency && (vc == VehicleClass.Emergency || v.Driver.IsInPoliceVehicle)) return true;
        if (IgnoreService && (vc == VehicleClass.Service || vc == VehicleClass.Commercial || v.Model.IsBus || v.Model.Hash == unchecked((int)VehicleHash.Taxi))) return true;
        if (IgnoreBig && (vc == VehicleClass.Industrial || vc == VehicleClass.Utility || vc == VehicleClass.Military)) return true;
        return false;
    }

    private bool IsSwapped(Vehicle v)
    {
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, DECOR_NAME)) return true;
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG)) return true;
        return false;
    }

    private bool IsVehicleOnDirt(Vehicle v) { OutputArgument outDensity = new OutputArgument(); OutputArgument outFlags = new OutputArgument(); if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, v.Position.X, v.Position.Y, v.Position.Z, outDensity, outFlags)) { if ((outFlags.GetResult<int>() & (int)VehicleNodeFlags.Dirt) != 0) return true; } return false; }
    private void CreateBlip(Vehicle v, string modelKey)
    {
        Blip b = v.AddBlip(); Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
        b.Sprite = BlipSprite.Standard;
        b.Color = BlipColor.Blue; b.Scale = 0.7f;
        b.IsShortRange = false; Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, b, false);
        if (ShowVehicleNameOnBlips)
        {
            b.Name = Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v.Model.Hash));
        }
        else
        {
            b.Name = "Vehicle";
        }
        if (!_debugMode && !ShowBlips) b.Alpha = 0;
        _activeBlips.Add(b);
    }
    private void CleanupBlips() { 
        Ped player = Game.Player.Character; 
        for (int i = _activeBlips.Count - 1; i >= 0; i--) { Blip b = _activeBlips[i]; 
            if (!b.Exists() || b.Entity == null || !b.Entity.Exists() || player.IsInVehicle((Vehicle)b.Entity) || b.Entity.IsDead)
            { 
                if (b.Exists()) b.Delete(); 
                _activeBlips.RemoveAt(i);
            }
        } 
    }

    private void ReleaseAllToGame()
    {
        // 1. Delete Blips (Clean the map UI immediately)
        foreach (var blip in _activeBlips)
        {
            if (blip != null && blip.Exists()) blip.Delete();
        }
        _activeBlips.Clear();

        // 2. Release Vehicles (Don't delete! Just let the game manage them)
        foreach (var vehicle in _activeSwaps)
        {
            if (vehicle != null && vehicle.Exists())
            {
                vehicle.MarkAsNoLongerNeeded();
            }
        }

        // 3. Clear memory trackers
        _activeSwaps.Clear();
        _lockedVehicles.Clear();
    }
    private void DrawDebugInfo() { foreach (Vehicle v in World.GetAllVehicles()) { if (v.Exists() && IsSwapped(v) && v.IsOnScreen) World.DrawMarker(MarkerType.Chevron1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow); } }
    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e) { if (e.KeyCode == System.Windows.Forms.Keys.F11) { _debugMode = !_debugMode; GTA.UI.Notification.PostTicker($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true, false); foreach (var b in _activeBlips) if (b.Exists()) b.Alpha = _debugMode || ShowBlips ? 255 : 0; } }
    private void OnAborted(object sender, EventArgs e) { foreach (var b in _activeBlips) if (b.Exists()) b.Delete(); }
    public struct SelectionLayer { public HashSet<string> List; public SpawnBehavior Behavior; public string SourceProfile; }
    [Flags] public enum VehicleNodeFlags { None = 0, Dirt = 32 }
}