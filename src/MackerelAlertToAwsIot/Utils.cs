using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MackerelAlertToAwsIot
{
    class Utils
    {
        private static SHA1 _sha1 = new SHA1CryptoServiceProvider();

        public static string ToHash(string value)
        {
            return BitConverter.ToString(_sha1.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
