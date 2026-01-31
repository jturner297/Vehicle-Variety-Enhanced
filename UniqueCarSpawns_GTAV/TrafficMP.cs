using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;
using System.Drawing;

public class TrafficMP : Script
{
    // =============================================================
    //                 TUNING DASHBOARD
    // =============================================================

    private int SpawnCooldown = 2500;
    private int CheckInterval = 250;

    // LIMITS
    private int MaxActiveSwaps = 2;
    private List<Vehicle> _activeSwaps = new List<Vehicle>();

    // VARIETY CONTROL
    private List<string> _recentSpawnHistory = new List<string>();
    private int _historyCapacity = 10;

    // SCORING
    private float MinSpawnDist = 130;
    private float MaxSpawnDist = 240f;
    private float SpawnFOV = 60f; // Wide FOV for windy roads
    private float ScoreThreshold = 50f;

    // BONUSES
    private float ScoreOncoming = 100f;
    private float ScoreOvertake = 20f;
    private float ScoreVisible = 50f;
    private float ScoreSameRoad = 150f;

    // DEBUG & LOGGING
    private bool ShowBlips = true;
    private bool EnableFileLogging = true;
    private bool _debugMode = false;

    // =============================================================

    private bool IgnoreEmergency = true;
    private bool IgnoreService = true;
    private bool IgnoreBig = true;

    private const string DECOR_NAME = "TMP_Swap_ID";
    private const string AMB_TAG = "Ambient_Swap_ID";

    private VehicleDrivingFlags DriveStyle = (VehicleDrivingFlags)786603 | (VehicleDrivingFlags)262144;

