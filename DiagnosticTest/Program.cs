using System;
using HalconDotNet;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== HALCON Camera Diagnostic ===");
        Console.WriteLine();

        try
        {
            Console.WriteLine("--- InfoFramegrabber (MediaFoundation) ---");
            HOperatorSet.InfoFramegrabber("MediaFoundation", "default", out HTuple information, out HTuple valueList);
            Console.WriteLine($"  Information: {information.Length} items");
            for (int i = 0; i < information.Length; i++)
                Console.WriteLine($"    [{i}]: '{information[i].ToString()}'");
            Console.WriteLine($"  ValueList: {valueList.Length} items");
            for (int i = 0; i < valueList.Length; i++)
                Console.WriteLine($"    [{i}]: '{valueList[i].ToString()}'");
        }
        catch (HOperatorException ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();

        string[] ifaces = { "MediaFoundation", "USB3Vision", "DirectShow" };
        foreach (var iface in ifaces)
        {
            Console.WriteLine($"--- Trying {iface} ---");
            for (int dev = 0; dev < 3; dev++)
            {
                try
                {
                    Console.Write($"  Device {dev}: ");
                    using var h = new HFramegrabber(
                        iface, 1, dev, 0, 0, 0, 0, "progressive", -1, "default", -1, "false", "default",
                        "default", 0, -1);
                    Console.WriteLine("SUCCESS!");
                    h.Dispose();
                }
                catch (HOperatorException ex)
                {
                    Console.WriteLine($"FAIL: {ex.Message}");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine("Done. Press any key to exit.");
        Console.ReadKey();
    }
}