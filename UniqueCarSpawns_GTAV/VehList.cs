
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using GTA.Math;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.IO;

public class VehList
{
    public static List<string> models_cult = new List<string>() {
    "entity2",
    "autarch",
    "sc1",
    "cyclone",
    "visione",
    "xa21",
    "gp1",
    "italigtb",
    "italigtb2",
    "nero",
    "nero2",
    "tempesta",
    "penetrator",
    "pfister811",
    "prototipo",
    "reaper",
    "fmj",
    "t20",
    "osiris",
    "cyclone2",
        "granger2",
    "astron",
    "landstalker2",
    "rebla",
    "novak",
    "toros",
    };

    public static List<string> models_boats = new List<string>() {
    "longfin",
    "toro"
    };

    public static List<string> models_cemetery = new List<string>() {
    "tornado6",
    "btype2",
    "sanctus",
    "lurcher",
    "brigham",
    };

    public static List<string> models_cheburek = new List<string>() {
    "cheburek",
    "ratbike",
    "slamvan3",
   "clique2",
    };

    public static List<string> models_cinema = new List<string>() {
    "scramjet",
    "vigilante",
    "voltic2",
    "toreador",
    "jb7002",
    "deluxo",
    "stromberg",
    "rrocket",
    "shotaro",
    "dune5"
    };

    public static List<string> models_cluckin = new List<string>() {
    "benson2"
    };

    public static List<string> models_compacts = new List<string>() {
    "brioso",
    "brioso2",
    "brioso3",
    "weevil",
    "club",
    "kanjo",
    "asbo",
    "issi3"
    };

    public static List<string> models_coupes = new List<string>() {
    "kanjosj",
    "postlude",
    "previon",
    "windsor2",
    "windsor",
    "fr36",
    "eurosX32",
    };



    public static List<string> models_ghetto = new List<string>() {
    "eudora",
        "faction3",
        "Buccaneer2",
     "Chino2",
     "Faction2",
     "Moonbeam2",
     "Primo2",
     "Voodoo",
     "SlamVan3",
     "Tornado5",
     "Minivan2",
     "Peyote3",
     "Yosemite2",
     "Chimera",
     "Glendale2",
     "Manana2",
     "SabreGT2",
        "virgo2"
    };
    public static List<string> models_poor = new List<string>() {
        "greenwood",
        "retinue2",
    "dynasty",
    "cheburek",
    "fagaloa",
    "greenwood",
    };

    public static List<string> models_helicopter = new List<string>() {
    "conada",
    "swift2",
    "Havok",
    "Volatus",
    "SeaSparrow",
    "Supervolito",
    "Supervolito2",
    "Swift2"
    };

    public static List<string> models_humanlabs = new List<string>() {
    "brickade2",
    };

    public static List<string> models_industrial = new List<string>() {
    "pounder2",
    "mule4",
    "phantom3",
    "hauler2",
    "phantom2",
    "mule3",
    "boxville4",
    "flatbed2",
    "stockade4",
    "keitora",

    };

    public static List<string> models_karting = new List<string>() {
    "veto",
    "veto2",
    };



    public static string thruster_model = "thruster";
    public static string apc_model = "apc";
    public static string raiju_model = "raiju";
    public static string conada2_model = "conada2";

    public static List<string> models_military_planes = new List<string>() {
    "strikeforce",
    "nokota",
    "pyro",
    "mogul",
    "howard",
    "molotok",
    "tula",
    "rogue",
    "starling",
    "alphaz1",
    "hydra",
    };

    public static List<string> models_military_helicopters = new List<string>() {
    "annihilator2",
    "akula",
    "hunter",
    "valkyrie",
    "savage",
    };

    public static List<string> models_military_opressors = new List<string>() {
    "oppressor",
    "oppressor2",
    };

    public static List<string> models_military_bikes = new List<string>() {
    "squaddie",
    "manchez2",
    "winky",
    "insurgent3",
    };

