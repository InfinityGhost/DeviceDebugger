using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Linq;
using System.Threading;
using HidSharp;

namespace DeviceDebuggerCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            var root = new RootCommand("Device Debugger CLI")
            {
                new Argument<string>("vendor"),
                new Argument<string>("product"),
                new Argument<uint>("reportlength")
            };

            root.Handler = CommandHandler.Create<string, string, uint>(MainHandler);
            root.Invoke(args);
        }

        static void MainHandler(string vendor, string product, uint reportlength)
        {
            var vendorId = vendor.Contains("0x") ? int.Parse(vendor.Replace("0x", ""), NumberStyles.HexNumber) : int.Parse(vendor);
            var productId = product.Contains("0x") ? int.Parse(product.Replace("0x", ""), NumberStyles.HexNumber) : int.Parse(product);

            var devices = from device in DeviceList.Local.GetHidDevices()
                where device.VendorID == vendorId
                where device.ProductID == productId
                where device.GetMaxInputReportLength() == reportlength
                select device;

            var selected = devices.FirstOrDefault();
            if (selected != null)
            {
                Console.WriteLine($"Found HID device: {selected.GetFriendlyName()}");
                bool cancel = false;
                var thread = new Thread(() => WriteStream(selected, ref cancel))
                {
                    Name = "Device Data Reader"
                };
                thread.Start();
                while (thread.IsAlive)
                {
                    var key = Console.ReadKey();
                    if (key != null)
                    {
                        cancel = true;
                        break;
                    }
                }
            }
        }

        static void WriteStream(HidDevice device, ref bool cancel)
        {
            using (var datastream = device.Open())
            {
                datastream.ReadTimeout = int.MaxValue;
                while (!cancel)
                {
                    var data = datastream.Read();
                    var str = BitConverter.ToString(data);
                    Console.WriteLine(str);
                }
            }
        }
    }
}
