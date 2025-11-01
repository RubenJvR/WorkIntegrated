using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADIX
{
    class UserSession
    {
        public static string CurrentUsername { get; set; }
        public static string CurrentRole { get; set; }
        public static bool IsAdmin => CurrentRole?.ToLower() == "admin";

        public static void Clear()
        {
            CurrentUsername = null;
            CurrentRole = null;
        }
    }
}
