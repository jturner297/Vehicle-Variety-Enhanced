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

    private const string MARKER_PLATE = "TMP_SWAP";
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
            GTA.UI.Notification.PostTicker($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true);
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
            if (v.Exists() && v.Mods.LicensePlate == MARKER_PLATE)
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
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer || v.Mods.LicensePlate == MARKER_PLATE) continue;

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
            // CHECK: Is the winner on a dirt road?
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

        // LOGIC: If on Dirt, force the hidden "OFFROAD" profile.
        // Otherwise, use the standard Zone Registry.
        if (onDirt && _zoneRegistry.ContainsKey("_OVERRIDE_OFFROAD_"))
        {
            layer = _zoneRegistry["_OVERRIDE_OFFROAD_"].PickWeightedLayer();
            GTA.UI.Notification.PostTicker($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true, false);
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
            newVehicle.Mods.LicensePlate = MARKER_PLATE;

            driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);
            oldVehicle.Delete();

            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            driver.BlockPermanentEvents = true;
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, DriveStyle);

            if (ShowBlips || _debugMode) CreateBlip(newVehicle, modelName);
            if (_debugMode) GTA.UI.Notification.PostTicker($"~y~Swap: {modelName}", true);

            newVehicle.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();
            model.MarkAsNoLongerNeeded();

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));

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
        // 1. HARD BANS (Planes, Helis, Trains, Boats)
        if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) return true;

        // 2. CYCLE BAN
        // This stops Bicycles (BMX) from being swapped, but allows Motorcycles (Sanchez)
        if (v.ClassType == VehicleClass.Cycles) return true;

        // 3. SCENARIO BAN (FIXED)
        // Changed VehiclePopulationType -> EntityPopulationType
        if (v.PopulationType == EntityPopulationType.RandomScenario) return true;

        VehicleClass vc = v.ClassType;
        if (IgnoreEmergency && (vc == VehicleClass.Emergency || v.Driver.IsInPoliceVehicle)) return true;
        if (IgnoreService && (vc == VehicleClass.Service || vc == VehicleClass.Commercial || v.Model.IsBus)) return true;
        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        if (IgnoreBig && (vc == VehicleClass.Industrial || vc == VehicleClass.Utility || vc == VehicleClass.Military)) return true;

        return false;
    }

    private bool IsVehicleOnDirt(Vehicle v)
    {
        OutputArgument outDensity = new OutputArgument();
        OutputArgument outFlags = new OutputArgument();

        if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, v.Position.X, v.Position.Y, v.Position.Z, outDensity, outFlags))
        {
            int flags = outFlags.GetResult<int>();
            // Check for Dirt flag (Bit 5 / 32)
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

        // --- NAME LOGIC (UPDATED FOR SHVDN v3) ---
        // 1. Get the GXT Label (e.g., "BANSHEE2")
        string gxtLabel = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v.Model.Hash);

        // 2. Translate Label to English (e.g., "Banshee 900R") using GetLocalizedString
        string friendlyName = Game.GetLocalizedString(gxtLabel);

        // 3. Fallback: If name is missing or "NULL", use "Vehicle"
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
            if (v.Mods.LicensePlate == MARKER_PLATE && v.IsOnScreen)
            {
                World.DrawMarker(MarkerType.Chevron1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow);
            }
        }
    }

    private void InitializeZones()
    {
        // --- STANDARD ZONES ---
        ZoneProfile ruralProfile = new ZoneProfile("RURAL");
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_rural), 50, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 30, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 10, SpawnBehavior.Stock);
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_wacky), 10, SpawnBehavior.RandomSpec);

        ZoneProfile richProfile = new ZoneProfile("RICH");
        richProfile.AddIngredient(new HashSet<string>(VehList.models_supers_common), 40, SpawnBehavior.Spec);
        richProfile.AddIngredient(new HashSet<string>(VehList.models_classics_common), 40, SpawnBehavior.Spec);
        richProfile.AddIngredient(new HashSet<string>(VehList.models_city), 20, SpawnBehavior.Spec);

        ZoneProfile ghettoProfile = new ZoneProfile("GHETTO");
        ghettoProfile.AddIngredient(new HashSet<string>(VehList.models_lowriders), 60, SpawnBehavior.RandomSpec);
        ghettoProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 30, SpawnBehavior.Spec);
        ghettoProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 10, SpawnBehavior.Stock);

        ZoneProfile urbanProfile = new ZoneProfile("URBAN");
        urbanProfile.AddIngredient(new HashSet<string>(VehList.models_city), 30, SpawnBehavior.Spec);
        urbanProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 40, SpawnBehavior.Spec);
        urbanProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 30, SpawnBehavior.Stock);

        ZoneProfile generalProfile = new ZoneProfile("GENERAL");
        generalProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 50, SpawnBehavior.Spec);
        generalProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 50, SpawnBehavior.Stock);

        // --- NEW: SPECIAL OFFROAD PROFILE ---
        // This is not attached to a Zone Name. It is attached to the Dirt Flag.
        ZoneProfile offroadProfile = new ZoneProfile("OFFROAD");
        offroadProfile.AddIngredient(new HashSet<string>(VehList.models_offroad), 70, SpawnBehavior.Spec);
        offroadProfile.AddIngredient(new HashSet<string>(VehList.models_rural), 30, SpawnBehavior.Spec); // Bajas, Buggies


        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE", "MORN", "MOVIE", "PBLUFF", "CHU", "BHAMCA");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON", "LOSPUER", "AIRP");
        AssignToProfile(generalProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO");

        // Manually register the special profile
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