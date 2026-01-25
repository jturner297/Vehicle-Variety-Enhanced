using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficColors : Script
{
    // =============================================================
    //                          SETTINGS
    // =============================================================

    private int _checkInterval = 250;

    // PACING: Only change 40% of traffic. 
    private int _swapChance = 50;

    // BLACKLIST: Specific models to IGNORE (e.g. Utility/Service vehicles)
    private HashSet<string> _excludedModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "boxville", "boxville2", "taxi", "trash", "trash2", "boxville4"
    };

    // =============================================================

    private int _nextCheckTime = 0;
    private Random _rnd = new Random();
    private bool _debugMode = false;

    private HashSet<int> _processedVehicles = new HashSet<int>();
    private List<Blip> _activeBlips = new List<Blip>();

    private HashSet<int> _excludedHashes = new HashSet<int>();

    public TrafficColors()
    {
        // FIX 1: Use Native GET_HASH_KEY instead of obsolete Game.GenerateHash
        foreach (string name in _excludedModelNames)
        {
            int hash = Function.Call<int>(Hash.GET_HASH_KEY, name);
            _excludedHashes.Add(hash);
        }

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11)
        {
            _debugMode = !_debugMode;
            string status = _debugMode ? "~g~ON" : "~r~OFF";

            // FIX 2: Use PostTicker instead of obsolete Show
            GTA.UI.Notification.PostTicker($"TrafficColors Debug: {status} (Tracked: {_processedVehicles.Count})", true);

            ToggleBlipVisibility(_debugMode);
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (Game.GameTime > _nextCheckTime)
        {
            CleanupLists();
            ProcessTraffic();
            _nextCheckTime = Game.GameTime + _checkInterval;
        }

        if (_debugMode) DrawDebugMarkers();
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var b in _activeBlips) if (b.Exists()) b.Delete();
        _processedVehicles.Clear();
    }

    private void ProcessTraffic()
    {
        Vehicle[] allVehicles = World.GetAllVehicles();
        Vehicle lastVehicle = Game.Player.LastVehicle;

        foreach (Vehicle v in allVehicles)
        {
            // --- BASIC CHECKS ---
            if (v == null || !v.Exists()) continue;

            if (_processedVehicles.Contains(v.Handle)) continue;

            // --- PROTECTION LAYER ---
            if (v.Driver == Game.Player.Character) continue;
            if (lastVehicle != null && v.Handle == lastVehicle.Handle) continue;
            if (v.IsPersistent) continue;
            if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) continue;

            // --- EXCLUSIONS ---
            if (v.ClassType == VehicleClass.Emergency) continue;
            if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) continue;

            // Model Blacklist Check
            if (_excludedHashes.Contains(v.Model.Hash))
            {
                _processedVehicles.Add(v.Handle);
                continue;
            }

            // --- ACTION (THE PACING FIX) ---
            if (_rnd.Next(0, 100) < _swapChance)
            {
                ApplyRandomCombination(v);
                if (_debugMode) AddDebugBlip(v);
            }

            // Mark as done so we don't re-roll constantly
            _processedVehicles.Add(v.Handle);
        }
    }

    private void ApplyRandomCombination(Vehicle v)
    {
        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, v);
        if (comboCount > 0)
        {
            int randomID = _rnd.Next(0, comboCount);
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, v, randomID);
        }
    }

    // ==========================================
    //           CLEANUP & DEBUG
    // ==========================================
    private void CleanupLists()
    {
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            if (!_activeBlips[i].Exists()) _activeBlips.RemoveAt(i);
        }

        if (_processedVehicles.Count > 200)
        {
            _processedVehicles.RemoveWhere(handle => !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle));
        }
    }

    private void AddDebugBlip(Vehicle v)
    {
        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.PersonalVehicleCar;
        b.Color = BlipColor.Purple;
        b.Scale = 0.6f;
        b.Name = "Randomized Color";
        b.IsShortRange = true;
        b.Alpha = _debugMode ? 255 : 0;
        _activeBlips.Add(b);
    }

    private void ToggleBlipVisibility(bool visible)
    {
        foreach (var b in _activeBlips)
        {
            if (b.Exists()) b.Alpha = visible ? 255 : 0;
        }
    }

    private void DrawDebugMarkers()
    {
        Vehicle[] nearbyVehs = World.GetNearbyVehicles(Game.Player.Character.Position, 80.0f);
        foreach (Vehicle v in nearbyVehs)
        {
            if (_processedVehicles.Contains(v.Handle))
            {
                // Only drawing logic for debug
            }
        }
    }
}