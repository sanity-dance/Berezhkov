using System;
using System.Collections.Generic;
using System.Text;

namespace Berezhkov
{
    public static class StringExtensions
    {
        /// <summary>
        /// Executes string.IsNullOrEmpty() and string.IsNullOrWhiteSpace().
        /// </summary>
        /// <param name="str">String to check for emptiness.</param>
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str);
        }
    }
}
