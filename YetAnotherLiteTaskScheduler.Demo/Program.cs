using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YetAnotherLiteTaskScheduler.Demo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            var scheduler = new ScheduleManager(new RunnerConfiguration(maxRunnerInstances: 3, tasksDividerPerRunner: 2), logger);

            var r = new Random();
            for (int i = 0; i < 10; i++)
            {
                var time = r.Next(10, 30);
                var name = $"Test {i.ToString()}";
                var task = new ScheduledTask(name, () =>
                {
                    Console.WriteLine($">>> {name} >> Running each {time}sec");
                }, TimeSpan.FromSeconds(time).TotalMilliseconds, true);

                scheduler.TryScheduleTask(task);
            }

            while (true)
            {
                Console.WriteLine($"{DateTime.UtcNow} | Sleeping");
                Thread.Sleep(1000);
            }
        }
    }
}
