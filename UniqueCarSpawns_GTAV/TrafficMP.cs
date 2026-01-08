using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficMP : Script
{
    // ==========================================
    //              QUICK SETTINGS
    // ==========================================
    private bool ShowBlips = true;

    // 260.0f allows the 240m probe to exist without instant cleanup
    private float DespawnDistance = 320.0f;

    private int SpawnChance = 100;
    private int CheckInterval = 1000;
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

        OutputArgument playerGroundZArg = new OutputArgument();
        float playerDeviation = 0f;

        if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, player.Position.X, player.Position.Y, player.Position.Z, playerGroundZArg, false))
        {
            float gZ = playerGroundZArg.GetResult<float>();
            playerDeviation = player.Position.Z - gZ;
        }

        Vector3 flatFwd = player.ForwardVector;
        flatFwd.Z = 0; flatFwd.Normalize();

        float[] probeDistances = { 240.0f, 170.0f, 120.0f };

        Vector3 finalSpawnPos = Vector3.Zero;
        float finalHeading = 0f;
        bool foundValidSpot = false;

        foreach (float dist in probeDistances)
        {
            Vector3 searchPos = player.Position + (flatFwd * dist);

            OutputArgument targetGroundZArg = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, searchPos.X, searchPos.Y, searchPos.Z + 100f, targetGroundZArg, false))
            {
                float tGZ = targetGroundZArg.GetResult<float>();
                searchPos.Z = tGZ + playerDeviation;
            }

            // Tightened Radius (Prevents parking lot spawns)
            float searchRadius = (dist > 200f) ? 45.0f : 30.0f;

            OutputArgument outPos = new OutputArgument();
            OutputArgument outHead = new OutputArgument();

            Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, 0, searchRadius, 0);

            Vector3 candidatePos = outPos.GetResult<Vector3>();
            float candidateHead = outHead.GetResult<float>();

            if (candidatePos == Vector3.Zero) continue;
            if (IsZoneBanned(candidatePos)) continue;

            float snapDist = Vector2.Distance(new Vector2(searchPos.X, searchPos.Y), new Vector2(candidatePos.X, candidatePos.Y));
            string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, player.Position.X, player.Position.Y, player.Position.Z);
            float maxSnap = (_ruralZones.Contains(currentZone)) ? 90.0f : 60.0f;

            if (snapDist > maxSnap) continue;

            float nodeDeviation = 0f;
            if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, candidatePos.X, candidatePos.Y, candidatePos.Z + 5.0f, targetGroundZArg, false))
            {
                nodeDeviation = candidatePos.Z - targetGroundZArg.GetResult<float>();
            }
            if (Math.Abs(playerDeviation - nodeDeviation) > 10.0f) continue;

            // VISIBILITY & DISTANCE CHECK
            float distToPlayer = player.Position.DistanceTo(candidatePos);

            if (distToPlayer < 85.0f) continue;

            // [THE DOOMED CHECK]
            // If the found node is further than our Cleanup Distance, ignore it.
            // This prevents the "Flash" where we spawn a car only to delete it 1ms later.
            if (distToPlayer > DespawnDistance - 10.0f) continue;

            // A. Horizon 
            if (distToPlayer > 215.0f)
            {
                finalSpawnPos = candidatePos; finalHeading = candidateHead; foundValidSpot = true; break;
            }

            // B. Frustum Check
            bool isWithinScreenBounds = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, candidatePos.X, candidatePos.Y, candidatePos.Z, 2.0f);

            if (!isWithinScreenBounds)
            {
                finalSpawnPos = candidatePos; finalHeading = candidateHead; foundValidSpot = true; break;
            }

            // C. Raycast Check
            RaycastResult ray = World.Raycast(GameplayCamera.Position, candidatePos, IntersectFlags.Map);
            if (ray.DidHit)
            {
                finalSpawnPos = candidatePos; finalHeading = candidateHead; foundValidSpot = true; break;
            }
        }

        if (!foundValidSpot) return;

        SpawnCandidate candidate = GetCandidateForLocation(finalSpawnPos);
        if (string.IsNullOrEmpty(candidate.ModelName) || candidate.ModelName == _lastGlobalModel) return;

        CreateTrafficEntity(candidate, finalSpawnPos, finalHeading);
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

            // REMOVED: Redundant GetClosestVehicleNode call that was overwriting the good heading with bad data.
            // Now strictly uses the 'heading' passed from ManageSpawning.

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