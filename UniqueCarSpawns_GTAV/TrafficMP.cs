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
    private float MinSpawnDist = 130f;
    private float MaxSpawnDist = 240f;
    private float SpawnFOV = 35f;
    private float ScoreThreshold = 50f;
    private float ScoreOncoming = 100f;
    private float ScoreOvertake = 20f;
    private float ScoreVisible = 50f;

    // DEBUG & LOGGING
    private bool ShowBlips = true;
    private bool EnableFileLogging = true;
    private bool _debugMode = false;

    // =============================================================

    private bool IgnoreEmergency = true;
    private bool IgnoreService = true;
    private bool IgnoreBig = true;

    private const string DECOR_NAME = "TMP_Swap_ID";     // My Tag (Blue Blip)
    private const string AMB_TAG = "Ambient_Swap_ID";    // Enhanced Tag (Yellow Blip)

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

        // --- 1. REGISTRY CLEANUP (The "Handoff") ---
        for (int i = _activeSwaps.Count - 1; i >= 0; i--)
        {
            Vehicle v = _activeSwaps[i];

            // Clean up invalid vehicles
            if (!v.Exists())
            {
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + SpawnCooldown;
                continue;
            }

            // Handoff if player steals it
            if (player.IsInVehicle(v))
            {
                if (v.AttachedBlip != null) v.AttachedBlip.Delete();
                v.MarkAsNoLongerNeeded();
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + SpawnCooldown;
                if (_debugMode) GTA.UI.Notification.PostTicker("~b~TrafficMP: Car stolen. Handoff complete.", true, false);
                continue;
            }

            // DISTANCE CLEANUP: If the car drives too far away, forget it so we can spawn a new one.
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

        if (_activeSwaps.Count >= MaxActiveSwaps)
        {
            _nextSpawnTime = Game.GameTime + 1000;
            return;
        }

        Ped player = Game.Player.Character;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;
        Vector3 playerVel = player.Velocity;
        Vector3 playerDir = player.ForwardVector;

        Vehicle[] allVehicles = World.GetAllVehicles();

        Vehicle bestCandidate = null;
        float bestScore = 0f;

        foreach (Vehicle v in allVehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer || IsSwapped(v)) continue;

            if (IsExcludedCategory(v)) continue;

            float score = GetCinematicScore(v, camPos, camDir, playerDir, playerVel);

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
            else
            {
                _nextSpawnTime = Game.GameTime + 1000;
            }
        }
    }

    private bool TransformVehicle(Vehicle oldVehicle, bool onDirt)
    {
        // 1. Double check before we do any heavy lifting
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
        else
        {
            layer = GetLayerForLocation(oldVehicle.Position);
        }

        if (layer.List == null || layer.List.Count == 0) return false;

        string modelName = null;
        int attempts = 0;

        while (attempts < 3)
        {
            string candidate = VehicleSelector.GetNext(layer.List, "Traffic");
            if (candidate == null) break;

            if (!_recentSpawnHistory.Contains(candidate))
            {
                modelName = candidate;
                break;
            }
            attempts++;
        }
        if (modelName == null) modelName = VehicleSelector.GetNext(layer.List, "Traffic");
        if (modelName == null) return false;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return false;

        model.Request();
        int timeout = Game.GameTime + 1000;
        
        // SAFE YIELDING: Check if oldVehicle is invalid during the wait
        while (!model.IsLoaded && Game.GameTime < timeout)
        {
            Script.Yield();
            if (!oldVehicle.Exists()) { model.MarkAsNoLongerNeeded(); return false; }
        }
        
        if (!model.IsLoaded) { model.MarkAsNoLongerNeeded(); return false; }

        // 2. Final Sanity Check before modification
        if (!oldVehicle.Exists()) { model.MarkAsNoLongerNeeded(); return false; }

        // 3. RACE CONDITION CHECK: Did TrafficEnhanced steal it while we were loading the model?
        if (IsSwapped(oldVehicle)) { model.MarkAsNoLongerNeeded(); return false; }

        Ped driver = oldVehicle.Driver;
        if (driver == null || !driver.Exists()) { model.MarkAsNoLongerNeeded(); return false; }

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);
        Vehicle newVehicle = World.CreateVehicle(model, oldVehicle.Position, oldVehicle.Heading);

        if (newVehicle != null)
        {
            _activeSwaps.Add(newVehicle);
            _recentSpawnHistory.Add(modelName);
            if (_recentSpawnHistory.Count > _historyCapacity) _recentSpawnHistory.RemoveAt(0);

            // Transfer basics
            newVehicle.IsEngineRunning = true;
            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));

            // MARK AS MP SWAP IMMEDIATELY
            Function.Call(Hash.DECOR_SET_INT, newVehicle, DECOR_NAME, 1);

            driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);
            oldVehicle.Delete();

            // APPLY MODS
            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            // --- SUSPENSION FIX START ---
            // 1. Force physics to activate so suspension compresses NOW, not later.
            Function.Call(Hash.ACTIVATE_PHYSICS, newVehicle);
            Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, newVehicle, true, 1);
            
            // 2. Place on ground properly (this calculates height based on the NEW suspension limits)
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, newVehicle);
            
            // 3. Re-apply velocity AFTER settling to ensure it doesn't lose momentum
            newVehicle.Velocity = oldVehicle.Velocity;
            newVehicle.ForwardSpeed = oldVehicle.Speed;
            // --- SUSPENSION FIX END ---

            driver.BlockPermanentEvents = true;
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, DriveStyle);

            if (EnableFileLogging) LogSwap(layer.SourceProfile, modelName, layer.Behavior);
            if (ShowBlips || _debugMode) CreateBlip(newVehicle, modelName);
            if (_debugMode) GTA.UI.Notification.PostTicker($"~y~Swap: {modelName}", true, false);

            newVehicle.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();
            model.MarkAsNoLongerNeeded();

            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    // ... [Zone Initialization Code Omitted for Brevity - It remains the same] ...
    private void InitializeZones()
    {
        // 1. HIPSTER (Mirror Park) - Dominant: Beater (3), Wacky (2)
        ZoneProfile Hippy = new ZoneProfile("HIPSTER", _excludedModels);
        Hippy.AddIngredient("WACKY", VehList.models_wacky, SpawnBehavior.Beater, 2);
        Hippy.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Beater, 2);
        Hippy.AddIngredient("BEATER", VehList.models_beaters, SpawnBehavior.Beater, 3);
        Hippy.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Beater, 1);
        AssignToProfile(Hippy, "MIRR", "EAST_V");

        // 2. GANGSTER (South LS) - Dominant: Lowrider (4)
        ZoneProfile Gangster = new ZoneProfile("GHETTO", _excludedModels);
        Gangster.AddIngredient("LOWRIDER", VehList.models_lowriders, SpawnBehavior.RandomSpec, 4);
        Gangster.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle, 3);
        AssignToProfile(Gangster, "CHAMH", "DAVIS", "RANCHO", "STRAW", "STAD");

        // 3. DOWNTOWN - Dominant: Luxury (4)
        ZoneProfile Downtown = new ZoneProfile("DOWNTOWN", _excludedModels);
        Downtown.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 4);
        Downtown.AddIngredient("SUV", VehList.models_armoured, SpawnBehavior.VIP, 3);
        Downtown.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 1);
        Downtown.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Tuner, 3);
        Downtown.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle, 3);
        AssignToProfile(Downtown, "VINE", "PBOX", "TEXTI", "SKID", "DOWNT", "LOSPUER", "DELSOL", "KOREAT", "AIRP");

        // 4. VINEWOOD - Balanced High Life
        ZoneProfile Vinewood = new ZoneProfile("VINEWOOD", _excludedModels);
        Vinewood.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 3);
        Vinewood.AddIngredient("CLASSIC", VehList.models_classics, SpawnBehavior.Spec, 3);
        Vinewood.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 2);
        Vinewood.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Tuner, 2);
        Vinewood.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle, 2);
        AssignToProfile(Vinewood, "WVINE", "DTVINE", "BURTON", "HAWICK", "ALTA");

        // 5. COASTAL
        ZoneProfile Coastal = new ZoneProfile("COASTAL", _excludedModels);
        Coastal.AddIngredient("CLASSIC", VehList.models_classics, SpawnBehavior.Spec, 3);
        Coastal.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 1);
        AssignToProfile(Coastal, "VCANA", "VESP", "PBLUFF", "BHAMCA", "CHU", "DELPE");

        // 6. ELITE 
        ZoneProfile Elite = new ZoneProfile("ELITE", _excludedModels);
        Elite.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 3);
        Elite.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP, 1);
        AssignToProfile(Elite, "ROCKF", "RICHM", "MOVIE", "GOLF", "MORN");

        // 7. HILLS 
        ZoneProfile VinewoodHills = new ZoneProfile("HILLS", _excludedModels);
        VinewoodHills.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec, 1);
        VinewoodHills.AddIngredient("CLASSIC", VehList.models_classics, SpawnBehavior.Spec, 1);
        AssignToProfile(VinewoodHills, "RGLEN", "CHIL", "BAYTRE", "GALLI", "OBSERV");

        // 8. INDUSTRIAL
        ZoneProfile Industry = new ZoneProfile("INDUSTRIAL", _excludedModels);
        Industry.AddIngredient("BEATER", VehList.models_beaters, SpawnBehavior.Beater, 3);
        Industry.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Beater, 2);
        Industry.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Beater, 2);
        AssignToProfile(Industry, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO", "TERMINA", "ELYSIAN");

        // 9. COUNTRYSIDE
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

    // --- FIX: Strict Decorator Check ---
    private bool IsSwapped(Vehicle v)
    {
        // 1. Is it a TrafficMP car?
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, DECOR_NAME)) return true;
        // 2. Is it a TrafficEnhanced car? (This stops stealing)
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG)) return true;
        return false;
    }

    private bool IsVehicleOnDirt(Vehicle v) { OutputArgument outDensity = new OutputArgument(); OutputArgument outFlags = new OutputArgument(); if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, v.Position.X, v.Position.Y, v.Position.Z, outDensity, outFlags)) { if ((outFlags.GetResult<int>() & (int)VehicleNodeFlags.Dirt) != 0) return true; } return false; }
    private float GetCinematicScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerDir, Vector3 playerVel) { float score = 0f; float dist = v.Position.DistanceTo(camPos); if (dist < MinSpawnDist || dist > MaxSpawnDist) return 0f; Vector3 toCar = (v.Position - camPos).Normalized; if (Vector3.Angle(camDir, toCar) > SpawnFOV && Vector3.Angle(playerDir, toCar) > SpawnFOV) return 0f; float closingSpeed = Vector3.Dot(v.Velocity.Normalized, playerVel.Normalized); if (closingSpeed < -0.5f) score += ScoreOncoming; else if (closingSpeed > 0.5f) score += ScoreOvertake; if (v.IsOnScreen && !World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.5f), IntersectFlags.Map).DidHit) score += ScoreVisible; score += dist * 0.5f; return score; }
    private void CreateBlip(Vehicle v, string modelKey) { Blip b = v.AddBlip(); b.Sprite = BlipSprite.Standard; b.Color = BlipColor.Blue; b.Scale = 0.7f; b.IsShortRange = true; Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, b, false); b.Name = Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v.Model.Hash)); if (!_debugMode && !ShowBlips) b.Alpha = 0; _activeBlips.Add(b); }
    private void CleanupBlips() { Ped player = Game.Player.Character; for (int i = _activeBlips.Count - 1; i >= 0; i--) { Blip b = _activeBlips[i]; if (!b.Exists() || b.Entity == null || !b.Entity.Exists() || player.IsInVehicle((Vehicle)b.Entity)) { if (b.Exists()) b.Delete(); _activeBlips.RemoveAt(i); } } }
    private void DrawDebugInfo() { foreach (Vehicle v in World.GetAllVehicles()) { if (v.Exists() && IsSwapped(v) && v.IsOnScreen) World.DrawMarker(MarkerType.Chevron1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow); } }
    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e) { if (e.KeyCode == System.Windows.Forms.Keys.F11) { _debugMode = !_debugMode; GTA.UI.Notification.PostTicker($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true, false); foreach (var b in _activeBlips) if (b.Exists()) b.Alpha = _debugMode || ShowBlips ? 255 : 0; } }
    private void OnAborted(object sender, EventArgs e) { foreach (var b in _activeBlips) if (b.Exists()) b.Delete(); }
    public struct SelectionLayer { public HashSet<string> List; public SpawnBehavior Behavior; public string SourceProfile; }
    [Flags] public enum VehicleNodeFlags { None = 0, Dirt = 32 }
}