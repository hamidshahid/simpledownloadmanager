using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDownloadManager
{
    class Program
    {
        private static object lockObject = new object();

        private static ConcurrentDictionary<int, string> downloads = new ConcurrentDictionary<int, string>();

        static void Main(string[] args)
        {
            char userInput = char.MinValue;
            var tasks = new List<Task>();
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            while (userInput != '3')
            {
                lock (lockObject)
                {
                    var colour = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("========================");
                    Console.WriteLine("1. Download File");
                    Console.WriteLine("2. View Active downloads");
                    Console.WriteLine("3. Exit");
                    Console.WriteLine("========================");
                    Console.WriteLine("..............................");
                    Console.WriteLine("Type X to stop all downloads");
                    Console.WriteLine("..............................");
                    Console.ForegroundColor = colour;
                }

                userInput = Console.ReadKey(true).KeyChar;
                
                if (userInput == '1')
                {
                    var task = DoDownload(token);
                    tasks.Add(task);
                }
                else if (userInput == '2')
                {
                    var activeTasks = tasks.Where(s => !s.IsCompleted && !s.IsCanceled);
                    lock (lockObject)
                    {
                        Console.WriteLine("========================================");
                        Console.WriteLine("Currently Downloading....");
                        Console.WriteLine("========================================");

                        foreach (var activeTask in activeTasks)
                        {
                            if (downloads.ContainsKey(activeTask.Id))
                            {
                                Console.WriteLine(downloads[activeTask.Id]);
                            }
                        }
                    }
                }
                else if (userInput == 'X' || userInput == 'x')
                {
                    tokenSource.Cancel();
                }
            }

            Console.WriteLine($"Waiting for all downloads to complete. Current Time {DateTime.Now.ToLongTimeString()}");
            Task.WaitAll(tasks.Where( s => !s.IsCanceled).ToArray());
            Console.WriteLine($"All downloads are completed. Current Time {DateTime.Now.ToLongTimeString()}");
        }

        private static Task DoDownload(CancellationToken token)
        {
            Console.WriteLine("");
            Console.Write("Type in the url of the file to download: ");
            var url = Console.ReadLine();
            var task = Task.Run (() => {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return;
                }

                if (Task.CurrentId != null)
                {
                    downloads.TryAdd(Task.CurrentId.Value, url);
                }

                WriteBackgroundConsoleMessage($"Downloading {url} in the background. Thread ID {Thread.CurrentThread.ManagedThreadId}");
                try
                {
                    var uri = new Uri(url);
                    var bytes = DownloadFile(uri, token).Result;
                    var filename = Path.GetFileName(uri.AbsolutePath);
                    var originalfileName = filename;
                    var index = 1;
                    Thread.Sleep(6000);
                    while (File.Exists($".\\{filename}"))
                    {
                        filename = $"{Path.GetFileNameWithoutExtension(originalfileName)}({index}){Path.GetExtension(originalfileName)}";
                        index = index + 1;
                    }

                    File.WriteAllBytes($".\\{filename}", bytes);
                    WriteBackgroundConsoleMessage($"Downloaded {Path.GetFullPath($".\\{filename}")}. Thread ID {Thread.CurrentThread.ManagedThreadId}");
                }
                catch(Exception ex)
                {
                    WriteBackgroundConsoleMessage($"Error in downloading file {ex.Message}");
                }
            });

            return task;
        }

        private static void WriteBackgroundConsoleMessage(string message)
        {
            lock (lockObject)
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("");
                Console.WriteLine(message);
                Console.ForegroundColor = color;
            }
        }

        private async static Task<byte[]> DownloadFile(Uri fileToDownload, CancellationToken token)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(fileToDownload, token);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    return bytes;
                }

                throw new Exception($"Non success http response code {response.StatusCode}");
            }
        }
    }
}