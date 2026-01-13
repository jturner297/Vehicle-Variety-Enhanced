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

    // --- 1. PACING ---
    private int SpawnCooldown = 5000;    // Time between swaps (ms)
    private int MaxVisibleHeroes = 1;    // Max custom cars visible at once

    // --- 2. DIRECTOR CAMERA (The "Where" Logic) ---
    private float MinSpawnDist = 110f;   // Closest allowed swap (Meters). Lower = Pop-in risk.
    private float MaxSpawnDist = 300f;   // Furthest allowed swap.
    private float SpawnFOV = 35f;        // Viewing Angle (Degrees). Lower = Center screen only.

    // --- 3. SCORING WEIGHTS (The "Why" Logic) ---
    private float ScoreThreshold = 50f;  // Minimum score required to trigger a swap
    private float ScoreOncoming = 100f;  // Bonus for cars driving TOWARDS you
    private float ScoreOvertake = 20f;   // Bonus for cars driving SAME direction
    private float ScoreVisible = 50f;    // Bonus if car is currently on screen

    // --- 4. SYSTEM ---
    private int CheckInterval = 250;     // Scan frequency (ms)
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

        Vehicle[] allVehicles = World.GetAllVehicles();

        // 1. SCENE CHECK (Are there heroes nearby?)
        int heroesOnSet = 0;
        foreach (Vehicle v in allVehicles)
        {
            if (v.Exists() && v.Mods.LicensePlate == MARKER_PLATE)
            {
                // Using MinSpawnDist as the "Safety Zone" for existing heroes too
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
            if (IsExcludedCategory(v)) continue;

            float score = GetCinematicScore(v, camPos, camDir, playerVel);

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = v;
            }
        }

        // 3. ACTION
        if (bestCandidate != null && bestScore > ScoreThreshold)
        {
            if (TransformVehicle(bestCandidate))
            {
                _nextSpawnTime = Game.GameTime + SpawnCooldown;
            }
        }
    }

    private float GetCinematicScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerVel)
    {
        float score = 0f;
        float dist = v.Position.DistanceTo(camPos);

        // A. TUNABLE DISTANCE CHECKS
        if (dist < MinSpawnDist) return 0f;
        if (dist > MaxSpawnDist) return 0f;

        // B. TUNABLE FOV CHECK
        Vector3 toCar = (v.Position - camPos).Normalized;
        float angle = Vector3.Angle(camDir, toCar);
        if (angle > SpawnFOV) return 0f;

        // C. TUNABLE MOVEMENT SCORING
        float closingSpeed = Vector3.Dot(v.Velocity.Normalized, playerVel.Normalized);
        if (closingSpeed < -0.5f) score += ScoreOncoming;
        else if (closingSpeed > 0.5f) score += ScoreOvertake;

        // D. VISIBILITY CHECK (Raycast is still hardcoded as it's physics)
        bool visible = !World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.5f), IntersectFlags.Map).DidHit;
        if (!visible) return 0f;

        if (v.IsOnScreen) score += ScoreVisible;

        // E. DISTANCE WEIGHT (Further is better)
        // This encourages selecting cars at the edge of the MinSpawnDist
        score += dist * 0.5f;

        return score;
    }

    // ==========================================
    //           SELECTION LOGIC
    // ==========================================
    private bool TransformVehicle(Vehicle oldVehicle)
    {
        SelectionLayer layer = GetLayerForLocation(oldVehicle.Position);
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

    private bool IsExcludedCategory(Vehicle v)
    {
        if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) return true;
        VehicleClass vc = v.ClassType;
        if (IgnoreEmergency && (vc == VehicleClass.Emergency || v.Driver.IsInPoliceVehicle)) return true;
        if (IgnoreService && (vc == VehicleClass.Service || vc == VehicleClass.Commercial || v.Model.IsBus)) return true;
        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        if (IgnoreBig && (vc == VehicleClass.Industrial || vc == VehicleClass.Utility || vc == VehicleClass.Military)) return true;
        return false;
    }

    private void CreateBlip(Vehicle v, string name)
    {
        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.Standard;
        b.Color = BlipColor.White;
        b.Scale = 0.5f;
        b.Name = name;
        b.IsShortRange = true;
        if (!_debugMode && !ShowBlips) b.Alpha = 0;
        _activeBlips.Add(b);
    }

    private void CleanupBlips()
    {
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            if (!_activeBlips[i].Exists() || _activeBlips[i].Entity == null || !_activeBlips[i].Entity.Exists())
            {
                if (_activeBlips[i].Exists()) _activeBlips[i].Delete();
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

    // ==========================================
    //           ZONE SETUP (RESTORED)
    // ==========================================
    private void InitializeZones()
    {
        ZoneProfile ruralProfile = new ZoneProfile("RURAL");
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_rural), 50, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 30, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 10, SpawnBehavior.Stock);
        ruralProfile.AddIngredient(new HashSet<string>(VehList.models_wacky), 10, SpawnBehavior.RandomSpec);

        ZoneProfile richProfile = new ZoneProfile("RICH");
        richProfile.AddIngredient(new HashSet<string>(VehList.models_supers_common), 50, SpawnBehavior.Spec);
        richProfile.AddIngredient(new HashSet<string>(VehList.models_classics_common), 50, SpawnBehavior.Spec);
   //     richProfile.AddIngredient(new HashSet<string>(VehList.models_city), 50, SpawnBehavior.Spec);

        ZoneProfile ghettoProfile = new ZoneProfile("GHETTO");
        ghettoProfile.AddIngredient(new HashSet<string>(VehList.models_lowriders), 50, SpawnBehavior.RandomSpec);
        ghettoProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 40, SpawnBehavior.Spec);
        ghettoProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 10, SpawnBehavior.Stock);

        ZoneProfile urbanProfile = new ZoneProfile("URBAN");
        urbanProfile.AddIngredient(new HashSet<string>(VehList.models_city), 30, SpawnBehavior.Spec);
        urbanProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 40, SpawnBehavior.Spec);
        urbanProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 30, SpawnBehavior.Stock);

        ZoneProfile generalProfile = new ZoneProfile("GENERAL");
        generalProfile.AddIngredient(new HashSet<string>(VehList.models_general_common), 50, SpawnBehavior.Spec);
        generalProfile.AddIngredient(new HashSet<string>(VehList.models_general_rare), 50, SpawnBehavior.Stock);

        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE", "MORN");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON", "LOSPUER", "AIRP");
        AssignToProfile(generalProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO");
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
}