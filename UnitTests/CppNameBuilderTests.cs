using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SymbolDecoder.UnitTests
{
    /// <summary>
    /// Basic unit tests of the CppNameBuilder. Note that most coverage is provided by the parser tests when 
    /// verifying the undecoration as the class is then exercised extensively. Here we are mainly testing
    /// exceptional cases.
    /// </summary>
    [TestClass]
    public class CppNameBuilderTests
    {
        [TestMethod]
        public void OptionsTest()
        {
            var options1 = UndecorateOptions.NoUndnameEmulation;
            var builder = new CppNameBuilder(options1);
            Assert.AreEqual(options1, builder.Options);

            // Attempt to empty the options stack (invalid)
            ExceptionAssert.Expect<InvalidOperationException>(() => builder.PopOptions());

            // Push some new options, then pop
            var options2 = UndecorateOptions.NoMsftExtensions;
            builder.PushOptions(options2);
            Assert.AreEqual(options2, builder.Options);
            var options3 = UndecorateOptions.NameOnly | UndecorateOptions.NoUndnameEmulation;
            builder.PushOptions(options3);
            Assert.AreEqual(options3, builder.Options);
            var popped = builder.PopOptions();
            Assert.AreEqual(options3, popped);
            Assert.AreEqual(options2, builder.Options);
            popped = builder.PopOptions();
            Assert.AreEqual(options2, popped);
            Assert.AreEqual(options1, builder.Options);

            ExceptionAssert.Expect<InvalidOperationException>(() => builder.PopOptions());
        }

        [TestMethod]
        public void AppendNullArgTest()
        {
            var builder = new CppNameBuilder(UndecorateOptions.NoUndnameEmulation);
            string s = null;
            ExceptionAssert.Expect<ArgumentNullException>(() => builder.Append(s));
            ExceptionAssert.Expect<ArgumentNullException>(() => builder.Append(s, CppNameBuilder.Spacing.Leading));
            ExceptionAssert.Expect<ArgumentNullException>(() => CppNameBuilder.AppendSpecialNameOn("x", null));
        }
    }
}
