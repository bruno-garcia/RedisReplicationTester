using System;
using System.IO;
using CommandLine;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace RedisReplicationTester
{
    internal abstract class Command
    {
        [Option('f', "file", Required = true, HelpText = "The file containing the target redis servers.")]
        public string TargetsFile { get; set; }

        [Option('a', "auth", Required = true, HelpText = "The auth password for the redis servers.")]
        public string Auth { get; set; }

        [Option('t', "timeout", Required = false, HelpText = "The timeout in seconds.")]
        public int Timeout { get; set; } = 5;

        private readonly ILogger _logger;
        protected ILogger Logger => _logger;

        protected Command()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Trace);
            _logger = loggerFactory.CreateLogger(GetType());
        }

        public int Run()
        {
            Logger.LogInformation("Testing using hosts from file: {targetsFile}", TargetsFile);

            var targets = JsonConvert.DeserializeObject<Targets>(File.ReadAllText(TargetsFile));

            try
            {
                Test(targets);
                return 0;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Verification failed.");
                return -1;
            }
        }

        protected abstract void Test(Targets targets);
    }
}