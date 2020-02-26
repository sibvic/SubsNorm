using CommandLine;

namespace SubsNorm
{
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<NormilizeOptions>(args)
                .MapResult(
                    (NormilizeOptions opts) => new NormilizeWorker(opts).StartAsync().Result,
                    errs => 1);
        }
    }
}
