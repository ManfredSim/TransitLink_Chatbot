using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TransitLinkChatbot
{
    public class Topup
    {
        public string NRIC { get; set; }
        public string Value { get; set; }
        public string CardType { get; set; }
        public string CardNo { get; set; }
        public string Confirmation { get; set; }
    }
}
