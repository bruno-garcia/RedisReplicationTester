using CommandLine;

namespace RedisReplicationTester
{
    class Program
    {
        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<ReplicationOffsetTester, PubSubTester>(args)
                .MapResult(
                   (ReplicationOffsetTester o) => o.Run(),
                   (PubSubTester o) => o.Run(),
                    errs => 1);

            return result;
        }
    }
}