using GTA;
using GTA.Math;
using GTA.Native;
﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

public class SpawnMP : Script
{
    ScriptSettings config;
    int vehicles_spawned;
    private int doors_config = 0;
    private int blip_config = 0;
    private int[] mode_type = new int[5];
    private float[] angle = new float[1];
    private GTA.Vehicle car;
    private int tuning_flag;
    private int tuning_hsw_flag;
    private int blip_color;
    private int mod_plate;
    private int plate_id = -1;
    private bool IsHSW = false;
    private Vehicle[] veh = new Vehicle[200];
    private Vehicle[] street_veh = new Vehicle[200];
    private List<Blip> marker = new List<Blip>();
    private int debugging = 0;
    private int _canSpawn = 1;
    private string mod_version = "1.72";
    Dictionary<SpawnId, Vehicle> vehDict = new Dictionary<SpawnId, Vehicle>();
    Dictionary<SpawnId, Blip> markerDict = new Dictionary<SpawnId, Blip>();

  


    struct SpawnPoint
    {
        public Vector3 Position;
        public float Heading;

        public SpawnPoint(Vector3 pos, float heading)
        {
            Position = pos;
            Heading = heading;
        }
    }

    enum SpawnId
    {
        ArenaHotring1,
        ArenaHotring2,
        ArenaSpeed1,
        ArenaSpeed2,
        ArenaOffroad1,
        ArenaOffroad2,

   
    }
    Dictionary<SpawnId, SpawnPoint> Spawns = new Dictionary<SpawnId, SpawnPoint>()
    {
        [SpawnId.ArenaHotring1] = new SpawnPoint(
            new Vector3(-206.046f, -1988.758f, 26.96269f),
        90.24596f
        ),
        [SpawnId.ArenaHotring2] = new SpawnPoint(
            new Vector3(1189.208f, 304.6935f, 81.48812f),
            146.9079f
        ),
        [SpawnId.ArenaSpeed1] = new SpawnPoint(
            new Vector3(-176.5869f, -2019.529f, 27.14398f),
             75.33392f
        ),
        [SpawnId.ArenaSpeed2] = new SpawnPoint(
            new Vector3(1117.519f, 257.8285f, 80.31487f),
          -122.0743f
        ),
        [SpawnId.ArenaOffroad1] = new SpawnPoint(
            new Vector3(-192.949f, -1928.497f, 27.20675f),
            -151.2955f
        ),
        [SpawnId.ArenaOffroad2] = new SpawnPoint(
            new Vector3(1151.233f, 183.6329f, 80.23096f),
           -53.71473f
        ),
    };


    

