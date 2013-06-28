using System;
using System.Security.Cryptography;

namespace LastPass
{
    static class FetcherHelper
    {
        public static byte[] MakeKey(string username, string password, int iterationCount)
        {
            // TODO: Check for invalid interationCount

            if (iterationCount == 1)
            {
                using (var sha = new SHA256Managed())
                {
                    return sha.ComputeHash((username + password).ToBytes());
                }
            }

            return Pbkdf2.Generate(password, username, iterationCount, 32);
        }

        public static string MakeHash(string username, string password, int iterationCount)
        {
            // TODO: Check for invalid interationCount

            var key = MakeKey(username, password, iterationCount);
            if (iterationCount == 1)
            {
                using (var sha = new SHA256Managed())
                {
                    return ToHexString(sha.ComputeHash((ToHexString(key) + password).ToBytes()));
                }
            }

            return ToHexString(Pbkdf2.Generate(key, password, 1, 32));
        }

        public static string ToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLower();
        }
    }
}
