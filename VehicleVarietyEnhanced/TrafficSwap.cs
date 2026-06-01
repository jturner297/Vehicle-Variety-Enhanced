using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

public class TrafficSwap : Script
{


    // ==========================================
    //      TUNING VARIABLES & DEBUG SETTINGS
    // ==========================================

    // --- GEOMETRY & ANGLES ---
    private const int SWEEP_ATTEMPTS = 100;                // Brute-force checking to guarantee a hit
    private const int MAX_CONE_ANGLE = 45;                 // 180-degree peripheral vision sweep (90 left, 90 right)
    private const int DEAD_AHEAD_ANGLE_LIMIT = 45;         // Massive dead-ahead wedge
    private const float MAX_Z_DIFFERENCE = 100.0f;         // Ignores height limits; overpasses and tunnels are fair game

    // --- SPAWN DISTANCES (EXTREME CLOSE QUARTERS) ---
    private const int CROSS_STREET_MIN_DIST = 15;          // Spawns practically on top of the player
    private const int CROSS_STREET_MAX_DIST = 45;          // Extremely tight corner spawns
    private const int OPEN_ROAD_MIN_DIST = 80;             // Severe pop-in territory for straightaways
    private const int OPEN_ROAD_MAX_DIST = 200;             // Barely down the block
    private const float LOD_HAZE_FALLBACK_DIST = 40f;      // Completely ignores the on-screen pop-in safeguard

    // --- RAYCASTING & SYSTEM ---
    // Despawn distance is fixed: player moving 150m away from an active car will despawn it
    private const float RAYCAST_CORNER_PADDING = 0.5f;     // Zero margin for error; if it's 1 inch behind a pole, it spawns

    // --- VEHICLE SPEEDS ---
    private const float SPAWN_FORWARD_SPEED = 10.0f;       // Spawns coming in hot (approx 55 mph)
    private const float WANDER_DRIVE_SPEED = 15.0f;        // Fast cruising

    // --- DEBUGGING ---
    private const Keys DEBUG_SPAWN_KEY = Keys.NumPad9;
    private const bool ENABLE_DEBUG_NOTIFICATIONS = true;

    // ==========================================
    //               INTERNAL STATE
    // ==========================================

    private readonly List<Vehicle> _activeSwaps = new List<Vehicle>();
    private readonly List<string> _recentSpawnHistory = new List<string>();

