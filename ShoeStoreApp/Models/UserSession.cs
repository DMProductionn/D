using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShoeStoreApp.Models
{
    internal class UserSession
    {
        public static  string FullName { get; set; }
        public static string Role { get; set; }
        public static bool IsAuthenticated { get; set; } = false;

        public static void Clear()
        {
            FullName = null; 
            Role = null;
            IsAuthenticated = false;
        }
    }
}
