using System;
using System.Security.Cryptography;
using System.Text;

namespace NxDrawingPdfExporter.Core.Drawing
{
    // Produces privacy-safe, non-reversible sheet identity tokens. The salt
    // never leaves the gitignored run artifacts, so committed evidence can
    // carry tokens without exposing private sheet names.
    public static class SheetNameTokenizer
    {
        private const int MinimumSaltBytes = 32;
        private const int TokenHexChars = 16;

        public static byte[] NewSalt()
        {
            var salt = new byte[MinimumSaltBytes];
            using var generator = RandomNumberGenerator.Create();
            generator.GetBytes(salt);
            return salt;
        }

        public static string Tokenize(string sheetName, byte[] salt)
        {
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                throw new ArgumentException("图纸页名称不能为空。", nameof(sheetName));
            }

            if (salt == null || salt.Length < MinimumSaltBytes)
            {
                throw new ArgumentException("令牌盐值必须至少 32 字节。", nameof(salt));
            }

            using var hmac = new HMACSHA256(salt);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sheetName));
            var builder = new StringBuilder(TokenHexChars);
            for (var index = 0; index < TokenHexChars / 2; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
