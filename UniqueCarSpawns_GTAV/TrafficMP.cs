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
    private float DespawnDistance = 260.0f;
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
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "AIRP", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };

    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();

    [Flags]
    public enum VehicleNodeFlags
    {
        OffRoad = 1 << 0,
        OnPlayersRoad = 1 << 1,
        NoBigVehicles = 1 << 2,
        SwitchedOff = 1 << 3,
        TunnelOrInterior = 1 << 4,
        LeadsToDeadEnd = 1 << 5,
        Highway = 1 << 6,
        Junction = 1 << 7,
        TrafficLight = 1 << 8,
        GiveWay = 1 << 9,
        Water = 1 << 10
    }

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
        ruralProfile.IsRural = true;
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

        ZoneProfile generalProfile = new ZoneProfile();
        generalProfile.AddIngredient(VehList.models_general_common, 50);
        generalProfile.AddIngredient(VehList.models_general_rare, 50);

        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON");
        AssignToProfile(generalProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO");
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

        // The probe gradient as defined in your current setup
        float[] probeDistances = { 75f, 110f, 170f, 240f };
        Vector3 finalSpawnPos = Vector3.Zero;
        float finalHeading = 0f;
        bool foundValidSpot = false;

        foreach (float dist in probeDistances)
        {
            // --- DYNAMIC ANGULAR OFFSET CALCULATION ---
            // At 75m, the angle is ~15 degrees (Forward Bias)
            // At 240m, the angle grows to ~45 degrees (Wide Sweep)
            float dynamicAngle = 10.0f + (dist / 240.0f) * 35.0f;

            float angleRad = dynamicAngle * (float)(Math.PI / 180.0f);
            float cosA = (float)Math.Cos(angleRad);
            float sinA = (float)Math.Sin(angleRad);

            Vector3 dirLeft = new Vector3(
                flatFwd.X * cosA - flatFwd.Y * sinA,
                flatFwd.X * sinA + flatFwd.Y * cosA,
                0
            );

            Vector3 dirRight = new Vector3(
                flatFwd.X * cosA + flatFwd.Y * sinA,
                -flatFwd.X * sinA + flatFwd.Y * cosA,
                0
            );

            Vector3[] searchDirections = { flatFwd, dirLeft, dirRight };
            // -------------------------------------------

            foreach (Vector3 searchDir in searchDirections)
            {
                Vector3 searchPos = player.Position + (searchDir * dist);

                OutputArgument targetGroundZArg = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, searchPos.X, searchPos.Y, searchPos.Z + 100f, targetGroundZArg, false))
                {
                    float tGZ = targetGroundZArg.GetResult<float>();
                    searchPos.Z = tGZ + playerDeviation;
                }

                float searchRadius = (dist > 200f) ? 45.0f : 30.0f;

                OutputArgument outPos = new OutputArgument();
                OutputArgument outHead = new OutputArgument();

                // Native: GET_CLOSEST_VEHICLE_NODE_WITH_HEADING (Flag 1 for lane alignment)
                Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, 1, searchRadius, 0);

                Vector3 candidatePos = outPos.GetResult<Vector3>();
                float candidateHead = outHead.GetResult<float>();

                if (candidatePos == Vector3.Zero) continue;
                if (IsZoneBanned(candidatePos)) continue;

                string nodeZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, candidatePos.X, candidatePos.Y, candidatePos.Z);
                bool isRuralNode = _zoneRegistry.ContainsKey(nodeZone) && _zoneRegistry[nodeZone].IsRural;

                // --- FILTERING ---
                OutputArgument outDensity = new OutputArgument();
                OutputArgument outFlags = new OutputArgument();

                if (Function.Call<bool>(Hash.GET_VEHICLE_NODE_PROPERTIES, candidatePos.X, candidatePos.Y, candidatePos.Z, outDensity, outFlags))
                {
                    int density = outDensity.GetResult<int>();
                    int flags = outFlags.GetResult<int>();

                    if (density == 0) continue;
                    if ((flags & (int)VehicleNodeFlags.SwitchedOff) != 0) continue;
                    if (!isRuralNode)
                    {
                        if ((flags & (int)VehicleNodeFlags.OffRoad) != 0) continue;
                    }
                }

                float snapDist = Vector2.Distance(new Vector2(searchPos.X, searchPos.Y), new Vector2(candidatePos.X, candidatePos.Y));
                float maxSnap = (isRuralNode) ? 90.0f : 60.0f;
                if (snapDist > maxSnap) continue;

                float nodeDeviation = 0f;
                if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, candidatePos.X, candidatePos.Y, candidatePos.Z + 5.0f, targetGroundZArg, false))
                {
                    nodeDeviation = candidatePos.Z - targetGroundZArg.GetResult<float>();
                }
                if (Math.Abs(playerDeviation - nodeDeviation) > 10.0f) continue;

                float distToPlayer = player.Position.DistanceTo(candidatePos);
                if (distToPlayer < 35.0f) continue;
                if (distToPlayer > DespawnDistance - 10.0f) continue;

                // Visibility Logic
                if (distToPlayer > 210.0f)
                {
                    finalSpawnPos = candidatePos; finalHeading = candidateHead; foundValidSpot = true; break;
                }

                bool isWithinScreenBounds = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, candidatePos.X, candidatePos.Y, candidatePos.Z, 1.0f);
                if (!isWithinScreenBounds)
                {
                    finalSpawnPos = candidatePos; finalHeading = candidateHead; foundValidSpot = true; break;
                }

                bool isLowBlocked = World.Raycast(GameplayCamera.Position, candidatePos + new Vector3(0, 0, 0.4f), IntersectFlags.Map).DidHit;
                bool isHighBlocked = World.Raycast(GameplayCamera.Position, candidatePos + new Vector3(0, 0, 1.3f), IntersectFlags.Map).DidHit;

                if (isLowBlocked || isHighBlocked)
                {
                    finalSpawnPos = candidatePos; finalHeading = candidateHead; foundValidSpot = true; break;
                }
            }
            if (foundValidSpot) break;
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
        if (_excludedModels.Contains(candidate.ModelName)) return;
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
    public bool IsRural { get; set; } = false;
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