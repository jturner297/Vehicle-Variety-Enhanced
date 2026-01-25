
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
    public static HashSet<string> models_cult = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "autarch",
    "sc1",
    "cyclone",
    "visione",
    "xa21",
    "gp1",
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
    "rebla",
    "novak",
    "sm722"
    };


    public static HashSet<string> models_cemetery = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "tornado6",
    "btype2",
    "sanctus",
    "lurcher",
    "brigham",
    };


    public static HashSet<string> models_studio = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "scramjet", //speedracer
    "vigilante", //batmobile
    "voltic2", //rocket voltic
    "toreador",//submarine car #2
    "jb7002",//bond car
    "deluxo",//delorean
    "stromberg",//submarine car
    "rrocket",//rampant rocket
    "shotaro",//tron
    "dune5",//rampbuggy
    "ruiner2", //nightrider
    "oppressor" //streethawk
    };

    public static HashSet<string> models_cluckin = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "benson2"
    };





    public static HashSet<string> models_lowriders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
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
     "Chimera",
     "Glendale2",
     "Manana2",
     "SabreGT2",
        "virgo2"
    };


    public static HashSet<string> models_helicopter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "conada",
    "swift2",
    "Havok",
    "Volatus",
    "SeaSparrow",
    "Supervolito",
    "Supervolito2",
    "Swift2"
    };

    public static HashSet<string> models_humanlabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "brickade2",
    };


    public static HashSet<string> models_karting = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "veto",
    "veto2",
         "locust",
          "ruston",
          "raptor",
          "stryder",
          "dubsta3",
          "everon",
          "brawler",
          "kamacho",
          "monstrociti",
          "vagrant",
          "outlaw"
    };





    public static string apc_model = "apc";
    public static string raiju_model = "raiju";
    public static string conada2_model = "conada2";



    public static HashSet<string> models_kuruma = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     "kuruma2"
    };

    public static HashSet<string> models_insurgents = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "insurgent2",
    "insurgent3",
    "nightshark",
    };


    public static HashSet<string> models_thruster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     "thruster"
    };
    public static HashSet<string> models_oppressor2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     "oppressor2"
    };

    public static HashSet<string> models_military_planes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "strikeforce",
    "nokota",
    "pyro",
    "mogul",
       "raiju",
    "molotok",
    "tula",
    "starling",
    "hydra",
    };

    public static HashSet<string> models_military_helicopters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "annihilator2",
    "hunter",
    "valkyrie",
    "savage",
     "conada2",
    };

    public static HashSet<string> models_military_opressors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "oppressor",
    "oppressor2",
    };

    public static HashSet<string> models_military_bikes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "squaddie",
    "manchez2",
    "winky",
    "insurgent3",
    };

    public static HashSet<string> models_motorcycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "manchez3",
    "shinobi",
    "manchez2",
    //"stryder",
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
    //"chimera",
    "avarus",
    "cliffhanger",
    "gargoyle",
    "bf400",
    "vindicator",
    "lectro",
    "enduro",
    };



    public static HashSet<string> models_beach = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "pbus2",
    };

    public static HashSet<string> models_planes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
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



    public static HashSet<string> models_super = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these cars do not spawn in natural-popgroups traffic and dont have a dedicated rockstar spawn
        "tempesta",
        "reaper",
        "t20",
        "osiris",
        "gp1",
        "xa21",
        "fmj",  
        "prototipo",
        "nero",
        "nero2",
        "banshee3",
             "sm722",
       "visione",
       "deveste",
    };

    
    public static HashSet<string> models_doomsday = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { //these cars already spawn in traffic or have an in game location
       "tampa3",
    };



    public static HashSet<string> models_valentine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "btype3",
    "broadway",
    "stafford",
     "coquette5",
    };


    public static HashSet<string> models_wastelander = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "wastelander",
    };

    public static HashSet<string> models_weaponboats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "tug",
    };



    public static HashSet<string> models_pizza = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "pizzaboy",
    };


    public static HashSet<string> models_higgins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "maverick2",
    "conada",
    };





    public static HashSet<string> models_arena_speed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "sheava",
    "tyrus",
    "le7b",
    "tropos",
    "lm87",
    "specter2",
    "italigtb2",
    "s80",
};

    public static HashSet<string> models_arena_hotring = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "tampa2",
    "gauntlet6",
    "hotring",
    "everon2",
    "flashgt",
    "gb200",
    "omnis",
};

    public static HashSet<string> models_arena_offroad = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { 
    "ratel",
    "monstrociti",
    "hellion",
    "trophytruck",
    "trophytruck2",
    "bf400",
    "cliffhanger",
    "raptor",
};
    public static HashSet<string> models_openwheel = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "openwheel1",
        "openwheel2",
        "formula",
        "formula2",
    };

    public static HashSet<string> models_armoured = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "baller5",
        "baller6",
        "xls2",
        "cognoscenti2",
        "cog552",
        "schafter5",
        "schafter6",
        "paragon2"
    };
   
    public static HashSet<string> models_classics = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {

         "cheetah2",
         "feltzer3",
         "infernus2",
         "gt500",
         "rapidgt3",
         "swinger",
         "torero",
         "turismo2",
         "tropos",
                "monroe",
       "stingergt",
       "casco",
      "jb7002",
            "ardent",

    };




    public static HashSet<string> models_wacky = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "dukes3",
        "cheburek",
        "comet4",
        "hotknife",
        "weevil2",
        "patriot3",
        "peyote2",
        "ratbike",
        "ratloader2",
        "ratloader",
        "tornado6",
        "winky",
             "Yosemite2",
    };



    public static HashSet<string> models_bikes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "shinobi",
    "manchez2",
    "stryder", 
    "fcr2",
    "fcr",
    "diablous", 
    "diablous2", 
   "esskey", //sports and dirt bike hybrid
   "vortex", //sports bike
    "daemon2",
    "zombieb",
    "nightblade",
    "manchez", //dirt bike
    "hakuchou2", //sports bike
    "faggio", //faggio sport
    "faggio3", //faggio mod
    "defiler", //spots bike
    "chimera",
    "avarus",
    "cliffhanger",
    "gargoyle",
   // "bf400", //dirt bike
    "vindicator",  //sports bike
   "lectro",  //sports bike
    "enduro", //dirt bike
    };




    public static HashSet<string> models_tuner = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these spawn everywhere desert and city
                 "kanjo",
        "calico",
        "jester3", //jester classic
              "kuruma",
        "previon",
        "remus",
                "rt3000",
                "sentinel3", //sentinel classic
             "penumbra2", //penumbraff
                "yosemite2",
                        "banshee2",



        "z190",


        "fr36",
        "futo2", //futo gtx

        "kanjosj",


        "nebula",

        "retinue",

                "sultanrs",

                        "sultan3",
        "sultan2",

 

        "zr350",
            "kanjosj",

    "previon",




    "dominator7",

    "remus",
    "sentinel4",
    "elegy",
        "comet3",
        "jugular"

    };

    public static HashSet<string> models_muscle = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
  "arbitergt",
    "chino",
    "clique",
    "coquette3",
    "deviant",
    "dominator10",
    "dominator3",
    "dominator7",
    "dominator8",
    "dukes3",
 //   "dynasty",
    "ellie",
  //  "faction",
   // "fr36",
 //   "futo2",
    "gauntlet3",
    "gauntlet5",
    "greenwood",
  //  "hellion",
    "hermes",
    "hustler",
    "impaler",
 //   "issi3",
