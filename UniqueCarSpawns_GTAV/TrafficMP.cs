using System;
using System.Collections.Generic;
using System.Windows.Forms; // Added for Key Handling
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficMP : Script
{
    // ==========================================
    //              QUICK SETTINGS
    // ==========================================
    private bool ShowBlips = true;
    private float SpawnDistance = 130.0f;

    // Increased to 230 to safely cover wider searches + spawn buffers
    private float DespawnDistance = 230.0f;

    private int SpawnChance = 100;
    private int CheckInterval = 1000; // 1s Check for smoother spawning
    private int RareCarChance = 15;
    // ==========================================

    private Vehicle _activeVehicle;
    private Ped _activeDriver;
    private Blip _activeBlip;
    private int _nextSpawnCheckTime = 0;
    private Random _rnd = new Random();

    private Vehicle _fadingVehicle;
    private int _fadingAlpha = 0;
    private bool _isInMissionMode = false;
    private string _lastGlobalModel = "";

    private Dictionary<HashSet<string>, SpawnBehavior> _behaviorRegistry;
    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "AIRP", "ZQ_UAR", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL", "TATAMO", "MTJOSE" };
    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();
    private HashSet<string> _ruralZones = new HashSet<string> { "DESRT", "MTCHIL", "CANNY", "CCREAK", "GREATC" };
    private HashSet<string> _denseCityZones = new HashSet<string> { "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "HAWICK", "BURTON", "ALTA", "EAST_V", "CHAMH", "DAVIS", "RANCHO", "STRAW", "PALETO", "SANDY", "GRAPES" };

    public TrafficMP()
    {
        InitializeRegistry();
        InitializeZones();
        Tick += OnTick;
        Aborted += OnAborted;
        KeyDown += OnDebugKeyDown;
    }

    private void InitializeRegistry()
    {
        _behaviorRegistry = new Dictionary<HashSet<string>, SpawnBehavior>();
        _behaviorRegistry.Add(VehList.models_supers_common, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_city, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_classics_common, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_lowriders, SpawnBehavior.RandomSpec);
        _behaviorRegistry.Add(VehList.models_general_common, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_general_rare, SpawnBehavior.Stock);
    }

    private void InitializeZones()
    {
        ZoneProfile ruralProfile = new ZoneProfile();
        ruralProfile.AddIngredient(VehList.models_rural, 50);
        ruralProfile.AddIngredient(VehList.models_general_common, 30);
        ruralProfile.AddIngredient(VehList.models_general_rare, 10);
        ruralProfile.AddIngredient(VehList.models_wacky, 10);

        ZoneProfile richProfile = new ZoneProfile();
        richProfile.AddIngredient(VehList.models_supers_common, 40);
        richProfile.AddIngredient(VehList.models_classics_common, 40);
        richProfile.AddIngredient(VehList.models_city, 20);

        ZoneProfile ghettoProfile = new ZoneProfile();
        ghettoProfile.AddIngredient(VehList.models_lowriders, 50);
        ghettoProfile.AddIngredient(VehList.models_general_common, 40);
        ghettoProfile.AddIngredient(VehList.models_general_rare, 10);

        ZoneProfile urbanProfile = new ZoneProfile();
        urbanProfile.AddIngredient(VehList.models_city, 50);
        urbanProfile.AddIngredient(VehList.models_general_common, 30);
        urbanProfile.AddIngredient(VehList.models_general_rare, 20);

        ZoneProfile industrialProfile = new ZoneProfile();
        industrialProfile.AddIngredient(VehList.models_general_common, 50);
        industrialProfile.AddIngredient(VehList.models_general_rare, 50);

        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON");
        AssignToProfile(industrialProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI");
    }

    private void AssignToProfile(ZoneProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }

    private void OnTick(object sender, EventArgs e)
    {
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);
        if (isMissionActive) { if (!_isInMissionMode) { ReleaseResourcesToGame(); _isInMissionMode = true; } return; }
        else if (_isInMissionMode) { _isInMissionMode = false; _nextSpawnCheckTime = Game.GameTime + 2000; }

        if (_fadingVehicle != null && _fadingVehicle.Exists())
        {
            if (_fadingAlpha < 255) { _fadingAlpha += 15; _fadingVehicle.Opacity = _fadingAlpha; Function.Call(Hash.SET_ENTITY_ALPHA, _fadingVehicle, _fadingAlpha, false); }
            else { Function.Call(Hash.RESET_ENTITY_ALPHA, _fadingVehicle); _fadingVehicle = null; }
        }

        Ped player = Game.Player.Character;
        if (_activeVehicle != null && _activeVehicle.Exists() && player.IsInVehicle(_activeVehicle)) ReleaseVehicleToPlayer();
        ManageCleanup(player);

        if (_activeVehicle == null && Game.GameTime > _nextSpawnCheckTime)
        {
            ManageSpawning(player);
            _nextSpawnCheckTime = Game.GameTime + CheckInterval;
        }
    }

    private void ManageSpawning(Ped player)
    {
        if (_rnd.Next(1, 101) > SpawnChance) return;
        if (IsZoneBanned(player.Position)) return;

        // 1. CALCULATE TERRAIN DEVIATION
        // Check if the PLAYER is on the ground (Hill/Flat) or in the air (Bridge)
        // This is key: If player is 80m above the terrain, they are on a bridge. We need to look for nodes 80m above the terrain.
        OutputArgument playerGroundZArg = new OutputArgument();
        float playerDeviation = 0f;

        if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, player.Position.X, player.Position.Y, player.Position.Z, playerGroundZArg, false))
        {
            float gZ = playerGroundZArg.GetResult<float>();
            playerDeviation = player.Position.Z - gZ;
        }

        // 2. PROJECT SEARCH POS (Flattened)
        // Use Z=0 for the forward vector so we don't dig into hills
        Vector3 flatFwd = player.ForwardVector;
        flatFwd.Z = 0; flatFwd.Normalize();

        float testDist = SpawnDistance;
        Vector3 searchPos = player.Position + (flatFwd * testDist);

        // 3. APPLY SMART HEIGHT
        // Find the terrain height at the spawn point, then ADD the player's deviation (Bridge Height)
        OutputArgument targetGroundZArg = new OutputArgument();
        if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, searchPos.X, searchPos.Y, searchPos.Z + 100f, targetGroundZArg, false))
        {
            float tGZ = targetGroundZArg.GetResult<float>();
            searchPos.Z = tGZ + playerDeviation; // Snap to the correct "Layer" (Ground or Bridge)
        }

        // 4. CURVE CHECK
        bool isVisible = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, searchPos.X, searchPos.Y, searchPos.Z, 2.0f);
        if (!isVisible)
        {
            testDist = 110.0f;
            searchPos = player.Position + (flatFwd * testDist);
            // Re-apply Smart Height for closer point
            if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, searchPos.X, searchPos.Y, searchPos.Z + 100f, targetGroundZArg, false))
            {
                searchPos.Z = targetGroundZArg.GetResult<float>() + playerDeviation;
            }
        }

        // 5. WIDE SEARCH (Intersection Fix)
        // 60.0f radius allows grabbing cross-streets and winding roads
        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, 0, 60.0f, 0);

        Vector3 spawnPos = outPos.GetResult<Vector3>();
        float spawnHeading = outHead.GetResult<float>();

        if (spawnPos == Vector3.Zero) return;
        if (player.Position.DistanceTo(spawnPos) < 100.0f) return;
        if (IsZoneBanned(spawnPos)) return;

        // 6. SNAP CHECK (Restored MaxSnap logic)
        float snapDist = Vector2.Distance(new Vector2(searchPos.X, searchPos.Y), new Vector2(spawnPos.X, spawnPos.Y));
        string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, player.Position.X, player.Position.Y, player.Position.Z);
        float maxSnap = (_ruralZones.Contains(currentZone)) ? 80.0f : 55.0f;

        if (snapDist > maxSnap) return;

        // 7. SMART VERTICAL CHECK (The Hill Fix)
        // Instead of verifying Z vs Player Z, we verify Z vs Terrain Z.
        // If the NODE's deviation from terrain matches the PLAYER'S deviation, it's a valid path.
        // This allows spawning 50m above the player IF both are on the ground (Hill).

        float nodeDeviation = 0f;
        if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, spawnPos.X, spawnPos.Y, spawnPos.Z + 5.0f, targetGroundZArg, false))
        {
            nodeDeviation = spawnPos.Z - targetGroundZArg.GetResult<float>();
        }

        // Allow 10m tolerance for deviation (e.g. slight bumps/dips)
        if (Math.Abs(playerDeviation - nodeDeviation) > 10.0f) return;

        SpawnCandidate candidate = GetCandidateForLocation(spawnPos);
        if (string.IsNullOrEmpty(candidate.ModelName) || candidate.ModelName == _lastGlobalModel) return;

        CreateTrafficEntity(candidate, spawnPos, spawnHeading);
    }

    private void ReleaseVehicleToPlayer()
    {
        if (_activeVehicle != null && _activeVehicle.Exists())
        {
            _activeVehicle.Opacity = 255;
            Function.Call(Hash.RESET_ENTITY_ALPHA, _activeVehicle);
            _activeVehicle.MarkAsNoLongerNeeded();
        }
        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();
        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.MarkAsNoLongerNeeded();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.MarkAsNoLongerNeeded();
        _activeVehicle = null; _activeDriver = null; _activeBlip = null; _fadingVehicle = null;
        _nextSpawnCheckTime = Game.GameTime + CheckInterval;
    }

    private void ManageCleanup(Ped player)
    {
        if (_activeVehicle != null && !_activeVehicle.Exists()) { RemoveResources(); return; }
        if (_activeVehicle != null && _activeVehicle.Exists())
        {
            if (player.Position.DistanceTo(_activeVehicle.Position) > DespawnDistance)
            {
                if (!player.IsInVehicle(_activeVehicle)) RemoveResources();
            }
        }
    }

    //
    private void CreateTrafficEntity(SpawnCandidate candidate, Vector3 pos, float heading)
    {
        Model model = new Model(candidate.ModelName);
        if (!model.IsValid || !model.IsInCdImage) return;
        model.Request(500);
        if (!model.IsLoaded) return;

        Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, pos.X, pos.Y, pos.Z, 5.0f, false, false, false, false, false);
        _activeVehicle = World.CreateVehicle(model, pos, heading);

        if (_activeVehicle != null)
        {
            _activeVehicle.Opacity = 0; Function.Call(Hash.SET_ENTITY_ALPHA, _activeVehicle, 0, false);
            _fadingVehicle = _activeVehicle; _fadingAlpha = 0;
            _activeVehicle.IsPersistent = true; _activeVehicle.IsEngineRunning = true;
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, _activeVehicle, 5.0f);

            OutputArgument outRoadHead = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, pos.X, pos.Y, pos.Z, new OutputArgument(), outRoadHead, 1, 3.0f, 0))
            {
                _activeVehicle.Heading = outRoadHead.GetResult<float>();
            }

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, _activeVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, _activeVehicle, _rnd.Next(0, comboCount));

            CarMod.ApplyStyle(_activeVehicle, candidate.Behavior, candidate.ModelName);

            float driveSpeed = 20.0f;
            Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, _activeVehicle, driveSpeed);

            _activeDriver = _activeVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
            if (_activeDriver != null)
            {
                _activeDriver.BlockPermanentEvents = false;
                _activeDriver.Task.CruiseWithVehicle(_activeVehicle, driveSpeed, (VehicleDrivingFlags)786603);
            }
            if (ShowBlips)
            {
                _activeBlip = _activeVehicle.AddBlip(); _activeBlip.Sprite = BlipSprite.PersonalVehicleCar;
                _activeBlip.Color = BlipColor.Purple; _activeBlip.Name = "Exotic Traffic";
                Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
            }
            _lastGlobalModel = candidate.ModelName;
        }
        model.MarkAsNoLongerNeeded();
    }

    private SpawnCandidate GetCandidateForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (_bannedZones.Contains(zone)) return new SpawnCandidate();
        if (_zoneRegistry.ContainsKey(zone))
        {
            ZoneProfile profile = _zoneRegistry[zone];
            HashSet<string> selectedList = profile.PickList();
            if (selectedList != null) return PickFromList(selectedList);
        }
        return new SpawnCandidate();
    }

    private SpawnCandidate PickFromList(HashSet<string> list)
    {
        string modelName = VehicleSelector.GetNext(list, _excludedModels);
        if (string.IsNullOrEmpty(modelName)) return new SpawnCandidate();
        return new SpawnCandidate { ModelName = modelName, Behavior = _behaviorRegistry.ContainsKey(list) ? _behaviorRegistry[list] : SpawnBehavior.Stock };
    }

    private void RemoveResources()
    {
        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.Delete();
        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.Delete();
        _activeBlip = null; _activeDriver = null; _activeVehicle = null; _fadingVehicle = null;
        _nextSpawnCheckTime = Game.GameTime + CheckInterval;
    }

    private void ReleaseResourcesToGame()
    {
        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();
        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.MarkAsNoLongerNeeded();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.MarkAsNoLongerNeeded();
        _activeBlip = null; _activeDriver = null; _activeVehicle = null; _fadingVehicle = null;
        _nextSpawnCheckTime = Game.GameTime + CheckInterval;
    }
    private void OnAborted(object sender, EventArgs e) => RemoveResources();
    private bool IsZoneBanned(Vector3 pos) => _bannedZones.Contains(Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z));
    private void OnDebugKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Z)
        {
            if (_activeVehicle != null && _activeVehicle.Exists()) RemoveResources();
            GTA.UI.Screen.ShowSubtitle("~g~DEBUG: Force Spawn Triggered");
            ManageSpawning(Game.Player.Character);
        }
    }
}

public struct SpawnCandidate { public string ModelName; public SpawnBehavior Behavior; }
public class ZoneProfile
{
    private struct Ingredient { public HashSet<string> List; public int Weight; }
    private List<Ingredient> _ingredients = new List<Ingredient>();
    private int _totalWeight = 0;
    private Random _rnd = new Random();
    public void AddIngredient(HashSet<string> list, int weight) { _ingredients.Add(new Ingredient { List = list, Weight = weight }); _totalWeight += weight; }
    public HashSet<string> PickList()
    {
        if (_ingredients.Count == 0) return null;
        int roll = _rnd.Next(0, _totalWeight);
        int current = 0;
        foreach (var item in _ingredients) { current += item.Weight; if (roll < current) return item.List; }
        return _ingredients[0].List;
    }
}