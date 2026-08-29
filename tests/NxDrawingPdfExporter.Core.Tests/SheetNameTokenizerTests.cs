using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Core.Drawing;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class SheetNameTokenizerTests
    {
        private static readonly byte[] Salt = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

        [TestMethod]
        public void Tokenize_SameNameAndSalt_IsDeterministic()
        {
            var first = SheetNameTokenizer.Tokenize("Sheet A-1", Salt);
            var second = SheetNameTokenizer.Tokenize("Sheet A-1", Salt);

            Assert.AreEqual(first, second);
            Assert.IsTrue(Regex.IsMatch(first, "^[0-9a-f]{16}$"));
        }

        [TestMethod]
        public void Tokenize_DifferentSalt_ChangesToken()
        {
            var otherSalt = Enumerable.Range(32, 32).Select(i => (byte)i).ToArray();

            Assert.AreNotEqual(
                SheetNameTokenizer.Tokenize("Sheet A-1", Salt),
                SheetNameTokenizer.Tokenize("Sheet A-1", otherSalt));
        }

        [TestMethod]
        public void Tokenize_DifferentName_ChangesToken()
        {
            Assert.AreNotEqual(
                SheetNameTokenizer.Tokenize("Sheet A-1", Salt),
                SheetNameTokenizer.Tokenize("Sheet B-2", Salt));
        }

        [TestMethod]
        public void Tokenize_NeverContainsPlainName()
        {
            var token = SheetNameTokenizer.Tokenize("SecretSheet", Salt);

            Assert.IsFalse(token.Contains("secret", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(token.Contains("Secret", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Tokenize_NullOrBlankName_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => SheetNameTokenizer.Tokenize(null!, Salt));
            Assert.ThrowsExactly<ArgumentException>(() => SheetNameTokenizer.Tokenize("  ", Salt));
        }

        [TestMethod]
        public void Tokenize_SaltTooShort_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => SheetNameTokenizer.Tokenize("Sheet A-1", new byte[8]));
            Assert.ThrowsExactly<ArgumentException>(() => SheetNameTokenizer.Tokenize("Sheet A-1", null!));
        }
    }
}
