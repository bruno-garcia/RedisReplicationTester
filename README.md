# Redis replication tester

Different tests on a redis replication with 1 master and N slaves.

## Redis replication offset

Connects to all nodes and ensures all are at the same replication id/offset.
Assumes all nodes use the same AUTH password.

### Example call:
```{r, engine='bash', sample}
$ dotnet run -- offset -f targets.json -a foobared

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

## Test Pub/Sub

Connects to multiple nodes, publishes a message on Master and verifies the latency to the slaves.
This is useful to test the latency of nodes in different data centers and countries.

### Example call:
```{r, engine='bash', sample}
$ dotnet run -- pubsub -f targets.json -a foobared -t 5

info: RedisReplicationTester.PubSubTester[0]
      Testing Redis pub/sub using hosts from file: targets.json
info: RedisReplicationTester.PubSubTester[0]
      Connecting to 3 Redis servers...
info: RedisReplicationTester.PubSubTester[0]
      Publishing message on master.
info: RedisReplicationTester.PubSubTester[0]
      00:00:00.0135370 - Message received by Hostname: localhost, Port: 6381.
info: RedisReplicationTester.PubSubTester[0]
      00:00:00.0128850 - Message received by Hostname: localhost, Port: 6380.


```