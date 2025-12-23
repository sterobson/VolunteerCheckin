using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.IO;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class CsvParserServiceTests
{
    [TestMethod]
    public void ParseLocationsCsv_SimpleCsv_ReturnsLocations()
    {
        string csv = "Label,Lat,Long\nA,47.0,-122.0\nB,48.0,-123.0";
        using MemoryStream ms = new(System.Text.Encoding.UTF8.GetBytes(csv));

        CsvParserService.CsvParseResult result = CsvParserService.ParseLocationsCsv(ms);

        result.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(0);
        result.Locations.Count.ShouldBe(2);
        result.Locations[0].Name.ShouldBe("A");
    }

    [TestMethod]
    public void ParseLocationsCsv_SimpleCsv_RespectsOptionalData()
    {
        string csv = """
            Label,  Latitude (oPtioNAl),    Long,   marshals
            CP1,    1.23,                   4.56,   "Ste, Ruth and Joseph" 
            CP2,    4.56,                   7.89,   Joanne 
            CP3,    4.56,                   7.89,   "Mike and Jenna Jones, Killian Murphy + 1, Sarah Bridges, +2" 
        """;

        using MemoryStream ms = new(System.Text.Encoding.UTF8.GetBytes(csv));

        CsvParserService.CsvParseResult result = CsvParserService.ParseLocationsCsv(ms);

        result.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(0);
        result.Locations.Count.ShouldBe(3);
        result.Locations[0].Name.ShouldBe("CP1");
        result.Locations[0].MarshalNames.ShouldBe(["Ste", "Ruth", "Joseph"]);
        result.Locations[1].MarshalNames.ShouldBe(["Joanne"]);
        result.Locations[2].MarshalNames.ShouldBe(["Mike Jones", "Jenna Jones", "Killian Murphy", "Killian Murphy's (+1)", "Sarah Bridges", "Sarah Bridges' (+1)", "Sarah Bridges' (+2)"]);
    }

    [TestMethod]
    public void ParseLocationsCsv_SimpleCsv_TestRealFile()
    {
        string csv = """
            Position,,What3Words Location,Latitude,Longitude,Marshal(s) 2026,,,,,
            1A,Start/Finish (last 100M/CROSSING ),issue.copper.shock,53.939184,-1.091049,Pete Tasker,,,,,
            1B,Start/Finish (250M to go PATH CORSSING),woof.enjoyable.always,53.937918,-1.090133,Sophie Sasimowicz,,,,,
            1C,Racecourse Entrance,apple.whites.blank,53.938053,-1.08766,"Caroline Doherty, Emily Ludgate",,,,,
            2,Manor Farm Caravan Park,bonds.loss.bottom,53.935735,-1.087523,Sam Broadbent & Delia Ellis,,,,,
            3,One School Global (College of Law) - Entrance,refuse.flute.undulation,53.933067,-1.089721,Qin Zhou & Jet Jon Shepherd,,,,,
            4,Middlethorpe Hall Entrance,tribe.probe.trout,53.930803,-1.090453,Rose Mather & Kenny Harris,,,,,
            5,A64 flyover,stop.exists.error,53.928512,-1.093201,Graham Myers & Andrew Hill,,,,,
            6,Crematorium Entrance,bumpy.rise.caller,53.926625,-1.093887,"Laura Bennett, Lauar Barratt & Jenna Drury",,,,,
            7,Church Lane junction (+ Traffic management),woof.sung.sends,53.922906,-1.094437,Lucy Hart & Lucy Knight,,,,,
            8,Bishop's Palace bend,wiped.groups.with,53.922151,-1.093979,"Neil Croxon, Jill Armes & Andrew Johnson",,,,,
            9,Acaster Lane junction,hurray.wipe.joins,53.921963,-1.094528,Matt & Kate Mills,,,,,
            10,Sim Balk Lane junction (+Traffic management),tables.spring.vows,53.921127,-1.100206,"Lance Hill, Pip Hill",,,,,
            11,Junction before Sim Balk,hidden.name.puff,53.921019,-1.100572,"Gareth Green,Beth Benger",,,,,
            12,Maple Avenue junction,chop.likely.sparks,53.920372,-1.101351,Linda & Mike Grewer,,,,,
            13,Ex - Railway bridge,flight.belong.bump,53.918998,-1.103182,"Andrew Bradley, Mel Hudson & Louise Groves",,,,,
            14,Junction with Moor Lane,yoga.drape.random,53.916977,-1.105563,Andy Richardson & Chris Scott,,,,,
            15,Acaster X- roads (+Traffic management),tedious.restored.cookie,53.90563,-1.110829,Stephen Greenwood & Sam Dredge,,,,,
            16,Darling Lane,stars.waltz.trimmer,53.903258,-1.113072,Pam & Richard Anderson,,,,,
            17,Foss Field Lane,gilding.basis.betrayal,53.902531,-1.113897,"Simon Fricke, + 2",,,,,
            18,Whinney Hills T-junction,mend.apron.slogans,53.893044,-1.124153,"Nick Griffin, Ed & Amy Woolard",,,,,
            19,Bends at Acaster junction,performed.daytime.losses,53.891858,-1.12255,Louise Walley & Emily Harper,,,,,
            20,Boat Yard bend,runways.fattening.spans,53.890241,-1.120536,David & Liz Piper,,,,,
            21,EBOR Trucks,sooner.sounds.excavate,53.885201,-1.124794,Rebecca & Roger West,,,,,
            22,Airfield,lofts.sung.insurance,53.878086,-1.125847,Mike Denner & Dave I'Anson,,,,,
            23,Acaster Selby T-junction,look.forecast.dockers,53.867521,-1.130008,"Andy Clark, Richard & Emily Walker",,,,,
            24,Last Bend before Appleton,dark.wolves.cheerily,53.872938,-1.156399,Pete Downes,,,,,
            25,Appleton junction,edicts.dynamics.grove,53.873208,-1.159097,"Stephen Peters, Ian White, Dave Bygrave",,,,,
            26,Shoulder of Mutton,sporting.lavished.relegate,53.875553,-1.156937,"Chris Murphy, Don Formhals",,,,,
            27,Bends over bridge,reclined.quintet.cocktail,53.874933,-1.154706,Kelly Temple +2,,,,,
            28,Broad Lane corner,toothpick.prompting.briefer,53.877601,-1.148329,Mike & Janet Leake,,,,,
            29,Woolas Hall Farm,adjuster.hopping.tech,53.884824,-1.142834,Phil Reid,,,,,
            30,90 degree bend before Whinney Hills,coasting.talker.rejoins,53.894203,-1.126122,Rob Ward & Chien-i Chang,,,,,
        """;

        using MemoryStream ms = new(System.Text.Encoding.UTF8.GetBytes(csv));

        CsvParserService.CsvParseResult result = CsvParserService.ParseLocationsCsv(ms);

        result.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(0);
        result.Locations.Count.ShouldBe(32);

        // 23,Acaster Selby T-junction,look.forecast.dockers,53.867521,-1.130008,"Andy Clark, Richard & Emily Walker",,,,,
        result.Locations[24].Name.ShouldBe("23");
        result.Locations[24].Description.ShouldBe("Acaster Selby T-junction");
        result.Locations[24].What3Words.ShouldBe("look.forecast.dockers");
        result.Locations[24].Longitude.ShouldBe(-1.130008);
        result.Locations[24].Latitude.ShouldBe(53.867521);
        result.Locations[24].MarshalNames.ShouldBe(["Andy Clark", "Richard Walker", "Emily Walker"]);

        // 5,A64 flyover, stop.exists.error,53.928512,-1.093201,Graham Myers & Andrew Hill,,,,,
        result.Locations[6].MarshalNames.ShouldBe(["Graham Myers", "Andrew Hill"]);

        // 17,Foss Field Lane,gilding.basis.betrayal,53.902531,-1.113897,"Simon Fricke, + 2",,,,,
        result.Locations[18].MarshalNames.ShouldBe(["Simon Fricke", "Simon Fricke's (+1)", "Simon Fricke's (+2)"]);

        // 27,Bends over bridge,reclined.quintet.cocktail,53.874933,-1.154706,Kelly Temple +2,,,,,
        result.Locations[28].MarshalNames.ShouldBe(["Kelly Temple", "Kelly Temple's (+1)", "Kelly Temple's (+2)"]);
    }

}
