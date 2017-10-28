using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RedisReplicationTester
{
    [Verb("offset", HelpText = @"Connects to a master and its slave to check replication offset.")]
    internal class ReplicationOffsetTester : Command
    {
        protected override void Test(Targets targets)
        {
            var repl = VerifyMaster(targets);
            VerifySlaves(targets.Slaves, repl);
        }

        private (string replId, long replOffset) VerifyMaster(Targets targets)
        {
            Logger.LogInformation("Connecting to master: {masterNode}", targets.Master);

            using (var multi = ConnectionMultiplexer.Connect(targets.Master.ToRedisOptions(Auth)))
            {
                var master = multi.GetServer(targets.Master);
                if (master.IsSlave)
                    throw new InvalidOperationException($"Node '{targets.Master}' is in fact slave!");

                var masterRepl = GetReplicationInfo(master);

                if (masterRepl.slaveCount != targets.Slaves.Count)
                {
                    Logger.LogWarning(
                        @"Targets file include {expectedSlaveCount} slaves but master reports {actualSlaveCount} connected.",
                        targets.Slaves.Count, masterRepl.slaveCount);
                }
                else
                {
                    Logger.LogInformation(@"Master {master} has {slavesConnected} slaves attached.",
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
                using (var multi = ConnectionMultiplexer.Connect(host.ToRedisOptions(Auth)))
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

                    Logger.LogInformation($"Slave {host} is up to date with master");
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
            Logger.LogInformation("Connecting to {nodeCount} slave Redis servers...", slaves.Count);

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
    }
}