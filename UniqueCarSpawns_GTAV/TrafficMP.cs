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

    private int SpawnCooldown = 10000;
    private int MaxVisibleHeroes = 1;

    private float MinSpawnDist = 130f;
    private float MaxSpawnDist = 240f;
    private float SpawnFOV = 35f;

    private float ScoreThreshold = 50f;
    private float ScoreOncoming = 100f;
    private float ScoreOvertake = 20f;
    private float ScoreVisible = 50f;

    private int CheckInterval = 250;
    private bool ShowBlips = true;
    private bool EnableFileLogging = true;

    // =============================================================

    private bool IgnoreEmergency = true;
    private bool IgnoreService = true;
    private bool IgnoreBig = true;

    private const string DECOR_NAME = "TMP_Swap_ID";
    private VehicleDrivingFlags DriveStyle = (VehicleDrivingFlags)786603 | (VehicleDrivingFlags)262144;

    private int _nextCheckTime = 0;
    private int _nextSpawnTime = 0;

    private bool _debugMode = false;

    // RESTORED: This list protects Traffic from spawning Parked-Only cars
    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };

    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };
    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();

    private List<Blip> _activeBlips = new List<Blip>();
    private Random _rnd = new Random();

    public TrafficMP()
    {
        Function.Call(Hash.DECOR_REGISTER, DECOR_NAME, 3);
        InitializeZones();
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (_debugMode) DrawDebugInfo();
        if (Game.GameTime < _nextCheckTime) return;

        try
        {
            CleanupBlips();
            RunDirectorAI();
        }
        catch (Exception) { }

        _nextCheckTime = Game.GameTime + CheckInterval;
    }

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.F11)
        {
            _debugMode = !_debugMode;
            GTA.UI.Notification.PostTicker($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true, false);
            foreach (var b in _activeBlips) if (b.Exists()) b.Alpha = _debugMode || ShowBlips ? 255 : 0;
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var b in _activeBlips) if (b.Exists()) b.Delete();
    }

    // ==========================================
    //           THE DIRECTOR A.I.
    // ==========================================
    private void RunDirectorAI()
    {
        if (Game.GameTime < _nextSpawnTime) return;

        Ped player = Game.Player.Character;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;
        Vector3 playerVel = player.Velocity;
        Vector3 playerDir = player.ForwardVector;

        Vehicle[] allVehicles = World.GetAllVehicles();

        // 1. SCENE CHECK
        int heroesOnSet = 0;
        foreach (Vehicle v in allVehicles)
        {
            if (v.Exists() && IsSwapped(v))
            {
                if (v.IsOnScreen || v.Position.DistanceTo(player.Position) < MinSpawnDist)
                {
                    heroesOnSet++;
                }
            }
        }

        if (heroesOnSet >= MaxVisibleHeroes)
        {
            _nextSpawnTime = Game.GameTime + SpawnCooldown;
            return;
        }

        // 2. CASTING CALL
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

        // 3. ACTION
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

    // ==========================================
    //           SELECTION LOGIC
    // ==========================================
    private bool TransformVehicle(Vehicle oldVehicle, bool onDirt)
    {
        SelectionLayer layer;

        if (onDirt && _zoneRegistry.ContainsKey("_OVERRIDE_OFFROAD_"))
        {
            layer = _zoneRegistry["_OVERRIDE_OFFROAD_"].PickLayer();
            layer.SourceProfile = "OFFROAD (Dirt Override)";
            if (_debugMode) GTA.UI.Notification.PostTicker("~o~TrafficMP: Dirt Road Override Triggered", true, false);
        }
        else
        {
            layer = GetLayerForLocation(oldVehicle.Position);
        }

        if (layer.List == null || layer.List.Count == 0) return false;

        // The list returned here is already filtered and shuffled by ZoneProfile/VehicleSelector
        string modelName = layer.List.First();

        Model model = new Model(modelName);

        if (!model.IsValid || !model.IsInCdImage) return false;

        model.Request();
        int timeout = Game.GameTime + 1000;
        while (!model.IsLoaded && Game.GameTime < timeout)
        {
            Script.Yield();
        }

        if (!model.IsLoaded)
        {
            model.MarkAsNoLongerNeeded();
            return false;
        }

        // --- SPAWN LOGIC ---
        Ped driver = oldVehicle.Driver;
        if (driver == null || !driver.Exists())
        {
            model.MarkAsNoLongerNeeded();
            return false;
        }

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);

        Vehicle newVehicle = World.CreateVehicle(model, oldVehicle.Position, oldVehicle.Heading);

        if (newVehicle != null)
        {
            newVehicle.Velocity = oldVehicle.Velocity;
            newVehicle.ForwardSpeed = oldVehicle.Speed;
            newVehicle.IsEngineRunning = true;

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));

            Function.Call(Hash.DECOR_SET_INT, newVehicle, DECOR_NAME, 1);

            driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);
            oldVehicle.Delete();

            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            driver.BlockPermanentEvents = true;
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, DriveStyle);

            if (EnableFileLogging) LogSwap(layer.SourceProfile, modelName, layer.Behavior);
            if (ShowBlips || _debugMode) CreateBlip(newVehicle, modelName);
            if (_debugMode) GTA.UI.Notification.PostTicker($"~y~Swap: {modelName}", true, false);

            newVehicle.PlaceOnGround();
            newVehicle.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();
            model.MarkAsNoLongerNeeded();

            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    private void LogSwap(string zoneName, string carModel, SpawnBehavior behavior)
    {
        try
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{time}] ZONE: {zoneName} | CAR: {carModel} | TYPE: {behavior}";
            File.AppendAllText("TrafficMP_SwapLog.txt", line + Environment.NewLine);
        }
        catch (Exception) { }
    }

    private SelectionLayer GetLayerForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (string.IsNullOrEmpty(zone) || _bannedZones.Contains(zone)) return new SelectionLayer();

        if (_zoneRegistry.ContainsKey(zone)) return _zoneRegistry[zone].PickLayer();
        return new SelectionLayer();
    }

    private bool IsExcludedCategory(Vehicle v)
    {
        if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) return true;
        if (v.ClassType == VehicleClass.Cycles) return true;
        if (v.IsPersistent) return true;
        if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) return true;
        if (v.PopulationType == EntityPopulationType.RandomScenario) return true;
        if (IsSwapped(v)) return true;

        VehicleClass vc = v.ClassType;
        if (IgnoreEmergency && (vc == VehicleClass.Emergency || v.Driver.IsInPoliceVehicle)) return true;
        if (IgnoreService && (vc == VehicleClass.Service || vc == VehicleClass.Commercial || v.Model.IsBus)) return true;
        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        if (IgnoreBig && (vc == VehicleClass.Industrial || vc == VehicleClass.Utility || vc == VehicleClass.Military)) return true;

        return false;
    }

    private bool IsSwapped(Vehicle v)
    {
        return Function.Call<bool>(Hash.DECOR_EXIST_ON, v, DECOR_NAME);
    }

    private bool IsVehicleOnDirt(Vehicle v)
    {
        OutputArgument outDensity = new OutputArgument();
        OutputArgument outFlags = new OutputArgument();
        if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, v.Position.X, v.Position.Y, v.Position.Z, outDensity, outFlags))
        {
            int flags = outFlags.GetResult<int>();
            if ((flags & (int)VehicleNodeFlags.Dirt) != 0) return true;
        }
        return false;
    }

    private float GetCinematicScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerDir, Vector3 playerVel)
    {
        float score = 0f;
        float dist = v.Position.DistanceTo(camPos);
        if (dist < MinSpawnDist) return 0f;
        if (dist > MaxSpawnDist) return 0f;

        Vector3 toCar = (v.Position - camPos).Normalized;
        float angleCam = Vector3.Angle(camDir, toCar);
        float anglePlayer = Vector3.Angle(playerDir, toCar);

        if (angleCam > SpawnFOV && anglePlayer > SpawnFOV) return 0f;

        float closingSpeed = Vector3.Dot(v.Velocity.Normalized, playerVel.Normalized);
        if (closingSpeed < -0.5f) score += ScoreOncoming;
        else if (closingSpeed > 0.5f) score += ScoreOvertake;

        bool visible = !World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.5f), IntersectFlags.Map).DidHit;
        if (!visible) return 0f;

        if (v.IsOnScreen) score += ScoreVisible;
        score += dist * 0.5f;

        return score;
    }

    private void CreateBlip(Vehicle v, string modelKey)
    {
        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.Standard;
        b.Color = BlipColor.Blue;
        b.Scale = 0.7f;
        b.IsShortRange = true;
        Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, b, false);
        string gxtLabel = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v.Model.Hash);
        string friendlyName = Game.GetLocalizedString(gxtLabel);
        b.Name = (string.IsNullOrEmpty(friendlyName) || friendlyName.ToUpper() == "NULL") ? "Vehicle" : friendlyName;
        if (!_debugMode && !ShowBlips) b.Alpha = 0;
        _activeBlips.Add(b);
    }

    private void CleanupBlips()
    {
        Ped player = Game.Player.Character;
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            Blip b = _activeBlips[i];
            if (!b.Exists() || b.Entity == null || !b.Entity.Exists() || player.IsInVehicle((Vehicle)b.Entity))
            {
                if (b.Exists()) b.Delete();
                _activeBlips.RemoveAt(i);
            }
        }
    }

    private void DrawDebugInfo()
    {
        foreach (Vehicle v in World.GetAllVehicles())
        {
            if (v.Exists() && IsSwapped(v) && v.IsOnScreen)
            {
                World.DrawMarker(MarkerType.Chevron1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow);
            }
        }
    }

 

    private void InitializeZones()
    {
        // 1. RURAL PROFILE (Smart Mixing)
        ZoneProfile ruralProfile = new ZoneProfile("RURAL", _excludedModels);
        // We give them IDs: "BEATER", "HEAVY", "TOURIST", "BIKE"
        ruralProfile.AddIngredient("WACKY", VehList.models_wacky, SpawnBehavior.RandomSpec);
        ruralProfile.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Beater);
        ruralProfile.AddIngredient("BEATER", VehList.models_beaters, SpawnBehavior.Beater);
        ruralProfile.AddIngredient("OFFROAD", VehList.models_offroad, SpawnBehavior.Beater);

        // 2. RICH PROFILE
        ZoneProfile richProfile = new ZoneProfile("RICH", _excludedModels);
        richProfile.AddIngredient("SUPER", VehList.models_super, SpawnBehavior.Spec);
        richProfile.AddIngredient("CLASSIC", VehList.models_classics, SpawnBehavior.Spec);
        richProfile.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP);
        richProfile.AddIngredient("SUV", VehList.models_armoured, SpawnBehavior.VIP);

        // 3. GHETTO PROFILE
        ZoneProfile ghettoProfile = new ZoneProfile("GHETTO", _excludedModels);
        ghettoProfile.AddIngredient("LOWRIDER", VehList.models_lowriders, SpawnBehavior.RandomSpec);
        ghettoProfile.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle);

        // 4. URBAN PROFILE
        ZoneProfile urbanProfile = new ZoneProfile("URBAN", _excludedModels);
        urbanProfile.AddIngredient("LUX", VehList.models_luxury, SpawnBehavior.VIP);
        urbanProfile.AddIngredient("TUNER", VehList.models_tuner, SpawnBehavior.Tuner);
        urbanProfile.AddIngredient("MUSCLE", VehList.models_muscle, SpawnBehavior.Muscle);

        // 5. INDUSTRY PROFILE
        ZoneProfile industryProfile = new ZoneProfile("INDUSTRY", _excludedModels);
        industryProfile.AddIngredient("BEATER", VehList.models_beaters, SpawnBehavior.Beater);
        // Note: Industry only has 1 category. The logic safely falls back to allowing repeats here.

        // 6. OFFROAD OVERRIDE
        ZoneProfile offroadProfile = new ZoneProfile("OFFROAD", _excludedModels);
        offroadProfile.AddIngredient("OFFROAD", VehList.models_offroad, SpawnBehavior.Beater);

        // ASSIGNMENTS
        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "DTVINE", "WVINE", "CHIL", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE", "MORN", "MOVIE", "PBLUFF", "CHU", "BHAMCA");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON", "LOSPUER", "AIRP", "VINE");
        AssignToProfile(industryProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO", "TERMINA", "ELYSIAN");

        _zoneRegistry["_OVERRIDE_OFFROAD_"] = offroadProfile;
    }

    private void AssignToProfile(ZoneProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }

    public struct SelectionLayer { public HashSet<string> List; public SpawnBehavior Behavior; public string SourceProfile; }

    public class ZoneProfile
    {
        public string Name;
        // NEW: We store distinct ingredients instead of one mixed list
        private List<Ingredient> _ingredients = new List<Ingredient>();
        private HashSet<string> _blacklist;

        // NEW: Memory to track the last category used
        private string _lastCategoryId = "";
        private Random _rnd = new Random();

        public ZoneProfile(string name, HashSet<string> blacklist)
        {
            Name = name;
            _blacklist = blacklist;
        }

        // UPDATED: Now requires a unique ID for the category (e.g., "TUNER")
        public void AddIngredient(string id, HashSet<string> list, SpawnBehavior behavior)
        {
            if (list == null || list.Count == 0) return;

            // Filter blacklist immediately
            HashSet<string> filteredList = new HashSet<string>();
            foreach (string model in list)
            {
                if (_blacklist != null && _blacklist.Contains(model)) continue;
                filteredList.Add(model);
            }

            if (filteredList.Count > 0)
            {
                _ingredients.Add(new Ingredient
                {
                    Id = id,
                    List = filteredList,
                    Behavior = behavior
                });
            }
        }

        public SelectionLayer PickLayer()
        {
            if (_ingredients.Count == 0) return new SelectionLayer();

            // 1. "NO REPEATS" LOGIC
            // Filter out the category we just used.
            // Result: If we just spawned a Tuner, Tuners are removed from this specific draw.
            var candidates = _ingredients.Where(i => i.Id != _lastCategoryId).ToList();

            // Safety: If candidates is empty (e.g., Profile only has 1 category total), use everything.
            if (candidates.Count == 0) candidates = _ingredients;

            // 2. Pick a random Category from the remaining valid ones
            Ingredient selected = candidates[_rnd.Next(candidates.Count)];

            // 3. Update History
            _lastCategoryId = selected.Id;

            // 4. Get the actual car
            string modelName = VehicleSelector.GetNext(selected.List, "Traffic");

            return new SelectionLayer
            {
                List = new HashSet<string> { modelName },
                Behavior = selected.Behavior,
                SourceProfile = this.Name
            };
        }

        // Helper Class
        private class Ingredient
        {
            public string Id;
            public HashSet<string> List;
            public SpawnBehavior Behavior;
        }
    }

    [Flags]
    public enum VehicleNodeFlags { None = 0, SwitchedOff = 1, Highway = 2, Arg3 = 4, Arg4 = 8, Dirt = 32, Arg6 = 64 }
}