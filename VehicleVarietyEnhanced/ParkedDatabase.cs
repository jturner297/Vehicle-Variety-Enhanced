using GTA.Math;
using System.Collections.Generic;

public static class SpawnDatabase
{
    public static List<SpawnSpot> GetSpawns()
    {
        return new List<SpawnSpot>()
        {
              // --- SUPERS (Spec) ---
            new SpawnSpot("PacificBluffsHotel", new Vector3(-1873.6f, -343.933f, 48.26f), 225.300f, VehList.models_super, SpawnBehavior.Spec),
            new SpawnSpot("Rehab", new Vector3(-1535.044f, 890.5871f, 181.3348f), 19.505f, VehList.models_super, SpawnBehavior.Spec),
            new SpawnSpot("RichmanHotel", new Vector3(-1297.2f, 252.495f, 61.813f), 3.035f, VehList.models_super, SpawnBehavior.Spec),
         
            new SpawnSpot("VinewoodHills_1", new Vector3(-345.267f, 662.299f, 168.587f), 171.211f, VehList.models_super, SpawnBehavior.Spec, VehList.models_classics, 50),
            new SpawnSpot("VinewoodHills_2", new Vector3(-504.323f, 424.21f, 96.287f), 313.167f, VehList.models_super, SpawnBehavior.Spec, VehList.models_classics, 50),
       
            new SpawnSpot("LakeVinewoodEstate", new Vector3(-72.605f, 902.579f, 234.631f), 291.351f, VehList.models_super, SpawnBehavior.Spec),
            new SpawnSpot("VinewoodHotel", new Vector3(443.542f, 253.197f, 102.21f), 245.845f, VehList.models_super, SpawnBehavior.Spec),
            
            new SpawnSpot("EclipseStripClub", new Vector3(-397.528f, 210.366f, 82.789f), 91.136f, VehList.models_bikes, SpawnBehavior.Spec),
            
            new SpawnSpot("ArcadiusCenter", new Vector3(-220.102f, -590.273f, 33.264f), 341.667f, VehList.models_super, SpawnBehavior.Spec),
            new SpawnSpot("RodeoDriveParking", new Vector3(-718.511f, -74.684f, 36.916f), 62.242f, VehList.models_super, SpawnBehavior.Spec),
            new SpawnSpot("RodeoDrive", new Vector3(-703.480f, -192.036f, 36.161f), 28.986f, VehList.models_super, SpawnBehavior.Spec,  VehList.models_classics, 50),
           
            new SpawnSpot("RockfordHotel", new Vector3(-1126.722f, -318.281f, 37.21f), -95.129f, VehList.models_super, SpawnBehavior.Spec, VehList.models_armoured, 50),
            
            new SpawnSpot("VespucciYachtClub", new Vector3(-801.566f, -1313.92f, 4.0f), 169.408f, VehList.models_super, SpawnBehavior.Spec, VehList.models_classics, 30),
         
            new SpawnSpot("WesrVinewoodHills_1", new Vector3(-1979.25f, 586.078f, 116.479f), 185.087f, VehList.models_super, SpawnBehavior.Spec),
            new SpawnSpot("WesrVinewoodHills_2", new Vector3(-1451.92f, 533.495f, 118.177f), 73.674f, VehList.models_super, SpawnBehavior.Spec),
            new SpawnSpot("KortzCenter", new Vector3(-2340.907f, 295.8933f, 169.1187f), 294.0081f, VehList.models_super, SpawnBehavior.Spec),

            // --- CLASSICS (Spec) ---
            new SpawnSpot("VinewoodHills_3", new Vector3(-1114.1f, 479.205f, 81.161f), 169.13f, VehList.models_classics, SpawnBehavior.Spec, VehList.models_super, 20),
          
            new SpawnSpot("GolfClub", new Vector3(-1405.12f, 81.983f, 52.099f), 58.178f, VehList.models_classics, SpawnBehavior.Spec, VehList.models_super, 50),
          
            new SpawnSpot("Vineyard", new Vector3(-1886.25f, 2016.572f, 139.951f), 160.257f, VehList.models_classics, SpawnBehavior.Spec),
          
            new SpawnSpot("VespucciViceroyHotel", new Vector3(-817.325f, -1201.59f, 5.935f), 318.133f, VehList.models_classics, SpawnBehavior.Spec),
        
            new SpawnSpot("Tequi-la-la", new Vector3(-552.673f, 309.154f, 82.191f), 260.340f, VehList.models_muscle, SpawnBehavior.Muscle, VehList.models_classics, 15),
          
            new SpawnSpot("VinewoodCinema", new Vector3(339.481f, 159.143f, 102.146f), 71.345f, VehList.models_classics, SpawnBehavior.Spec),
            new SpawnSpot("BluffsYachtClub", new Vector3(-3036.57f, 105.31f, 10.593f), 141.262f, VehList.models_classics, SpawnBehavior.Spec, VehList.models_super, 15),
        
            new SpawnSpot("Beverly", new Vector3(-205.516f, 281.035f, 91.818f), 165.351f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_armoured, 15), //VIP
         
            new SpawnSpot("Marina", new Vector3(-972.578f, -1464.27f, 4.013f), 294.730f, VehList.models_classics, SpawnBehavior.Spec, VehList.models_super, 15),
            new SpawnSpot("ModernArtMuesuem", new Vector3(-489.2397f, -596.5908f, 30.56949f), 358.1453f, VehList.models_classics, SpawnBehavior.Spec),
            new SpawnSpot("BahamaMamas", new Vector3(-1407.751f, -589.1447f, 29.65687f), 298.673f, VehList.models_classics, SpawnBehavior.Spec),
            new SpawnSpot("VinewoodBowl", new Vector3(231.9765f, 1161.922f, 224.9349f), 98.82303f, VehList.models_classics, SpawnBehavior.Spec, VehList.models_super, 15),

            // Special Cases
            new SpawnSpot("VespucciApartment", new Vector3(-1334.63f, -1008.97f, 6.867f), 126.968f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_classics, 20),

            // --- RACING / ARENA (RandomSpec) ---
            new SpawnSpot("ArenaHotring", new Vector3(-206.046f, -1988.758f, 26.96269f), 90.246f, VehList.models_arena_hotring, SpawnBehavior.RandomSpec, null, 0, 200f, 300f , false, 80),
            new SpawnSpot("ArenaSpeed", new Vector3(-176.5869f, -2019.529f, 27.14398f), 75.334f, VehList.models_arena_speed, SpawnBehavior.RandomSpec, null, 0, 200f, 300f, false, 80),
            new SpawnSpot("ArenaOffroad", new Vector3(-192.949f, -1928.497f, 27.20675f), -151.296f, VehList.models_arena_offroad, SpawnBehavior.RandomSpec, null, 0, 200f, 300f, false, 80),
                                                                                                                                                          
            // Casino
            new SpawnSpot("CasinoHotring", new Vector3(1189.208f, 304.6935f, 81.48812f), 146.908f, VehList.models_arena_hotring, SpawnBehavior.RandomSpec, null, 0, 400f, 300f , false, 80),
            new SpawnSpot("CasinoSpeed", new Vector3(1117.519f, 257.8285f, 80.31487f), -122.074f, VehList.models_arena_speed, SpawnBehavior.RandomSpec, null, 0, 400f, 300f, false, 80),
            new SpawnSpot("CasinoOffroad", new Vector3(1151.233f, 183.6329f, 80.23096f), -53.715f, VehList.models_arena_offroad, SpawnBehavior.RandomSpec, null, 0, 400f, 300f, false, 80),
            new SpawnSpot("CasinoOpenwheel", new Vector3(1135.19f, 39.81987f, 80.34249f), 58.875f, VehList.models_openwheel, SpawnBehavior.RandomSpec, null, 0, 400f, 300f, false, 80),             

            // --- SPECIAL SPOTS ---
            new SpawnSpot("LagoZancudo", new Vector3(-3092.066f, 3465.729f, -0.474f), 47.552f, VehList.models_largeboats, SpawnBehavior.Stock),
            new SpawnSpot("Cemetery", new Vector3(-1640.42f, -202.879f, 54.146f), 338.279f, VehList.models_cemetery, SpawnBehavior.Spec), // Spec = Clean
            new SpawnSpot("MovieStudio", new Vector3(-1084.873f, -477.591f, 36.2069f), 27.922f, VehList.models_studio, SpawnBehavior.Spec), // Spec calls Hero Specs
            new SpawnSpot("Cult", new Vector3(-719.9119f, 79.29325f, 55.13408f), 25.098f, VehList.models_cult, SpawnBehavior.RandomSpec), // RandomSpec + Cult List = Epsilon Blue
            new SpawnSpot("Rockford_Church", new Vector3(-762.865f, -38.192f, 37.687f), 115.427f, VehList.models_valentine, SpawnBehavior.Spec),
            new SpawnSpot("Beach_Karts1", new Vector3(-1530.63f, -993.47f, 12.017f), 254.258f, VehList.models_karting, SpawnBehavior.RandomSpec),
            new SpawnSpot("Beach_Karts2", new Vector3(-1235.388f, -1647.45f, 3.512795f), 124.5176f, VehList.models_karting, SpawnBehavior.RandomSpec),
            new SpawnSpot("GarmentFactory", new Vector3(651.461f, -1016.148f, 21.893f), 358.042f, VehList.models_kuruma, SpawnBehavior.Spec, null, 0, 100f, 100f, false, 20),

            // --- LARGE VEHICLE SPAWNS ---
            // Higgins = Spec (Logic in CarMod detects Higgins List)
            new SpawnSpot("Higgins_Heli", new Vector3(-746.4702f, -1469.937f, 6.87726f), 140.365f, VehList.models_higgins, SpawnBehavior.Spec, null, 0, 500f, 10f,false, 80),
            // Heli = Spec (Logic in CarMod detects Heli list)
            new SpawnSpot("LSIA_Helicopter", new Vector3(-979.378f, -2996.868f, 13.945f), 331.180f, VehList.models_helicopter, SpawnBehavior.RandomSpec, null, 0, 650f, 600f, false, 80),
            new SpawnSpot("LSIA_Planes_1", new Vector3(-961.005f, -2963.593f, 13.945f), 147.589f, VehList.models_planes, SpawnBehavior.RandomSpec, null, 0, 650f, 600f, false, 80),

            // Lowriders (RandomSpec)
            new SpawnSpot("Lowrider_1", new Vector3(-229.587f, -1483.44f, 30.352f), 146.244f, VehList.models_lowriders, SpawnBehavior.RandomSpec),
            new SpawnSpot("Lowrider_2", new Vector3(-22.296f, -1851.58f, 24.108f), 141.262f, VehList.models_lowriders, SpawnBehavior.RandomSpec),
            new SpawnSpot("Lowrider_3", new Vector3(321.798f, -1948.14f, 23.627f), 47.597f, VehList.models_lowriders, SpawnBehavior.RandomSpec),
            new SpawnSpot("Lowrider_4", new Vector3(455.602f, -1695.26f, 28.289f), 138.808f, VehList.models_lowriders, SpawnBehavior.RandomSpec),
            new SpawnSpot("Lowrider_5", new Vector3(1228.548f, -1605.65f, 50.736f), 33.185f, VehList.models_lowriders, SpawnBehavior.RandomSpec),
            new SpawnSpot("Lowrider_6", new Vector3(298.2452f, -1241.624f, 28.75226f), -179.719f, VehList.models_lowriders, SpawnBehavior.RandomSpec),
            new SpawnSpot("Lowrider_7", new Vector3(264.0245f, -1512.3302f, 28.7877f), 268.336f, VehList.models_lowriders, SpawnBehavior.RandomSpec),

            // Ambient Rich Population (Spec -> CarMod detects Armoured list -> Matte Black)
            new SpawnSpot("FIB_parking", new Vector3(110.261f, -714.605f, 32.133f), 341.667f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_armoured, 25),
            new SpawnSpot("Red_parking", new Vector3(-340.161f, -876.799f, 30.90968f), 347.7794f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_armoured, 5),
            new SpawnSpot("City3", new Vector3(-329.9433f, -700.7843f, 32.33982f), 88.68892f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_armoured, 5),
            new SpawnSpot("EastVinewoodBacklot", new Vector3(626.567f, 192.583f, 96.535f), 70.105f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_armoured, 5),
            new SpawnSpot("Galileo", new Vector3(-421.333f, 1198.125f, 325.160f), 50.838f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_armoured, 40),
           
            new SpawnSpot("DelPerro_LOMBANK", new Vector3(-1628.467f, -614.632f, 31.880f), 229.606f, VehList.models_luxury, SpawnBehavior.VIP, VehList.models_armoured, 60),
            
            new SpawnSpot("RockfordPlazza_1", new Vector3(-187.144f, -175.854f, 42.624f), 160.257f, VehList.models_luxury,  SpawnBehavior.VIP, VehList.models_armoured, 5),
            
            new SpawnSpot("CityHallParking", new Vector3(283.791f, -342.399f, 44.538f), 70.666f, VehList.models_luxury,  SpawnBehavior.VIP, VehList.models_armoured, 60),
            
            new SpawnSpot("Casino", new Vector3(870.7411f, -75.28734f, 78.10686f), 147.4842f, VehList.models_luxury,  SpawnBehavior.VIP, VehList.models_armoured, 60),
         
            new SpawnSpot("Kortz", new Vector3(-2316.357f, 280.0749f, 168.9348f), 201.5139f, VehList.models_luxury,  SpawnBehavior.VIP, VehList.models_armoured, 25),
            
            new SpawnSpot("ChumashBeachHouse", new Vector3(-3072.296f, 657.9456f, 10.53257f), 311.2028f, VehList.models_luxury,  SpawnBehavior.VIP, VehList.models_bikes, 50),
           
            new SpawnSpot("LegionSquareNorth", new Vector3(243.1413f, -861.0181f, 28.94244f), 248.5342f, VehList.models_luxury,  SpawnBehavior.VIP, VehList.models_armoured, 10),

            // Ambient Population - Primary Spawn is general
            new SpawnSpot("G_RockfordPlazza_2", new Vector3(-174.280f, -180.751f, 43.235f), 340.784f, VehList.models_muscle,  SpawnBehavior.Muscle),
            new SpawnSpot("G_VinewoodAmmo", new Vector3(238.2489f, -34.84402f, 69.18212f), 340.165f, VehList.models_muscle, SpawnBehavior.Muscle, VehList.models_bikes, 50),
            new SpawnSpot("G_DavisLuckyPlucker", new Vector3(124.0182f, -1472.58f, 28.6794f), 321.0109f, VehList.models_muscle, SpawnBehavior.Muscle),
            new SpawnSpot("G_DavisMegaMall", new Vector3(31.46499f, -1706.062f, 28.6591f), 23.36283f, VehList.models_muscle, SpawnBehavior.Muscle),
            new SpawnSpot("G_TextileCity", new Vector3(393.4623f, -649.7198f, 27.92926f), 90.89349f, VehList.models_muscle, SpawnBehavior.Muscle),
            new SpawnSpot("G_MirrorParkBodyShop", new Vector3(1136.156f, -773.997f, 56.632f), 269.604f, VehList.models_muscle, SpawnBehavior.Muscle),
            new SpawnSpot("G_MirrorParkHouse", new Vector3(1309.942f, -530.154f, 70.312f), 341.133f, VehList.models_muscle, SpawnBehavior.Muscle),
            new SpawnSpot("G_MorningWood", new Vector3(-1528.733f, -427.0032f, 35.01511f), 48.3741f, VehList.models_muscle, SpawnBehavior.Muscle),
          
            // Tuner Spawns - Primary Spawn is tuner, secondary is muscle
            new SpawnSpot("G_KoreanRestaurant",new Vector3(-604.9778f, -1218.401f, 13.92473f), 133.0528f, VehList.models_tuner,  SpawnBehavior.Tuner, VehList.models_muscle, 50),
            new SpawnSpot("G_LpSeoul", new Vector3(-582.6653f, -859.2297f, 25.49919f),358.6906f, VehList.models_tuner,  SpawnBehavior.Tuner, VehList.models_muscle, 50),
         

            new SpawnSpot("G_LegionSquareSouth", new Vector3(185.595f, -1016.01f, 28.3f), 33.185f, VehList.models_muscle,  SpawnBehavior.Muscle),
            new SpawnSpot("G_EastLosHospital", new Vector3(1156.74f, -1474.257f, 33.9701f), 268.8033f, VehList.models_muscle,  SpawnBehavior.Muscle),
            new SpawnSpot("G_ChumashShopping", new Vector3(-3139.044f, 1086.714f, 20.23225f),260.5882f, VehList.models_muscle,  SpawnBehavior.Muscle, VehList.models_bikes, 50),
            new SpawnSpot("G_MaibatsuDealer", new Vector3(246.847f, -1162.08f, 28.16f), 180.390f, VehList.models_muscle,  SpawnBehavior.Muscle,VehList.models_bikes, 50),

            // --- MILITARY SPAWNS (Spec) ---
            new SpawnSpot("M_Planes_1", new Vector3(-1892.247f, 3082.933f, 32.810f), 147.141f, VehList.models_military_planes, SpawnBehavior.Spec, null, 0, 600f, 600f, false, 80),
            new SpawnSpot("M_Planes_2", new Vector3(-1934.867f, 3109.608f, 32.810f), 150.073f, VehList.models_military_planes, SpawnBehavior.Spec, null, 0, 600f, 600f, false, 80),
            new SpawnSpot("M_Helis", new Vector3(-1965.212f, 3101.532f, 32.810f), 236.324f, VehList.models_military_helicopters, SpawnBehavior.Spec, null, 0, 600f, 600f, false, 80),
            new SpawnSpot("M_Insurgents", new Vector3(-1788.814f, 3088.862f, 32.737f), 240.882f, VehList.models_insurgents, SpawnBehavior.Spec, null, 0, 600f, 600f, false, 80),
            new SpawnSpot("M_Thruster", new Vector3(-1792.126f, 3085.639f, 32.656f), 279.921f, VehList.models_thruster, SpawnBehavior.Spec,  null, 0, 600f, 600f, false, 80),
            new SpawnSpot("M_OppressorMKII", new Vector3(-1787.989f, 3082.481f, 32.726f), 284.223f, VehList.models_oppressor2, SpawnBehavior.Spec,  null, 0, 600f, 600f, false, 80),
          
            // Desert (RandomSpec)
            new SpawnSpot("Paleto_modshop", new Vector3(140.945f, 6606.513f, 30.845f), 0.239f, VehList.models_wacky, SpawnBehavior.RandomSpec),
            new SpawnSpot("Route68_modshop", new Vector3(1205.454f, 2658.357f, 36.824f), 223.627f, VehList.models_wacky, SpawnBehavior.RandomSpec),
            new SpawnSpot("Cheburek", new Vector3(1546.591f, 3781.791f, 33.06f), 26.557f, VehList.models_wacky, SpawnBehavior.RandomSpec),
            // Doomsday
       //     new SpawnSpot("SandyShores_145422", new Vector3(1815.533f, 3907.388f, 33.245f), 104.928f, VehList.models_doomsday, SpawnBehavior.Spec), // Sandy Shores - Cholla Springs Ave - Ref: TAMPA3
//new SpawnSpot("SandyShores_145708", new Vector3(1434.183f, 3640.622f, 34.397f), 289.263f, VehList.models_doomsday, SpawnBehavior.Spec), // Sandy Shores - Lesbos Ln - Ref: TAMPA3
// new SpawnSpot("ZancudoRiver_145814", new Vector3(376.884f, 3565.175f, 32.763f), 71.017f, VehList.models_doomsday, SpawnBehavior.Spec), // Zancudo River - Marina Dr - Ref: TAMPA3
// new SpawnSpot("GrandSenoraDesert_150025", new Vector3(-27.991f, 2890.005f, 58.122f), 229.419f, VehList.models_doomsday, SpawnBehavior.Spec), // Grand Senora Desert - Route 68 Approach - Ref: TAMPA3
            new SpawnSpot("HarmonyAutoShop", new Vector3(222.276f, 2580.151f, 45.276f), 280.075f, VehList.models_doomsday, SpawnBehavior.RandomSpec), // Harmony - Route 68 - Ref: TAMPA3
    
            new SpawnSpot("Route68west", new Vector3(392.6896f, 2641.558f, 44.07256f), 205.5221f, VehList.models_beaters, SpawnBehavior.Beater, VehList.models_offroad, 20),

            // Sandy Shores / Yellow Jack Area
            new SpawnSpot("YellowJack", new Vector3(1991.201f, 3076.069f, 46.79815f), 58.8943f, VehList.models_beaters, SpawnBehavior.Beater, VehList.models_offroad, 20),

            // Grapeseed / North Alamo
            new SpawnSpot("Trevor", new Vector3(1977.402f, 3835.433f, 31.59359f), 297.7102f, VehList.models_beaters, SpawnBehavior.Beater, VehList.models_offroad, 20),

            // Sandy Shores / Alamo Sea Coast
            new SpawnSpot("Ace", new Vector3(1350.489f, 3605.351f, 34.47185f), 16.77524f, VehList.models_beaters, SpawnBehavior.Beater, VehList.models_offroad, 20),
            new SpawnSpot("EastLosOilFields", new Vector3(1566.097f, -1683.17f, 87.205f), 14.900f, VehList.models_offroad, SpawnBehavior.Beater, VehList.models_muscle, 20),
            new SpawnSpot("PowerStationNorth", new Vector3(2673.478f, 1678.569f, 23.488f), 270.297f, VehList.models_beaters, SpawnBehavior.Beater, VehList.models_muscle, 20, 200f, 150f),
            new SpawnSpot("PowerStationSouth", new Vector3(2717.772f, 1391.725f, 23.535f), 1.832709f, VehList.models_beaters, SpawnBehavior.Beater, VehList.models_muscle, 20, 200f, 150f),
            new SpawnSpot("RedwoodTrack", new Vector3(839.097f, 2202.196f, 50.46f), 245.553f, VehList.models_offroad, SpawnBehavior.Beater, VehList.models_muscle, 20),
            
            new SpawnSpot("RebelRadio", new Vector3(756.539f, 2525.957f, 72.161f), 270.240f, VehList.models_wacky, SpawnBehavior.RandomSpec, VehList.models_offroad, 50),

            //Motorcycles (Spec)
            new SpawnSpot("TataviamMountains", new Vector3(2593.022f, 364.349f, 107.457f), 174.745f, VehList.models_muscle, SpawnBehavior.Muscle),
            new SpawnSpot("TongvahGasStation", new Vector3(-1804.77f, 804.137f, 137.514f), 223.747f, VehList.models_muscle, SpawnBehavior.Muscle),
            new SpawnSpot("CypressFlatsNorth", new Vector3(934.148f, -1812.94f, 29.812f), 88.712f, VehList.models_lowriders, SpawnBehavior.RandomSpec),
            new SpawnSpot("CypressFlatsSouth", new Vector3(1028.898f, -2405.95f, 28.494f), 170.017f, VehList.models_muscle, SpawnBehavior.Spec, VehList.models_bikes, 50),
            new SpawnSpot("Hookies", new Vector3(-2221.14f, 4232.757f, 46.132f), 225.108f, VehList.models_bikes, SpawnBehavior.Spec),
           // new SpawnSpot("ZancudoGasStation", new Vector3(-2555.51f, 2322.827f, 32.06f), 273.837f, VehList.models_bikes, SpawnBehavior.Spec),




        };
    }
}