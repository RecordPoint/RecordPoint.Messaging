using System;
using System.Diagnostics;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    class Timer : IDisposable
    {
        private Stopwatch _sw;
        private string _name;

        public Timer(string name)
        {
            _name = name;
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _sw.Stop();
            Console.WriteLine($"{_name}: {_sw.Elapsed.TotalSeconds} seconds");
        }
    }
}
