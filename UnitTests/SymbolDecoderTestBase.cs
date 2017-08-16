using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SymbolDecoder.UnitTests
{
    public abstract class SymbolDecoderTestBase
    {
        protected abstract String Undecorate(String decoratedName, UndecorateOptions options = UndecorateOptions.None);

        protected Symbol VerifySymbolUndecoration(string symbolName, UndecorateOptions defaultOptions = UndecorateOptions.None, ParserOptions parsingOptions = ParserOptions.None)
        {
            Symbol symbol = Parser.Parse(symbolName, parsingOptions);
            Assert.AreEqual(symbolName, symbol.SymbolName);
            VerifyUndecoration(symbol, UndecorateOptions.None | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoLeadingUnderscores | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoMsftExtensions | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoReturnType | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoAllocationModel | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoCallingConvention | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoMemberStorageClass | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoMemberAccess | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoThrowSignatures | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoMemberType | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoReturnUdtModel | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.Decode32 | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NameOnly | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.TypeOnly | defaultOptions);
            //VerifyUndecoration(symbol, UndecorateOptions.NoSpecialNames | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoCompoundTypeClass | defaultOptions);
            VerifyUndecoration(symbol, UndecorateOptions.NoPtr64 | defaultOptions);

            // Check copy is same
            Symbol dup = Symbol.Copy(symbol);
            Assert.AreEqual(symbol.ToString(), dup.ToString());
            Assert.AreEqual(symbolName, dup.SymbolName);

            return symbol;
        }

        protected void VerifyUndecoration(Symbol symbol, UndecorateOptions options)
        {
            string expected = "?";
            try
            {
                expected = Undecorate(symbol.SymbolName, options);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Debug.WriteLine("UndecorateSymbolName failed for \"{0}\"", (object)symbol.SymbolName);
            }
            // UndecorateSymbolName also doesn't handle the NoPtr64 flag correctly
            if (options.HasFlag(UndecorateOptions.NoPtr64))
            {
                expected = expected.Replace(" __ptr64", "").Replace(" ptr64", "");
            }

            VerifyUndecoration(symbol, options, expected);
        }

        protected static void VerifyUndecoration(Symbol symbol, UndecorateOptions options, string expected)
        {
            string actual = symbol.ToString(options);
            ValidateWhitespace(symbol, options, actual);

            // The UndecorateSymbolName API fails in a number of cases, especially when requesting anything
            // other than complete output. If it does, then the expected will still be mangled, and we just
            // check that the actual output (from our parsing/undecoration) is not mangled

            if (expected[0] == '?' || expected.Contains("??"))
            {
                // UndecorateSymbolName messed it up
                Assert.IsFalse(string.IsNullOrWhiteSpace(actual), "Empty undecorated name can't be right");
                Assert.AreNotEqual('?', actual[0], "Undecorated name cannot start with '?'");
            }
            else
            {
                // Ignoring spaces as the UndecorateSymbolName function has lots of bugs in the spacing it emits
                string normalizedExpected = expected.Replace(" ", ""),
                    normalizedActual = actual.Replace(" ", "");
                Assert.AreEqual(normalizedExpected, normalizedActual, "Undecoration incorrect with options {0}", options, expected, actual);
            }
        }

        /// <summary>
        /// Verifies that the whitespacing in an undecorated symbol matches certain rules
        /// </summary>
        /// <param name="actual"></param>
        /// <param name="message"></param>
        private static void ValidateWhitespace(Symbol symbol, UndecorateOptions options, string actual)
        {
            // Keep this simple by doing a series of inefficient checks, rather than trying to do in a single pass

            foreach (string nospaceAfter in new string[] { " ", "::", "<", "(" })
            {
                int unexpectedSpace = actual.IndexOf(nospaceAfter + " ");
                Assert.IsTrue(unexpectedSpace < 0, "Unexpected space found at {0} in {1} (options = {2})", unexpectedSpace, actual, options);
            }

            string withoutRightShiftOp = actual.Replace("operator>>", "RighShiftOperator");
            // a<b<c>> is invalid, should be "a<b<c> >"
            Assert.IsTrue(withoutRightShiftOp.IndexOf(">>") == -1, "Template close brackets must be separated by a space (with options = {0})", options);

            // There should always be a space (or a certain fixed set of other characters) after some chars
            foreach (KeyValuePair<char, string> pair in new Dictionary<char, string>() {
                    { '&', "),>" },
                    { '*', "(),>" },
                    { ')', "(),>'" },
                })
            {
                char ch = pair.Key;
                string allowedFollowers = pair.Value;

                foreach (int i in IndicesOf(actual, ch))
                {
                    if (i < actual.Length - 1)
                    {
                        char after = actual[i + 1];
                        Assert.IsTrue(after == ' ' || allowedFollowers.Contains(after), "Unexpected character '{0}' after '{1}' at {2} in {3} (options = {4})", after, ch, i, actual, options);
                    }
                }
            }

        }

        static IEnumerable<int> IndicesOf(string target, char ch)
        {
            int index = target.IndexOf(ch);
            while (index >= 0)
            {
                yield return index;
                index = target.IndexOf(ch, index + 1);
            }
        }

    }
}
