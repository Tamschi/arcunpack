/*
 *  Copyright 2012 Tamme Schichler <tammeschichler@googlemail.com>
 * 
 *  This file is part of ArcUnpack.
 *
 *  ArcUnpack is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  ArcUnpack is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with ArcUnpack.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TQ.ArcLib;

namespace ArcUnpack
{
    internal static class Program
    {
        private static readonly object WriteLock = new object();

        private static void Main(string[] args)
        {
            const string filter = ".arc";
            var files = new List<string>();
            files.AddRange(
                args.Where(Directory.Exists).SelectMany(x => Directory.GetFiles(x, "*", SearchOption.AllDirectories)).
                    Where(x => x.ToLowerInvariant().EndsWith(filter)));
            files.AddRange(args.Where(File.Exists).Where(x => x.ToLowerInvariant().EndsWith(filter)));

            bool verbose = args.Contains("-v");

            DateTime startTime = DateTime.Now;

            foreach (string arc in files)
            {
                Extract(arc, verbose);
                Console.WriteLine();
            }

            DateTime endtime = DateTime.Now;

            Console.WriteLine("Decompressed {0} in {1}.", files.Count,
                              TimeSpan.FromTicks(endtime.Ticks - startTime.Ticks));
            Console.ReadLine();
        }

        private static void Extract(string arc, bool verbose)
        {
            Console.WriteLine(arc);
            string mainPath = arc.Replace(Path.GetExtension(arc), "");

            var reader = new ArcReader(File.OpenRead(arc));

            AssetInfo[] fileInfos = reader.GetFileInfo().ToArray();
            if (fileInfos.Length > 0)
            {
                int largestSize = fileInfos.Select(x => x.RealSize).Max();

                int threads = 50000000 / largestSize;
                if (threads > 63)
                {
                    threads = 63;
                }

                if (largestSize < 50000000)
                {
                    fileInfos.AsParallel().AsUnordered().WithDegreeOfParallelism(threads).Select(
                        x => ExtractFile(mainPath, reader, x, verbose)).Count();
                }
                else
                {
                    Console.WriteLine("Sequential.");
                    foreach (AssetInfo x in fileInfos)
                    {
                        ExtractFile(mainPath, reader, x, verbose);
                    }
                }
            }
            else
            {
                Console.WriteLine("Empty.");
            }
        }

        private static bool ExtractFile(string mainPath, ArcReader reader, AssetInfo fileInfo, bool verbose)
        {
            string subPath = reader.GetString(fileInfo.NameOffset, fileInfo.NameLength).Replace("/", @"\");
            if (subPath.Length > 0)
            {
                string filePath = Path.Combine(mainPath, subPath);
                if (verbose)
                {
                    Console.WriteLine(" S: " + filePath);
                }

                byte[] fileData = reader.GetBytes(fileInfo);

                lock (WriteLock)
                {
                    if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    }

                    File.WriteAllBytes(filePath, fileData);
                }
                if (verbose)
                {
                    Console.WriteLine(" F: " + filePath);
                }
                return true;
            }

            Console.WriteLine(
                " Skipped FileInfo:\n" +
                "   Offset: {0}\n" +
                "   CSize: {1}" +
                "   Size: {2}\n" +
                "   StorageType: {3}",
                fileInfo.Offset,
                fileInfo.CompressedSize,
                fileInfo.RealSize,
                fileInfo.Storage);
            return false;
        }
    }
}