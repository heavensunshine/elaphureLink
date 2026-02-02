using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace elaphureLink.Wpf.ViewModel
{
    public sealed class DiscoveredDevice
    {
        public string Ip { get; set; } = "";
        public int Port { get; set; }
        public string  Name { get; set; }
        public string  Model { get; set; }
        public string  Firmware { get; set; }
        public string Raw { get; set; } = "";

        public string Display =>
            $"{(Name ?? "Device")}  {Ip}{(string.IsNullOrWhiteSpace(Firmware) ? "" : $"  v{Firmware}")}";
    }
}

