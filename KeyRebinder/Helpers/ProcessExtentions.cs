using System;
using System.Diagnostics;
using System.Linq;

namespace KeyRebinder.Helpers
{
    public static class ProcessExtentions
    {
        private static readonly string[] InvalidApplicationNames =
        {
            @"Title",
        };

        public static string GetApplicationName(this Process process)
        {
            string applicationName = process?.MainWindowTitle.Split('-').Last().Trim();

            if (IsValidApplicationName(applicationName))
            {
                return applicationName;
            }

            applicationName = process?.MainWindowTitle.Trim();

            return IsValidApplicationName(applicationName) ? applicationName : process?.ProcessName.Trim();
        }

        private static bool IsValidApplicationName(string applicationName)
        {
            return !string.IsNullOrWhiteSpace(applicationName) &&
              !InvalidApplicationNames.Contains(applicationName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
