using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace RedisReplicaTester
{
    [Verb("test-replica-set", HelpText = @"Connects to a master and its slave and checks replication.")]
    internal class TestReplication
    {
        [Option('h', "hosts-file", Required = true, HelpText = "The file containing the hosts to read")]
        public string HostsFile { get; set; }

        [Option('a', "auth", Required = true, HelpText = "The auth password for the redis servers")]
        public string Auth { get; set; }

        private ILogger<TestReplicaSet> _logger;

        public int Run(ILogger<TestReplicaSet> logger)
        {
            _logger = logger;

            _logger.LogInformation("Testing hosts from file: {hostsFile}", HostsFile);

            try
            {
                var hosts = JsonConvert.DeserializeObject<Hosts>(File.ReadAllText(HostsFile));

                var repl = VerifyMaster(hosts);
                VerifySlaves(hosts.Slaves, repl);
                return 0;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Verification failed.");
                return -1;
            }
        }

        private void VerifySlaves(IReadOnlyList<Host> slaves, (string replId, long replOffset) repl)
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

        private void VerifySlave(Host host, (string replId, long replOffset) repl)
        {
            try
            {
                using (var multi = ConnectionMultiplexer.Connect(HostOptions(host)))
                {
                    var master = multi.GetServer(host);
                    if (!master.IsSlave)
                        throw new InvalidOperationException($"Node '{host}' is in fact a master!");

                    var masterInfo = master.Info();
                    var masterReplication = masterInfo.First(i => i.Key == "Replication").ToList();

                    //var connectedSlaves = int.Parse(masterReplication.First(p => p.Key == "connected_slaves").Value);

                    var slaveReplId = masterReplication.First(p => p.Key == "master_replid").Value;
                    var slaveReplOffset = long.Parse(masterReplication.First(p => p.Key == "master_repl_offset").Value);

                    if (slaveReplId != repl.replId || slaveReplOffset != repl.replOffset)
                    {
                        throw new InvalidOperationException($@"Slave node {host} is not up to date:
Master replication id: '{repl.replId}'
Slave replication id:  '{slaveReplId}'
Master offset: '{repl.replOffset}'
Slave offset:  '{slaveReplOffset}'");
                    }

                    _logger.LogInformation($"Slave {host} is up to date with master");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed verifying slave: '{host}'", e);
            }
        }

        private (string replId, long replOffset) VerifyMaster(Hosts hosts)
        {
            _logger.LogInformation("Connecting to master: {masterNode}", hosts.Master);

            using (var multi = ConnectionMultiplexer.Connect(HostOptions(hosts.Master)))
            {
                var master = multi.GetServer(hosts.Master);
                if (master.IsSlave)
                    throw new InvalidOperationException($"Node '{hosts.Master}' is in fact slave!");

                var masterInfo = master.Info();
                var masterReplication = masterInfo.First(i => i.Key == "Replication").ToList();

                var connectedSlaves = int.Parse(masterReplication.First(p => p.Key == "connected_slaves").Value);
//                if (connectedSlaves != hosts.Slaves.Count)
//                {
//                    _logger.LogError("Expected #{expectedSlaveCount} slaves but master reports #{actualSlaveCount}",
//                        hosts.Slaves.Count, connectedSlaves);
//                }

                _logger.LogInformation(@"Master node {master} has #{slavesConnected} slaves connected.",
                    hosts.Master,
                    connectedSlaves);

                return (masterReplication.First(p => p.Key == "master_replid").Value,
                    long.Parse(masterReplication.First(p => p.Key == "master_repl_offset").Value));
            }
        }

        private ConfigurationOptions HostOptions(Host host) => new ConfigurationOptions
        {
            EndPoints = {new DnsEndPoint(host.Hostname, host.Port)},
            Password = Auth,
            AllowAdmin = true
        };
    }

    class Hosts
    {
        public Host Master { get; set; }
        public IReadOnlyList<Host> Slaves { get; set; }
    }

    class Host
    {
        public string Hostname { get; set; }
        public int Port { get; set; }

        public static implicit operator DnsEndPoint(Host host)
        {
            return new DnsEndPoint(host.Hostname, host.Port);
        }

        public override string ToString()
        {
            return $"{nameof(Hostname)}: {Hostname}, {nameof(Port)}: {Port}";
        }
    }
}