    public SpawnMP()
    {
        string onlineVersion = Function.Call<string>(Hash.GET_ONLINE_VERSION);
        if (onlineVersion != mod_version)
        {
            GTA.UI.Notification.PostTicker($"~r~WARNING:\n~s~Your version of the game is out of date. ~g~All MP Vehicles in SP ~s~will not be able to load vehicles from new updates.\n\nRequired Game Version:\n{mod_version}\nYour Game Version: {onlineVersion}", true);
        }

        config = ScriptSettings.Load("Scripts\\UniqueCarSpawns.ini");
        _canSpawn = config.GetValue<int>("MAIN", "parking_lots_spawn", 1);
        doors_config = config.GetValue<int>("MAIN", "doors", -1);
        blip_config = config.GetValue<int>("MAIN", "blips", -1); 
        tuning_flag = config.GetValue<int>("MAIN", "tuning", -1);
        tuning_hsw_flag = config.GetValue<int>("MAIN", "tuning_hsw", -1);
        mod_plate = config.GetValue<int>("MAIN", "new_license_plates", -1);
        blip_color = config.GetValue<int>("MAIN", "blip_color", -1);
        debugging = config.GetValue<int>("MAIN", "show_errors", 0);

        if (doors_config == -1) config.SetValue<int>("MAIN", "doors", 1);
        if (blip_config == -1) config.SetValue<int>("MAIN", "blips", 1);
        if (tuning_flag == -1) config.SetValue<int>("MAIN", "tuning", 1);
        if (tuning_hsw_flag == -1) config.SetValue<int>("MAIN", "tuning_hsw", 1);
        if (mod_plate == -1) config.SetValue<int>("MAIN", "doors", 0);
        if (blip_color == -1) config.SetValue<int>("MAIN", "blip_color", 3);

        config.Save();

        char symbol = '#';
        string[] lines = File.ReadAllLines("Scripts\\mp_blacklist.txt");
        string[] lines_addon = File.ReadAllLines("Scripts\\NewVehiclesList.txt");
        List<string> blacklist_str = new List<string>();
        List<string> new_list_str = new List<string>();

        foreach (string s in lines)
        {
            if (s.IndexOf(symbol) == -1)
                blacklist_str.Add(s);
        }

        foreach (string s in lines_addon)
        {
            if (s.IndexOf(symbol) == -1 && s.Length > 0)
            {
                new_list_str.Add(s);
            }
        }

        foreach (string line in new_list_str)
        {
            string[] veh_data = line.Split(',');
            try
            {
                AddCustomVehicle(veh_data[0], veh_data[1]);
            }
            catch
            {
                //GTA.UI.Notification.Show("Error in loading the vehicle Add-On. Check if the entries in NewVehiclesList.txt are correct and try again.");
                GTA.UI.Notification.PostTicker("Error in loading the vehicle Add-On. Check if the entries in NewVehiclesList.txt are correct and try again.", true);
            }
        }
        
        foreach (string hash in blacklist_str)
        {
            if (VehList.models_cult.Contains(hash))
                VehList.models_cult.Remove(hash);

            if (VehList.models_beach.Contains(hash))
                VehList.models_beach.Remove(hash);

            if (VehList.models_boats.Contains(hash))
                VehList.models_boats.Remove(hash);

            if (VehList.models_cemetery.Contains(hash))
                VehList.models_cemetery.Remove(hash);

            if (VehList.models_cheburek.Contains(hash))
                VehList.models_cheburek.Remove(hash);

            if (VehList.models_cinema.Contains(hash))
                VehList.models_cinema.Remove(hash);

            if (VehList.models_cluckin.Contains(hash))
                VehList.models_cluckin.Remove(hash);

            if (VehList.models_compacts.Contains(hash))
                VehList.models_compacts.Remove(hash);

            if (VehList.models_coupes.Contains(hash))
                VehList.models_coupes.Remove(hash);


            if (VehList.models_ghetto.Contains(hash))
                VehList.models_ghetto.Remove(hash);

            if (VehList.models_helicopter.Contains(hash))
                VehList.models_helicopter.Remove(hash);

            if (VehList.models_humanlabs.Contains(hash))
                VehList.models_humanlabs.Remove(hash);

            if (VehList.models_industrial.Contains(hash))
                VehList.models_industrial.Remove(hash);

            if (VehList.models_karting.Contains(hash))
                VehList.models_karting.Remove(hash);

            if (VehList.models_military_bikes.Contains(hash))
                VehList.models_military_bikes.Remove(hash);

            if (VehList.models_military_helicopters.Contains(hash))
                VehList.models_military_helicopters.Remove(hash);

            if (VehList.models_military_opressors.Contains(hash))
                VehList.models_military_opressors.Remove(hash);

            if (VehList.models_military_planes.Contains(hash))
                VehList.models_military_planes.Remove(hash);

            if (VehList.models_motorcycles.Contains(hash))
                VehList.models_motorcycles.Remove(hash);

            if (VehList.models_muscle.Contains(hash))
                VehList.models_muscle.Remove(hash);

            if (VehList.models_offroad.Contains(hash))
                VehList.models_offroad.Remove(hash);

            if (VehList.models_openwheel.Contains(hash))
                VehList.models_openwheel.Remove(hash);

            if (VehList.models_planes.Contains(hash))
                VehList.models_planes.Remove(hash);

            if (VehList.models_sedans.Contains(hash))
                VehList.models_sedans.Remove(hash);



            if (VehList.models_sportclassic.Contains(hash))
                VehList.models_sportclassic.Remove(hash);

            if (VehList.models_submarine.Contains(hash))
                VehList.models_submarine.Remove(hash);

            if (VehList.models_supers.Contains(hash))
                VehList.models_supers.Remove(hash);

            if (VehList.models_suvs.Contains(hash))
                VehList.models_suvs.Remove(hash);

   

            if (VehList.models_tuners.Contains(hash))
                VehList.models_tuners.Remove(hash);

            if (VehList.models_valentine.Contains(hash))
                VehList.models_valentine.Remove(hash);

            if (VehList.models_vans.Contains(hash))
                VehList.models_vans.Remove(hash);

            if (VehList.models_wastelander.Contains(hash))
                VehList.models_wastelander.Remove(hash);

            if (VehList.models_weaponboats.Contains(hash))
                VehList.models_weaponboats.Remove(hash);

            if (VehList.models_hsw.Contains(hash))
                VehList.models_hsw.Remove(hash);

            if (VehList.models_higgins.Contains(hash))
                VehList.models_higgins.Remove(hash);

            if (hash == "vivanite2") TrafficMP.disableTaxiFlag = 1;
        }

        Tick += OnTick;
        Aborted += OnAborded;
    }

