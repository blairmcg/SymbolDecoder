using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace SymbolDecoder.UnitTests
{
    [TestClass]
    public class LexerTests
    {
        [TestMethod]
        public void ClassifyTest()
        {
            for (char c = '0'; c <= '9'; c++)
            {
                Assert.AreEqual(CharacterClass.Digit, Lexer.Token.Classify(c));
            }
            for (char c = 'A'; c <= 'Z'; c++)
            {
                Assert.AreEqual(CharacterClass.UppercaseLetter, Lexer.Token.Classify(c));
            }
            for (char c = 'a'; c <= 'z'; c++)
            {
                Assert.AreEqual(CharacterClass.LowercaseLetter, Lexer.Token.Classify(c));
            }
            for (char c = (char)0x80; c <= 0xFE; c++)
            {
                Assert.AreEqual(CharacterClass.HighAnsi, Lexer.Token.Classify(c));
            }
            Assert.AreEqual(CharacterClass.GreaterThan, Lexer.Token.Classify('>'));
            Assert.AreEqual(CharacterClass.LessThan, Lexer.Token.Classify('<'));
            Assert.AreEqual(CharacterClass.Extend, Lexer.Token.Classify('_'));
            Assert.AreEqual(CharacterClass.Minus, Lexer.Token.Classify('-'));
            Assert.AreEqual(CharacterClass.Special, Lexer.Token.Classify('?'));
            Assert.AreEqual(CharacterClass.Template, Lexer.Token.Classify('$'));
            Assert.AreEqual(CharacterClass.Terminator, Lexer.Token.Classify('@'));
            Assert.AreEqual(CharacterClass.EOF, Lexer.Token.Classify(26));
            Assert.AreEqual(CharacterClass.Anon, Lexer.Token.Classify('%'));

            Assert.AreEqual(CharacterClass.Invalid, Lexer.Token.Classify(256));
            Assert.AreEqual(CharacterClass.Invalid, Lexer.Token.Classify(-1));
        }

        [TestMethod]
        public void MoveNextTest()
        {
            const string badSymbol = "a";
            var lexer = new Lexer(badSymbol);
            // Although the lexer constructor primes the pump by reading a char and so it is at the end of the input stream, it is not "AtEnd"
            // since the current char is that first char.
            Assert.IsFalse(lexer.AtEnd);
            lexer.MoveNext();
            // We have now advanced so that the current char is EOF (i.e. we are AtEnd)
            Assert.IsTrue(lexer.AtEnd);
            ExpectLexicalError(badSymbol, ParseErrors.PrematureEndOfSymbol, (char)Lexer.EOF, 2, () => lexer.MoveNext());
        }

        private static void ExpectLexicalError(string symbolName, string format, char erroneousChar, int errorPosition, Action op)
        {
            string errorMsg = string.Format(CultureInfo.CurrentCulture, format, erroneousChar);
            string exceptionMessage = string.Format(CultureInfo.CurrentCulture, ParseErrors.SymbolParseErrorFormat, symbolName, errorMsg, errorPosition);
            ExceptionAssert.Expect<InvalidSymbolNameException>(exceptionMessage, op);
        }

        [TestMethod]
        public void ConstructorsArgsTest()
        {
            ExceptionAssert.Expect<ArgumentNullException>(() => new Lexer(null));
            ExceptionAssert.Expect<ArgumentNullException>(() => new Lexer(""));

        }
    }
}
