using System;
using System.Collections.Generic;

namespace RedisReplicationTester
{
    internal class Targets
    {
        public Host Master { get; }
        public IReadOnlyList<Host> Slaves { get; }

        public Targets(Host master, IReadOnlyList<Host> slaves)
        {
            Master = master ?? throw new ArgumentNullException(nameof(master));
            Slaves = slaves ?? throw new ArgumentNullException(nameof(slaves));
            if (slaves.Count == 0) throw new ArgumentException("At least a single slave node expected.", nameof(slaves));
        }
    }
}