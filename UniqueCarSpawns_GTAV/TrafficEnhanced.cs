using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using System.Drawing;

public class TrafficEnhanced : Script
{
    // =============================================================
    //                    MASTER SETTINGS
    // =============================================================

    // --- PIPELINE SETTINGS ---
    private int CheckInterval = 300;
    private bool ShowBlips = true;
    private bool DebugMode = false;

    // --- DIRECTOR (HERO) SETTINGS ---
    private int DirectorCooldown = 5000;
    private int MaxHeroCount = 1;
    private float MinSpawnDist = 130f;
    private float MaxSpawnDist = 240f;
    private float SpawnFOV = 35f;
    private float ScoreThreshold = 50f;

    // --- FLOODING (AMBIENT) SETTINGS ---
    private int MaxFloodsPerCycle = 2;
    private float FloodMinDist = 60f;
    private float FloodFoveal = 160f;
    private float FloodPeripheral = 70f;

    // --- COLOR SETTINGS ---
    private bool RandomizeVanilla = true;

    // =============================================================

    private int _nextCheckTime = 0;
    private int _nextDirectorTime = 0;
    private const string MARKER_PLATE = "TMP_ENH";

    private HashSet<int> _processedColors = new HashSet<int>();
    private List<Blip> _activeBlips = new List<Blip>();
    private Random _rnd = new Random();

    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };

    // TWO SEPARATE REGISTRIES
    private Dictionary<string, ZoneProfile> _directorRegistry = new Dictionary<string, ZoneProfile>();
    private Dictionary<string, ZoneProfile> _ambientRegistry = new Dictionary<string, ZoneProfile>();

    private VehicleDrivingFlags _driveStyle = (VehicleDrivingFlags)786603 | (VehicleDrivingFlags)262144;

    public TrafficEnhanced()
    {
        InitializeZones();
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    // =============================================================
    //                     THE MASTER LOOP
    // =============================================================
    private void OnTick(object sender, EventArgs e)
    {
        if (DebugMode) DrawDebugInfo();

        if (Game.GameTime < _nextCheckTime) return;

        try
        {
            CleanupLists();
            RunUnifiedPipeline();
        }
        catch (Exception ex) { if (DebugMode) GTA.UI.Notification.PostTicker($"Error: {ex.Message}", true); }

        _nextCheckTime = Game.GameTime + CheckInterval;
    }

    private void RunUnifiedPipeline()
    {
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;
        Vector3 playerVel = player.Velocity;
        Vector3 playerDir = player.ForwardVector;

        Vehicle[] allVehicles = World.GetAllVehicles();

        // 1. ANALYSIS PHASE
        int heroCount = 0;
        Dictionary<int, int> modelCounts = new Dictionary<int, int>();
        List<Vehicle> candidates = new List<Vehicle>();

        foreach (Vehicle v in allVehicles)
        {
            if (!v.Exists()) continue;

            if (v.Mods.LicensePlate == MARKER_PLATE)
            {
                if (v.IsOnScreen || v.Position.DistanceTo(playerPos) < MinSpawnDist) heroCount++;
                continue;
            }

            if (v.Driver != null && !v.Driver.IsPlayer)
            {
                int hash = v.Model.Hash;
                if (!modelCounts.ContainsKey(hash)) modelCounts[hash] = 0;
                modelCounts[hash]++;
                candidates.Add(v);
            }
        }

        // 2. DIRECTOR SCORING
        bool directorActive = (Game.GameTime >= _nextDirectorTime && heroCount < MaxHeroCount);
        Vehicle bestDirectorCand = null;
        float bestDirectorScore = 0f;

        if (directorActive)
        {
            foreach (Vehicle v in candidates)
            {
                if (IsProtected(v)) continue;

                float score = GetCinematicScore(v, camPos, camDir, playerDir, playerVel);
                if (score > bestDirectorScore)
                {
                    bestDirectorScore = score;
                    bestDirectorCand = v;
                }
            }
        }

        // 3. EXECUTION ITERATION
        int floodsDone = 0;

        foreach (Vehicle v in candidates)
        {
            if (IsProtected(v)) continue;

            // HERO CHECK
            if (v == bestDirectorCand && bestDirectorScore > ScoreThreshold)
            {
                bool onDirt = IsVehicleOnDirt(v);
                if (PerformSwap(v, true, onDirt))
                {
                    _nextDirectorTime = Game.GameTime + DirectorCooldown;
                }
                continue;
            }

            // AMBIENT CHECK (FLOODING)
            if (floodsDone < MaxFloodsPerCycle)
            {
                float dist = v.Position.DistanceTo(playerPos);
                bool isDuplicate = modelCounts[v.Model.Hash] > 1;
                bool isBoring = v.Model.Hash == unchecked((int)VehicleHash.Taxi);

                if ((isDuplicate || isBoring) && dist > FloodMinDist)
                {
                    if (IsSafeToSwapAmbient(v, dist, camPos, camDir))
                    {
                        if (PerformSwap(v, false))
                        {
                            floodsDone++;
                            modelCounts[v.Model.Hash]--;
                            continue;
                        }
                    }
                }
            }

            // COLOR CHECK
            if (RandomizeVanilla && !_processedColors.Contains(v.Handle))
            {
                // SAFETY GATE: Prevents colors changing right in your face.
                // 1. If it's on screen...
                if (v.IsOnScreen)
                {
                    // 2. ...AND it's closer than 100 meters...
                    if (v.Position.DistanceToSquared(playerPos) < 10000f) // 100*100 = 10,000
                    {
                        continue; // SKIP IT. We will catch it later when you look away.
                    }
                }

                // If we are here, the car is either off-screen or far away. Safe to paint.
                ApplyRandomCombination(v);
                _processedColors.Add(v.Handle);
            }
        }
    }

    // =============================================================
    //                     CORE LOGIC
    // =============================================================

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
        if (closingSpeed < -0.5f) score += 100f; // Oncoming
        else if (closingSpeed > 0.5f) score += 20f; // Overtake

        if (!World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.5f), IntersectFlags.Map).DidHit)
        {
            if (v.IsOnScreen) score += 50f;
            score += dist * 0.5f;
            return score;
        }
        return 0f;
    }

    private bool PerformSwap(Vehicle oldVehicle, bool isHero, bool forceOffroad = false)
    {
        // 1. GET LAYER FROM REGISTRY
        SelectionLayer layer;

        if (isHero)
        {
            // --- DIRECTOR LOGIC ---
            if (forceOffroad && _directorRegistry.ContainsKey("_OVERRIDE_OFFROAD_"))
            {
                layer = _directorRegistry["_OVERRIDE_OFFROAD_"].PickWeightedLayer();
            }
            else
            {
                layer = GetLayerFromRegistry(oldVehicle.Position, _directorRegistry);
            }
        }
        else
        {
            // --- AMBIENT LOGIC ---
            // Ambient doesn't care about Dirt/Offroad override, it just checks the zone economics
            layer = GetLayerFromRegistry(oldVehicle.Position, _ambientRegistry);
        }

        if (layer.List == null || layer.List.Count == 0) return false;

        // 2. SELECT MODEL
        string modelName = VehicleSelector.GetNext(layer.List, _excludedModels);
        if (string.IsNullOrEmpty(modelName)) return false;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return false;

        model.Request(5);
        if (!model.IsLoaded) return false;

        // 3. SPAWN
        Ped driver = oldVehicle.Driver;
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

            // BEHAVIOR DIFFERENCE
            if (isHero)
            {
                driver.BlockPermanentEvents = true;
                Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, _driveStyle);
            }
            else
            {
                // Ambient cars just go back to being traffic
                newVehicle.MarkAsNoLongerNeeded();
                driver.MarkAsNoLongerNeeded();
            }

            if (ShowBlips || DebugMode) CreateBlip(newVehicle, modelName, isHero);
            if (DebugMode && isHero) GTA.UI.Notification.PostTicker($"~y~DIRECTOR: {modelName}", true);

            // TRACKING
            if (isHero)
            {
                newVehicle.MarkAsNoLongerNeeded();
                driver.MarkAsNoLongerNeeded();
            }

            model.MarkAsNoLongerNeeded();
            ApplyRandomCombination(newVehicle);
            _processedColors.Add(newVehicle.Handle);

            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    private void ApplyRandomCombination(Vehicle v)
    {
        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, v);
        if (comboCount > 0)
        {
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, v, _rnd.Next(0, comboCount));
        }
    }

    // =============================================================
    //                   HELPER FUNCTIONS
    // =============================================================

    private bool IsProtected(Vehicle v)
    {
        if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) return true;
        if (v.ClassType == VehicleClass.Cycles) return true;
        if (v.PopulationType == EntityPopulationType.RandomScenario) return true;
        if (v.Driver == Game.Player.Character) return true;
        if (v.IsPersistent) return true;

        VehicleClass vc = v.ClassType;
        if (vc == VehicleClass.Emergency || v.Driver.IsInPoliceVehicle) return true;
        if (vc == VehicleClass.Service || vc == VehicleClass.Commercial || v.Model.IsBus) return true;
        if (vc == VehicleClass.Industrial || vc == VehicleClass.Utility || vc == VehicleClass.Military) return true;
        return false;
    }

    private bool IsSafeToSwapAmbient(Vehicle v, float dist, Vector3 camPos, Vector3 camDir)
    {
        if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, v.Position.X, v.Position.Y, v.Position.Z, 4.0f)) return true;
        if (dist > (FloodFoveal * FloodFoveal)) return true;

        Vector3 toCar = (v.Position - camPos).Normalized;
        if (Vector3.Angle(camDir, toCar) > 25.0f)
        {
            if (dist > (FloodPeripheral * FloodPeripheral)) return true;
        }

        bool hideLow = World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.4f), IntersectFlags.Map).DidHit;
        if (hideLow) return true;

        return false;
    }

    private bool IsVehicleOnDirt(Vehicle v)
    {
        OutputArgument outDensity = new OutputArgument();
        OutputArgument outFlags = new OutputArgument();
        if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, v.Position.X, v.Position.Y, v.Position.Z, outDensity, outFlags))
        {
            int flags = outFlags.GetResult<int>();
            if ((flags & 32) != 0) return true;
        }
        return false;
    }

    private void CreateBlip(Vehicle v, string name, bool isHero)
    {
        Blip b = v.AddBlip();
        b.Scale = 0.7f;
        b.IsShortRange = true;

        string gxtLabel = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v.Model.Hash);
        string friendlyName = Game.GetLocalizedString(gxtLabel);
        if (string.IsNullOrEmpty(friendlyName) || friendlyName.ToUpper() == "NULL") friendlyName = name;

        if (isHero)
        {
            b.Sprite = BlipSprite.PersonalVehicleCar;
            b.Color = BlipColor.Yellow;
            b.Name = $"HERO: {friendlyName}";
        }
        else
        {
            b.Sprite = BlipSprite.Standard;
            b.Color = BlipColor.Blue;
            b.Name = friendlyName;
        }

        if (!DebugMode && !ShowBlips) b.Alpha = 0;
        _activeBlips.Add(b);
    }

    private void CleanupLists()
    {
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            if (!_activeBlips[i].Exists() || _activeBlips[i].Entity == null || !_activeBlips[i].Entity.Exists())
            {
                if (_activeBlips[i].Exists()) _activeBlips[i].Delete();
                _activeBlips.RemoveAt(i);
            }
        }

        if (_processedColors.Count > 300)
            _processedColors.RemoveWhere(handle => !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle));
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
            if (_processedColors.Contains(v.Handle) && v.IsOnScreen)
            {
                World.DrawMarker(MarkerType.DebugSphere, v.Position + new Vector3(0, 0, 1.2f), Vector3.Zero, Vector3.Zero, new Vector3(0.2f, 0.2f, 0.2f), Color.Purple);
            }
        }
    }

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.F11)
        {
            DebugMode = !DebugMode;
            GTA.UI.Notification.PostTicker($"TrafficEnhanced Debug: {(DebugMode ? "~g~ON" : "~r~OFF")}", true);
            foreach (var b in _activeBlips) if (b.Exists()) b.Alpha = DebugMode || ShowBlips ? 255 : 0;
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var b in _activeBlips) if (b.Exists()) b.Delete();
    }

    // =============================================================
    //                ZONE REGISTRY & SETUP
    // =============================================================

    private void InitializeZones()
    {
        // ------------------------------------------
        // 1. DIRECTOR PROFILES (The "Hero" Cars)
        // ------------------------------------------
        ZoneProfile dirRural = new ZoneProfile("DIR_RURAL");
        dirRural.AddIngredient(new HashSet<string>(VehList.models_rural), 50, SpawnBehavior.Spec);
        dirRural.AddIngredient(new HashSet<string>(VehList.models_wacky), 10, SpawnBehavior.RandomSpec);
        dirRural.AddIngredient(new HashSet<string>(VehList.models_general_common), 40, SpawnBehavior.Spec);

        ZoneProfile dirRich = new ZoneProfile("DIR_RICH");
        dirRich.AddIngredient(new HashSet<string>(VehList.models_supers_common), 40, SpawnBehavior.Spec);
        dirRich.AddIngredient(new HashSet<string>(VehList.models_classics_common), 40, SpawnBehavior.Spec);
        dirRich.AddIngredient(new HashSet<string>(VehList.models_city), 20, SpawnBehavior.Spec);

        ZoneProfile dirUrban = new ZoneProfile("DIR_URBAN");
        dirUrban.AddIngredient(new HashSet<string>(VehList.models_city), 50, SpawnBehavior.Spec);
        dirUrban.AddIngredient(new HashSet<string>(VehList.models_general_rare), 50, SpawnBehavior.Stock);

        ZoneProfile dirOffroad = new ZoneProfile("DIR_OFFROAD");
        dirOffroad.AddIngredient(new HashSet<string>(VehList.models_offroad), 80, SpawnBehavior.Spec);
        dirOffroad.AddIngredient(new HashSet<string>(VehList.models_rural), 20, SpawnBehavior.Spec);

        // ------------------------------------------
        // 2. AMBIENT PROFILES (The "Economic" Mix)
        // ------------------------------------------
        // Note: Assumes VehList.veh_poor, veh_mid, veh_rich exist.

        ZoneProfile ambRural = new ZoneProfile("AMB_RURAL");
        ambRural.AddIngredient(new HashSet<string>(VehList.veh_poor), 70, SpawnBehavior.Spec);
        ambRural.AddIngredient(new HashSet<string>(VehList.veh_mid), 30, SpawnBehavior.Spec);

        ZoneProfile ambGhetto = new ZoneProfile("AMB_GHETTO");
        ambGhetto.AddIngredient(new HashSet<string>(VehList.veh_poor), 60, SpawnBehavior.Spec);
        ambGhetto.AddIngredient(new HashSet<string>(VehList.veh_mid), 40, SpawnBehavior.Spec);

        ZoneProfile ambUrban = new ZoneProfile("AMB_URBAN");
        ambUrban.AddIngredient(new HashSet<string>(VehList.veh_rich), 20, SpawnBehavior.Stock);
        ambUrban.AddIngredient(new HashSet<string>(VehList.veh_mid), 50, SpawnBehavior.Stock);
        ambUrban.AddIngredient(new HashSet<string>(VehList.veh_poor), 30, SpawnBehavior.Stock);

        ZoneProfile ambRich = new ZoneProfile("AMB_RICH");
        ambRich.AddIngredient(new HashSet<string>(VehList.veh_rich), 60, SpawnBehavior.Spec);
        ambRich.AddIngredient(new HashSet<string>(VehList.veh_mid), 40, SpawnBehavior.Spec);

        ZoneProfile ambGeneral = new ZoneProfile("AMB_GENERAL");
        ambGeneral.AddIngredient(new HashSet<string>(VehList.veh_rich), 10, SpawnBehavior.Stock);
        ambGeneral.AddIngredient(new HashSet<string>(VehList.veh_mid), 50, SpawnBehavior.Stock);
        ambGeneral.AddIngredient(new HashSet<string>(VehList.veh_poor), 40, SpawnBehavior.Stock);


        // ------------------------------------------
        // 3. ASSIGNMENTS
        // ------------------------------------------

        // Define Zone Groups
        string[] zonesRural = { "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE" };
        string[] zonesRich = { "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE", "MORN", "MOVIE", "PBLUFF", "CHU", "BHAMCA" };
        string[] zonesGhetto = { "CHAMH", "DAVIS", "RANCHO", "STRAW" };
        string[] zonesUrban = { "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON", "LOSPUER", "AIRP" };
        string[] zonesGeneral = { "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO" };

        // Register Director (Hero) Profiles
        AssignToRegistry(_directorRegistry, dirRural, zonesRural);
        AssignToRegistry(_directorRegistry, dirRich, zonesRich);
        AssignToRegistry(_directorRegistry, dirUrban, zonesUrban);
        AssignToRegistry(_directorRegistry, dirUrban, zonesGhetto); // Ghetto uses Urban heroes for now, or define separate
        AssignToRegistry(_directorRegistry, dirUrban, zonesGeneral);
        _directorRegistry["_OVERRIDE_OFFROAD_"] = dirOffroad;

        // Register Ambient (Flood) Profiles
        AssignToRegistry(_ambientRegistry, ambRural, zonesRural);
        AssignToRegistry(_ambientRegistry, ambRich, zonesRich);
        AssignToRegistry(_ambientRegistry, ambGhetto, zonesGhetto);
        AssignToRegistry(_ambientRegistry, ambUrban, zonesUrban);
        AssignToRegistry(_ambientRegistry, ambGeneral, zonesGeneral);
    }

    private void AssignToRegistry(Dictionary<string, ZoneProfile> registry, ZoneProfile profile, string[] zones)
    {
        foreach (string z in zones) registry[z] = profile;
    }

    private SelectionLayer GetLayerFromRegistry(Vector3 pos, Dictionary<string, ZoneProfile> registry)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (string.IsNullOrEmpty(zone) || _bannedZones.Contains(zone)) return new SelectionLayer();

        if (registry.ContainsKey(zone)) return registry[zone].PickWeightedLayer();
        return new SelectionLayer();
    }

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