    public enum Nodetype
    {
        AnyRoad,
        Road,
        Offroad,
        Water
    }

    void AddCustomVehicle(string Model, string Class)
    {
        switch(Class)
        {
            case "boats":
                VehList.models_boats.Add(Model);
                break;

            case "commercial":
                VehList.models_industrial.Add(Model);
                break;

            case "compacts":
                VehList.models_compacts.Add(Model);
                break;

            case "coupes":
                VehList.models_coupes.Add(Model);
                break;


            case "emergency":
                VehList.models_industrial.Add(Model);
                break;

            case "helicopters":
                VehList.models_helicopter.Add(Model);
                break;

            case "industrial":
                VehList.models_industrial.Add(Model);
                break;

            case "karting":
                VehList.models_karting.Add(Model);
                break;

            case "motorcycles":
                VehList.models_motorcycles.Add(Model);
                break;

            case "muscle":
                VehList.models_muscle.Add(Model);
                break;

            case "openwheel":
                VehList.models_openwheel.Add(Model);
                break;

            case "offroad":
                VehList.models_offroad.Add(Model);
                break;

            case "planes":
                VehList.models_planes.Add(Model);
                break;

            case "sedans":
                VehList.models_sedans.Add(Model);
                break;

            case "service":
                VehList.models_industrial.Add(Model);
                break;

            case "sports":
                VehList.models_sportclassic.Add(Model);
                break;

            case "sportsclassics":
                VehList.models_sportclassic.Add(Model);
                break;

            case "super":
                VehList.models_supers.Add(Model);
                break;

            case "suvs":
                VehList.models_suvs.Add(Model);
                break;

            case "vans":
                VehList.models_vans.Add(Model);
                break;
        }
    }