    private int _lastPlayerHandle = 0;
    private bool _wasPlayerDead = false;

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
        ModSettings.Load();
        Function.Call(Hash.DECOR_REGISTER, DECOR_NAME, 3);
        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);

        InitializeZones();
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == DEBUG_SPAWN_KEY)
        {
            ExecuteNodeInjectionSweep(forceSpawn: true);
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;
        if (player == null || !player.Exists()) return;

        int currentHandle = player.Handle;
        bool isDead = player.IsDead;

        if (_lastPlayerHandle == 0 || currentHandle != _lastPlayerHandle || (!isDead && _wasPlayerDead))
        {
            ReleaseAllToGame();
            ModUtilities.TriggerGlobalDelay(ModSettings.StartupDelay);

            _nextSpawnTime = Game.GameTime + ModSettings.StartupDelay + 5000;
            _nextCheckTime = Game.GameTime + ModSettings.StartupDelay + 5000;
        }

        _lastPlayerHandle = currentHandle;
        _wasPlayerDead = isDead;

        if (!ModUtilities.IsModReady()) return;

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
                _nextSpawnTime = Game.GameTime + ModSettings.MissionEndDelay;
                _nextCheckTime = Game.GameTime + ModSettings.MissionEndDelay;
            }
        }

        for (int i = _activeSwaps.Count - 1; i >= 0; i--)
        {
            Vehicle v = _activeSwaps[i];

            if (!v.Exists() || v.IsDead)
            {
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + _rnd.Next(ModSettings.MinSwapCooldown, ModSettings.MaxSwapCooldown);
                continue;
            }

            if (player.IsInVehicle(v))
            {
                if (v.AttachedBlip != null) v.AttachedBlip.Delete();
                v.MarkAsNoLongerNeeded();
                _activeSwaps.RemoveAt(i);
                _nextSpawnTime = Game.GameTime + _rnd.Next(ModSettings.MinSwapCooldown, ModSettings.MaxSwapCooldown);
                continue;
            }

            float activeDespawnDist = 150f;
            if (v.Position.DistanceTo(player.Position) > activeDespawnDist)
            {
                if (v.AttachedBlip != null) v.AttachedBlip.Delete();
                foreach (Ped occupant in v.Occupants)
                {
                    if (occupant != null && occupant.Exists()) occupant.Delete();
                }
                v.Delete();
                _activeSwaps.RemoveAt(i);
                continue;
            }
        }

        if (Game.GameTime < _nextCheckTime) return;

        try
        {
            CleanupBlips();
            ExecuteNodeInjectionSweep(forceSpawn: false);
        }
        catch (Exception) { }

        _nextCheckTime = Game.GameTime + ModSettings.CheckInterval;
    }

    private void ForceDeleteAllSwaps()
    {
        foreach (var blip in _activeBlips)
        {
            if (blip != null && blip.Exists()) blip.Delete();
        }
        _activeBlips.Clear();

        foreach (var vehicle in _activeSwaps)
        {
            if (vehicle != null && vehicle.Exists())
            {
                foreach (Ped p in vehicle.Occupants)
                {
                    if (p != null && p.Exists()) p.Delete();
                }
                vehicle.Delete();
            }
        }
        _activeSwaps.Clear();
    }

    private void ExecuteNodeInjectionSweep(bool forceSpawn)
    {
        if (forceSpawn)
        {
            ForceDeleteAllSwaps();
        }
        else
        {
            if (Game.GameTime < _nextSpawnTime) return;
            if (_activeSwaps.Count >= ModSettings.MaxActiveSwaps) return;
        }

        Ped player = Game.Player.Character;
        Vector3 pPos = player.Position;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;
        Vector3 forward2D = new Vector3(camDir.X, camDir.Y, 0).Normalized;

        bool foundSpawn = false;
        string failReason = "No valid main roads found nearby";

        // Track a best candidate in case we don't immediately find a same-road ahead node
        float bestScore = float.MinValue;
        Vector3 bestNodePos = Vector3.Zero;
        float bestNodeHeading = 0f;
        bool bestNodeOnDirt = false;
        bool haveBest = false;

        for (int attempt = 0; attempt < SWEEP_ATTEMPTS; attempt++)
        {
            int sign = _rnd.Next(0, 2) == 0 ? 1 : -1;
            float randomAngleOffset = _rnd.Next(0, MAX_CONE_ANGLE) * sign;

            float targetDist;

            if (Math.Abs(randomAngleOffset) <= DEAD_AHEAD_ANGLE_LIMIT)
            {
                targetDist = _rnd.Next(OPEN_ROAD_MIN_DIST, OPEN_ROAD_MAX_DIST);
            }
            else
            {
                targetDist = _rnd.Next(CROSS_STREET_MIN_DIST, CROSS_STREET_MAX_DIST);
            }

            float angleRad = randomAngleOffset * (float)(Math.PI / 180.0);
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);

            Vector3 projectedDir = new Vector3(
                forward2D.X * cos - forward2D.Y * sin,
                forward2D.X * sin + forward2D.Y * cos,
                0
            ).Normalized;

            Vector3 searchPos = pPos + (projectedDir * targetDist);
            searchPos.Z = pPos.Z;

            OutputArgument outPos = new OutputArgument();
            OutputArgument outHeading = new OutputArgument();

            if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHeading, 1, 3.0f, 0))
            {
                Vector3 nodePos = outPos.GetResult<Vector3>();
                float nodeHeading = outHeading.GetResult<float>();
                float horizDist = GetHorizontalDistance(pPos, nodePos);

                if (Math.Abs(nodePos.Z - pPos.Z) > MAX_Z_DIFFERENCE)
                {
                    failReason = "Node is on a different vertical level";
                    continue;
                }

                OutputArgument outDensity = new OutputArgument();
                OutputArgument outFlags = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, nodePos.X, nodePos.Y, nodePos.Z, outDensity, outFlags))
                {
                    int density = outDensity.GetResult<int>();
                    int flags = outFlags.GetResult<int>();

                    string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, nodePos.X, nodePos.Y, nodePos.Z);
                    bool isUrban = _urbanZones.Contains(currentZone);

                    if (density == 0) { failReason = "Node has zero traffic density"; continue; }
                    if ((flags & 8) != 0) { failReason = "Node is switched off"; continue; }

                    if (isUrban && (flags & 1) != 0)
                    {
                        failReason = "Node is an alley/dirt path in the city";
                        continue;
                    }
                }

                if (horizDist < (CROSS_STREET_MIN_DIST - 10f) || horizDist > (OPEN_ROAD_MAX_DIST + 10f))
                {
                    failReason = "Node fell out of absolute bounds";
                    continue;
                }

                bool isOnScreen = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, nodePos.X, nodePos.Y, nodePos.Z, 3.0f);

                if (isOnScreen && horizDist < LOD_HAZE_FALLBACK_DIST)
                {
                    failReason = "Node bled into screen and is too close to pop-in smoothly";
                    continue;
                }

                bool tooCloseToAnother = false;
                foreach (Vehicle activeSpawn in _activeSwaps)
                {
                    if (activeSpawn.Exists() && GetHorizontalDistance(nodePos, activeSpawn.Position) < 150f)
                    {
                        tooCloseToAnother = true;
                        break;
                    }
                }
                if (tooCloseToAnother)
                {
                    failReason = "Too close to existing spawn";
                    continue;
                }

                RaycastResult hit = World.Raycast(camPos, nodePos, IntersectFlags.Map | IntersectFlags.Vehicles);

                bool hidden = false;
                if (hit.DidHit)
                {
                    if (hit.HitPosition.DistanceTo(nodePos) > RAYCAST_CORNER_PADDING) hidden = true;
                }
                else if (horizDist >= LOD_HAZE_FALLBACK_DIST)
                {
                    hidden = true;
                }

                if (hidden)
                {
                    bool isDirt = IsNodeOnDirt(nodePos);

                    // Get street hashes for player and node to prefer same-road spawns
                    OutputArgument playerStreetArg = new OutputArgument();
                    OutputArgument playerCross = new OutputArgument();
                    Function.Call(Hash.GET_STREET_NAME_AT_COORD, pPos.X, pPos.Y, pPos.Z, playerStreetArg, playerCross);
                    int playerStreetHash = playerStreetArg.GetResult<int>();

                    OutputArgument nodeStreetArg = new OutputArgument();
                    OutputArgument nodeCross = new OutputArgument();
                    Function.Call(Hash.GET_STREET_NAME_AT_COORD, nodePos.X, nodePos.Y, nodePos.Z, nodeStreetArg, nodeCross);
                    int nodeStreetHash = nodeStreetArg.GetResult<int>();

                    bool sameRoad = (playerStreetHash != 0 && playerStreetHash == nodeStreetHash);

                    // Directional alignment: prefer nodes ahead of player's forward vector
                    Vector3 dirToNode = new Vector3(nodePos.X - pPos.X, nodePos.Y - pPos.Y, 0f);
                    if (dirToNode.Length() > 0.001f) dirToNode = dirToNode.Normalized;
                    float forwardDot = Vector3.Dot(forward2D, dirToNode); // 1.0 = straight ahead

                    // If it's the same road and roughly ahead, prefer immediately
                    if (sameRoad && forwardDot > 0.5f)
                    {
                        if (SpawnVehicleAtNode(nodePos, nodeHeading, isDirt))
                        {
                            foundSpawn = true;
                            break;
                        }
                        else
                        {
                            // failed to spawn here; record reason and continue
                            failReason = "Spawn failed on a preferred same-road node";
                            continue;
                        }
                    }

                    // Score other candidates so we can fall back to the best option
                    float score = 0f;
                    // prefer same road even if not perfectly ahead
                    if (sameRoad) score += 250f;
                    // directional preference (ahead is better)
                    score += forwardDot * 200f; // can be negative if behind
                    // closer nodes score higher
                    score += Math.Max(0f, (OPEN_ROAD_MAX_DIST - horizDist));
                    // penalize nodes that are too far off the ideal range
                    if (horizDist > OPEN_ROAD_MAX_DIST) score -= (horizDist - OPEN_ROAD_MAX_DIST) * 0.5f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestNodePos = nodePos;
                        bestNodeHeading = nodeHeading;
                        bestNodeOnDirt = isDirt;
                        haveBest = true;
                    }
                }
                else
                {
                    failReason = "Node is not physically hidden by geometry or traffic";
                }
            }
        }

        if (foundSpawn)
        {
            if (forceSpawn && ENABLE_DEBUG_NOTIFICATIONS) GTA.UI.Notification.Show("~g~Forced Spawn Successful!");
            _nextSpawnTime = Game.GameTime + _rnd.Next(ModSettings.MinSwapCooldown, ModSettings.MaxSwapCooldown);
        }
        else
        {
            // If we didn't find an immediate preferred spawn, try the best-scored fallback
            if (!foundSpawn && haveBest)
            {
                if (SpawnVehicleAtNode(bestNodePos, bestNodeHeading, bestNodeOnDirt))
                {
                    foundSpawn = true;
                }
                else
                {
                    // fallback failed; let the failure notification show original reason
                }
            }

            if (forceSpawn && ENABLE_DEBUG_NOTIFICATIONS) GTA.UI.Notification.Show($"~r~Forced Spawn Failed:~s~ {failReason}");
        }
    }

    private float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private bool SpawnVehicleAtNode(Vector3 spawnPos, float heading, bool onDirt)
    {
        SelectionLayer layer;
        string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, spawnPos.X, spawnPos.Y, spawnPos.Z);
        bool isUrban = _urbanZones.Contains(currentZone);

        if (isUrban && onDirt) return false;

        if (onDirt && _zoneRegistry.ContainsKey("_OVERRIDE_OFFROAD_"))
        {
            layer = _zoneRegistry["_OVERRIDE_OFFROAD_"].PickLayer();
            layer.SourceProfile = "OFFROAD (Dirt Override)";
        }
        else { layer = GetLayerForLocation(spawnPos); }

        if (layer.List == null || layer.List.Count == 0) return false;

        string modelName = null;
        var candidates = layer.List.Except(_recentSpawnHistory).ToList();

        if (candidates.Count == 0 && layer.List.Count > 1)
        {
            _recentSpawnHistory.RemoveRange(0, _recentSpawnHistory.Count / 2);
            candidates = layer.List.Except(_recentSpawnHistory).ToList();
        }

        if (candidates.Count > 0) modelName = candidates[_rnd.Next(candidates.Count)];
        else modelName = layer.List.ElementAt(_rnd.Next(layer.List.Count));

        if (modelName == null) return false;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return false;
        model.Request();

        int timeout = Game.GameTime + 1000;
        while (!model.IsLoaded && Game.GameTime < timeout)
        {
            Script.Yield();
        }
        if (!model.IsLoaded) { model.MarkAsNoLongerNeeded(); return false; }

        Vehicle newVehicle = World.CreateVehicle(model, spawnPos, heading);

        if (newVehicle != null)
        {
            // ==========================================
            // THE LANE-SNAP FIX
            // Push the car 2.5 meters to its right so it spawns 
            // in the actual lane instead of on the center yellow line.
            // ==========================================
            newVehicle.Position = newVehicle.Position + (newVehicle.RightVector * 2.5f);

            // Now we drop it to the pavement so the suspension settles correctly
            newVehicle.PlaceOnGround();

            _activeSwaps.Add(newVehicle);
            _recentSpawnHistory.Add(modelName);
            if (_recentSpawnHistory.Count > ModSettings._historyCapacity) _recentSpawnHistory.RemoveAt(0);

            newVehicle.IsEngineRunning = true;

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));

            Function.Call(Hash.DECOR_SET_INT, newVehicle, DECOR_NAME, 1);

            Ped driver = newVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
            if (driver != null)
            {
                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);
                driver.BlockPermanentEvents = false;
                Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, WANDER_DRIVE_SPEED, (int)DriveStyle);
            }

            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, newVehicle, true, 1);

            newVehicle.ForwardSpeed = SPAWN_FORWARD_SPEED;

            if (ModSettings.TrafficShowBlips) CreateBlip(newVehicle, modelName);

            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    private bool IsNodeOnDirt(Vector3 pos)
    {
        OutputArgument outDensity = new OutputArgument();
        OutputArgument outFlags = new OutputArgument();
        if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, pos.X, pos.Y, pos.Z, outDensity, outFlags))
        {
            if ((outFlags.GetResult<int>() & (int)VehicleNodeFlags.Dirt) != 0) return true;
        }
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
    private SelectionLayer GetLayerForLocation(Vector3 pos) { string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z); if (string.IsNullOrEmpty(zone) || _bannedZones.Contains(zone)) return new SelectionLayer(); return _zoneRegistry.ContainsKey(zone) ? _zoneRegistry[zone].PickLayer() : new SelectionLayer(); }

    private void CreateBlip(Vehicle v, string modelKey)
    {
        Blip b = ModUtilities.CreateVehicleBlip(v, BlipColor.Blue);
        _activeBlips.Add(b);
    }

    private void CleanupBlips()
    {
        Ped player = Game.Player.Character;
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            Blip b = _activeBlips[i];
            if (!b.Exists() || b.Entity == null || !b.Entity.Exists() || player.IsInVehicle((Vehicle)b.Entity) || b.Entity.IsDead)
            {
                if (b.Exists()) b.Delete();
                _activeBlips.RemoveAt(i);
            }
        }
    }

    private void ReleaseAllToGame()
    {
        foreach (var blip in _activeBlips)
        {
            if (blip != null && blip.Exists()) blip.Delete();
        }
        _activeBlips.Clear();

        foreach (var vehicle in _activeSwaps)
        {
            if (vehicle != null && vehicle.Exists()) vehicle.MarkAsNoLongerNeeded();
        }

        _activeSwaps.Clear();
    }

    private void OnAborted(object sender, EventArgs e) { foreach (var b in _activeBlips) if (b.Exists()) b.Delete(); }
    public struct SelectionLayer { public HashSet<string> List; public SpawnBehavior Behavior; public string SourceProfile; }
    [Flags] public enum VehicleNodeFlags { None = 0, Dirt = 32 }
}