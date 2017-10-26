using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace RedisReplicationTester
{
    [Verb("test-replication", HelpText = @"Connects to a master and its slave to check replication state.")]
    internal class TestReplication
    {
        [Option('t', "targets-file", Required = true, HelpText = "The file containing the target redis servers.")]
        public string TargetsFile { get; set; }

        [Option('a', "auth", Required = true, HelpText = "The auth password for the redis servers.")]
        public string Auth { get; set; }

        private ILogger<TestReplication> _logger;

        public int Run(ILogger<TestReplication> logger)
        {
            _logger = logger;

            _logger.LogInformation("Testing Redis replication using hosts from file: {targetsFile}", TargetsFile);

            var targets = JsonConvert.DeserializeObject<Targets>(File.ReadAllText(TargetsFile));

            try
            {
                var repl = VerifyMaster(targets);
                VerifySlaves(targets.Slaves, repl);
                return 0;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Verification failed.");
                return -1;
            }
        }

        private (string replId, long replOffset) VerifyMaster(Targets targets)
        {
            _logger.LogInformation("Connecting to master: {masterNode}", targets.Master);

            using (var multi = ConnectionMultiplexer.Connect(HostOptions(targets.Master)))
            {
                var master = multi.GetServer(targets.Master);
                if (master.IsSlave)
                    throw new InvalidOperationException($"Node '{targets.Master}' is in fact slave!");

                var masterRepl = GetReplicationInfo(master);

                if (masterRepl.slaveCount != targets.Slaves.Count)
                {
                    _logger.LogWarning(
                        @"Targets file include {expectedSlaveCount} slaves but master reports {actualSlaveCount} connected.",
                        targets.Slaves.Count, masterRepl.slaveCount);
                }
                else
                {
                    _logger.LogInformation(@"Master {master} has {slavesConnected} slaves attached.",
                        targets.Master,
                        masterRepl.slaveCount);
                }

                return (masterRepl.replId, masterRepl.replOffset);
            }
        }

        private void VerifySlave(Host host, (string replId, long replOffset) masterRepl)
        {
            try
            {
                using (var multi = ConnectionMultiplexer.Connect(HostOptions(host)))
                {
                    var master = multi.GetServer(host);
                    if (!master.IsSlave)
                        throw new InvalidOperationException($"Node '{host}' is in fact a master!");

                    var slaveRepl = GetReplicationInfo(master);

                    if (slaveRepl.replId != masterRepl.replId || slaveRepl.replOffset != masterRepl.replOffset)
                    {
                        throw new InvalidOperationException($@"Slave node {host} is not up to date:
Master replication id: '{masterRepl.replId}'
Slave replication id:  '{slaveRepl.replId}'
Master offset: '{masterRepl.replOffset}'
Slave offset:  '{slaveRepl.replOffset}'");
                    }

                    _logger.LogInformation($"Slave {host} is up to date with master");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed verifying slave: '{host}'", e);
            }
        }

        private static (string replId, long replOffset, int slaveCount) GetReplicationInfo(IServer server)
        {
            var info = server.Info();
            var masterReplication = info.First(i => i.Key == "Replication").ToList();

            return (masterReplication.First(p => p.Key == "master_replid").Value,
                long.Parse(masterReplication.First(p => p.Key == "master_repl_offset").Value),
                int.Parse(masterReplication.First(p => p.Key == "connected_slaves").Value));
        }

        private void VerifySlaves(IReadOnlyCollection<Host> slaves, (string replId, long replOffset) repl)
        {
            _logger.LogInformation("Connecting to {nodeCount} slave Redis servers...", slaves.Count);

            var exs = new ConcurrentQueue<Exception>();

            Parallel.ForEach(slaves, host =>
            {
                try
                {
                    VerifySlave(host, repl);
                }
                catch (Exception e)
                {
                    exs.Enqueue(e);
                }
            });

            if (exs.Any())
                throw new AggregateException(exs);
        }

        private ConfigurationOptions HostOptions(Host host) => new ConfigurationOptions
        {
            EndPoints = {new DnsEndPoint(host.Hostname, host.Port)},
            Password = Auth,
            AllowAdmin = true
        };
    }
}