    public static List<string> models_motorcycles = new List<string>() {
    "manchez3",
    "shinobi",
    "manchez2",
    "stryder",
    "fcr2",
    "fcr",
    "diablous",
    "diablous2",
    "esskey",
    "vortex",
    "daemon2",
    "zombiea",
    "zombieb",
    "wolfsbane",
    "nightblade",
    "manchez",
    "hakuchou2",
    "faggio",
    "faggio3",
    "defiler",
    "chimera",
    "avarus",
    "cliffhanger",
    "gargoyle",
    "bf400",
    "vindicator",
    "lectro",
    "enduro",
    };

    public static List<string> models_muscle = new List<string>() {
    "tahoma",
    "tulip2",
    "weevil2",
    "ruiner4",
    "dominator7",
    "dominator8",
    "gauntlet5",
    "manana2",
    "dukes3",
    "yosemite2",
    "peyote2",
    "gauntlet4",
    "gauntlet3",
    "vamos",
    "deviant",
    "tulip",
    "clique",
    "impaler",
    "dominator3",
    "ellie",
    "hustler",
    "hermes",
    "yosemite",
    "sabreGT2",
    "virgo2",
    "virgo3",
    "tampa",
    "nightshade",
    "moonbeam2",
    "moonbeam",
    "faction2",
    "faction",
    "chino2",
    "buccaneer2",
    "coquette3",
    "chino",
    "vigero",
    "slamVan2",
    "impaler6",
    "dominator10",
    "arbitergt",
    "tampa4",
    };

    public static List<string> models_offroad = new List<string>() {
    "boor",
    "draugur",
    "patriot3",
    "yosemite3",
    "outlaw",
    "everon",
    "vagrant",
    "hellion",
    "caracara2",
    "kamacho",
    "riata",
    "blazer4",
    "rallyTruck",
    "trophyTruck",
    "trophyTruck2",
    "brawler",
    "guardian",
    "l35",
    "ratel",
    "monstrociti",
    "yosemite1500",
    "firebolt",
    "uranus",
    "l352",
    };


    public static List<string> models_beach = new List<string>() {
    "pbus2",
    };

    public static List<string> models_planes = new List<string>() {
    "Nimbus",
    "Luxor2",
    "Velum2",
    "Microlight",
    "Seabreeze",
    "Howard",
    "Rogue",
    "AlphaZ1",
    "streamer216"
    };

 


    public static List<string> models_sedans = new List<string>() {
    "rhinehart",
    "cinquemila",
    "tailgater2",
    "warrener2",
    "glendale2",
    "stafford",
    "schafter3",
    "schafter4",
    "cog55",
    "cognoscenti",
    "asterope2",
    "impaler5",
    "vorschlaghammer",
    "chavosv6",
    "hardy",
    "minimus",
    "sentinel6",

    };



    public static List<string> models_sportclassic = new List<string>() {
    "sentinel4",
    "cypher",
    "sultan3",
    "vectre",
    "remus",
    "rt3000",
    "zr350",
    "euros",
    "futo2",
    "penumbra2",
    "sugoi",
    "vstr",
    "sultan2",
    "komoda",
    "jugular",
    "zion3",
    "locust",
    "nebula",
    "neo",
    //"issi7",
    "schlagen",
    "italigto",
    "swinger",
    "jester3",
    "michelli",
    "comet5",
    "z190",
    "neon",
    "revolter",
    "gt500",
    "viseris",
    "savestra",
    "sentinel3",
    "raiden",
    "pariah",
    "rapidgt3",
    "retinue",
    "torero",
    "cheetah2",
    "turismo2",
    "infernus2",
    "ruston",
    "specter2",
    "specter",
    "comet3",
    "elegy",
    "lynx",
    "tropos",
    "seven70",
    "bestiagts",
    "mamba",
    "verlierer2",
    "schafter3",
    "schafter4",
    "feltzer3",
    "casco",
    "kuruma",
    "coquette5",
    "niobe",
    "banshee3",
    "coquette6",
    "s95",
    "astrale",
    "gt750",
    "itali2",
    };

    public static List<string> models_submarine = new List<string>() {
    "avisa",
    };

