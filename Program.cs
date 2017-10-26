using CommandLine;
using System;
using Microsoft.Extensions.Logging;

namespace CheckRedisDeployment
{
    class Program
    {
        static int Main(string[] args)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Trace);

            var result = Parser.Default.ParseArguments<TestReplicaSet>(args)
                .MapResult(
                    o => o.Run(loggerFactory.CreateLogger<TestReplicaSet>()),
                    errs => 1);

            Console.ReadKey();
            return result;
        }
    }
}