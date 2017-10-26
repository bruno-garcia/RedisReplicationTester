# Redis replication tester

Runs tests on a redis replication with 1 master and N slaves.

Connects to all nodes and ensures all are at the same replication id/offset.
Assumes all nodes use the same AUTH password.

### Example call:
```{r, engine='bash', sample}
$ dotnet run -- -t targets.json -a foobared

info: RedisReplicationTester.TestReplication[0]
      Testing Redis replication using hosts from file: targets.json
info: RedisReplicationTester.TestReplication[0]
      Connecting to master: Hostname: localhost, Port: 6379
info: RedisReplicationTester.TestReplication[0]
      Master Hostname: localhost, Port: 6379 has 2 slaves attached.
info: RedisReplicationTester.TestReplication[0]
      Connecting to 2 slave Redis servers...
info: RedisReplicationTester.TestReplication[0]
      Slave Hostname: localhost, Port: 6381 is up to date with master
info: RedisReplicationTester.TestReplication[0]
      Slave Hostname: localhost, Port: 6380 is up to date with master
```