    private int _nextCheckTime = 0;
    private int _nextSpawnTime = 0;

    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };

    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();
    private List<Blip> _activeBlips = new List<Blip>();
    private Random _rnd = new Random();

    private HashSet<string> _urbanZones = new HashSet<string>
    {
        "AIRP", "PBOX", "TEXTI", "SKID", "DOWNT", "LOSPUER", "DELSOL", "KOREAT", "STAD", "LEGSQU",
        "VINE", "WVINE", "DTVINE", "BURTON", "HAWICK", "ALTA", "RGLEN", "CHIL", "BAYTRE", "GALLI", "OBSERV",
        "CHAMH", "DAVIS", "RANCHO", "STRAW", "BANNIN",
        "ROCKF", "RICHM", "MOVIE", "GOLF", "MORN", "VCANA", "VESP", "PBLUFF", "BHAMCA", "CHU", "DELPE",
        "MIRR", "EAST_V",
        "EBURO", "CYPRE", "LMESA", "MURRI", "PALHIGH", "TATAMO", "TERMINA", "ELYSIAN", "ZP_ORT"
    };

    public TrafficMP()
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

        // --- 1. REGISTRY CLEANUP ---
        for (int i = _activeSwaps.Count - 1; i >= 0; i--)
        {
            Vehicle v = _activeSwaps[i];

            if (!v.Exists())
            {
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + SpawnCooldown;
                continue;
            }

            if (player.IsInVehicle(v))
            {
                if (v.AttachedBlip != null) v.AttachedBlip.Delete();
                v.MarkAsNoLongerNeeded();
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + SpawnCooldown;
                continue;
            }

            if (v.Position.DistanceTo(player.Position) > 400f)
            {
                if (v.AttachedBlip != null) v.AttachedBlip.Delete();
                v.MarkAsNoLongerNeeded();
                _activeSwaps.RemoveAt(i);
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
        Vector3 playerDir = player.ForwardVector;

        // Get Player Road ID for comparison
        int playerRoadID = GetVehicleNodeID(player.Position);

        Vehicle[] allVehicles = World.GetAllVehicles();
        Vehicle bestCandidate = null;
        float bestScore = 0f;

        foreach (Vehicle v in allVehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer || IsSwapped(v)) continue;
            if (IsExcludedCategory(v)) continue;

            float score = GetCinematicScore(v, camPos, camDir, playerDir, playerVel, playerRoadID);

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
                _nextSpawnTime = Game.GameTime + SpawnCooldown;
            }
        }
    }

    // --- HELPER: Get Road ID ---
    private int GetVehicleNodeID(Vector3 pos)
    {
        return Function.Call<int>(Hash.GET_NTH_CLOSEST_VEHICLE_NODE_ID, pos.X, pos.Y, pos.Z, 1, 1, 1073741824, 0);
    }

    // --- MAIN SCORING LOGIC ---
    private float GetCinematicScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerDir, Vector3 playerVel, int playerRoadID)
    {
        float score = 0f;
        Vector3 vPos = v.Position;
        float dist = vPos.DistanceTo(camPos);

        // 1. CHEAP FILTERS (Fail Fast)
        if (dist < MinSpawnDist || dist > MaxSpawnDist) return 0f;

        // Slope Check (Replaces strict Z check for hills)
        float heightDiff = Math.Abs(vPos.Z - camPos.Z);
        double slopeAngle = Math.Atan2(heightDiff, dist) * (180 / Math.PI);
        if (slopeAngle > 45) return 0f; // Only reject if it's practically vertical (bridges)

        // FOV Check
        Vector3 toCar = (vPos - camPos).Normalized;
        if (Vector3.Angle(camDir, toCar) > SpawnFOV) return 0f;

        // 2. LOGIC BONUSES

        // Same Road Bonus
        int carRoadID = GetVehicleNodeID(vPos);
        if (playerRoadID != 0 && carRoadID == playerRoadID) score += ScoreSameRoad;

        // Movement Bonus
        float closingSpeed = Vector3.Dot(v.Velocity.Normalized, playerVel.Normalized);
        if (closingSpeed < -0.5f) score += ScoreOncoming;
        else if (closingSpeed > 0.5f) score += ScoreOvertake;

        // 3. EXPENSIVE VISIBILITY CHECK (The "Super Raycast")
        // Only run this if we are serious about this car
        if (IsVehicleVisibleSmart(v, camPos))
        {
            score += ScoreVisible;
        }
        else
        {
            return 0f; // If invisible, it's worthless
        }

        score += dist * 0.5f;
        return score;
    }

    // --- SUPER RAYCAST: Cascading Checks ---
    private bool IsVehicleVisibleSmart(Vehicle v, Vector3 camPos)
    {
        if (!v.IsOnScreen) return false;

        Vector3 min, max;
        v.Model.GetDimensions(out min, out max);

        // 1. Check Roof (Most likely)
        Vector3 roof = v.GetOffsetPosition(new Vector3(0, 0, max.Z + 0.1f));
        if (!World.Raycast(camPos, roof, IntersectFlags.Map).DidHit) return true;

        // 2. Check Bumpers (Peekers)
        Vector3 front = v.GetOffsetPosition(new Vector3(0, max.Y, 0.5f));
        if (!World.Raycast(camPos, front, IntersectFlags.Map).DidHit) return true;

        Vector3 rear = v.GetOffsetPosition(new Vector3(0, min.Y, 0.5f));
        if (!World.Raycast(camPos, rear, IntersectFlags.Map).DidHit) return true;

        // 3. Check Sides (Cross traffic)
        Vector3 left = v.GetOffsetPosition(new Vector3(min.X, 0, 0.5f));
        if (!World.Raycast(camPos, left, IntersectFlags.Map).DidHit) return true;

        Vector3 right = v.GetOffsetPosition(new Vector3(max.X, 0, 0.5f));
        if (!World.Raycast(camPos, right, IntersectFlags.Map).DidHit) return true;

        return false;
    }

    // --- TRANSFORMATION & ZONES (Unchanged) ---
    private bool TransformVehicle(Vehicle oldVehicle, bool onDirt)
    {
        // 1. Safety Checks
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

        // --- NEW HISTORY LOGIC START ---
        string modelName = null;

        // Create a list of Valid Candidates by subtracting History from the Full List
        // This guarantees we never pick a history car if a fresh one exists
        var candidates = layer.List.Except(_recentSpawnHistory).ToList();

        if (candidates.Count > 0)
        {
            // Pick a random car from the SAFE list
            modelName = candidates[_rnd.Next(candidates.Count)];
        }
        else
        {
            // Fallback: If we have seen EVERY car in this category recently,
            // we have no choice but to pick a random one from the full list.
            modelName = layer.List.ElementAt(_rnd.Next(layer.List.Count));
        }
        // --- NEW HISTORY LOGIC END ---

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

        // Final validation
        if (!oldVehicle.Exists() || IsSwapped(oldVehicle)) { model.MarkAsNoLongerNeeded(); return false; }

        Ped driver = oldVehicle.Driver;
        if (driver == null || !driver.Exists()) { model.MarkAsNoLongerNeeded(); return false; }

        // 2. CAPTURE OLD PHYSICS STATE
        Vector3 oldVelocity = oldVehicle.Velocity;
        float oldSpeed = oldVehicle.Speed;

        // 3. THE SWAP
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
            oldVehicle.Delete();

            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            // --- PHYSICS FIX ---
            Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, newVehicle, true, 1);
            newVehicle.Velocity = oldVelocity;
            newVehicle.ForwardSpeed = oldSpeed;

            driver.BlockPermanentEvents = true;
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, DriveStyle);

            if (EnableFileLogging) LogSwap(layer.SourceProfile, modelName, layer.Behavior);
            if (ShowBlips || _debugMode) CreateBlip(newVehicle, modelName);

            newVehicle.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();
            model.MarkAsNoLongerNeeded();

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
        private Random _rnd = new Random();

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
    private void CreateBlip(Vehicle v, string modelKey) { Blip b = v.AddBlip(); b.Sprite = BlipSprite.Standard; b.Color = BlipColor.Blue; b.Scale = 0.7f; b.IsShortRange = true; Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, b, false); b.Name = Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v.Model.Hash)); if (!_debugMode && !ShowBlips) b.Alpha = 0; _activeBlips.Add(b); }
    private void CleanupBlips() { Ped player = Game.Player.Character; for (int i = _activeBlips.Count - 1; i >= 0; i--) { Blip b = _activeBlips[i]; if (!b.Exists() || b.Entity == null || !b.Entity.Exists() || player.IsInVehicle((Vehicle)b.Entity)) { if (b.Exists()) b.Delete(); _activeBlips.RemoveAt(i); } } }
    private void DrawDebugInfo() { foreach (Vehicle v in World.GetAllVehicles()) { if (v.Exists() && IsSwapped(v) && v.IsOnScreen) World.DrawMarker(MarkerType.Chevron1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow); } }
    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e) { if (e.KeyCode == System.Windows.Forms.Keys.F11) { _debugMode = !_debugMode; GTA.UI.Notification.PostTicker($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true, false); foreach (var b in _activeBlips) if (b.Exists()) b.Alpha = _debugMode || ShowBlips ? 255 : 0; } }
    private void OnAborted(object sender, EventArgs e) { foreach (var b in _activeBlips) if (b.Exists()) b.Delete(); }
    public struct SelectionLayer { public HashSet<string> List; public SpawnBehavior Behavior; public string SourceProfile; }
    [Flags] public enum VehicleNodeFlags { None = 0, Dirt = 32 }
}