    public static List<string> models_supers = new List<string>() {
   /* //base game traffic cars 
       "zentorno",
       "turismor",
       "cheetah",
       "entityxf",
       "vacca", 
        
        //base game parked cars
   "adder", */
        
     /*   //traffic  cars
        "tempesta",
"italigtb",
"sc1",
"reaper",
"penetrator",*/
        //parked  cars
        "t20",
"pfister811",
"osiris",
"gp1",
"xa21",
"fmj",
"cyclone",
"prototipo",
"nero",
"nero2",
"visione",
"entity2",
        /*
    "lm87",
    "s80",
    "entity2",
    "autarch",
    "sc1",
    "cyclone",
    "visione",
    "xa21",
    "gp1",
    "italigtb",
    "italigtb2",
    "nero",
    "nero2",
    "tempesta",
    "penetrator",
    "tyrus",
    "le7b",
    "sheava",
    "pfister811",
    "prototipo",
    "reaper",
    "fmj",
    "t20",
    "osiris",
    "cyclone2",*/
    };

    public static List<string> models_suvs = new List<string>() {
    "granger2",
    "astron",
    "seminole2",
    "landstalker2",
    "rebla",
    "novak",
    "toros",
    "contender",
    "xls",
    "baller3",
    "baller4",
    "dorado",
    "astron2",
    "woodlander",
    };



    public static List<string> models_tuners = new List<string>() {
    "kanjosj",
    "postlude",
    "previon",
    "cypher",
    "sultan3",
    "vectre",
    "dominator7",
    "remus",
    "warrener2",
    "rt3000",
    "zr350",
    "dominator8",
    "euros",
    "futo2",
    "calico",
            "sultanrs",
            "banshee2",
    };

    public static List<string> models_valentine = new List<string>() {
    "btype3",
    "broadway",
    "stafford"

    };

    public static List<string> models_vans = new List<string>() {
    "journey2",
    "surfer3",
    "youga3",
    "speedo4",
    "youga2",
    "rumpo3",
    "minivan2",
    "boxville6",
    };

    public static List<string> models_wastelander = new List<string>() {
    "wastelander",
    };

    public static List<string> models_weaponboats = new List<string>() {
    "tug",
    };



    public static List<string> models_pizza = new List<string>() {
    "pizzaboy",
    };



    public static List<string> models_plane_sandy = new List<string>() {
    "duster2",
    };

    public static List<string> models_heli_sandy = new List<string>() {
    "cargobob5",
    };


    public static List<string> models_hsw = new List<string>() {
    "s95",
    "astron2",
    "firebolt",
    "banshee3",
    "eurosx32",
    "niobe",
    "fr36",
    "monstrociti",
    "issi8",
    "arbitergt",
    "turismo2",
    "sentinel",
    "banshee",
    "hakuchou2",
    "brioso",
    "feltzer3"
    };

    public static List<string> models_higgins = new List<string>() {
    "maverick2",
    "conada",
    };





    public static List<string> models_arena_speed = new List<string>() {
    "sheava",
    "tyrus",
    "le7b",
    "tropos",
    "lm87",
    "specter2",
    "italigtb2",
    "s80",
};

    public static List<string> models_arena_hotring = new List<string>() {
    "tampa2",
    "gauntlet6",
    "hotring",
    "everon2",
    "flashgt",
    "gb200",
    "omnis",
};

    public static List<string> models_arena_offroad = new List<string>() { 
    "ratel",
    "monstrociti",
    "hellion",
    "trophytruck",
    "trophytruck2",
    "bf400",
    "cliffhanger",
    "raptor",
};
    public static List<string> models_openwheel = new List<string>() {
    "openwheel1",
    "openwheel2",
    "formula",
    "formula2",
    };

    public static List<string> models_armoured = new List<string>() {
        "baller5",
    "baller6",
        "granger2",
    "landstalker2",
    "novak",
    "toros",
    "xls2",
    "cognoscenti2",
        "cog552",
            "schafter5",
    "schafter6",
    };
    public static List<string> models_classics = new List<string>() {
       
 
        //traffic classics
/*
         "casco",
         "cheetah2",
         "comet3",
         "coquette2",
         "coquette3",
         "coquette5",
         "feltzer3",
         "issi3",
         "michelli",
         "rapidgt3",
         "tropos",
         "viseris",
*/
         //parked classic
             "infernus2",
            "gt500",
            "jb7002",
            "mamba",
            "monroe",
            "stinger",
            "stingergt",
            "swinger",
            "torero",
            "turismo2",


    };


}