
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
   "clique2",
   "peyote2",
   "weevil2"
    };

    public static List<string> models_studio = new List<string>() {
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



    public static List<string> models_lowriders = new List<string>() {
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


    public static List<string> models_karting = new List<string>() {
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

    /*public static List<string> models_beach_cars = new List<string>() {
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

    };*/



    public static string apc_model = "apc";
    public static string raiju_model = "raiju";
    public static string conada2_model = "conada2";


    public static List<string> models_insurgents = new List<string>() {
    "insurgent2",
    "insurgent3",
    "nightshark",
    };


    public static List<string> models_thruster = new List<string>() {
     "thruster"
    };
    public static List<string> models_oppressor2 = new List<string>() {
     "oppressor2"
    };

    public static List<string> models_military_planes = new List<string>() {
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

    public static List<string> models_military_helicopters = new List<string>() {
    "annihilator2",
    "hunter",
    "valkyrie",
    "savage",
     "conada2",
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

    public static List<string> models_submarine = new List<string>() {
    "avisa",
    };


    public static List<string> models_supers_common = new List<string>() {//these cars do not spawn in natural-popgroups traffic and dont have a dedicated rockstar spawn
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
    };

    public static List<string> models_supers_rare = new List<string>() { //these cars already spawn in traffic or have an in game location
       "zentorno",
       "turismor",
       "cheetah",
       "entityxf",
       "vacca",     
        "adder",     
       "sc1",
       "penetrator",
       "pfister811",
       "cyclone",
       "sm722",
       "visione",
       "deveste",

    };



    public static List<string> models_valentine = new List<string>() {
    "btype3",
    "broadway",
    "stafford",
     "coquette5",
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
        "xls2",
        "cognoscenti2",
        "cog552",
        "schafter5",
        "schafter6",
        "paragon2"
    };

    public static List<string> models_classics_common = new List<string>() {

         "cheetah2",
         "feltzer3",
         "infernus2",
         "gt500",
         "rapidgt3",
         "swinger",
         "torero",
         "turismo2",
         "tropos",

    };
    public static List<string> models_classics_rare = new List<string>() {
        "monroe",
        "stingergt",
        "mamba",
        "commet3",
        // "casco",
        //"jb7002",
    };


/*
    public static List<string> models_old_school = new List<string>() { 
        "issi3",
        "dynasty",
        "hustler",
        "hermes",
        "coquette3",
    };*/
    public static List<string> models_wacky = new List<string>() {
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
        "winky"
    };

   /* public static List<string> models_city_rich = new List<string>() {
        "banshee2",
        "bestiagts",
        "brioso",
        "cinquemila",
      //  "cog55",
      //  "cognoscenti",
        "comet5",
        "coquette6",
        "cypher",
        "drafter",
        "fr36",
        "growler",
        "italigtb",
        "italigto",
        "jugular",
        "khamelion",
        "komoda",
        "lynx",
        "neon",
        "novak",
        "panthere",
        "paragon",
        "pariah",
        "raiden",
        "rebla",
        "revolter",
        "schlagen",
        "seven70",
        "specter",
        "s95",
        "vectre",
        "verlierer2",
        "vstr",
        "windsor",
        "windsor2",
        "xls"
    };*/


    public static List<string> models_city_mid = new List<string>() {  //my favorite low end cars i like seeing and would like to see in the city
        "brioso",
        "seven70",
        "lynx",
        "xls",
        "specter",
        "schlagen",
        "revolter",
        "paragon",
        "panthere",
        "pariah",
        "raiden",
        "neo",
        "italigto",
        "italigtb",
    };

    public static List<string> models_city_low = new List<string>() {  //my favorite low end cars i like seeing and would like to see in the city
        "asbo",
        "kanjo",
        "calico",
        "xls",
        "specter",
        "schlagen",
        "revolter",
        "paragon",
        "panthere",
        "pariah",
        "raiden",
        "neo",
        "italigto",
        "italigtb",
    };


    public static List<string> models_choppers = new List<string>() {
 //   "shinobi",
  //  "manchez2",
  //  "stryder", 3 wheeler trike
 //   "fcr2",
 //   "fcr",
  //  "diablous", sports
   // "diablous2", sports
  //  "esskey", //sports and dirt bike hybrid
   // "vortex", //sports bike
    "daemon2",
    "zombieb",
    "nightblade",
   // "manchez", //dirt bike
  //  "hakuchou2", //sports bike
   // "faggio", //faggio sport
    "faggio3", //faggio mod
   // "defiler", //spots bike
    "chimera",
    "avarus",
    "cliffhanger",
    "gargoyle",
  //  "bf400", //dirt bike
 //   "vindicator",  //sports bike
 //   "lectro",  //sports bike
  //  "enduro", //dirt bike
    };
    public static List<string> models_bikes = new List<string>() {

    "stryder",
    "fcr2",
    "fcr",
    "diablous2", 
    "esskey", //sports and dirt bike hybrid
    "vortex", //sports bike
    "manchez", //dirt bike



    "bf400", //dirt bike
   "vindicator",  //sports bike
    "lectro",  //sports bike
    "enduro", //dirt bike
    };

    public static List<string> models_general_rare = new List<string>() {//these spawn everywhere desert and city

        "asterope2",


        "chavosv6",
        "z190",


        "fr36",
        "futo2", //futo gtx
        "hellion",

        "kanjosj",
        "khamelion",
        "moonbeam",
        "nebula",

        "retinue",

        "seminole2",

        "sultan2",

        "vstr",

        "zr350",
        "youga2", //youga classic
   
        "hakuchou2", //sports bike
            "wolfsbane",
                "zombiea",
         "hermes",


    };
    public static List<string> models_general_common = new List<string>() {//these spawn everywhere desert and city
         "asbo",
                 "kanjo",
                         "brioso",
        "calico",
        "club",
        "dominator7", //dominator gtx
        "dominator8", //dominator gtt
        "dominator3", //dominator asp
                "eudora",
        "faction",
                "impaler",
        "jester3", //jester classic
              "kuruma",
                "nightshade",
        "previon",
        "remus",
                "rt3000",
                "sentinel3", //sentinel classic
        "slamvan",
                "tulip",
        "virgo",
                "zion3", //zion classic
             "penumbra2", //penumbraff
                            "issi3",
        "dynasty",
        "hustler",
                "yosemite",
    };


    public static List<string> models_city = new List<string>() {//these will spawn in most urban areas, more prevalent in rich zones (city only)
        "banshee2",
        "bestiagts",
     //   "brioso",
        "cinquemila",
        "cog55",
        "cognoscenti",
        "comet5",
      //  "coquette6",
        "cypher",
        "drafter",
                "euros",
     //   "fr36",
        "growler",
        "italigtb",
        "italigto",
        "jugular",
        "komoda",
        "lynx",
        "neon",
        "novak",
        "panthere",
        "paragon",
        "pariah",
        "raiden",
        "rebla",
        "revolter",
        "schlagen",
        "seven70",
        "specter",
        "s95",
        "vectre",
        "verlierer2",
      //  "vstr",
        "windsor",
        "windsor2",
        "xls",
        "diablous",
         "shinobi",
            "faggio", //faggio sport
                "defiler", //spots bike
                        "coquette3",
    };

    public static List<string> models_rural = new List<string>() {//these will spawn in rural areas, more prevalent in rich zones (desert only)
        "boor",
        "brawler",
        "cheburek",
        "clique2",
        "dominator10", //fx
        "dorado",
        "esskey",
        "firebolt",
        "gauntlet5", //gauntlet classic custom
        "journey2",
        "kamacho",
        "monstrociti",
        "retinue2",
        "riata",
        "rumpo3",
        "savestra",
        "seminole2",
        "vamos",
        "yosemite3",
        "youga3",
        "enduro",
        "gargoyle",
        "manchez2",
        "sovereign",
        "bf400",

    };

}