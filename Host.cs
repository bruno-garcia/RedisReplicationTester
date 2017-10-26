using System;
using System.Net;

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

        public override string ToString()
        {
            return $"{nameof(Hostname)}: {Hostname}, {nameof(Port)}: {Port}";
        }
    }
}