using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using ICSharpCode.SharpZipLib.BZip2;

namespace fastbzip
{
    class Program
    {
        static Queue<string> RemainingFiles = new Queue<string>();
        static int NumFiles = 0;

        public static void Run()
        {
            while(true)
                try
                {
                    string path = RemainingFiles.Dequeue();
                    FileInfo fileToBeZipped = new FileInfo(path);
                    FileInfo zipFileName = new FileInfo(path + ".bz2");

                    using (FileStream fileToBeZippedAsStream = fileToBeZipped.OpenRead())
                    {
                        using (FileStream zipTargetAsStream = zipFileName.Create())
                        {
                            try
                            {
                                BZip2.Compress(fileToBeZippedAsStream, zipTargetAsStream, true, 9);
                                File.Delete(fileToBeZipped.FullName);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }

                    Interlocked.Decrement(ref NumFiles);
                } catch (Exception)
                {
                    Thread.CurrentThread.Abort();
                }
        }

        static void PopulateFile(string path)
        {
            if (path.Substring(path.Length - 4) != ".bz2")
                RemainingFiles.Enqueue(path);
        }
        static void PopulateDir(DirectoryInfo info)
        {
            foreach (FileInfo file in info.GetFiles())
                PopulateFile(file.FullName);

            foreach (DirectoryInfo dir in info.GetDirectories())
                PopulateDir(dir);
        }

        static void Main(string[] filepaths)
        {
            foreach(string path in filepaths)
            {

                if (File.Exists(path))
                    PopulateFile(path);
                else if (Directory.Exists(path))
                    PopulateDir(new DirectoryInfo(path));
            }

            NumFiles = RemainingFiles.Count;
            Console.WriteLine("Found " + RemainingFiles.Count + " files to compress.");

            int numThreads = Math.Min(RemainingFiles.Count, Convert.ToInt32(Math.Ceiling((double)(Environment.ProcessorCount * 0.75))));
            List<Thread> threadPool = new List<Thread>();

            Console.WriteLine("Spawning " + numThreads + " threads to work.");
            for (int i = 0; i < numThreads; i++)
            {
                ThreadStart threadDelegate = new ThreadStart(Run);
                Thread thread = new Thread(threadDelegate);
                thread.Start();

                threadPool.Add(thread);
            }

            Console.WriteLine("Compressing..");
            var maxTicks = RemainingFiles.Count;
            using (var progress = new ProgressBar())
            {
                bool finished = false;
                while (!finished)
                {
                    progress.Report((double)(maxTicks - NumFiles) / maxTicks);
                    finished = true;
                    foreach (Thread thread in threadPool)
                    {
                        if (thread.IsAlive)
                        {
                            finished = false;
                            break;
                        }
                    }
                }
            }

            Console.WriteLine("Complete! Press enter to exit.");
            Console.ReadLine();
        }
    }
}
