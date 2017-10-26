using CommandLine;
using System;
using Microsoft.Extensions.Logging;

namespace RedisReplicationTester
{
    class Program
    {
        static int Main(string[] args)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Trace);

            var result = Parser.Default.ParseArguments<TestReplication>(args)
                .MapResult(
                    o => o.Run(loggerFactory.CreateLogger<TestReplication>()),
                    errs => 1);

            Console.ReadKey();
            return result;
        }
    }
}