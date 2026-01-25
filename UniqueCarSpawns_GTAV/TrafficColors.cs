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

    // =============================================================

    private int _nextCheckTime = 0;
    private Random _rnd = new Random();
    private bool _debugMode = false;

    private HashSet<int> _processedVehicles = new HashSet<int>();
    private List<Blip> _activeBlips = new List<Blip>();

    public TrafficColors()
    {
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
            GTA.UI.Notification.Show($"TrafficColors Debug: {status} (Tracked: {_processedVehicles.Count})");
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

        // Grab the player's last vehicle ONCE per loop to save processing
        Vehicle lastVehicle = Game.Player.LastVehicle;

        foreach (Vehicle v in allVehicles)
        {
            // --- BASIC CHECKS ---
            if (v == null || !v.Exists()) continue;
            if (_processedVehicles.Contains(v.Handle)) continue;

            // --- PROTECTION LAYER (Updated) ---

            // 1. Is the player driving it?
            if (v.Driver == Game.Player.Character) continue;

            // 2. NEW: Is this the last car the player used?
            // This protects your car during cutscenes or immediately after you exit.
            if (lastVehicle != null && v.Handle == lastVehicle.Handle) continue;

            // 3. Persistence Check
            // Protects mission vehicles and saved cars.
            if (v.IsPersistent) continue;

            // 4. Mission Entity Check
            if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) continue;

            // --- EXCLUSIONS ---
            if (v.ClassType == VehicleClass.Emergency) continue;
            if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) continue;

            // --- ACTION ---
            ApplyRandomCombination(v);

            // --- MARK AS DONE ---
            _processedVehicles.Add(v.Handle);
            AddDebugBlip(v);
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
                World.DrawMarker(
                    MarkerType.Chevron1,
                    v.Position + new Vector3(0, 0, 1.5f),
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(0.5f, 0.5f, 0.5f),
                    Color.Purple
                );
            }
        }
    }
}