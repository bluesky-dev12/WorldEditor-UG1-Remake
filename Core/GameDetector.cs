
using System;
using System.IO;

namespace Core
{
    public static class GameDetector
    {
        public enum Game
        {
            MostWanted,
            Underground2,
            Underground,
            UndergroundRemake,
            Unknown,
            World
        }

        /// <summary>
        /// Attempt to determine the NFS game installed in the given directory.
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static Game DetectGame(string directory)
        {
            // speed.exe can be UG1 or MW
            if (File.Exists(Path.Combine(directory, "speed.exe")))
            {
                var tracksPath = Path.Combine(directory, "TRACKS");
                if (!Directory.Exists(tracksPath))
                {
                    throw new ArgumentException("TRACKS folder does not exist! Cannot determine game.");
                }

                if (File.Exists(Path.Combine(tracksPath, "L2RA.BUN"))
                    && File.Exists(Path.Combine(tracksPath, "STREAML2RA.BUN")))
                {
                    return Game.MostWanted;
                }

                if (File.Exists(Path.Combine(tracksPath, "STREAML1RA.BUN")))
                {
                    return Game.Underground;
                }
            }

            if (File.Exists(Path.Combine(directory, "speed2.exe")))
            {
                return Game.Underground2;
            }

            if (File.Exists(Path.Combine(directory, "Underground.exe")))
            {
                return Game.UndergroundRemake;
            }

            return File.Exists(Path.Combine(directory, "nfsw.exe")) ? Game.World : Game.Unknown;
        }
    }
}