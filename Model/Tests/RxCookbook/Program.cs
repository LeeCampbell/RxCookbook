using System;

namespace RxCookbook
{
    class Program
    {
        static void Main(string[] args)
        {
            INPC.InpcPerfTests.Run();
            Console.ReadLine();
        }

        public static void Clean()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }
    }    
}
