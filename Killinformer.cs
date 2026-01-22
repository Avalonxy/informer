using System;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        foreach (var process in Process.GetProcessesByName("Informer"))
        {
            try
            {
                process.Kill();
            }
            catch { }
        }
    }
}