using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMUDAreaGenerator
{
    public class CommandLineOptions
    {
        public String MAP { get; set; }
        public String KEY { get; set; }
        public String AREANAME { get; set; }
        public String OUTDIRECTORY { get; set; }
        public String BASEPATH { get; set; }
        public bool HELP { get; set; }
    }

    class Program
    {
        public static Dictionary<int,String> RoomFileNames = new Dictionary<int,string>();
        public static int MapWidth = 0;
        public static int MapHeight = 0;
        public static List<String> MapData = new List<string>();
        public static Dictionary<int, UniqueLocation> Key = new Dictionary<int,UniqueLocation>();

        public static int NormalizeCoordinate(int X, int Y) { return (Y * MapWidth) + X; }

        static string LocationFileName(int X, int Y)
        {
            var normalizedCoordinate = NormalizeCoordinate(X,Y);
            if (RoomFileNames.ContainsKey(normalizedCoordinate)) return RoomFileNames[normalizedCoordinate];
            else FailWith(String.Format("There is no room at {0}, {1}", X, Y));
            return "";
        }

        public class UniqueLocation
        {
            public String Name;
            public String FileName;
            public int Instances = 0;
            public int TotalInstances = 0;
        }

        private static CommandLineOptions Options = new CommandLineOptions();

        public class ErrorException : Exception
        {
            public ErrorException(String Message) : base(Message) { }
        }

        static void FailWith(String Message) { throw new ErrorException(Message); }

        private static String EscapeFileName(String Name)
        {
            var r = new StringBuilder();
            var valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
            
            if ("0123456789".Contains(Name[0])) r.Append("_");

            foreach (var c in Name)
            {
                if (valid.Contains(c)) r.Append(c);
                else r.Append("_");
            }

            return r.ToString();
        }

        static void Main(string[] args)
        {
            try
            {
                var commandLineError = RMUD.CommandLine.ParseCommandLine(Options);
                if (commandLineError != RMUD.CommandLine.Error.Success) FailWith("Error in command line: " + commandLineError);

                if (Options.HELP || args.Length == 0)
                {
                    Console.Write(
@"RMUD Area Generator - Generate the framework of an area from a map and key file.

 Required arguments:
  MAP: The map file
  KEY: The key file
  AREANAME: The name of the area
  OUTDIRECTORY: Where to put the new files. This should be the relative path into
    the mud database.
 
 Optional arguments:
  BASEPATH: Path OUTDIRECTORY is relative to. Determines where files will be written, 
    but is NOT included in the relative filenames used to create links between rooms.
");
                    return;
                }

                if (String.IsNullOrEmpty(Options.MAP)) FailWith("No map file specified.");
                if (String.IsNullOrEmpty(Options.KEY)) FailWith("No key file specified.");
                if (String.IsNullOrEmpty(Options.AREANAME)) FailWith("No area name specified.");
                if (String.IsNullOrEmpty(Options.OUTDIRECTORY)) FailWith("No output directory specified.");


                Console.WriteLine("Reading map file..");
                var mapFile = System.IO.File.OpenText(Options.MAP);
                var widthInitialized = false;
                while (!mapFile.EndOfStream)
                {
                    var line = mapFile.ReadLine();
                    MapData.Add(line);
                    if (widthInitialized)
                    {
                        if (line.Length != MapWidth) FailWith("Map lines not of equal length");
                    }
                    else
                    {
                        MapWidth = line.Length;
                    }
                }
                MapHeight = MapData.Count;

                Console.WriteLine("Reading key file..");
                var keyFile = System.IO.File.OpenText(Options.KEY);
                while (!keyFile.EndOfStream)
                {
                    var line = keyFile.ReadLine();
                    if (line.Length < 3) FailWith("Malformed key entry");
                    if (line[1] != ' ') FailWith("Malformed key entry");
                    if (Key.ContainsKey(line[0])) FailWith("Duplicate key entry");
                    Key.Add(line[0], new UniqueLocation { Name = line.Substring(2), FileName = EscapeFileName(line.Substring(2)) });
                }

                Console.WriteLine("Preparing room filenames..");

                //Count instances.
                for (int y = 0; y < MapHeight; ++y)
                    for (int x = 0; x < MapWidth; ++x)
                        if (Key.ContainsKey(MapData[y][x]))
                            Key[MapData[y][x]].Instances += 1;

                //Mark true uniques and reset instance count.
                foreach (var loc in Key)
                {
                    loc.Value.TotalInstances = loc.Value.Instances;
                    loc.Value.Instances = 0;
                }

                //Finally, create filenames.
                for (int y = 0; y < MapHeight; ++y)
                    for (int x = 0; x < MapWidth; ++x)
                        if (Key.ContainsKey(MapData[y][x]))
                        {
                            var location = Key[MapData[y][x]];
                            var filename = location.FileName;
                            if (location.TotalInstances > 1)
                            {
                                filename += "_";
                                if (location.TotalInstances >= 100)
                                    filename += String.Format("{0:000}", location.Instances);
                                else if (location.TotalInstances >= 10)
                                    filename += String.Format("{0:00}", location.Instances);
                                else
                                    filename += location.Instances;
                            }
                            location.Instances += 1;
                            RoomFileNames.Add(NormalizeCoordinate(x, y), filename);
                        }

                Console.WriteLine("Preparing output directory..");
                var fullOutputDirectory = Options.BASEPATH + Options.OUTDIRECTORY;
                System.IO.Directory.CreateDirectory(fullOutputDirectory);


                Console.WriteLine("Generating rooms..");
                int roomsGenerated = 0;

                for (int y = 0; y < MapHeight; ++y)
                    for (int x = 0; x < MapWidth; ++x)
                        if (Key.ContainsKey(MapData[y][x]))
                        {
                            var key = Key[MapData[y][x]];
                            var className = LocationFileName(x, y);
                            var builder = new StringBuilder();

                            builder.Append("//Generated by rmud area generator\n");
                            builder.AppendFormat(
    @"public class {0} : RMUD.Room {{
    public override void Initialize() {{
        Short = ""{1} - {2}"";

", className, Options.AREANAME, key.Name);

                            DoExit(builder, x, y - 1, x, y - 2, "NORTH", "|");
                            DoExit(builder, x + 1, y - 1, x + 2, y - 2, "NORTHEAST", "/");
                            DoExit(builder, x + 1, y, x + 2, y, "EAST", "-");
                            DoExit(builder, x + 1, y + 1, x + 2, y + 2, "SOUTHEAST", "\\");
                            DoExit(builder, x, y + 1, x, y + 2, "SOUTH", "|");
                            DoExit(builder, x - 1, y + 1, x - 2, y + 2, "SOUTHWEST", "/");
                            DoExit(builder, x - 1, y, x - 2, y, "WEST", "-");
                            DoExit(builder, x - 1, y - 1, x - 2, y - 2, "NORTHWEST", "\\");

                            builder.Append("    }\n}\n");

                            var outStream = new System.IO.StreamWriter(fullOutputDirectory + "/" + className + ".cs");
                            outStream.Write(builder.ToString());
                            outStream.Close();

                            ++roomsGenerated;
                        }

                Console.WriteLine("Generated {0} rooms.", roomsGenerated);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        private static void DoExit(StringBuilder builder, int x1, int y1, int x2, int y2, string dir, string codes)
        {
            if (codes.Contains(FetchCell(x1, y1)))
                AppendLink(builder, x2, y2, dir);
        }

        private static void AppendLink(StringBuilder builder, int x, int y, string dir)
        {
            builder.AppendFormat(@"      OpenLink(RMUD.Direction.{0}, ""{1}/{2}"");
", dir, Options.OUTDIRECTORY, LocationFileName(x, y));
        }

        private static char FetchCell(int x, int y)
        {
            if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight)
                return MapData[y][x];
            return '@';
        }
    }
}
