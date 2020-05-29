using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TransitLinkChatbot
{
    // Defines a state property used to track information about the user.
    public class UserProfile
    {
        public string Name { get; set; }
        public string IsAsking { get; set; }
        public string Feedback { get; set; }
    }
}
