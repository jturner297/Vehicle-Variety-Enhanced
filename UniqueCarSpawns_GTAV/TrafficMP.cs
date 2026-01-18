using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using System.Drawing;

public class TrafficMP : Script
{
    // =============================================================
    //                 TUNING DASHBOARD
    // =============================================================

    private int SpawnCooldown = 5000;
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

    // =============================================================

    private bool IgnoreEmergency = true;
    private bool IgnoreService = true;
    private bool IgnoreBig = true;

    // REMOVED: private const string MARKER_PLATE = "TMP_SWAP"; 
    private const string DECOR_NAME = "TMP_Swap_ID"; // New Invisible Tag Name

    private VehicleDrivingFlags DriveStyle = (VehicleDrivingFlags)786603 | (VehicleDrivingFlags)262144;

    private int _nextCheckTime = 0;
    private int _nextSpawnTime = 0;

    private bool _debugMode = false;

    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };
    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();

    private List<Blip> _activeBlips = new List<Blip>();
    private Random _rnd = new Random();

    public TrafficMP()
    {
        // 1. REGISTER DECORATOR (Essential Step)
        // 3 = Integer type. This allows us to "stick" a number onto a car entity.
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
            // CHECK: Use new helper instead of checking License Plate
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
            // CHECK: Ignore cars we already swapped (using Decorator)
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer || IsSwapped(v)) continue;

            if (IsExcludedCategory(v, player.Position)) continue;

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
            layer = _zoneRegistry["_OVERRIDE_OFFROAD_"].PickWeightedLayer();
            if (_debugMode) GTA.UI.Notification.PostTicker("~o~TrafficMP: Dirt Road Override Triggered", true, false);
        }
        else
        {
            layer = GetLayerForLocation(oldVehicle.Position);
        }

        if (layer.List == null) return false;

        string modelName = VehicleSelector.GetNext(layer.List, _excludedModels);
        if (string.IsNullOrEmpty(modelName)) return false;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return false;

        model.Request(5);
        if (!model.IsLoaded) return false;

        Ped driver = oldVehicle.Driver;
        if (driver == null || !driver.Exists()) return false;

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);

        Vehicle newVehicle = World.CreateVehicle(model, oldVehicle.Position, oldVehicle.Heading);

        if (newVehicle != null)
        {
            newVehicle.Velocity = oldVehicle.Velocity;
            newVehicle.ForwardSpeed = oldVehicle.Speed;
            newVehicle.IsEngineRunning = true;

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));

            // --- NEW: Apply Invisible Decorator ---
            Function.Call(Hash.DECOR_SET_INT, newVehicle, DECOR_NAME, 1);
            // We NO LONGER touch .Mods.LicensePlate

            driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);
            oldVehicle.Delete();

            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            driver.BlockPermanentEvents = true;
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, DriveStyle);

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

    private SelectionLayer GetLayerForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (string.IsNullOrEmpty(zone) || _bannedZones.Contains(zone)) return new SelectionLayer();

        if (_zoneRegistry.ContainsKey(zone)) return _zoneRegistry[zone].PickWeightedLayer();
        return new SelectionLayer();
    }

    // ==========================================
    //      EXCLUSION LOGIC (SAFE NODE CHECK)
    // ==========================================
    private bool IsExcludedCategory(Vehicle v, Vector3 playerPos)
    {
        if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) return true;

        if (v.ClassType == VehicleClass.Cycles) return true;

        if (v.PopulationType == EntityPopulationType.RandomScenario) return true;

        // NEW: Safety Check - Don't swap a car that was already swapped (Decorator Check)
        if (IsSwapped(v)) return true;

        VehicleClass vc = v.ClassType;
        if (IgnoreEmergency && (vc == VehicleClass.Emergency || v.Driver.IsInPoliceVehicle)) return true;
        if (IgnoreService && (vc == VehicleClass.Service || vc == VehicleClass.Commercial || v.Model.IsBus)) return true;
        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        if (IgnoreBig && (vc == VehicleClass.Industrial || vc == VehicleClass.Utility || vc == VehicleClass.Military)) return true;

        return false;
    }

    // --- NEW HELPER ---
    private bool IsSwapped(Vehicle v)
    {
        // Ask the game engine: "Does this car have the 'TMP_Swap_ID' sticky note?"
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

        if (string.IsNullOrEmpty(friendlyName) || friendlyName.ToUpper() == "NULL")
        {
            b.Name = "Vehicle";
        }
        else
        {
            b.Name = friendlyName;
        }

        if (!_debugMode && !ShowBlips) b.Alpha = 0;
        _activeBlips.Add(b);
    }

    private void CleanupBlips()
    {
        Ped player = Game.Player.Character;
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            Blip b = _activeBlips[i];
            if (!b.Exists() || b.Entity == null || !b.Entity.Exists())
            {
                if (b.Exists()) b.Delete();
                _activeBlips.RemoveAt(i);
                continue;
            }
            if (player.IsInVehicle((Vehicle)b.Entity))
            {
                b.Delete();
                _activeBlips.RemoveAt(i);
            }
        }
    }

    private void DrawDebugInfo()
    {
        Vehicle[] vehs = World.GetAllVehicles();
        foreach (Vehicle v in vehs)
        {
            // CHECK: Use IsSwapped instead of License Plate
            if (v.Exists() && IsSwapped(v) && v.IsOnScreen)
            {
                World.DrawMarker(MarkerType.Chevron1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow);
            }
        }
    }

    private void InitializeZones()
    {
        // 1. RURAL PROFILE
        ZoneProfile ruralProfile = new ZoneProfile("RURAL");
        // FIX: Removed "new HashSet<string>(...)" wrapper. Passing direct Master Reference.
        ruralProfile.AddIngredient(VehList.models_rural, 50, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(VehList.models_general_common, 30, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(VehList.models_general_rare, 10, SpawnBehavior.Stock);
        ruralProfile.AddIngredient(VehList.models_wacky, 10, SpawnBehavior.RandomSpec);

        // 2. RICH PROFILE
        ZoneProfile richProfile = new ZoneProfile("RICH");
        richProfile.AddIngredient(VehList.models_super, 35, SpawnBehavior.Spec);
        richProfile.AddIngredient(VehList.models_classics, 35, SpawnBehavior.Spec);
        richProfile.AddIngredient(VehList.models_luxury, 25, SpawnBehavior.VIP);
        richProfile.AddIngredient(VehList.models_armoured, 5, SpawnBehavior.VIP);

        // 3. GHETTO PROFILE
        ZoneProfile ghettoProfile = new ZoneProfile("GHETTO");
        ghettoProfile.AddIngredient(VehList.models_lowriders, 60, SpawnBehavior.RandomSpec);
        ghettoProfile.AddIngredient(VehList.models_muscle, 40, SpawnBehavior.Muscle);

        // 4. URBAN PROFILE
        ZoneProfile urbanProfile = new ZoneProfile("URBAN");
        urbanProfile.AddIngredient(VehList.models_luxury, 40, SpawnBehavior.VIP);
        urbanProfile.AddIngredient(VehList.models_tuner, 30, SpawnBehavior.Tuner);
        urbanProfile.AddIngredient(VehList.models_muscle, 30, SpawnBehavior.Muscle);

        // 5. INDUSTRY PROFILE
        ZoneProfile industryProfile = new ZoneProfile("INDUSTRY");
        industryProfile.AddIngredient(VehList.models_industry, 100, SpawnBehavior.Spec);
      //  industryProfile.AddIngredient(VehList.models_general_common, 50, SpawnBehavior.Spec);
      //  industryProfile.AddIngredient(VehList.models_general_rare, 50, SpawnBehavior.Spec);

        // 6. OFFROAD OVERRIDE
        ZoneProfile offroadProfile = new ZoneProfile("OFFROAD");
        offroadProfile.AddIngredient(VehList.models_offroad, 100, SpawnBehavior.Muscle);

        // ASSIGNMENTS
        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "DTVINE", "WVINE", "CHIL", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE", "MORN", "MOVIE", "PBLUFF", "CHU", "BHAMCA");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON", "LOSPUER", "AIRP", "VINE");
        AssignToProfile(industryProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO", "TERMINA");

        _zoneRegistry["_OVERRIDE_OFFROAD_"] = offroadProfile;
    }

    private void AssignToProfile(ZoneProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }

    public struct SelectionLayer { public HashSet<string> List; public SpawnBehavior Behavior; }

    public class ZoneProfile
    {
        public string Name;
        private struct Ingredient { public HashSet<string> List; public int Weight; public SpawnBehavior Behavior; }
        private List<Ingredient> _ingredients = new List<Ingredient>();
        private int _totalWeight = 0;
        private Random _rnd = new Random();

        public ZoneProfile(string name) { Name = name; }

        public void AddIngredient(HashSet<string> list, int weight, SpawnBehavior behavior)
        {
            if (list == null) return;
            _ingredients.Add(new Ingredient { List = list, Weight = weight, Behavior = behavior });
            _totalWeight += weight;
        }

        public SelectionLayer PickWeightedLayer()
        {
            if (_ingredients.Count == 0) return new SelectionLayer();

            int roll = _rnd.Next(0, _totalWeight);
            int current = 0;
            foreach (var item in _ingredients)
            {
                current += item.Weight;
                if (roll < current) return new SelectionLayer { List = item.List, Behavior = item.Behavior };
            }
            return new SelectionLayer { List = _ingredients[0].List, Behavior = _ingredients[0].Behavior };
        }
    }

    [Flags]
    public enum VehicleNodeFlags
    {
        None = 0,
        SwitchedOff = 1,
        Highway = 2,
        Arg3 = 4,
        Arg4 = 8,
        Dirt = 32,
        Arg6 = 64
    }
}