    /*string GenerateVehicleModelName(int index_db, int type)
   {
       string model_name = null;
       bool isEmpty;
       var random = new Random();
       switch (index_db)
       {

           case cult:
               isEmpty = !VehList.models_cult.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_cult[random.Next(VehList.models_cult.Count)];
               }
               break;

           case boats:
               isEmpty = !VehList.models_boats.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_boats[random.Next(VehList.models_boats.Count)];
               }
               break;

           case cemetery:
               isEmpty = !VehList.models_cemetery.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_cemetery[random.Next(VehList.models_cemetery.Count)];
               }
               break;

           case cheburek:
               isEmpty = !VehList.models_cheburek.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_cheburek[random.Next(VehList.models_cheburek.Count)];
                   plate_id = 8;
               }
               break;

           case cinema:
               isEmpty = !VehList.models_cinema.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_cinema[random.Next(VehList.models_cinema.Count)];
                   plate_id = 6;
               }
               break;


           case classics_1:
           case classics_2:
           case classics_3:
           case classics_4:
           case classics_5:
           case classics_6:
               isEmpty = !VehList.models_classics.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_classics[random.Next(VehList.models_classics.Count)];
               }
               break;
           case hyper_1:
           case hyper_2:
           case hyper_3:
           case hyper_4:
           case hyper_5:
               isEmpty = !VehList.models_supers.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_supers[random.Next(VehList.models_supers.Count)];
               }
               break;
           case compacts_1:
           case compacts_2:
               isEmpty = !VehList.models_compacts.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_compacts[random.Next(VehList.models_compacts.Count)];
               }
               break;

           case coupes_1:
           case coupes_2:
           case coupes_3:
           case coupes_4:
           case coupes_5:
           case coupes_6:
               isEmpty = !VehList.models_coupes.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_coupes[random.Next(VehList.models_coupes.Count)];
               }
               break;

           case ghetto_1:
           case ghetto_2:
           case ghetto_3:
           case ghetto_4:
           case ghetto_5:
           case ghetto_6:
               isEmpty = !VehList.models_ghetto.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_ghetto[random.Next(VehList.models_ghetto.Count)];
               }
               break;

           case helicopter:
               isEmpty = !VehList.models_helicopter.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_helicopter[random.Next(VehList.models_helicopter.Count)];
               }
               break;

           case humanlabs:
               isEmpty = !VehList.models_humanlabs.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_humanlabs[random.Next(VehList.models_humanlabs.Count)];
               }
               break;

           case industrial_1:
           case industrial_2:
           case industrial_3:
           case industrial_4:
               isEmpty = !VehList.models_industrial.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_industrial[random.Next(VehList.models_industrial.Count)];
               }
               break;

           case karting:
               isEmpty = !VehList.models_karting.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_karting[random.Next(VehList.models_karting.Count)];
               }
               break;


           case thruster:
               if (veh[index_db] == null && VehList.thruster_model != "Blocked")
               {
                   model_name = VehList.thruster_model;
               }
               break;




           case military_planes_1:
           case military_planes_2:
               isEmpty = !VehList.models_military_planes.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_military_planes[random.Next(VehList.models_military_planes.Count)];
               }
               break;

           case military_helicopters:
               isEmpty = !VehList.models_military_helicopters.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_military_helicopters[random.Next(VehList.models_military_helicopters.Count)];
               }
               break;

           case military_opressors:
               isEmpty = !VehList.models_military_opressors.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_military_opressors[random.Next(VehList.models_military_opressors.Count)];
               }
               break;

           case military_bikes:
               isEmpty = !VehList.models_military_bikes.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_military_bikes[random.Next(VehList.models_military_bikes.Count)];
               }
               break;

           case raiju:
               if (veh[index_db] == null && VehList.raiju_model != "Blocked")
               {
                   model_name = VehList.raiju_model;
               }
               break;



           case conada2:
               if (veh[index_db] == null && VehList.conada2_model != "Blocked")
               {
                   model_name = VehList.conada2_model;
               }
               break;

           case motorcycles_1:
           case motorcycles_2:
           case motorcycles_3:
           case motorcycles_4:
           case motorcycles_5:
           case motorcycles_6:
               isEmpty = !VehList.models_motorcycles.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_motorcycles[random.Next(VehList.models_motorcycles.Count)];
               }
               break;

           case muscle_1:
           case muscle_2:
           case muscle_3:
           case muscle_4:
           case muscle_5:
           case muscle_6:
           case muscle_7:
               isEmpty = !VehList.models_muscle.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_muscle[random.Next(VehList.models_muscle.Count)];
               }
               break;

           case offroad_1:
           case offroad_2:
           case offroad_3:
               isEmpty = !VehList.models_offroad.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_offroad[random.Next(VehList.models_offroad.Count)];
               }
               break;

           case openwheel:
               isEmpty = !VehList.models_openwheel.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_openwheel[random.Next(VehList.models_openwheel.Count)];
               }
               break;

           case beach:
               isEmpty = !VehList.models_beach.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_beach[random.Next(VehList.models_beach.Count)];
               }
               break;

           case planes:
               isEmpty = !VehList.models_planes.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_planes[random.Next(VehList.models_planes.Count)];
               }
               break;


           case sedans_1:
           case sedans_2:
           case sedans_3:
           case sedans_4:
           case sedans_5:
           case sedans_6:
           case sedans_7:
               isEmpty = !VehList.models_sedans.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_sedans[random.Next(VehList.models_sedans.Count)];
               }
               break;



           case sportclassic_1:
           case sportclassic_2:
           case sportclassic_3:
           case sportclassic_4:
           case sportclassic_5:
           case sportclassic_6:
           case sportclassic_7:
               isEmpty = !VehList.models_sportclassic.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_sportclassic[random.Next(VehList.models_sportclassic.Count)];
                   plate_id = 7;
               }
               break;

           case submarine:
               isEmpty = !VehList.models_submarine.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_submarine[random.Next(VehList.models_submarine.Count)];
               }
               break;

           case supers_1:
           case supers_2:
           case supers_3:
           case supers_4:
           case supers_5:
           case supers_6:
           case supers_7:
               isEmpty = !VehList.models_supers.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_supers[random.Next(VehList.models_supers.Count)];
               }
               break;

           case suvs_1:
           case suvs_2:
           case suvs_3:
           case suvs_4:
           case suvs_5:
           case suvs_6:
           case suvs_7:
           case suvs_8:
               isEmpty = !VehList.models_suvs.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_suvs[random.Next(VehList.models_suvs.Count)];
               }
               break;



           case tuners_1:
           case tuners_2:
           case tuners_3:
           case tuners_4:
           case tuners_5:
               isEmpty = !VehList.models_tuners.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_tuners[random.Next(VehList.models_tuners.Count)];
               }
               break;

           case valentine:
               isEmpty = !VehList.models_valentine.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_valentine[random.Next(VehList.models_valentine.Count)];
               }
               break;

           case vans_1:
           case vans_2:
           case vans_3:
           case vans_4:
           case vans_5:
           case vans_6:
           case vans_7:
           case vans_8:
           case vans_9:
           case vans_10:
           case vans_11:
           case vans_12:
           case vans_13:
               isEmpty = !VehList.models_vans.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_vans[random.Next(VehList.models_vans.Count)];
               }
               break;

           case wastelander:
               isEmpty = !VehList.models_wastelander.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_wastelander[random.Next(VehList.models_wastelander.Count)];
               }
               break;

           case weaponboats:
               isEmpty = !VehList.models_weaponboats.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_weaponboats[random.Next(VehList.models_weaponboats.Count)];
               }
               break;



           case pizzaboy:
               isEmpty = !VehList.models_pizza.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_pizza[random.Next(VehList.models_pizza.Count)];
               }
               break;



           case plane_sandy:
               isEmpty = !VehList.models_plane_sandy.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_plane_sandy[random.Next(VehList.models_plane_sandy.Count)];
               }
               break;

           case heli_sandy:
               isEmpty = !VehList.models_heli_sandy.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_heli_sandy[random.Next(VehList.models_heli_sandy.Count)];
               }
               break;



           case hsw:
               isEmpty = !VehList.models_hsw.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_hsw[random.Next(VehList.models_hsw.Count)];
                   IsHSW = true;
               }
               break;

           case heli_higgins:
               isEmpty = !VehList.models_higgins.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_higgins[random.Next(VehList.models_higgins.Count)];
               }


               break;


           case arena_hotring_1:
           case arena_hotring_2:
               // Check if the list has cars AND if the spot is currently empty
               isEmpty = !VehList.models_arena_hotring.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_arena_hotring[random.Next(VehList.models_arena_hotring.Count)];
               }
               break;

           case arena_speed_1:
           case arena_speed_2:
               isEmpty = !VehList.models_arena_speed.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_arena_speed[random.Next(VehList.models_arena_speed.Count)];
               }
               break;

           case arena_offroad_1:
           case arena_offroad_2:
               isEmpty = !VehList.models_arena_offroad.Any();
               if ((veh[index_db] == null && !isEmpty) || type == 1)
               {
                   model_name = VehList.models_arena_offroad[random.Next(VehList.models_arena_offroad.Count)];
               }
               break;
       }
       return model_name;
   }*/