//    "kanjo",
//    "kanjosj",
 //   "moonbeam",
  //  "nebula",
    "nightshade",
    "ruiner4",
  //  "slamvan",
    "tahoma",
   // "tampa",
    "tulip",
    "vamos",
//    "virgo",
    "yosemite",
    "yosemite2",
    "youga2"
    };


    public static HashSet<string> models_luxury = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     //   "banshee2",
        //"bestiagts",
     //   "brioso",
      //  "cinquemila",
        "cog55",
        "cognoscenti",
     //   "comet5",
       // "coquette6",
       "cypher",
        "drafter",
             //   "euros",
     //   "fr36",
      //  "growler",
        "italigtb2",
        "italigto",
       // "jugular",
     //   "komoda",
        "lynx",
      //  "neon",
        "novak",
        "panthere",
        "paragon",
        "pariah",
        //"raiden",
      //  "rebla",
        "revolter",
        "schlagen",
        "seven70",
        "specter2",
        "s95",
      //  "vectre",
      //  "verlierer2",
        "vstr",
        "windsor",
        "windsor2",
       // "xls",
                       // "coquette3",
                       
       "mamba",
       "comet3",
             "zentorno",
       "turismor",
       "cheetah",
       "entityxf",
       "sc1",
       "penetrator",
       "cyclone",
       "locust",
       "ruston"
    };
    public static HashSet<string> models_beaters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these will spawn in rural areas, more prevalent in rich zones (desert only)
        "asbo",
        "dukes3",
        "kanjo",
        "boor",
        "cheburek", 
        "clique2",
        "club",
        "dynasty",
        "eudora", 
        "fagaloa",
        "futo2",
        "youga2",
        "issi3",
        "kanjosj",
        "nebula",
        "retinue2",
        "warrener2",
        "zion3",

    };



    public static HashSet<string> models_offroad = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these will spawn in rural areas, more prevalent in rich zones (desert only)
        "brawler",
        "comet4",
        "contender",
        "everon",
        "firebolt",
        "hellion",
        "journey2",
        "kamacho",
        "monstrociti",
        "outlaw",
        "patriot3",
        "riata",
        "rumpo3",
        "seminole2",
        "squaddie",
        "outlaw",
       "vagrant",
               "yosemite3",
               "youga3"

    };




  
}