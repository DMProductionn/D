using System;

namespace ShoeStoreApp.Models
{
    internal class UserSession
    {
        public static string FullName { get; set; }
        public static string Role { get; set; }
        public static bool IsAuthenticated { get; set; } = false;

        public static bool IsAdmin
        {
            get
            {
                return HasRole("Admin", "Administrator", "Администратор");
            }
        }

        public static bool CanManageCatalogView
        {
            get
            {
                return IsAdmin || HasRole("Manager", "Менеджер");
            }
        }

        public static void Clear()
        {
            FullName = null;
            Role = null;
            IsAuthenticated = false;
        }

        private static bool HasRole(params string[] allowedRoles)
        {
            if (string.IsNullOrWhiteSpace(Role))
                return false;

            foreach (string allowedRole in allowedRoles)
            {
                if (string.Equals(Role.Trim(), allowedRole, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
