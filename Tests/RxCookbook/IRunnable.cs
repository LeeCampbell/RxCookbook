using System.Collections.Generic;

namespace RxCookbook
{
    interface IRunnable
    {
        IEnumerable<ThroughputTestResult> Run();
    }
}