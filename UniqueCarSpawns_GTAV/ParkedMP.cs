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

    //Coords
    /*
    private const int arena_hotring_1 = 0;
    private const int arena_hotring_2 = 1;
    private const int arena_speed_1 = 2;
    private const int arena_speed_2 = 3;
    private const int arena_offroad_1 = 4;
    private const int arena_offroad_2 = 5;
    private const int cult = 6;
    private const int boats = 7;
    private const int cemetery = 8;
    private const int cheburek = 9;
    private const int cinema = 10;
    private const int classics_1 = 11;
    private const int classics_2 = 12;
    private const int classics_3 = 13;
    private const int classics_4 = 14;
    private const int classics_5 = 15;
    private const int classics_6 = 16;
    private const int hyper_1 = 17;
    private const int hyper_2 = 18;
    private const int hyper_3 = 19;
    private const int hyper_4 = 20;
    private const int hyper_5 = 21;
    private const int compacts_1 = 22;
    private const int compacts_2 = 23;
    private const int coupes_1 = 24;
    private const int coupes_2 = 25;
    private const int coupes_3 = 26;
    private const int coupes_4 = 27;
    private const int coupes_5 = 28;
    private const int coupes_6 = 29;
    private const int ghetto_1 = 30;
    private const int ghetto_2 = 31;
    private const int ghetto_3 = 32;
    private const int ghetto_4 = 33;
    private const int ghetto_5 = 34;
    private const int ghetto_6 = 35;
    private const int helicopter = 36;
    private const int humanlabs = 37;
    private const int industrial_1 = 38;
    private const int industrial_2 = 39;
    private const int industrial_3 = 40;
    private const int industrial_4 = 41;
    private const int karting = 42;
    private const int thruster = 43;
    private const int military_planes_1 = 44;
    private const int military_planes_2 = 45;
    private const int military_helicopters = 46;
    private const int military_opressors = 47;
    private const int military_bikes = 48;
    private const int raiju = 49;
    private const int conada2 = 50;
    private const int motorcycles_1 = 51;
    private const int motorcycles_2 = 52;
    private const int motorcycles_3 = 53;
    private const int motorcycles_4 = 54;
    private const int motorcycles_5 = 55;
    private const int motorcycles_6 = 56;
    private const int muscle_1 = 57;
    private const int muscle_2 = 58;
    private const int muscle_3 = 59;
    private const int muscle_4 = 60;
    private const int muscle_5 = 61;
    private const int muscle_6 = 62;
    private const int muscle_7 = 63;
    private const int offroad_1 = 64;
    private const int offroad_2 = 65;
    private const int offroad_3 = 66;
    private const int openwheel = 67;
    private const int beach = 68;
    private const int planes = 69;
    private const int sedans_1 = 70;
    private const int sedans_2 = 71;
    private const int sedans_3 = 72;
    private const int sedans_4 = 73;
    private const int sedans_5 = 74;
    private const int sedans_6 = 75;
    private const int sedans_7 = 76;
    private const int sportclassic_1 = 77;
    private const int sportclassic_2 = 78;
    private const int sportclassic_3 = 79;
    private const int sportclassic_4 = 80;
    private const int sportclassic_5 = 81;
    private const int sportclassic_6 = 82;
    private const int sportclassic_7 = 83;
    private const int submarine = 84;
    private const int supers_1 = 85;
    private const int supers_2 = 86;
    private const int supers_3 = 87;
    private const int supers_4 = 88;
    private const int supers_5 = 89;
    private const int supers_6 = 90;
    private const int supers_7 = 91;
    private const int suvs_1 = 92;
    private const int suvs_2 = 93;
    private const int suvs_3 = 94;
    private const int suvs_4 = 95;
    private const int suvs_5 = 96;
    private const int suvs_6 = 97;
    private const int suvs_7 = 98;
    private const int suvs_8 = 99;
    private const int tuners_1 = 100;
    private const int tuners_2 = 101;
    private const int tuners_3 = 102;
    private const int tuners_4 = 103;
    private const int tuners_5 = 104;
    private const int valentine = 105;
    private const int vans_1 = 106;
    private const int vans_2 = 107;
    private const int vans_3 = 108;
    private const int vans_4 = 109;
    private const int vans_5 = 110;
    private const int vans_6 = 111;
    private const int vans_7 = 112;
    private const int vans_8 = 113;
    private const int vans_9 = 114;
    private const int vans_10 = 115;
    private const int vans_11 = 116;
    private const int vans_12 = 117;
    private const int vans_13 = 118;
    private const int wastelander = 119;
    private const int weaponboats = 120;
    private const int pizzaboy = 121;
    private const int plane_sandy = 122;
    private const int heli_sandy = 123;
    private const int hsw = 124;
    private const int heli_higgins = 125; */


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
            140.3646f
        ),
        [SpawnId.ArenaHotring2] = new SpawnPoint(
            new Vector3(1189.208f, 304.6935f, 81.48812f),
            90.24596f
        ),
        [SpawnId.ArenaSpeed1] = new SpawnPoint(
            new Vector3(-176.5869f, -2019.529f, 27.14398f),
            146.9079f
        ),
        [SpawnId.ArenaSpeed2] = new SpawnPoint(
            new Vector3(1117.519f, 257.8285f, 80.31487f),
            75.33392f
        ),
        [SpawnId.ArenaOffroad1] = new SpawnPoint(
            new Vector3(-192.949f, -1928.497f, 27.20675f),
            -122.0743f
        ),
        [SpawnId.ArenaOffroad2] = new SpawnPoint(
            new Vector3(1151.233f, 183.6329f, 80.23096f),
            -151.2955f
        ),
    };


    /*
    private List<Vector3> coords = new List<Vector3>()
    {
        new Vector3(-206.046f, -1988.758f, 26.96269f),  // Index 152 (Hotring )
        new Vector3(1189.208f, 304.6935f, 81.48812f),
        new Vector3(-176.5869f, -2019.529f, 27.14398f), // Index 153 (Speed )
        new Vector3(1117.519f, 257.8285f, 80.31487f),
        new Vector3(-192.949f, -1928.497f, 27.20675f), // Index 154 (Offroad )
        new Vector3(1151.233f, 183.6329f, 80.23096f),
        new Vector3(-719.9119f, 79.29325f, 55.13408f),           
        new Vector3(-926.119f, -1478.350f, -0.474f),
        new Vector3(-1640.42f, -202.879f, 54.146f),
        new Vector3(1546.591f, 3781.791f, 33.06f),
        new Vector3(-1084.873f, -477.591f, 36.2069f),
        new Vector3(-1407.751f, -589.1447f, 29.65687f), //c1
        new Vector3(-817.325f, -1201.59f, 5.935f), //classics 2
           new Vector3(-489.2397f, -596.5908f, 30.56949f), //muesuem hyper car spawn //c3
        new Vector3(-1403.43884f, 56.3168755f, 52.4210129f), //golf club car spawn //c4
         new Vector3(339.481f, 159.143f, 102.146f), //vinewood spawn//c5
          new Vector3(-552.673f, 309.154f, 82.191f), //tequillala c6
         new Vector3(-220.102f, -590.273f, 33.264f), //h1
        new Vector3(-1533.031f, 82.01414f, 56.29016f),// h2
           new Vector3(-1405.12f, 81.983f, 52.099f), //h3
         new Vector3(-397.528046f, 210.366409f, 82.7887802f), //h4 eclipse strip club
          new Vector3(  -718.510742f, -74.6843796f, 36.9155846f), //h5 rodeo
        new Vector3(870.7411f, -75.28734f, 78.10686f), //compact 1
        new Vector3(110.261f, -714.605f, 32.133f), //compact2
        new Vector3(-74.575f, -619.874f, 35.173f), //coupes 1
        new Vector3(283.769f, -342.644f, 43.92f),
        new Vector3(-1044.02f, -2608.02f, 19.775f),
        new Vector3(-801.566f, -1313.92f, 4.0f),
        new Vector3(-972.578f, -1464.27f, 4.013f),
        new Vector3(1309.942f, -530.154f, 70.312f), //coupes end
        new Vector3(-229.587f, -1483.44f, 30.352f), //ghetto 1 jb's
        new Vector3(-22.296f, -1851.58f, 24.108f), //ghetto 2 grove street
        new Vector3(321.798f, -1948.14f, 23.627f),//ghetto 3 near vagos projects
        new Vector3(455.602f, -1695.26f, 28.289f), //ghetto 4 near police station
        new Vector3(1228.548f, -1605.65f, 50.736f),//ghetto 5 east los santos neighborhood
         new Vector3(298.2452f, -1241.624f, 28.75226f), //g6
        new Vector3(-1006.93f, -3015.079f, 13.82286f), //heli
        new Vector3(3511.653f, 3783.877f, 28.925f), //humane
        new Vector3(1566.097f, -1683.17f, 87.205f), //i1
        new Vector3(2673.478f, 1678.569f, 23.488f),//i2
        new Vector3(839.097f, 2202.196f, 50.46f),//i3
        new Vector3(2717.772f, 1391.725f, 23.535f),//i4
        new Vector3(-1530.63f, -993.47f, 12.017f), //karting
        new Vector3(-1985.113f, 3044.164f, 32.810f), //thruster
        new Vector3(-1892.247f, 3082.933f, 32.810f),//m
        new Vector3(-1934.867f, 3109.608f, 32.810f),//m
        new Vector3(-1965.212f, 3101.532f, 32.810f),//m
        new Vector3(-1907.528f, 3117.613f, 32.959f),//m
        new Vector3(-1903.383f, 3115.676f, 32.810f),//m
        new Vector3(-2060.052f, 3146.352f, 32.8103f),//m
        new Vector3(-2116.779f, 3166.792f, 32.8101f),//m
        new Vector3(-2316.357f, 280.0749f, 168.9348f),
        new Vector3(-3036.57f, 105.31f, 10.593f),
        new Vector3(-3072.296f, 657.9456f, 10.53257f),
        new Vector3(-1535.044f, 890.5871f, 181.3348f),
        new Vector3(231.9765f, 1161.922f, 224.9349f),
        new Vector3(-582.6653f, -859.2297f, 25.49919f),
        new Vector3(-604.9778f, -1218.401f, 13.92473f),
        new Vector3(31.46499f, -1706.062f, 28.6591f),
        new Vector3(-329.9433f, -700.7843f, 32.33982f),
        new Vector3(238.2489f, -34.84402f, 69.18212f),
        new Vector3(393.4623f, -649.7198f, 27.92926f),
        new Vector3(124.0182f, -1472.58f, 28.6794f),
        new Vector3(185.595f, -1016.01f, 28.3f),
        new Vector3(1991.201f, 3076.069f, 46.79815f),
        new Vector3(1977.402f, 3835.433f, 31.59359f),
        new Vector3(1350.489f, 3605.351f, 34.47185f),
        new Vector3(1135.19f, 39.81987f, 80.34249f), //openwheel
        new Vector3(-1513.889f, -1253.183f, 2.433f),
        new Vector3(-960.1053f, -2933.973f, 13.823f),
        new Vector3(1156.74f, -1474.257f, 33.9701f),
        new Vector3(-936.2781f, -2692.023f, 16.11801f),
        new Vector3(-532.5765f, -2133.869f, 5.491799f),
        new Vector3(-1528.733f, -427.0032f, 35.01511f),
        new Vector3(642.1031f, 587.9972f, 128.4254f),
        new Vector3(-3139.044f, 1086.714f, 20.23225f),
        new Vector3(-1144.189f, 2666.219f, 17.47463f),
        new Vector3(-1114.1f, 479.205f, 81.161f),
        new Vector3(-205.516f, 281.035f, 91.818f),
        new Vector3(-504.323f, 424.21f, 96.287f),
        new Vector3(-1299.92f, -228.464f, 59.654f),
        new Vector3(-1334.63f, -1008.97f, 6.867f),
        new Vector3(-187.144f, -175.854f, 42.624f),
        new Vector3(-1886.25f, 2016.572f, 139.951f), //sports classics
        new Vector3(-1616.693f, 5270.917f, -0.298f), //north chumash? submarine
        new Vector3(-1297.2f, 252.495f, 61.813f), //hotel super
        new Vector3(-345.267f, 662.299f, 168.587f), //vinewood hills
        new Vector3(-72.605f, 902.579f, 234.631f), //vinewood lake
        new Vector3(-1451.92f, 533.495f, 118.177f), //north of the hotel
        new Vector3(-1979.25f, 586.078f, 116.479f), //near kortz center
        new Vector3(-1873.6f, -343.933f, 48.26f), //super hotel
        new Vector3(443.542f, 253.197f, 102.21f), //north vinewood blvd
        new Vector3(-2340.907f, 295.8933f, 169.1187f), //in the kortz center
        new Vector3(627.8824f, 196.4409f, 96.67142f), //east vinewood parking lot
        new Vector3(1147.651f, -985.0583f, 45.4853f),           //mortor
        new Vector3(243.1413f, -861.0181f, 28.94244f),          //mortor
        new Vector3(-340.161f, -876.799f, 30.90968f),           //mortor
        new Vector3(388.3879f, -215.6955f, 56.76986f),          //mortor
        new Vector3(-1235.388f, -1647.45f, 3.512795f),          //mortor
        new Vector3(-472.0576f, 6034.684f, 30.74616f),          //mortor
        new Vector3(934.148f, -1812.94f, 29.812f),            
        new Vector3(246.847f, -1162.08f, 28.16f),
        new Vector3(1136.156f, -773.997f, 56.632f),
        new Vector3(1028.898f, -2405.95f, 28.494f),
        new Vector3(-762.865f, -38.192f, 37.687f),
        new Vector3(140.945f, 6606.513f, 30.845f),
        new Vector3(1362.672f, 1178.352f, 111.609f),
        new Vector3(2593.022f, 364.349f, 107.457f),
        new Vector3(2002.724f, 3769.429f, 31.181f),
        new Vector3(-771.927f, 5566.46f, 32.486f),
        new Vector3(1697.817f, 6414.365f, 31.73f),
        new Vector3(1700.445f, 4937.267f, 41.078f),
        new Vector3(-1804.77f, 804.137f, 137.514f),
        new Vector3(756.539f, 2525.957f, 72.161f),
        new Vector3(1205.454f, 2658.357f, 36.824f),
        new Vector3(-165.839f, 6454.25f, 30.495f),
        new Vector3(-2221.14f, 4232.757f, 46.132f),
        new Vector3(-2555.51f, 2322.827f, 32.06f),
        new Vector3(1111.018f, 2221.073f, 50.140f), //wastelander
        new Vector3(-3092.066f, 3465.729f, -0.474f), //tug
        new Vector3(541.7154f, 97.22734f, 95.95358f), //tug should be pizzaboy
        new Vector3(1705.741f, 3271.236f, 41.56281f),
        new Vector3(2140.588f, 4816.544f, 41.05009f),
        new Vector3(-1118.157f, -2013.173f ,12.76037f), //hsw
        new Vector3(-746.4702f, -1469.937f, 6.87726f),    //higgins


};
    */
    /* private List<float> heading = new List<float>()
     {
                 90.24596f,  // Index 152 (Hotring Angle)
         146.9079f,
         75.33392f,  // Index 153 (Speed Angle)
         -122.0743f,
         -151.2955f,// Index 154 (Offroad Angle)
         -53.71473f,
         25.0975f, //cult cars
         12.163f,
         338.279f,
         26.557f,
         27.92156f,
         298.6727f,
         318.133f,
                 358.1453f,
                   61.6596375f,
       71.345f, //coupes end vinewood spawn
       260.340f,
        341.667f, //hyper arcadius
       316.77f, //mansion
        53.1f, // 6
        91.0271606f, //7
        60.0618172f, // h5
         147.4842f,
         341.667f,
         341.667f,
         66.978f,
         66.226f,
         169.408f,
         294.730f,
         341.133f,
         146.244f,//g1       
         141.262f,//g2
         47.597f,//g3
         138.808f,//g4
         33.185f,                //g5
         -179.7188f,
         330.7697f,
         166.594f,
         14.900f,
         270.297f,
         245.553f,
         1.832709f,
         254.258f,
         344.361f,
         147.141f,
         150.073f,
         236.324f,
         147.964f,
         150.114f,
         239.2013f,
         237.5298f,
         201.5139f,
         141.262f,
         311.2028f,
         19.50508f,
         98.82303f,
         358.6906f,
         133.0528f,
         23.36283f,
         88.68892f,
         340.165f,
         90.89349f,
         321.0109f,
         33.185f,
         58.8943f,
         297.7102f,
         16.77524f,
         58.2715f, //openwheel
         276.757f,
         149.6461f,
         268.8033f,
         241.2007f,
         353.1183f,
         48.3741f,
         160.515f,
         260.5882f,
         130.5594f,
         171.220f,
         165.351f,
         313.167f,
         0,
         126.968f,
         118.474f,
         160.257f,
         174.739f,
         304.052f,
         181.166f,
         171.211f,
         291.351f,
         73.674f,
         185.087f,
         225.300f,
         245.845f,
         294.0081f,
         70.38502f,
         182.3871f,
         248.5342f,
         347.7794f,
         341.7853f,
         124.5176f,
         43.40757f,
         88.712f,
         180.390f,
         269.604f,
         170.017f,
         115.427f,
         0.239f,
         359.306f,
         174.745f,
         298.783f,
         271.230f,
         247.870f,
         147.276f,
         223.747f,
         270.240f,
         223.627f,
         225.116f,
         225.108f,
         273.837f,
         273.390f,
         47.552f,
         113.416f,
        -179.1466f, 
         115.5491f,
         -45.004f,//hsw //X:0 Y:0 Z:0.3822183 W:0.9240721
         140.3646f, 


 };*/

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

    /*Vehicle CreateNewVehicle(string hash, Vector3 pos, float heading)
    {
        var veh_model = new Model(hash);
        veh_model.Request(500);
        if (!veh_model.IsValid)
        {

            return null;
        }
        else
        {
            while (!veh_model.IsLoaded) Script.Wait(100);
            car = World.CreateVehicle(veh_model, pos, heading);
            veh_model.MarkAsNoLongerNeeded();
            if (doors_config == 1)
            {
                GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_DOORS_LOCKED, car, 7);

            }

            if (tuning_flag == 1)
            {
                Function.Call(Hash.SET_VEHICLE_MOD_KIT, car, 0);
                Random rnd = new Random();
                int num;
                int modindex;
             
                int choose = rnd.Next(1, 3);
                if (choose == 1)
                {
                    num = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, car, 48);
                    if (num != -1)
                    {
                        modindex = rnd.Next(0, num + 1);
                        Function.Call(Hash.SET_VEHICLE_MOD, car, 48, modindex, true);
                    }
                }
                else
                {
                    modindex = rnd.Next(0, 7);
                    num = Function.Call<int>(Hash.GET_NUM_MOD_COLORS, 6, true);
                    int color_1 = rnd.Next(0, 159);
                    int color_2 = rnd.Next(0, 159);
                    Function.Call(Hash.SET_VEHICLE_MOD_COLOR_1, car, modindex, color_1, 0);
                    Function.Call(Hash.SET_VEHICLE_MOD_COLOR_2, car, modindex, color_2, 0);
                }
            }

            if (tuning_hsw_flag == 1 && IsHSW)
            {
                Function.Call(Hash.SET_VEHICLE_MOD, car, 36, 0, 0); //HSW Base
                Function.Call(Hash.SET_VEHICLE_MOD, car, 34, 2, 0); //HSW Turbo

                Random rnd_liv = new Random();
                int livery = rnd_liv.Next(1, 3);
                int mods = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, car, 48) - livery;
                //Function.Call(Hash.SET_VEHICLE_MOD, car, 48, mods, 0); //HSW Livery

                IsHSW = false;
            }

            vehicles_spawned = 1;

            return car;
        }

    }*/
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

  

        return car;
    }

    /*Blip CreateMarkerAboveCar(Vehicle car)
    {
        Blip mark = Function.Call<Blip>(GTA.Native.Hash.ADD_BLIP_FOR_ENTITY, car);
        Function.Call(GTA.Native.Hash.SET_BLIP_SPRITE, mark, 1);
        Function.Call(GTA.Native.Hash.SET_BLIP_COLOUR, mark, blip_color);
        Function.Call(GTA.Native.Hash.FLASH_MINIMAP_DISPLAY);
        mark.Name = "Unique vehicle";
        return mark;
    }*/
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
    /*

    void OnAborded(object sender, EventArgs e)
    {
        foreach (Blip mark in marker)
        {
            if (mark != null && mark.Exists())
                mark.Delete();
        }

        foreach (Vehicle car in veh)
        {
            if (car != null && car.Exists())
                car.Delete();
        }
    }*/
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


    /*void OnTick(object sender, EventArgs e)
    {

        if (_canSpawn == 0)
            return;

        if (vehicles_spawned == 1 && (Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING)))
        {
            bool isEmpty = !veh.Any();

            if (!isEmpty)
            {
                foreach (Vehicle car in veh)
                {
                    if (car != null && car.Exists())
                    {
                        car.Delete();
                    }
                }
                vehicles_spawned = 0;
            }
        }


        int index_db = 0;

        //Create vehicles (ON_MISSION = 0)
        if (!(Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING)))
        {
            foreach (Vector3 veh_coords in coords)
            {
                var position = Game.Player.Character.GetOffsetPosition(new Vector3(0, 0, 0));
                if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS, veh_coords.X, veh_coords.Y, veh_coords.Z, position.X, position.Y, position.Z, 0) < 300)
                {
                    //spawn in parking lots
                    string model_name = GenerateVehicleModelName(index_db, 0);
                    if (model_name != null)
                    {
                        veh[index_db] = CreateNewVehicle(model_name, coords[index_db], heading[index_db]);
                        if (veh[index_db] != null)
                        {
                            SetNumberPlate(veh[index_db], mod_plate, plate_id);
                            plate_id = -1;
                            if (blip_config == 1)
                            {
                                Blip mark = CreateMarkerAboveCar(veh[index_db]);
                                marker.Add(mark);
                            }
                            // Inside the foreach loop in OnTick, after the vehicle is created
                            if (veh[index_db] != null)
                            {
                                Random rndMax = new Random();
                                // 1. Initialize Mod Kit (Required for all tuning) 
                                Function.Call(Hash.SET_VEHICLE_MOD_KIT, veh[index_db], 0);

                                switch(index_db)
                                {
                                    case arena_hotring_1:
                                    case arena_hotring_2:
                                    case arena_speed_1:
                                    case arena_speed_2:
                                    case arena_offroad_1:
                                    case arena_offroad_2:
                                    case cult:
                                    case ghetto_1:
                                    case ghetto_2:
                                    case ghetto_3:
                                    case ghetto_4:
                                    case ghetto_5:
                                    case ghetto_6:
                                        {
                                            Vehicle v = veh[index_db];

                                            if (model_name == "retinue2" || model_name == "dynasty" || model_name == "cheburek" || model_name == "fagaloa" || model_name == "gburrito2"){
                                                Function.Call(Hash.SET_VEHICLE_MOD, v, 48, -1, false); //make it spawn in a standard paint job - no livery
                                                break; //do not apply tuning to these basic, non lowrider-like cars
                                            }
                                         

                                            // --- MANDATORY PERFORMANCE (MAXED) ---
                                            Function.Call(Hash.SET_VEHICLE_MOD, v, 11, 3, false); // Engine Level 4
                                            Function.Call(Hash.SET_VEHICLE_MOD, v, 12, 2, false); // Race Brakes
                                            Function.Call(Hash.SET_VEHICLE_MOD, v, 13, 2, false); // Race Transmission
                                            Function.Call(Hash.SET_VEHICLE_MOD, v, 15, 3, false); // Competition Suspension
                                            Function.Call(Hash.SET_VEHICLE_MOD, v, 16, 4, false); // 100% Armor
                                            Function.Call(Hash.TOGGLE_VEHICLE_MOD, v, 18, true);  // Turbo Tuning

                                            // --- RANDOMIZED VISUAL PARTS ---
                                            // Array of visual mod IDs: 0=Spoiler, 1=FBumper, 2=RBumper, 3=Skirts, 7=Hood, 10=Roof
                                            int[] visualMods = { 0, 1, 2, 3, 7, 10 };
                                            foreach (int modType in visualMods)
                                            {
                                                int count = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, v, modType);
                                                if (count > 0)
                                                {
                                                    // rndMax.Next(-1, count) allows for "Stock" (-1) or a random mod
                                                    Function.Call(Hash.SET_VEHICLE_MOD, v, modType, rndMax.Next(-1, count), false);
                                                }
                                            }
                                            // --- GROUP-SPECIFIC DIFFERENCE ---
                                            if (index_db == cult)
                                            {
                                                
                                                // Cult cars: fixed Epsilon Blue
                                                v.Mods.PrimaryColor = (VehicleColor)157;
                                                v.Mods.SecondaryColor = (VehicleColor)157;
                                                v.Mods.PearlescentColor = (VehicleColor)157;

                                                Function.Call(Hash.SET_VEHICLE_MOD, v, 48, -1, false); //just a standard paint job - no livery (tacky for cult members)
                                            }
                                            else
                                            {
                                                // Arena/Openwheel/ghetto: random livery
                                                int numLiveries = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, v, 48);
                                                if (numLiveries > 0)
                                                {
                                                    Function.Call(Hash.SET_VEHICLE_MOD, v, 48, rndMax.Next(0, numLiveries), false);
                                                }
                                            }
                                        }

                                        break;
                                    case heli_higgins:
                                        {
                                            if (model_name == "conada")
                                            {
                                                Vehicle v = veh[index_db];

                                                v.Mods.PrimaryColor = (VehicleColor)89;
                                                v.Mods.SecondaryColor = (VehicleColor)6;
                                                v.Mods.PearlescentColor = (VehicleColor)1;
                                                Function.Call(Hash.SET_VEHICLE_MOD, v, 48, 9, false); //spawn with the higgins livery
                                                Function.Call(Hash.SET_ENTITY_LOAD_COLLISION_FLAG, v, true);
                                                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, v);
                                            }
                                        }
                                        break;
                                    case helicopter:
                                    case planes:
                                        {
                                            Vehicle v = veh[index_db];

                                            int numLiveries = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, v, 48);
                                            if (numLiveries > 0)
                                            {
                                                Function.Call(Hash.SET_VEHICLE_MOD, v, 48, rndMax.Next(0, numLiveries), false);
                                            }
                                            break;
                                        }
                                
                                }


                            }


                            //Optional mods (livery, colors, etc.)
                            switch (model_name)
                            {
                                case "brickade2":
                                    veh[index_db].Mods.CustomPrimaryColor = Color.Black;
                                    veh[index_db].Mods.CustomSecondaryColor = Color.Black;
                                    Function.Call(GTA.Native.Hash.SET_VEHICLE_MOD_KIT, car, 0);
                                    Function.Call(GTA.Native.Hash.SET_VEHICLE_MOD, car, 48, 5, false);
                                    break;
                            }
                        }
                    }
                }
                index_db++;
            }
        }

        //Player in car
        index_db = 0;
        foreach (Vehicle car in veh)
        {
            if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
            {
                Blip mark = Function.Call<Blip>(Hash.GET_BLIP_FROM_ENTITY, car);
                if (mark != null && mark.Exists())
                    mark.Delete();
                    marker.Remove(mark);
                car.MarkAsNoLongerNeeded();
            }
            index_db++;
        }
        index_db = 0;
        foreach (Vehicle car in street_veh)
        {
            if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
            {
                car.MarkAsNoLongerNeeded();
                street_veh[index_db] = null;
            }
            index_db++;
        }

        index_db = 0;
        foreach (Vector3 veh_coords in coords)
        {
            var position = Game.Player.Character.GetOffsetPosition(new Vector3(0, 0, 0));
            if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS, veh_coords.X, veh_coords.Y, veh_coords.Z, position.X, position.Y, position.Z, 0) > 300)
            {
                if (veh[index_db] != null && veh[index_db].Exists() && !Function.Call<bool>(Hash.IS_PED_SITTING_IN_VEHICLE, Game.Player.Character, veh[index_db]))
                {
                    Blip mark = Function.Call<Blip>(Hash.GET_BLIP_FROM_ENTITY, veh[index_db]);
                    if (mark != null && mark.Exists())
                        mark.Delete();
                    veh[index_db].Delete();
                }
                veh[index_db] = null;
            }
            index_db++;
        }
    }*/
    void OnTick(object sender, EventArgs e)
{
    if (_canSpawn == 0)
        return;

    var playerPos = Game.Player.Character.Position;

    // --- Cleanup vehicles if mission/cutscene is active ---
    if (vehicles_spawned == 1 && (Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING)))
    {
        if (vehDict.Values.Any(v => v != null))
        {
            foreach (var car in vehDict.Values)
            {
                if (car != null && car.Exists())
                    car.Delete();
            }
            vehicles_spawned = 0;
        }
    }

    // --- Spawn vehicles if mission/cutscene is NOT active ---
    if (!(Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING)))
    {
        foreach (var kvp in Spawns)
        {
            SpawnId spawnId = kvp.Key;
            SpawnPoint spawn = kvp.Value;

            // Only spawn if within 300 units
            if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                spawn.Position.X, spawn.Position.Y, spawn.Position.Z,
                playerPos.X, playerPos.Y, playerPos.Z, 0) < 300)
            {
                string model_name = GenerateVehicleModelName(spawnId, 0);
                if (model_name != null)
                {
                    // Create the vehicle
                    vehDict[spawnId] = CreateNewVehicle(model_name, spawn.Position, spawn.Heading, spawnId); 
                        var vehicle = vehDict[spawnId];

                    if (vehicle != null)
                    {
                        SetNumberPlate(vehicle, mod_plate, plate_id);
                        plate_id = -1;

                        if (blip_config == 1)
                        {
                                Blip mark = CreateMarkerAboveCar(vehDict[spawnId], spawnId);
                                marker.Add(mark);
                        }

                        // Apply mods
                        //ApplyVehicleMods(spawnId, vehicle, model_name);
                    }
                }
            }
        }
    }

    // --- Remove blips for vehicles player is in ---
    foreach (var kvp in vehDict)
    {
        Vehicle car = kvp.Value;
        if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
        {
            Blip mark = Function.Call<Blip>(Hash.GET_BLIP_FROM_ENTITY, car);
            if (mark != null && mark.Exists())
                mark.Delete();
            marker.Remove(mark);
            car.MarkAsNoLongerNeeded();
        }
    }

    // --- Cleanup street vehicles ---
    for (int i = 0; i < street_veh.Length; i++)
    {
        Vehicle car = street_veh[i];
        if (car != null && car.Exists() && Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, Game.Player.Character, car, false))
        {
            car.MarkAsNoLongerNeeded();
            street_veh[i] = null;
        }
    }

    // --- Delete vehicles that are too far ---
    foreach (var kvp in Spawns)
    {
        SpawnId spawnId = kvp.Key;
        SpawnPoint spawn = kvp.Value;

        if (Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
            spawn.Position.X, spawn.Position.Y, spawn.Position.Z,
            playerPos.X, playerPos.Y, playerPos.Z, 0) > 300)
        {
            if (vehDict.TryGetValue(spawnId, out var vehicle) && vehicle != null && vehicle.Exists() &&
                !Function.Call<bool>(Hash.IS_PED_SITTING_IN_VEHICLE, Game.Player.Character, vehicle))
            {
                Blip mark = Function.Call<Blip>(Hash.GET_BLIP_FROM_ENTITY, vehicle);
                if (mark != null && mark.Exists())
                    mark.Delete();

                vehicle.Delete();
                vehDict[spawnId] = null;
            }
        }
    }
}

}