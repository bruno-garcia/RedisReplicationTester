using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RedisReplicationTester
{
    [Verb("pubsub", HelpText = @"Publishes a message on master and verifies the slaves receive it in a timely fashion.")]
    internal class PubSubTester : Command
    {
        protected override void Test(Targets targets)
        {
            var source = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
            TestAsync(targets, source.Token).GetAwaiter().GetResult();
        }

        private async Task TestAsync(Targets targets, CancellationToken token)
        {
            var servers = targets.Slaves.Select(s => new {Host = s, IsMaster = false})
                .Prepend(new {Host = targets.Master, IsMaster = true})
                .ToList();

            var muxTasks = servers.Select(n => ConnectionMultiplexer.ConnectAsync(n.Host.ToRedisOptions(Auth)))
                .ToList();

            Logger.LogInformation("Connecting to {nodeCount} Redis servers...", servers.Count);

            await Task.WhenAll(muxTasks);

            var connections = muxTasks.Select((t, i) => new {Mux = t.Result, Server = servers[i]}).ToList();

            var slavesSubscribed = new CountdownEvent(servers.Count - 1);
            var startEvent = new ManualResetEventSlim(false);

            var message = Guid.NewGuid().ToString();
            var channel = new RedisChannel(message, RedisChannel.PatternMode.Literal);

            var jobs = connections.Where(c => !c.Server.IsMaster).Select(slave =>
                Task.Run(() =>
                {
                    var stopwatch = new Stopwatch();

                    try
                    {
                        var msgReceivedEvent = new ManualResetEventSlim(false);
                        slave.Mux.GetSubscriber().Subscribe(channel, (c, v) =>
                        {
                            Logger.LogInformation("{Latency} - Message received by {host}.", stopwatch.Elapsed, slave.Server.Host);
                            msgReceivedEvent.Set();
                        });

                        slavesSubscribed.Signal();
                        startEvent.Wait(token);
                        stopwatch.Start();
                        msgReceivedEvent.Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.LogError("{Latency} - Timed-out: {host}.", stopwatch.Elapsed, slave.Server.Host);
                    }
                    finally
                    {
                        slave.Mux.Dispose();
                    }
                }, token)).ToList();

            var master = connections.Single(c => c.Server.IsMaster);
            var db = master.Mux.GetDatabase();
            slavesSubscribed.Wait(token);

            Logger.LogInformation("Publishing message on master: {host}", master.Server.Host);
            startEvent.Set();
            db.Publish(channel, message);

            await Task.WhenAll(jobs);
        }
    }
}