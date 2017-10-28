using System;
using System.Net;
using StackExchange.Redis;

namespace RedisReplicationTester
{
    internal class Host
    {
        public string Hostname { get; }
        public int Port { get; }

        public Host(string hostname, int port)
        {
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));
            Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            Port = port;
        }

        public static implicit operator DnsEndPoint(Host host)
        {
            return new DnsEndPoint(host.Hostname, host.Port);
        }

        public ConfigurationOptions ToRedisOptions(string auth) => new ConfigurationOptions
        {
            EndPoints = {new DnsEndPoint(Hostname, Port)},
            Password = auth,
            AllowAdmin = true
        };

        public override string ToString()
        {
            return $"{nameof(Hostname)}: {Hostname}, {nameof(Port)}: {Port}";
        }
    }
}