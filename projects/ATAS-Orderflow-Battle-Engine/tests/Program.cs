using System;

namespace OrderflowBattleEngine.Tests;

public static class Program
{
    public static int Main()
    {
        try
        {
            NarrativeEngineSyntheticTests.RunAll();
            PrimitiveTests.RunAll();
            CausalEngineTests.RunAll();
            Console.WriteLine("All core tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