    string GenerateVehicleModelName(SpawnId spawnId, int type)
    {
        string model_name = null;
        bool isEmpty;
        var random = new Random();

        // Ensure key exists
        if (!vehDict.ContainsKey(spawnId))
            vehDict[spawnId] = null;

        switch (spawnId)
        {
            case SpawnId.ArenaHotring1:
            case SpawnId.ArenaHotring2:
                isEmpty = !VehList.models_arena_hotring.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_arena_hotring[random.Next(VehList.models_arena_hotring.Count)];
                break;

            case SpawnId.ArenaSpeed1:
            case SpawnId.ArenaSpeed2:
                isEmpty = !VehList.models_arena_speed.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_arena_speed[random.Next(VehList.models_arena_speed.Count)];
                break;

            case SpawnId.ArenaOffroad1:
            case SpawnId.ArenaOffroad2:
                isEmpty = !VehList.models_arena_offroad.Any();
                if ((vehDict[spawnId] == null && !isEmpty) || type == 1)
                    model_name = VehList.models_arena_offroad[random.Next(VehList.models_arena_offroad.Count)];
                break;
        }

        return model_name;
    }

    void SetNumberPlate(Vehicle car, int mode, int index)
    {
        if (mode == 1 && plate_id != -1)
        {
            Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, car, index);
        }
    }


    Vehicle CreateNewVehicle(string hash, Vector3 pos, float heading, SpawnId spawnId)
    {
        var veh_model = new Model(hash);
        veh_model.Request(500);
        if (!veh_model.IsValid) return null;

        while (!veh_model.IsLoaded) Script.Wait(100);
        Vehicle car = World.CreateVehicle(veh_model, pos, heading);
        veh_model.MarkAsNoLongerNeeded();

        if (doors_config == 1)
            Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, car, 7);
        // 3. GLOBAL COLOR LOGIC (Applies to everything)
        // This native checks if the car has developer-defined color presets (carcols.meta)
       
        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, car);

        if (comboCount > 0)
        {
            Random rnd = new Random();
            int randomCombo = rnd.Next(0, comboCount);
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, car, randomCombo);
        }


        return car;
    }

    Blip CreateMarkerAboveCar(Vehicle car, SpawnId spawnId)
    {
        Blip mark = Function.Call<Blip>(GTA.Native.Hash.ADD_BLIP_FOR_ENTITY, car);
        Function.Call(GTA.Native.Hash.SET_BLIP_SPRITE, mark, 1);
        Function.Call(GTA.Native.Hash.SET_BLIP_COLOUR, mark, blip_color);
        Function.Call(GTA.Native.Hash.FLASH_MINIMAP_DISPLAY);
        mark.Name = "Unique vehicle";
        markerDict[spawnId] = mark; // markerDict: Dictionary<SpawnId, Blip>
        return mark;
    }

    void OnAborded(object sender, EventArgs e)
    {
        foreach (var kvp in markerDict)
        {
            if (kvp.Value != null && kvp.Value.Exists())
                kvp.Value.Delete();
        }

        foreach (var kvp in vehDict)
        {
            if (kvp.Value != null && kvp.Value.Exists())
                kvp.Value.Delete();
            vehDict[kvp.Key] = null;
        }
    }



    void OnTick(object sender, EventArgs e)
    {
        if (_canSpawn == 0) return;

        var playerPos = Game.Player.Character.Position;
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);

        // --- 1. FORCE CLEANUP (Mission/Cutscene) ---
        if (vehicles_spawned == 1 && isMissionActive)
        {
            // Force delete everything if a mission starts
            foreach (var spawnId in vehDict.Keys.ToList())
            {
                var car = vehDict[spawnId];
                if (car != null && car.Exists()) car.Delete();

                // Also clean up blips using the Dictionary
                if (markerDict.ContainsKey(spawnId))
                {
                    if (markerDict[spawnId] != null && markerDict[spawnId].Exists())
                        markerDict[spawnId].Delete();
                    markerDict[spawnId] = null;
                }

                vehDict[spawnId] = null;
            }
            vehicles_spawned = 0;
        }

        // --- 2. SPAWN LOGIC (Only if no mission) ---
        if (!isMissionActive)
        {
            foreach (var kvp in Spawns)
            {
                SpawnId spawnId = kvp.Key;
                SpawnPoint spawn = kvp.Value;

                // Check if player is close enough to spawn point
                if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                    spawn.Position.X, spawn.Position.Y, spawn.Position.Z,
                    playerPos.X, playerPos.Y, playerPos.Z, 0) < 300)
                {
                    // Try to get a model name (checks internally if spot is empty)
                    string model_name = GenerateVehicleModelName(spawnId, 0);

                    if (model_name != null)
                    {
                        vehDict[spawnId] = CreateNewVehicle(model_name, spawn.Position, spawn.Heading, spawnId);
                        var vehicle = vehDict[spawnId];

                        if (vehicle != null)
                        {
                            SetNumberPlate(vehicle, mod_plate, plate_id);
                            plate_id = -1;

                            if (blip_config == 1)
                            {
                                // CreateMarkerAboveCar already adds it to markerDict
                                CreateMarkerAboveCar(vehicle, spawnId);
                            }

                            // Mark that we have vehicles spawned
                            vehicles_spawned = 1;
                        }
                    }
                }
            }
        }

        // --- 3. PLAYER INTERACTION (Entered Vehicle) ---
        // We iterate over a copy of keys to safely modify the dictionary if needed
        foreach (var spawnId in vehDict.Keys.ToList())
        {
            Vehicle car = vehDict[spawnId];

            // If player is in a tracked vehicle, remove the blip but keep tracking the car 
            // until they leave the area
            if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
            {
                if (markerDict.ContainsKey(spawnId))
                {
                    if (markerDict[spawnId] != null && markerDict[spawnId].Exists())
                        markerDict[spawnId].Delete();

                    markerDict[spawnId] = null; // Remove blip reference
                }

                car.MarkAsNoLongerNeeded(); // Let the game engine handle memory
            }
        }

        // --- 4. DISTANCE CLEANUP (The Fix) ---
        foreach (var kvp in Spawns)
        {
            SpawnId spawnId = kvp.Key;
            SpawnPoint spawn = kvp.Value;

            // Check distance between Player and the SPAWN POINT
            if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                spawn.Position.X, spawn.Position.Y, spawn.Position.Z,
                playerPos.X, playerPos.Y, playerPos.Z, 0) > 300)
            {
                // Check if we are currently tracking a vehicle here
                if (vehDict.TryGetValue(spawnId, out var vehicle))
                {
                    // Handle the blip first
                    if (markerDict.ContainsKey(spawnId))
                    {
                        if (markerDict[spawnId] != null && markerDict[spawnId].Exists())
                            markerDict[spawnId].Delete();
                        markerDict[spawnId] = null;
                    }

                    if (vehicle != null && vehicle.Exists())
                    {
                        // IF player is inside: Do NOT delete, just stop tracking.
                        // IF player is NOT inside: Delete it.
                        if (!Function.Call<bool>(Hash.IS_PED_SITTING_IN_VEHICLE, Game.Player.Character, vehicle))
                        {
                            vehicle.Delete();
                        }
                        else
                        {
                            // Player took the car away. We release control.
                            vehicle.MarkAsNoLongerNeeded();
                        }
                    }

                    // CRITICAL FIX: Always null the dictionary slot so a new car can spawn 
                    // when we return to this spot.
                    vehDict[spawnId] = null;
                }
            }
        }
    }

}