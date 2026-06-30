using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string logPath = "test_log.txt";
        string logEntry = new string('a', 100) + "\n";
        int iterations = 10000;

        // Cleanup
        if (File.Exists(logPath)) File.Delete(logPath);

        // Test SemaphoreSlim
        var semaphore = new SemaphoreSlim(1, 1);
        var sw = Stopwatch.StartNew();
        var tasks = new Task[iterations];
        for (int i = 0; i < iterations; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await File.AppendAllTextAsync(logPath, logEntry);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }
        await Task.WhenAll(tasks);
        sw.Stop();
        Console.WriteLine($"SemaphoreSlim: {sw.ElapsedMilliseconds} ms");

        // Cleanup
        if (File.Exists(logPath)) File.Delete(logPath);

        // Test Channel
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100000) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        var backgroundTask = Task.Run(async () =>
        {
            await foreach (var entry in channel.Reader.ReadAllAsync())
            {
                File.AppendAllText(logPath, entry);
            }
        });

        sw.Restart();
        var channelTasks = new Task[iterations];
        for (int i = 0; i < iterations; i++)
        {
            channelTasks[i] = Task.Run(async () =>
            {
                await channel.Writer.WriteAsync(logEntry);
            });
        }
        await Task.WhenAll(channelTasks);
        channel.Writer.TryComplete();
        await backgroundTask;
        sw.Stop();
        Console.WriteLine($"Channel: {sw.ElapsedMilliseconds} ms");
    }
}
