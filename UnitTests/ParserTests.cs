using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Interop = System.Runtime.InteropServices;

namespace SymbolDecoder.UnitTests
{
    /// <summary>
    /// Unit tests for the native binary model ArtefactName classes
    /// </summary>
    [TestClass]
    public class ParserTests : SymbolDecoderTestBase
    {
        #region Test boilerplate

        public TestContext TestContext { get; set; }

        #endregion

        #region Tests

        [TestMethod]
        public void ParseErrorsTest()
        {
            // TODO: All the other parse errors we can detect in malformed symbols

            // Symbols must always start with '?'
            ExpectParseError("x", ParseErrors.BadSymbolStart, 'x', 1);

            // Symbols must always start with '?'
            ExpectParseError("??@", ParseErrors.TruncatedSymbol, '@', 2);

            // Nested CV symbol should also start with ?
            ExpectParseError("?@i@@3H", ParseErrors.BadSymbolStart, 'i', 3);

            // '!' is not a valid character in a symbol anywhere
            ExpectParseError("?x!@", ParseErrors.InvalidCharacter, '!', 3);

            // '$' is not a valid character in an identifier anywhere
            ExpectParseError("?x$@", ParseErrors.InvalidIdentifierChar, '$', 3);

            // Unterminated identifier
            ExpectParseError("?x", ParseErrors.UnterminatedName, (char)Lexer.EOF, 3);

            // Unterminated qualifier list
            ExpectParseError("?x@y@", ParseErrors.UnterminatedQualifiedName, (char)Lexer.EOF, 6);

            // Invalid special symbol name code
            ExpectParseError("??a@", ParseErrors.InvalidSpecialNameCode, 'a', 3);
            ExpectParseError("??_a@", ParseErrors.InvalidSpecialNameCode, 'a', 4);
            ExpectParseError("??__a@", ParseErrors.InvalidSpecialNameCode, 'a', 5);

            // No symbol class code
            ExpectParseError("?x@y@@", ParseErrors.PrematureEndOfSymbol, (char)Lexer.EOF, 7);

            // No symbol class code
            ExpectParseError("?x@@x", ParseErrors.InvalidSymbolTypeCode, 'x', 5);

            // Invalid extended type code (only a single level of extensions is currently used, with a single '_' prefix
            ExpectParseError("?x@@3AA__A", ParseErrors.InvalidTypeEncoding, '_', 9);

            // Invalid character (not in lexicon)
            ExpectParseError("?x@@3~A", ParseErrors.InvalidCharacter, '~', 6);

            // Unused type codes
            ExpectParseError("?x@@3AAL", ParseErrors.UnusedTypeCode, 'L', 8);

            // Reference to reference
            ExpectParseError("?x@@3AAA", ParseErrors.DoubleReference, 'A', 8);

            // Unused extended type code
            ExpectParseError("?x@@2AA_A", ParseErrors.UnusedExtendedTypeCode, 'A', 9);

            // Invalid extended type codes
            ExpectParseError("?f@@YAHH$E@Z", ParseErrors.UnexpectedCharacter, 'E', 10);
            ExpectParseError("?f@@YAHH$$E@Z", ParseErrors.InvalidExtendedTypeCode, 'E', 11);
            ExpectParseError("?f@@YAHH$$P@Z", ParseErrors.InvalidExtendedTypeCode, 'P', 11);
            ExpectParseError("?f@@YAHH$$D@Z", ParseErrors.InvalidExtendedTypeCode, 'D', 11);
            ExpectParseError("?f@@YAHH$$S@Z", ParseErrors.InvalidExtendedTypeCode, 'S', 11);

            // Note that there are no unused operator codes

            // Invalid based pointer code
            ExpectParseError("?vbp@@3PM5HM5", ParseErrors.InvalidBasedPointerType, '5', 10);

            // Extra chars after symbol
            ExpectParseError("?var@@3NAX", ParseErrors.NonsenseAtEndOfSymbol, 'X', 10);

            // Invalid type of data symbol
            ExpectParseError("?var@@9NA", ParseErrors.InvalidDataEncoding, '9', 7);

            // Invalid vtbl symbol
            ExpectParseError("?var@@6EAX@@X", ParseErrors.UnexpectedCharacter, 'X', 13);

            // Missing function return type
            ExpectParseError("?f@@YA@Z", ParseErrors.ExpectedReturnType, '@', 7);

            // Missing function terminator
            ExpectParseError("?f@@YAHX@", ParseErrors.UnterminatedFunction, '@', 9);

            // Unterminated parameter list
            ExpectParseError("?f@@YAHH", ParseErrors.UnterminatedParameterList, (char)Lexer.EOF, 9);

            // Empty parameter list
            ExpectParseError("?f@@YAH@Z", ParseErrors.EmptyParameterList, (char)Lexer.EOF, 9);

            // Invalid char in parameter list
            ExpectParseError("?f@@YAHHh@Z", ParseErrors.UnexpectedCharacter, 'h', 9);
            ExpectParseError("?f@@YAHH%@Z", ParseErrors.UnexpectedCharacter, '%', 9);

            // Invalid calling convention in parameter list
            ExpectParseError("?f@@YQHH@@Z", ParseErrors.InvalidCallingConvention, 'Q', 6);
            ExpectParseError("?f@@Y1HH@@Z", ParseErrors.InvalidCallingConvention, '1', 6);

            // Empty anonymous namespace
            ExpectParseError("?f@?A@YAHHX@Z", ParseErrors.EmptyName, 'A', 6);
            ExpectParseError("?f@?%@YAHHX@Z", ParseErrors.EmptyName, 'A', 6);

            // Invalid enum base type name
            ExpectParseError("?f@@YAHWAX@Z", ParseErrors.InvalidEnumType, 'A', 9);
            ExpectParseError("?f@@YAHWAX@Z", ParseErrors.InvalidEnumType, 'A', 9);
            ExpectParseError("?f@@YAHW_X@Z", ParseErrors.InvalidEnumType, '_', 9);

            // Invalid parameter type name (only templates are allowed as special names)
            ExpectParseError("?f@@YAHW4?AX@Z", ParseErrors.ExpectedTemplateName, 'A', 11);

            // Invalid template param type encoding
            ExpectParseError("?x@@3V?$Tplate@a@@A", ParseErrors.InvalidTemplateArgument, 'a', 16);

            // Invalid template param const type encoding
            ExpectParseError("?x@@3V?$Tplate@$a@@A", ParseErrors.InvalidTemplateConst, 'a', 17);

            // Unterminated template param list
            ExpectParseError("?x@@3V?$Tplate@H", ParseErrors.UnterminatedTemplateParameterList, (char)Lexer.EOF, 17);

            // Invalid template param const type encoding
            ExpectParseError("?x@@3V?$Tplate@$@@A", ParseErrors.InvalidTemplateConst, '@', 17);
            ExpectParseError("?x@@3V?$Tplate@$a@@A", ParseErrors.InvalidTemplateConst, 'a', 17);

            ExpectParseError("??$Fn@$$AAAMN@Z@@YAHI@Z", ParseErrors.InvalidFunctionStorage, 'A', 10);
            ExpectParseError("??$Fn@$$ABAMN@Z@@YAHI@Z", ParseErrors.InvalidFunctionStorage, 'B', 10);
        }

        private static void ExpectParseError(string symbolName, string format, char erroneousChar, int errorPosition)
        {
            string parseError = string.Format(CultureInfo.CurrentCulture, format, erroneousChar);
            string exceptionMessage = string.Format(CultureInfo.CurrentCulture, ParseErrors.SymbolParseErrorFormat, symbolName, parseError, errorPosition);
            ExceptionAssert.Expect<InvalidSymbolNameException>(ex =>
            {
                Assert.AreEqual(symbolName, ex.Symbol);
                Assert.AreEqual<string>(exceptionMessage, ex.Message, "Unexpected parse error message");
                Assert.AreEqual(errorPosition, ex.Position);
            }, () => Parser.Parse(symbolName));
        }


        [TestMethod]
        public void SymbolParser_ConstructorsArgsTest()
        {
            ExceptionAssert.Expect<ArgumentNullException>(() => Parser.Parse(null));

            ExceptionAssert.Expect<ArgumentNullException>(() => new Symbol("blah", null));
            ExceptionAssert.Expect<ArgumentNullException>(() => new Symbol(null, new QualifiedNameNode(new IdentifierNode("blah"))));

            ExceptionAssert.Expect<ArgumentNullException>(() => new Lexer(null));
            ExceptionAssert.Expect<ArgumentNullException>(() => new Lexer(string.Empty));

            ExceptionAssert.Expect<ArgumentNullException>(() => new SymbolicName(null));
            ExceptionAssert.Expect<ArgumentNullException>(() => new SymbolicName(string.Empty));
        }

        [TestMethod]
        public void CvSymbolTest()
        {
            GlobalVariableSymbol symbol;

            symbol = this.VerifyGlobalVariableStorage("?@?i@@3HA", "i", "int", StorageClass.None);
        }

        [TestMethod]
        public void VariableStorageClassTest()
        {
            GlobalVariableSymbol symbol;

            // Simple case as a baseline, no storage specification:
            //      double var;
            symbol = this.VerifyGlobalVariableStorage("?var@@3NA", "var", "double", StorageClass.None);
            Assert.AreEqual("double var", symbol.ToString());

            // volatile long double volatileVar;
            this.VerifyGlobalVariableStorage("?volatileVar@@3OC", "volatileVar", "long double", StorageClass.Volatile);

            // const __int8 constVar;
            this.VerifyGlobalVariableStorage("?constVar@@3_DB", "constVar", "__int8", StorageClass.Const);

            // Note sure how a variable can be const and volatile, but you can do it
            // This time also using an extended type encoding
            this.VerifyGlobalVariableStorage("?cvi64@@3_JED", "cvi64", "__int64",
                StorageClass.Const | StorageClass.Volatile,
                variableModifiers: StorageModifiers.Ptr64);

            // Bring in unaligned modifier (other modifiers can only be applied to pointers)
            //  wchar_t volatile __unaligned unaligned
            // However, like UndecorateSymbolName, we don't treat the existence of the other modifiers on non-pointer types as an error
            this.VerifyGlobalVariableStorage("?x@@3_WFIC", "x", "wchar_t", StorageClass.Volatile,
                variableModifiers: StorageModifiers.Unaligned | StorageModifiers.Restrict);

            // Simple compound type case
            symbol = this.VerifyGlobalVariableStorage("?a@@3VAbc@@A", "a", "Abc", StorageClass.None);
            Assert.AreEqual("class Abc a", symbol.ToString());

            // Const
            // const Abc ca;
            symbol = this.VerifyGlobalVariableStorage("?ca@@3VAbc@@B", "ca", "Abc", StorageClass.Const);
            Assert.AreEqual("class Abc const ca", symbol.ToString());
        }

        private GlobalVariableSymbol VerifyGlobalVariableStorage(string symbolName, string variableName, string typeName, StorageClass variableStorage, StorageModifiers variableModifiers = StorageModifiers.None, StorageClass typeStorage = StorageClass.None)
        {
            Symbol symbol = VerifySymbolUndecoration(symbolName);
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));

            GlobalVariableSymbol varNode = (GlobalVariableSymbol)symbol;
            Assert.AreEqual(symbolName, varNode.QualifiedName.SymbolName);
            Assert.AreEqual(variableName, varNode.QualifiedName.ToString());
            Assert.AreEqual(variableStorage, varNode.StorageClassification, "Storage classification of variable is incorrect");
            Assert.AreEqual(variableModifiers, varNode.StorageModifierFlags, "Storage modifier flags of varaible are incorrect");
            Assert.IsInstanceOfType(varNode.VariableType, typeof(TypeNode));
            Assert.IsNotInstanceOfType(varNode.VariableType, typeof(PointerTypeNode));
            TypeNode varType = (TypeNode)varNode.VariableType;
            Assert.AreEqual(typeStorage, varType.StorageClassification, "Storage classification of variable type is incorrect");
            return varNode;
        }

        [TestMethod]
        public void StorageClassGlobalPointerVariableTest()
        {
            VariableSymbol symbol;

            // Simple non-const pointer to non-const object as a baseline, i.e. void* ptr;
            symbol = VerifyGlobalPointerStorage("?p@@3PAXA", "p", "void");
            Assert.AreEqual("void * p", symbol.ToString());

            // Pointer to const target (the pointer itself is not const)
            // Although in fact the compiler seems to generate this incorrectly as 3PBHB - see below
            symbol = VerifyGlobalPointerStorage("?ptrToConst@@3PBCA", "ptrToConst", "signed char",
                targetStorage: StorageClass.Const);
            Assert.AreEqual("signed char const * ptrToConst", symbol.ToString());

            // This is what the compiler actually emits for "const char* ptrToConst". I'm not sure why the final storage
            // class is emitted in this case since it seems to be redundant given the ability to encode constness in the pointer 
            // code itself and in the storage class for the its target. Logically it is the storage class for the variable
            // (which would be the same as the storage class of the pointer, not the target) so in this case it seems the compiler
            // is generating the wrong mangled name since the the pointer variable itself is not const
            VerifyGlobalPointerStorage("?ptrToConst@@3PBDB", "ptrToConst", "char",
                targetStorage: StorageClass.Const,
                // Not really correct for const char*, but it's what the name as emitted by the compiler actually says
                variableStorage: StorageClass.Const);

            // Const pointer to non-const object:
            //      unsigned char* const constPtr
            // Note how the storage class of the pointer itself IS now const (hence Q rather than P)
            // Haven't managed to get this in practice, because the compiler seems to emit an unmangled C format name
            // (which seems like a bug, as it does emit mangled names for similar static members - see below)
            symbol = VerifyGlobalPointerStorage("?constPtr@@3QAEA", "constPtr", "unsigned char",
                pointerStorage: StorageClass.Const);
            // undname undecorates this incorrectly as "unsigned char* constPtr", losing the const qualification
            Assert.AreEqual("unsigned char * const constPtr", symbol.ToString());

            // --------- Repeat for volatile only
            VerifyGlobalPointerStorage("?pv@@3PCFA", "pv", "short", targetStorage: StorageClass.Volatile);

            VerifyGlobalPointerStorage("?pv2@@3PCGC", "pv2", "unsigned short",
                targetStorage: StorageClass.Volatile,
                variableStorage: StorageClass.Volatile);

            VerifyGlobalPointerStorage("?vp@@3RAHA", "vp", "int",
                pointerStorage: StorageClass.Volatile);

            // --------- Repeat for const volatile
            VerifyGlobalPointerStorage("?pcv@@3PDIA", "pcv", "unsigned int",
                targetStorage: StorageClass.Volatile | StorageClass.Const);

            VerifyGlobalPointerStorage("?pcv2@@3PDJD", "pcv2", "long",
                targetStorage: StorageClass.Volatile | StorageClass.Const,
                variableStorage: StorageClass.Volatile | StorageClass.Const);

            VerifyGlobalPointerStorage("?cvp@@3SAKA", "cvp", "unsigned long",
                pointerStorage: StorageClass.Volatile | StorageClass.Const);

            // Of course there can be different classifications on the pointer and target too,
            // although as mentioned the compiler always incorrectly emits the target classificiation as that of
            // the variable
            // volatile float* const cpv;
            VerifyGlobalPointerStorage("?cpv@@3QCMC", "cpv", "float",
                pointerStorage: StorageClass.Const,
                targetStorage: StorageClass.Volatile,
                variableStorage: StorageClass.Volatile);

            // const bool* volatile cpv;
            VerifyGlobalPointerStorage("?cpv@@3RB_NB", "cpv", "bool",
                pointerStorage: StorageClass.Volatile,
                targetStorage: StorageClass.Const,
                variableStorage: StorageClass.Const);

            // TODO: Storage modifiers on the pointer and target and variable 

        }

        [TestMethod]
        public void MemberPointerVariablesTest()
        {
            // The representation of pointer variables is relatively complex, particularly because there are three storage
            // class settings; one for the target of the pointer, one for the pointer itself, and one for the variable. In fact the 
            // latter should really the same as the storage of the variable i.e. one or other is redundant. Non-pointer variables need
            // storage class information on the variable itself, but its not really needed for pointer variables as the pointer spec
            // has storage class information encoded in it. To make matters worse the compiler seems to have a bug in that the variable
            // storage class is set to be that of the pointed at object, not the pointer itself.
            // Adding in member pointers complicates things a little further

            VariableSymbol variable;

            // Simple pointer to member (variable, not member function), e.g.
            // int Abc::* pia;                        ?pia@@3PQAbc@@HQ1@
            // const int Abc::* pia:              ?pia@@3PRAbc@@HR1@	
            // volatile int Abc::* pia;           ?pia@@3PSAbc@@HS1@
            // const volatile int Abc::* cvpia;     ?cvpia@@3PTAbc@@HT1@
            // Note how the storage class of the target and variable are both set, but the storage class of the pointer is always 'P'
            // This isn't really correct, since the storage class of the variable is not const/volatile (the variable is pointing at a const/volatile
            // object). Also these are then undecorated correctly, .e.g const int (Abc::* pia) undecorates as "(int const Abc::* const cpia)"
            for (StorageClass sc = StorageClass.None; sc <= StorageClass.CvMask; sc++)
            {
                string symbol = string.Format("?pia@@3P{0}Abc@@H{0}1@", (char)('Q' + sc));
                StorageClass expectedStorage = StorageClass.Member | sc;
                var v = VerifyGlobalPointerStorage(symbol, "pia", "int",
                    pointerStorage: StorageClass.None,
                    targetStorage: expectedStorage,
                    variableStorage: expectedStorage);
                //Assert.IsInstanceOfType(v, typeof(StaticMemberVariableSymbol));
                //Assert.AreEqual("Abc", ((StaticMemberVariableSymbol)v).TypeName);
            }

            // Now const pointer rather than pointer to const
            // int Abc::* const pcia;
            variable = VerifyGlobalPointerStorage("?pcia@@3QQAbc@@HQ1@", "pcia", "int",
                            pointerStorage: StorageClass.Const,
                            targetStorage: StorageClass.Member,
                            variableStorage: StorageClass.Member);
            // Undname does not correctly handle the pointer storage and prints this without any const indication - normally we emulate the bug
            Assert.AreEqual("int Abc::* pcia", variable.ToString(UndecorateOptions.None));
            // But we can do better - this is a correct decoding of the storage classes
            Assert.AreEqual("int Abc::* const pcia", variable.ToString());

            // int (Abc::* const volatile pcvia)    ?pcvia@@3SQAbc@@HQ1@
            variable = VerifyGlobalPointerStorage("?pcvia@@3SQAbc@@HQ1@", "pcvia", "int",
                            pointerStorage: StorageClass.CvMask,
                            targetStorage: StorageClass.Member,
                            variableStorage: StorageClass.Member);
            Assert.AreEqual("int Abc::* pcvia", variable.ToString(UndecorateOptions.None));   // Wrong
            Assert.AreEqual("int Abc::* const volatile pcvia", variable.ToString());    // Correct

            // Combine both const pointer and const target
            // const int Abc::* const cpcia;   ?cpcia@@3QRAbc@@HR1@
            variable = VerifyGlobalPointerStorage("?cpcia@@3QRAbc@@HR1@", "cpcia", "int",
                     pointerStorage: StorageClass.Const,
                     targetStorage: StorageClass.Member | StorageClass.Const,
                     variableStorage: StorageClass.Member | StorageClass.Const);
            // Undname will decode this correctly, although only because symbol generated by the compiler wrongly specifies the variable
            // storage class to be the same as the target storage class, however if we ask for corrected undecoration the result
            // should be still be correct despite the error in the symbolic representation
            Assert.AreEqual("int const Abc::* const cpcia", variable.ToString());

            // const pointer to volatile
            // volatile int Abc::* const vpcia;  ?vpcia@@3QSAbc@@HS1@
            variable = VerifyGlobalPointerStorage("?vpcia@@3QSAbc@@HS1@", "vpcia", "int",
                     pointerStorage: StorageClass.Const,
                     targetStorage: StorageClass.Member | StorageClass.Volatile,
                     variableStorage: StorageClass.Member | StorageClass.Volatile);
            Assert.AreEqual("int volatile Abc::* const vpcia", variable.ToString());

            // volatile pointer to const
            // const int (Abc::* volatile cpvia);  ?cpvia@@3RRAbc@@HR1@
            variable = VerifyGlobalPointerStorage("?cpvia@@3RRAbc@@HR1@", "cpvia", "int",
                     pointerStorage: StorageClass.Volatile,
                     targetStorage: StorageClass.Member | StorageClass.Const,
                     variableStorage: StorageClass.Member | StorageClass.Const);
            // Undname wrongly decodes this as "int const Abc::* const cpvia"
            Assert.AreEqual("int const Abc::* volatile cpvia", variable.ToString());

            // Both CV
            // const volatile int (Abc::* const volatile cvpcvia);  ?cvpcvia@@3STAbc@@HT1@
            variable = VerifyGlobalPointerStorage("?cvpcvia@@3STAbc@@HT1@", "cvpcvia", "int",
                     pointerStorage: StorageClass.CvMask,
                     targetStorage: StorageClass.Member | StorageClass.CvMask,
                     variableStorage: StorageClass.Member | StorageClass.CvMask);
            Assert.AreEqual("int const volatile Abc::* const volatile cvpcvia", variable.ToString());

            // TODO: Doubly indirected
        }

        private VariableSymbol VerifyGlobalPointerStorage(string symbolName, string varName, string varType, StorageClass pointerStorage = StorageClass.None, StorageClass targetStorage = StorageClass.None, StorageClass variableStorage = StorageClass.None)
        {
            VariableSymbol symbol = VerifyPointerStorage(symbolName, varName, varType, pointerStorage, targetStorage, variableStorage);
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            return symbol;

        }

        private VariableSymbol VerifyPointerStorage(string symbolName, string varName, string varType, StorageClass pointerStorage, StorageClass targetStorage, StorageClass variableStorage)
        {
            Symbol symbol = VerifySymbolUndecoration(symbolName);

            VariableSymbol varNode = (VariableSymbol)symbol;
            Assert.AreEqual(varName, varNode.QualifiedName.ToString(), "Incorrect variable name");
            Assert.AreEqual(variableStorage, varNode.StorageClassification, "Storage classification of variable is incorrect");
            Assert.IsInstanceOfType(varNode.VariableType, typeof(PointerTypeNode));
            PointerTypeNode ptrNode = (PointerTypeNode)varNode.VariableType;
            Assert.AreEqual(pointerStorage, ptrNode.StorageClassification, "Storage classification of pointer is incorrect");
            Assert.IsInstanceOfType(ptrNode.TargetType, typeof(PrimitiveTypeNode));
            TypeNode targetNode = (TypeNode)ptrNode.TargetType;
            Assert.AreEqual(targetNode.Name, varType, "Incorrect target variable type name");
            Assert.AreEqual(targetStorage, targetNode.StorageClassification, "Storage classification of target type is incorrect");
            return varNode;
        }

        [TestMethod]
        public void StaticVariableStorageClassTest()
        {
            Symbol symbol;

            symbol = VerifySymbolUndecoration("?ptr@Class1@@0PAHA");
            Assert.IsInstanceOfType(symbol, typeof(StaticMemberVariableSymbol));
            var staticMember = (StaticMemberVariableSymbol)symbol;
            Assert.AreEqual("private: static int * Class1::ptr", staticMember.ToString());
            Assert.AreEqual("Class1", staticMember.TypeName);

            // Compiler seems to generate wrong mangled name here (I think it should end 3PBHA, since as is it undecorates as
            // if it were a const pointer to const), but as long as we can parse it ...
            symbol = VerifySymbolUndecoration("?ptrToConst@Class1@@1PBHB");
            Assert.IsInstanceOfType(symbol, typeof(StaticMemberVariableSymbol));
            Assert.AreEqual("protected: static int const * Class1::ptrToConst", symbol.ToString());

            symbol = VerifySymbolUndecoration("?constPtr@Class1@@2QAHB");
            Assert.IsInstanceOfType(symbol, typeof(StaticMemberVariableSymbol));
            Assert.AreEqual("public: static int * const Class1::constPtr", symbol.ToString(UndecorateOptions.NoPtr64));

            symbol = VerifySymbolUndecoration("?constPtrToConst@Class1@@2QBHB");
            Assert.IsInstanceOfType(symbol, typeof(StaticMemberVariableSymbol));
            Assert.AreEqual("public: static int const * const Class1::constPtrToConst", symbol.ToString());

        }

        [TestMethod]
        public void PointerTypesTest()
        {
            Symbol symbol;

            // Global variable pointer to int, i.e. int* pi
            symbol = VerifySymbolUndecoration("?pi@@3PAHA");
            Assert.AreEqual("int * pi", symbol.ToString());

            // Global variable pointer to pointer int, i.e. int** ppi
            symbol = VerifySymbolUndecoration("?ppi@@3PAPAHA");
            Assert.AreEqual("int * * ppi", symbol.ToString());
        }

        [TestMethod]
        public void BasedPointerTest()
        {
            Symbol symbol;

            // A based pointer is one that is defined relative to another and which therefore can be
            // serialized and re-used later in a different session because it is a relative address
            // rather than an absolute one. This is an MS extension.
            // Based pointer symbol names are special in including the name of the pointer off which
            // they are based in the mangling. That a pointer is based is indicated by the M storage modifier

            // Test a based variable 
            symbol = VerifySymbolUndecoration("?pBased@@3PM2pBase@@HM21@");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            // Compiler storage class bug causes the undname result to be incorrect
            Assert.AreEqual("int __based(pBase) * __based(pBase) pBased", symbol.ToString(UndecorateOptions.None));
            // This is what it should be
            Assert.AreEqual("int __based(pBase) * pBased", symbol.ToString());

            // This is perhaps how it should be (without the duplicated modifiers on the variable)
            symbol = VerifySymbolUndecoration("?pBased@@3PM2pBase@@HA");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            Assert.AreEqual("int __based(pBase) * pBased", symbol.ToString(UndecorateOptions.None));
            Assert.AreEqual("int __based(pBase) * pBased", symbol.ToString());

            // With const modifier too
            symbol = VerifySymbolUndecoration("?pBased@@3PN2pBase@@HN21@");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            // Compiler storage class bug causes the undname result to be incorrect
            Assert.AreEqual("int const __based(pBase) * const __based(pBase) pBased", symbol.ToString(UndecorateOptions.None));
            // This is what it should be
            Assert.AreEqual("int const __based(pBase) * pBased", symbol.ToString());

            // With volatile modifier too
            symbol = VerifySymbolUndecoration("?pBased@@3PO2pBase@@HO21@");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            // Compiler storage class bug causes the undname result to be incorrect
            Assert.AreEqual("int volatile __based(pBase) * volatile __based(pBase) pBased", symbol.ToString(UndecorateOptions.None));
            // This is what it should be
            Assert.AreEqual("int volatile __based(pBase) * pBased", symbol.ToString());

            // With const and volatile modifiers too
            symbol = VerifySymbolUndecoration("?cvpBased@@3PP2base_ptr@@HP21@");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            // Compiler storage class bug causes the undname result to be incorrect
            Assert.AreEqual("int const volatile __based(base_ptr) * const volatile __based(base_ptr) cvpBased", symbol.ToString(UndecorateOptions.None));
            // This is what it should be
            Assert.AreEqual("int const volatile __based(base_ptr) * cvpBased", symbol.ToString());

            // With ptr64 modifier too
            symbol = VerifySymbolUndecoration("?pBased@@3PEM2pBase@@HEM21@");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            Assert.AreEqual("int __based(pBase) * __ptr64 __based(pBase) __ptr64 pBased", symbol.ToString(UndecorateOptions.None));
            Assert.AreEqual("int __based(pBase) * __ptr64 pBased", symbol.ToString(UndecorateOptions.NoUndnameEmulation));

            // With ptr64 and const modifiers
            symbol = VerifySymbolUndecoration("?pBased@@3PEN2pBase@@HEN21@");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            Assert.AreEqual("int const based(pBase) * ptr64 const based(pBase) ptr64 pBased", symbol.ToString(UndecorateOptions.NoLeadingUnderscores));
            Assert.AreEqual("int const based(pBase) * ptr64 pBased", symbol.ToString(UndecorateOptions.NoLeadingUnderscores | UndecorateOptions.NoUndnameEmulation));

            // Test a based function parameter

            symbol = VerifySymbolUndecoration("?foo@@YAXPM2p@@H@Z");
            Assert.IsInstanceOfType(symbol, typeof(GlobalFunctionSymbol));
            Assert.AreEqual("void foo(int __based(p) *)", symbol.ToString(UndecorateOptions.NoCallingConvention));

            // void based
            symbol = VerifySymbolUndecoration("?vbp@@3PM0HM0");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            Assert.AreEqual("int __based(void) * vbp", symbol.ToString());

            // TODO: Doubly indirected
        }


        [TestMethod]
        public void FunctionPointerVariablesTest()
        {
            Symbol symbol;

            // Simple case without even __ptr64 storage modifier (which is the default for 64-bit compilation)
            // void (* __ptr32 fpx32)();				// 
            symbol = VerifySymbolUndecoration("?fpx32@@3P6AXXZA");
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            Assert.AreEqual("void (__cdecl * fpx32)(void)", symbol.ToString());
            // Undname only omits the calling convention on a top-level function (note also spacing bug)...
            Assert.AreEqual("void (__cdecl * fpx32)(void)", symbol.ToString(UndecorateOptions.NoCallingConvention));
            // .. but we can suppress that
            Assert.AreEqual("void (* fpx32)(void)", symbol.ToString(UndecorateOptions.NoCallingConvention | UndecorateOptions.NoUndnameEmulation));

            // TODO: Doubly indirected
        }

        /// <summary>
        /// Test operators (i.e. user-defined operator overloads)
        /// </summary>
        [TestMethod]
        public void OperatorsTest()
        {
            GlobalFunctionSymbol func;

            // Simple case
            func = this.VerifyGlobalFunctionSymbolParsing("??G@YAHH@Z", "operator-", "int", new string[] { "int" });
            Assert.AreEqual("int __cdecl operator-(int)", func.ToString());
            Assert.AreEqual(Operator.Subtract, func.Operator);

            // Extended case
            func = this.VerifyGlobalFunctionSymbolParsing("??_0@YAHH@Z", "operator/=", "int", new string[] { "int" });
            Assert.AreEqual("int __cdecl operator/=(int)", func.ToString());
            Assert.AreEqual(Operator.DivideLvalue, func.Operator);

            // Named (as opposed to symbolic) so requiring a space between "operator" and the name when displaying
            func = this.VerifyGlobalFunctionSymbolParsing("??_0@YAHH@Z", "operator/=", "int", new string[] { "int" });
            Assert.AreEqual("int __cdecl operator/=(int)", func.ToString());
            Assert.AreEqual(Operator.DivideLvalue, func.Operator);
        }

        [TestMethod]
        public void SpecialNamesTest()
        {
            //foreach (CompilerSpecialName name in Enum.GetValues(typeof(CompilerSpecialName)))
            //{
            //    VerifySpecialDataName(name);
            //}

            VerifySpecialDataName(CompilerSpecialName.Vftable);
            VerifySpecialDataName(CompilerSpecialName.Typeof);
            VerifySpecialDataName(CompilerSpecialName.PlacementDeleteArrayClosure);

            VerifySpecialDataName(CompilerSpecialName.LocalStaticThreadGuard);

        }

        private void VerifySpecialDataName(CompilerSpecialName specialDataCode)
        {
            string prefix = "";
            char code;
            if (specialDataCode >= CompilerSpecialName.ManagedVectorConstructorIterator)
            {
                prefix = "_";
                code = (char)('A' + specialDataCode - CompilerSpecialName.ManagedVectorConstructorIterator);
            }
            else if (specialDataCode >= CompilerSpecialName.Typeof)
            {
                code = (char)('A' + specialDataCode - CompilerSpecialName.Typeof);
            }
            else
            {
                code = (char)('7' + specialDataCode);
            }
            string specialDataSymbolicName = string.Format("??_{0}{1}x@@6B@", prefix, code);
            Symbol symbol = VerifySymbolUndecoration(specialDataSymbolicName);
            Assert.IsInstanceOfType(symbol, typeof(SpecialDataSymbol));
            var specialDataSymbol = (SpecialDataSymbol)symbol;

            Assert.IsInstanceOfType(symbol, typeof(SpecialDataSymbol));
            var specialData = (SpecialDataSymbol)symbol;
            Assert.AreEqual(Operator.None, specialData.Operator);
            Assert.AreEqual("x", specialData.ScopeName);
            Assert.AreEqual(StorageClass.Const, specialData.StorageClassification);
            Assert.IsInstanceOfType(specialData.QualifiedName.Identifier, typeof(SpecialNameNode));
            var nameNode = (SpecialNameNode)specialData.QualifiedName.Identifier;
            Assert.AreEqual(specialDataCode, nameNode.SpecialNameCode);
        }

        /// <summary>
        /// Test constructors, which are special case member functions that don't have a return type
        /// </summary>
        [TestMethod]
        public void ConstructorSymbolsTest()
        {
            MemberFunctionSymbol symbol;

            // Simple untemplated case
            // Default public constructor: Abc::Abc
            symbol = this.VerifyMemberFunctionSymbolParsing("??0Abc@@QAE@XZ", "Abc", "Abc", null,
                callingConvention: CallingConvention.ThisCall,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("public: __thiscall Abc::Abc(void)", symbol.ToString());
            Assert.AreEqual(Operator.None, symbol.Operator);

            // And another with an arg
            //  public: __thiscall Abc::Abc(int)"
            symbol = VerifyMemberFunctionSymbolParsing("??0Abc@@QAE@H@Z", "Abc", "Abc", null,
                new string[] { "int" },
                callingConvention: CallingConvention.ThisCall,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("public: __thiscall Abc::Abc(int)", symbol.ToString());
            Assert.AreEqual(Operator.None, symbol.Operator);
            Assert.IsInstanceOfType(symbol.QualifiedName.Identifier, typeof(ConstructorNode));

            // Qualified class name in namespace
            symbol = this.VerifyMemberFunctionSymbolParsing("??0Abc@Ns@@QAE@H@Z", "Abc", "Ns::Abc", null,
                new string[] { "int" },
                callingConvention: CallingConvention.ThisCall,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("public: __thiscall Ns::Abc::Abc(int)", symbol.ToString());
            Assert.AreEqual(Operator.None, symbol.Operator);
            Assert.IsInstanceOfType(symbol.QualifiedName.Identifier, typeof(ConstructorNode));

            // Simple templated case
            //  
            // Note that because the constructor is named after the type, the template parameters
            // form part of the constructor method name as well as the type name, so appear twice.
            // This symbol is from 64-bit compiler, so has ptr64 storage modifier (E after the Q)
            symbol = VerifyMemberFunctionSymbolParsing("??0?$abc@H@@QEAA@XZ", "abc", "abc<int>", null, null,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("public: __cdecl abc<int>::abc<int>(void)", symbol.ToString());
            Assert.AreEqual(Operator.None, symbol.Operator);
            Assert.IsInstanceOfType(symbol.QualifiedName.Identifier, typeof(ConstructorNode));

            // Fairly complex templated case with nested templates and use of pointer/ref types
            //  public: __cdecl def<class abc<int>,int * &,abc<int> >::def<class abc<int>,int * &,abc<int> >(int * &)
            symbol = VerifyMemberFunctionSymbolParsing("??0?$def@V?$abc@H@@AEAPEAHV1@@@QEAA@AEAPEAH@Z", "def",
                "def<abc<int>,int * &,abc<int> >", null,
                new string[] { "int * &" },
                protectionLevel: MemberProtectionLevel.Public
                );
            Assert.AreEqual(Operator.None, symbol.Operator);
            Assert.IsInstanceOfType(symbol.QualifiedName.Identifier, typeof(ConstructorNode));
        }


        [TestMethod]
        public void AnonymousNamespaceTest()
        {
            Symbol symbol;

            symbol = this.VerifyMemberFunctionSymbolParsing("?f@A@?A0xe79b6672@@QAEXXZ", "f", "`Anon$0xe79b6672'::A", "void",
                callingConvention: CallingConvention.ThisCall,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("`anonymous namespace'::A::f", symbol.ToString(UndecorateOptions.NameOnly));

            // Nested symbol scope with backref to name (the outermost scope name just happens to be the same as the function name)
            symbol = this.VerifyMemberFunctionSymbolParsing("?f@B@?1??0@YAXXZ@QAEXXZ", "f", "`f'::`2'::B", "void",
                callingConvention: CallingConvention.ThisCall,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("`f'::`2'::B::f", symbol.ToString(UndecorateOptions.NameOnly));
            Assert.IsInstanceOfType(symbol.QualifiedName.Qualifiers.ToList()[1], typeof(SpecialQualifierNode));
            SpecialQualifierNode lexicalFrame = (SpecialQualifierNode)symbol.QualifiedName.Qualifiers.ToList()[1];
            Assert.AreEqual("`2'", lexicalFrame.Name);

            // Anonymous namespace
            symbol = this.VerifyMemberFunctionSymbolParsing("??0Def@?A0xd8df4086@@QAE@XZ", "Def", "`Anon$0xd8df4086'::Def", null,
                callingConvention: CallingConvention.ThisCall,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("public: __thiscall `Anon$0xd8df4086'::Def::Def(void)", symbol.ToString());

            // Some obscure examples that undname doesn't support
            symbol = this.VerifySymbolUndecoration("?c@?A0x677883ef@@3HA");
            Assert.AreEqual("int `Anon$0x677883ef'::c", symbol.ToString());

            //symbol = this.VerifySymbolUndecoration("?b@?A0x677883ef@1@3HA");
            //Assert.AreEqual("int A0x677883ef::`anonymous namespace'::b", symbol.ToString());    // Undname is not correct here
            //symbol = this.VerifySymbolUndecoration("?a@?A0x677883ef@11@3HA");
            //Assert.AreEqual("int A0x677883ef::A0x677883ef::`anonymous namespace'::a", symbol.ToString());    // Undname is not correct here
        }

        /// <summary>
        /// Test destructors, which are special case member functions that don't have a return type
        /// or parameters
        /// </summary>
        [TestMethod]
        public void DestructorsTest()
        {
            Symbol symbol;

            // Simple untemplated case
            // public desnstructor: Abc::~Abc()
            symbol = this.VerifyMemberFunctionSymbolParsing("??1Abc@@QAE@XZ", "~Abc", "Abc", null,
                callingConvention: CallingConvention.ThisCall,
                protectionLevel: MemberProtectionLevel.Public);
            Assert.AreEqual("public: __thiscall Abc::~Abc(void)", symbol.ToString());
        }

        [TestMethod]
        public void EnumTypesTest()
        {
            VerifyEnumTypeParsing("?e@@3W4E@N@@A", EnumBaseType.Int, baseTypeName: null, qualifier: "N", enumTypeName: "E", varName: "e");
            VerifyEnumTypeParsing("?ch@@3W0Char@Blah@@A", EnumBaseType.Char, baseTypeName: "char", qualifier: "Blah", enumTypeName: "Char", varName: "ch");
            VerifyEnumTypeParsing("?b@@3W1Byte@Blah@@A", EnumBaseType.UnsignedChar, baseTypeName: "unsigned char", qualifier: "Blah", enumTypeName: "Byte", varName: "b");
            VerifyEnumTypeParsing("?ul@@3W7Ulong@Blah@@A", EnumBaseType.UnsignedLong, baseTypeName: "unsigned long", qualifier: "Blah", enumTypeName: "Ulong", varName: "ul");
        }

        private void VerifyEnumTypeParsing(string enumSymbol, EnumBaseType baseType, string baseTypeName, string qualifier, string enumTypeName, string varName)
        {
            Symbol symbol = this.VerifySymbolUndecoration(enumSymbol);
            string qualifiedName = qualifier + "::" + enumTypeName;
            StringBuilder unmangled = new StringBuilder();
            unmangled.Append("enum ");
            if (baseType != EnumBaseType.Int)
            {
                unmangled.Append(baseTypeName);
                unmangled.Append(' ');
            }
            unmangled.Append(qualifiedName);
            unmangled.Append(' ');
            unmangled.Append(varName);

            Assert.AreEqual(unmangled.ToString(), symbol.ToString());
            Assert.AreEqual(varName, symbol.ToString(UndecorateOptions.NameOnly | UndecorateOptions.NoUndnameEmulation));

            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            GlobalVariableSymbol varNode = (GlobalVariableSymbol)symbol;
            Assert.AreEqual(varName, varNode.Name);

            Assert.IsInstanceOfType(varNode.VariableType, typeof(EnumTypeNode));
            EnumTypeNode enumType = (EnumTypeNode)varNode.VariableType;
            Assert.AreEqual(enumTypeName, enumType.Name);
            Assert.AreEqual(qualifiedName, enumType.QualifiedName.ToString());
            Assert.AreEqual(baseType, enumType.BaseType);
        }

        [TestMethod]
        public void ClassTypeTest()
        {
            Symbol symbol = this.VerifySymbolUndecoration("?a@@3VAbc@Ns@@A");
            Assert.AreEqual("class Ns::Abc a", symbol.ToString());

            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            GlobalVariableSymbol varNode = (GlobalVariableSymbol)symbol;

            Assert.IsInstanceOfType(varNode.VariableType, typeof(CompoundTypeNode));
            CompoundTypeNode classType = (CompoundTypeNode)varNode.VariableType;
            Assert.AreEqual(CompoundTypeClass.Class, classType.CompoundTypeClass);
            Assert.AreEqual("Abc", classType.Name);
            Assert.AreEqual("Ns::Abc", classType.QualifiedName.Name);
        }


        [TestMethod]
        public void SpecialExtendedTypesTest()
        {
            // Simple case of func returning nullptr_t
            GlobalFunctionSymbol symbol = VerifyNullPtrFuncSymbol("?f1@@YA$$T$$T@Z", "f1", "std::nullptr_t", typeof(NullPtrTypeNode));

            // More complex nullptr_t param types

            // Ptr to nullptr
            symbol = VerifyNullPtrFuncSymbol("?f2@@YAPA$$TPA$$T@Z", "f2", "std::nullptr_t *", typeof(PointerTypeNode));
            var ptrRetType = (PointerTypeNode)symbol.ReturnType;
            Assert.IsInstanceOfType(ptrRetType.TargetType, typeof(NullPtrTypeNode));

            // Ptr to ptr to nullptr
            symbol = VerifyNullPtrFuncSymbol("?f3@@YAPAPA$$TPAPA$$T@Z", "f3", "std::nullptr_t * *", typeof(PointerTypeNode));
            ptrRetType = (PointerTypeNode)symbol.ReturnType;
            Assert.IsInstanceOfType(ptrRetType.TargetType, typeof(PointerTypeNode));
            ptrRetType = (PointerTypeNode)ptrRetType.TargetType;
            Assert.IsInstanceOfType(ptrRetType.TargetType, typeof(NullPtrTypeNode));

            // Ref to nullptr
            symbol = VerifyNullPtrFuncSymbol("?f4@@YAAA$$TAA$$T@Z", "f4", "std::nullptr_t &", typeof(ReferenceTypeNode));
            var refRetType = (ReferenceTypeNode)symbol.ReturnType;
            Assert.IsInstanceOfType(refRetType.TargetType, typeof(NullPtrTypeNode));

            // Ref to ptr to nullptr
            symbol = VerifyNullPtrFuncSymbol("?f5@@YAAAPA$$TAAPA$$T@Z", "f5", "std::nullptr_t * &", typeof(ReferenceTypeNode));
            // Note we have a ref to ptr (reverse is invalid)
            refRetType = (ReferenceTypeNode)symbol.ReturnType;
            Assert.IsInstanceOfType(refRetType.TargetType, typeof(PointerTypeNode));
            ptrRetType = (PointerTypeNode)refRetType.TargetType;
            Assert.IsInstanceOfType(ptrRetType.TargetType, typeof(NullPtrTypeNode));

            // rvalue reference (C++ 11). Undname/UnDecorateSymbolName do not support this, although the C++ compiler emits it
            SymbolicName sym = new SymbolicName("?f6@@YA$$QA$$T$$QA$$T@Z");
            Assert.IsInstanceOfType(sym.ParseTree, typeof(GlobalFunctionSymbol));
            Assert.AreEqual("f6", sym.Name);
            Assert.AreEqual("std::nullptr_t && __cdecl f6(std::nullptr_t &&)", sym.ToString(UndecorateOptions.NoUndnameEmulation));
            symbol = (GlobalFunctionSymbol)sym.ParseTree;
            var rvalueType = (RvalueReferenceTypeNode)symbol.ReturnType;
            Assert.AreEqual(StorageClass.None, rvalueType.StorageClassification);
            Assert.IsInstanceOfType(rvalueType.TargetType, typeof(NullPtrTypeNode));

            // rvalue reference to volatile nullptr
            sym = new SymbolicName("?f8@@YA$$QC$$T$$QC$$T@Z");
            Assert.IsInstanceOfType(sym.ParseTree, typeof(GlobalFunctionSymbol));
            Assert.AreEqual("f8", sym.Name);
            Assert.AreEqual("std::nullptr_t volatile && __cdecl f8(std::nullptr_t volatile &&)", sym.ToString(UndecorateOptions.NoUndnameEmulation));
            symbol = (GlobalFunctionSymbol)sym.ParseTree;
            rvalueType = (RvalueReferenceTypeNode)symbol.ReturnType;
            Assert.IsInstanceOfType(rvalueType.TargetType, typeof(NullPtrTypeNode));
            Assert.AreEqual(StorageClass.None, rvalueType.StorageClassification);
            Assert.AreEqual(StorageClass.Volatile, rvalueType.TargetType.StorageClassification);

            // volatile rvalue reference - N.B. compiler doesn't seem to actually emit this, describing it as an anachronism that is ignored
            sym = new SymbolicName("?f9@@YA$$RAH$$RAM@Z");
            Assert.IsInstanceOfType(sym.ParseTree, typeof(GlobalFunctionSymbol));
            Assert.AreEqual("f9", sym.Name);
            Assert.AreEqual("int && volatile __cdecl f9(float && volatile)", sym.ToString(UndecorateOptions.NoUndnameEmulation));
            symbol = (GlobalFunctionSymbol)sym.ParseTree;
            rvalueType = (RvalueReferenceTypeNode)symbol.ReturnType;
            Assert.AreEqual(StorageClass.Volatile, rvalueType.StorageClassification);

            // rvalue ref to ptr
            sym = new SymbolicName("?f7@@YA$$QAPA$$T$$QAPA$$T@Z");
            Assert.IsInstanceOfType(sym.ParseTree, typeof(GlobalFunctionSymbol));
            Assert.AreEqual("f7", sym.Name);
            Assert.AreEqual("std::nullptr_t * && __cdecl f7(std::nullptr_t * &&)", sym.ToString());
            symbol = (GlobalFunctionSymbol)sym.ParseTree;
            // Again, ref to ptr as reverse is invalid
            Assert.IsInstanceOfType(symbol.ReturnType, typeof(RvalueReferenceTypeNode));
            rvalueType = (RvalueReferenceTypeNode)symbol.ReturnType;
            Assert.IsInstanceOfType(rvalueType.TargetType, typeof(PointerTypeNode));
            ptrRetType = (PointerTypeNode)rvalueType.TargetType;
            Assert.IsInstanceOfType(ptrRetType.TargetType, typeof(NullPtrTypeNode));


        }

        private GlobalFunctionSymbol VerifyNullPtrFuncSymbol(string symbolName, string funcName, string nullTypeName, Type nodeType)
        {
            GlobalFunctionSymbol symbol = this.VerifyGlobalFunctionSymbolParsing(symbolName, funcName, nullTypeName, new string[] { nullTypeName });
            string unmangled = string.Format("{0} __cdecl {1}({0})", nullTypeName, funcName);
            Assert.AreEqual(unmangled, symbol.ToString());
            TypeNode returnType = symbol.ReturnType;
            Assert.IsInstanceOfType(returnType, nodeType);
            TypeNode paramType = symbol.Parameters.Single();
            Assert.IsInstanceOfType(paramType, nodeType);
            return symbol;
        }

        /// <summary>
        /// Test numbered template arguments
        /// </summary>
        [TestMethod]
        public void TemplateIndexedArgumentTest()
        {
            BaseSymbolNode[] templateArgs;

            // Simplest possible case
            templateArgs = VerifyGlobalTemplateFunc("??$Fn@?0@@YAXXZ", "void Fn<`template-parameter-1'>(void)", new string[] { "`template-parameter-1'" });

            // The numbering uses the integer encoding scheme, so
            templateArgs = VerifyGlobalTemplateFunc("??$Fn@?A@?0?1@@YAXXZ", "void Fn<`template-parameter-0',`template-parameter-1',`template-parameter-2'>(void)",
                new string[] { "`template-parameter-0'", "`template-parameter-1'", "`template-parameter-2'" });

            // Alternate form
            templateArgs = VerifyCompoundVariableTemplateParams("?x@@3V?$TClass@$D0@@A", "class TClass<`template-parameter-1'> x",
                "`template-parameter-1'");
            templateArgs = VerifyCompoundVariableTemplateParams("?x@@3V?$TClass@$Q0@@A", "class TClass<`non-type-template-parameter-1'> x",
                "`non-type-template-parameter-1'");


            // Alternate form supported by undname; not sure if this is ever valid, or an error in it. Seems more likely an error as only the non-type
            // variant works
            templateArgs = VerifyCompoundVariableTemplateParams("?x@@3V?$TClass@$0Q0@@A", "class TClass<`non-type-template-parameter-1'> x",
                "`non-type-template-parameter-1'");

            // Alternate forms in template functions
            templateArgs = VerifyGlobalTemplateFunc("??$Fn@$D2@@YAXXZ", "void Fn<`template-parameter-3'>(void)", new string[] { "`template-parameter-3'" });

            templateArgs = VerifyGlobalTemplateFunc("??$Fn@$Q9@@YAXXZ", "void Fn<`non-type-template-parameter-10'>(void)", new string[] { "`non-type-template-parameter-10'" });
            templateArgs = VerifyGlobalTemplateFunc("??$Fn@$0Q9@@YAXXZ", "void Fn<`non-type-template-parameter-10'>(void)", new string[] { "`non-type-template-parameter-10'" });

            // "Indexed named"
            templateArgs = VerifyGlobalTemplateFunc("??$Fn@$RBlah@2@@YAXXZ", "void Fn<Blah>(void)", new string[] { "Blah" });
            // Indexed and named, although UndecorateSymbolName does not display the index
            Assert.AreEqual(3, ((TemplateParameterNode)templateArgs[0]).Index);
        }

        private BaseSymbolNode[] VerifyCompoundVariableTemplateParams(string mangled, string unmangled, params string[] expectedParams)
        {
            Symbol symbol;

            symbol = VerifySymbolUndecoration(mangled);
            Assert.AreEqual(unmangled, symbol.ToString());
            Assert.IsInstanceOfType(symbol, typeof(VariableSymbol));
            var templateType = ((VariableSymbol)symbol).VariableType;
            Assert.IsInstanceOfType(templateType, typeof(CompoundTypeNode));
            var templateClassType = (CompoundTypeNode)templateType;
            Assert.IsInstanceOfType(templateClassType.QualifiedName.Identifier, typeof(TemplateNameNode));
            var templateName = (TemplateNameNode)templateClassType.QualifiedName.Identifier;
            return VerifyTemplateParams(templateName, expectedParams);
        }

        private BaseSymbolNode[] VerifyGlobalTemplateFunc(string mangled, string unmangled, string[] templateArgs, string funcName = "Fn", string returnType = "void", string[] funcArgs = null)
        {
            GlobalFunctionSymbol func = VerifyGlobalFunctionSymbolParsing(mangled, funcName, returnType, paramTypeNames: funcArgs);

            Assert.AreEqual(unmangled, func.ToString(UndecorateOptions.NoCallingConvention | UndecorateOptions.NoUndnameEmulation));

            var funcType = func.FunctionType;
            Assert.IsInstanceOfType(func.QualifiedName.Identifier, typeof(TemplateNameNode));
            var templateName = (TemplateNameNode)func.QualifiedName.Identifier;
            return VerifyTemplateParams(templateName, templateArgs);
        }

        private static BaseSymbolNode[] VerifyTemplateParams(TemplateNameNode templateName, string[] expectedParams)
        {
            var templateArgs = templateName.Arguments.ToArray();
            Assert.AreEqual(expectedParams.Length, templateArgs.Length);
            for (int i = 0; i < expectedParams.Length; i++)
            {
                var templateArg = templateArgs[i];
                string expectedParam = expectedParams[i];
                if (templateArg is NameNode)
                {
                    Assert.AreEqual(expectedParam, ((NameNode)templateArg).Name);
                }
                else
                {
                    Assert.AreEqual(expectedParam, templateArg.ToString());

                }

            }
            return templateArgs;
        }

        /// <summary>
        /// Test encoding of integer template arguments
        /// </summary>
        [TestMethod]
        public void TemplateIntegerArgumentTest()
        {
            Symbol symbol;

            // Simple example with various integer args
            VerifyIntegerTemplateArgument("?X@@3V?$TClass@D$00@@A", "class TClass<char,1> X", 1);

            // Note that zero is represented as $0A, not $00 (which is 1, previous test step)
            VerifyIntegerTemplateArgument("?X@@3V?$TClass@D$0A@@@A", "class TClass<char,0> X", 0);

            // -1
            VerifyIntegerTemplateArgument("?XM1@@3V?$TClass@D$0?0@@A", "class TClass<char,-1> XM1", -1);

            VerifyIntegerTemplateArgument("?X9@@3V?$TClass@D$08@@A", "class TClass<char,9> X9", 9);

            // 10 is not $0A, as the encoding is one based
            VerifyIntegerTemplateArgument("?X10@@3V?$TClass@D$09@@A", "class TClass<char,10> X10", 10);

            // 11 is not $0A either, as that is the encoding for 0, rather 'tis is $0L
            VerifyIntegerTemplateArgument("?X11@@3V?$TClass@D$0L@@@A", "class TClass<char,11> X11", 11);

            // Alpanumeric encodings, e.g. $0A for 0
            VerifyIntegerTemplateArgument("?X@@3V?$TClass@D$0A@@@A", "class TClass<char,0> X", 0);
            // $0AA is not an encoding one would encounter in practice, as it is still zero
            VerifyIntegerTemplateArgument("?X@@3V?$TClass@D$0AA@@@A", "class TClass<char,0> X", 0);
            // $0B is a duplicate encoding for 1
            VerifyIntegerTemplateArgument("?X@@3V?$TClass@D$0B@@@A", "class TClass<char,1> X", 1);
            // $0BA is encoding for 1 << 4 + 0, i.e. 16
            VerifyIntegerTemplateArgument("?X@@3V?$TClass@D$0BA@@@A", "class TClass<char,16> X", 16);
            // $0BB is encoding for 1 << 4 + 1, i.e. 17
            VerifyIntegerTemplateArgument("?X@@3V?$TClass@D$0BB@@@A", "class TClass<char,17> X", 17);

            // A constructor, but of a parametric type with a large negative integer argument to the instantiation
            symbol = VerifySymbolUndecoration("??0?$K@$0?IAAAAAAAAAAAAAAA@@@QAE@XZ");
            Assert.IsInstanceOfType(symbol, typeof(MemberFunctionSymbol));
            Assert.AreEqual("K<-9223372036854775808>::K<-9223372036854775808>(void)", symbol.ToString(UndecorateOptions.NoUndnameEmulation | UndecorateOptions.NoCallingConvention | UndecorateOptions.NoMemberAccess));
        }

        private void VerifyIntegerTemplateArgument(string mangled, string unmangled, long argValue)
        {
            Symbol symbol;
            symbol = VerifySymbolUndecoration(mangled);
            Assert.AreEqual(unmangled, symbol.ToString());
            Assert.IsInstanceOfType(symbol, typeof(GlobalVariableSymbol));
            var variable = (GlobalVariableSymbol)symbol;
            Assert.IsInstanceOfType(variable.VariableType, typeof(CompoundTypeNode));
            var templateClassType = (CompoundTypeNode)variable.VariableType;
            Assert.IsInstanceOfType(templateClassType.QualifiedName.Identifier, typeof(TemplateNameNode));
            var templateClassId = (TemplateNameNode)templateClassType.QualifiedName.Identifier;
            var templateArgs = templateClassId.Arguments.ToArray();
            Assert.AreEqual(2, templateArgs.Length);
            Assert.IsInstanceOfType(templateArgs[1], typeof(LiteralNode<long>));
            var intArg = (LiteralNode<long>)templateArgs[1];
            Assert.AreEqual(argValue, intArg.Value);
        }

        /// <summary>
        /// Test encoding of float template arguments
        /// </summary>
        /// <remarks>Not actually supported by current compiler, but still part of the symbol syntax</remarks>
        [TestMethod]
        public void TemplateFloatArgumentTest()
        {
            Symbol symbol;

            // Negative mantissa: -123
            symbol = VerifySymbolUndecoration("?F@@3V?$FTClass@$2?HL@3@@A");
            Assert.AreEqual("class FTClass<-1.23e4> F", symbol.ToString());

            // Zero exponent
            symbol = VerifySymbolUndecoration("?F@@3V?$FTClass@$2?HL@A@@@A");
            Assert.AreEqual("class FTClass<-1.23e0> F", symbol.ToString());

            // Negative exponent
            symbol = VerifySymbolUndecoration("?F@@3V?$FTClass@$2?HL@?0@@A");
            Assert.AreEqual("class FTClass<-1.23e-1> F", symbol.ToString());
        }



        [TestMethod]
        public void PointerToMemberCastOperatorTest()
        {
            // TODO: Simpler case of cast operator to member function pointer without all the templating to make this easier to understand

            // Complex case from the std library which is a user defined cast to a member function pointer
            //  public: std::basic_ostream<char,struct std::char_traits<char> >::sentry::operator int std::_Bool_struct<class std::basic_ostream<char,struct std::char_traits<char> > >::*(void) const
            Symbol symbol = VerifySymbolUndecoration(@"??Bsentry@?$basic_ostream@DU?$char_traits@D@std@@@std@@QBEPQ?$_Bool_struct@V?$basic_ostream@DU?$char_traits@D@std@@@std@@@2@HXZ",
                // UndecorateSymbolName gets the spacing around the member constness keyword wrong, so omit that, however it does
                // get the name only undecorate correct
                defaultOptions: UndecorateOptions.NoMemberStorageClass);

            //// This is the correct name only representation which has to include the class type, with any template parameters
            //// The basic form is "<scope>::operator <returnType> <memberFunc>::*"
            //// This isn't really a searchable symbol, and so the name doesn't matter that much, but it could be a call site
            //// so we need to be able to parse it to be able to create a record for it
            //// UndecorateSymbolName gets the name only decoding wrong by omitting the name of the class defining the operator
            string operatorName = "std::basic_ostream<char,std::char_traits<char> >::sentry::operator int std::_Bool_struct<std::basic_ostream<char,std::char_traits<char> > >::*";
            Assert.AreEqual(operatorName, symbol.ToString(UndecorateOptions.NameOnly));

            Assert.IsInstanceOfType(symbol, typeof(MemberFunctionSymbol));
            MemberFunctionSymbol memberFunc = (MemberFunctionSymbol)symbol;

            Assert.AreEqual(SymbolDecoder.CallingConvention.ThisCall, memberFunc.CallingConvention);
            Assert.AreEqual(MemberFunctionClassification.Normal, memberFunc.MemberClassification);
            Assert.AreEqual(MemberProtectionLevel.Public, memberFunc.ProtectionLevel);
            Assert.IsFalse(memberFunc.IsVarArgs);

            Assert.AreEqual("operator int std::_Bool_struct<std::basic_ostream<char,std::char_traits<char> > >::*", memberFunc.Name);
            Assert.AreEqual("std::basic_ostream<char,std::char_traits<char> >::sentry", memberFunc.TypeName);
            // Parameterless functions will be encoded as hanving a single void parameter
            var parms = memberFunc.Parameters;
            Assert.AreEqual(1, parms.Count());
            Assert.AreEqual("void", parms.First().Name);
            // The return type is a pointer to an integer member - as this is a cast operator this is essentially the operator name without the operator prefix
            Assert.AreEqual("int std::_Bool_struct<std::basic_ostream<char,std::char_traits<char> > >::*", memberFunc.ReturnType.Name);

            // TODO: Doubly indirected
        }

        [TestMethod]
        public void GlobalFunctionNamesTest()
        {
            GlobalFunctionSymbol function;

            // Just about the most simple case
            //  int __cdecl wibble(int)
            function = VerifyGlobalFunctionSymbolParsing("?wibble@@YAHH@Z", "wibble", "int", new String[] { "int" });
            Assert.AreEqual("int __cdecl wibble(int)", function.ToString());
            Assert.IsFalse(function.IsFar);

            // Same, but far (this is no longer used, but still present in the encoding)
            //  int __cdecl wibble(int)
            function = VerifyGlobalFunctionSymbolParsing("?wibble@@ZAHH@Z", "wibble", "int", new String[] { "int" });
            Assert.AreEqual("int __cdecl wibble(int)", function.ToString());
            Assert.IsTrue(function.IsFar);

            // No parameters (encoded as single void parameter, so the parameter list can never be empty). 
            function = VerifyGlobalFunctionSymbolParsing("?blah@@YAXXZ", "blah", "void");
            Assert.AreEqual("void __cdecl blah(void)", function.ToString());

            // Var args (only)
            function = VerifyGlobalFunctionSymbolParsing("?blah@@YAXZZ", "blah", "void", new string[0], isVarVargs: true);
            Assert.AreEqual("void __cdecl blah(...)", function.ToString());

            // Var args (as well as reference and pointer and pointer to reference)
            function = VerifyGlobalFunctionSymbolParsing("?blah@@YAXAA_NPAOAAPA_DZZ", "blah", "void", new string[] { "bool &", "long double *", "__int8 * &" }, isVarVargs: true);
            Assert.AreEqual("void __cdecl blah(bool &,long double *,__int8 * &,...)", function.ToString());

            // Extended parameter and return types (_J = int64, _N = bool):
            function = VerifyGlobalFunctionSymbolParsing("?B@@YGX_JPA_N@Z", "B", "void",
                new string[] { "__int64", "bool *" }, callingConvention: CallingConvention.StdCall);
            Assert.AreEqual("void __stdcall B(__int64,bool *)", function.ToString());
        }

        [TestMethod]
        public void TemplatedFunctionsTest()
        {
            BaseSymbolNode[] templateArgs;

            // Simple templated function case (the function itself is templated, but params/return type are not)
            templateArgs = VerifyGlobalTemplateFunc("??$Fn@_N@@YA_N_W@Z", "bool Fn<bool>(wchar_t)", new string[] { "bool" }, returnType: "bool", funcArgs: new string[] { "wchar_t" });
            // NameOnly printing undecorated represention includes the template params (these are part of the template instance type's name)
            Assert.AreEqual("Fn<bool>", templateArgs[0].SymbolNode.ToString(UndecorateOptions.NameOnly));

            // No template parameters specified (is this really valid?)
            templateArgs = VerifyGlobalTemplateFunc("??$Fn@@@YA_N_W@Z", "bool Fn<>(wchar_t)", new string[0], returnType: "bool", funcArgs: new string[] { "wchar_t" });
            Assert.AreEqual(0, templateArgs.Length);

            // TODO: Templated function parameter
            // TODO: Templated function return type

            // TODO: Complex template type for templated function
            // TODO: Template type with nested template
        }

        [TestMethod]
        public void TemplateAddressArgumentTest()
        {
            FunctionSymbol function;

            // Null address param (this is the '$1@' sequence)
            function = VerifyGlobalFunctionSymbolParsing("??$F@$1@@@YAXXZ", "F", "void", new string[] { "void" });
            Assert.AreEqual("void __cdecl F<NULL>(void)", function.ToString());

            // Null address param among other params
            function = VerifyGlobalFunctionSymbolParsing("??$F@D$1@F@@YAXXZ", "F", "void", new string[] { "void" });
            Assert.AreEqual("void __cdecl F<char,NULL,short>(void)", function.ToString());

            // Address-of template parameter that is a nested templated symbol
            // A real and rather complicated case from ATL:
            //
            // template <class StringType, class Helper, typename Helper::ReturnType (WINAPI *pFunc)(HINSTANCE, LPCDLGTEMPLATE, HWND,DLGPROC, LPARAM)>
            //   typename Helper::ReturnType AtlAxDialogCreateT(
            //       _In_ HINSTANCE hInstance,
            //       _In_z_ StringType lpTemplateName,
            //       _In_ HWND hWndParent,
            //       _In_ DLGPROC lpDialogProc,
            //       _In_ LPARAM dwInitParam)
            //
            //  ATLINLINE ATLAPI_(HWND) AtlAxCreateDialogW(...
            //  {
            //      return AtlAxDialogCreateT<LPCWSTR, _AtlCreateDialogIndirectParamHelper, CreateDialogIndirectParamW>(...
            //
            function = VerifyGlobalFunctionSymbolParsing("??$AtlAxDialogCreateT@PBGV_AtlCreateDialogIndirectParamHelper@ATL@@$1?CreateDialogIndirectParamW@@YGPAUHWND__@@PAUHINSTANCE__@@PBUDLGTEMPLATE@@PAU4@P6GH2IIJ@ZJ@Z@ATL@@YAPAUHWND__@@PAUHINSTANCE__@@PBGPAU1@P6GH2IIJ@ZJ@Z",
                "AtlAxDialogCreateT", "struct HWND__ *",
                new string[] {
                        "struct HINSTANCE__ *",     // HINSTANCE hInstance
                        "unsigned short const *",   // LPCWSTR lpTemplateName
                        "struct HWND__ *",          // HWND hWndParent
                        "int (__stdcall *)(struct HWND__ *,unsigned int,unsigned int,long)", // DLGPROC lpDialogProc
                        "long",                     // LPARAM dwInitParam
                });

        }

        [TestMethod]
        public void TemplatedTypeTest()
        {
            // TODO: Tests for template types
        }

        [TestMethod]
        [Ignore]             // Currently not working, apparently due to UndecorateSymbolName bug
        public void TemplateFnPtrParamTypeOnlyTest()
        {
            Symbol symbol;

            symbol = this.VerifySymbolUndecoration("?f@?$A@UBase@?1??main@@YAHXZ@$1?f@1?1??2@YAHXZ@QAEXXZ@@QAEXXZ", UndecorateOptions.TypeOnly);
            Assert.AreEqual("A<Base,Base::f>", symbol.ToString());
        }

        [TestMethod]
        public void FunctionPointerArgsTest()
        {
            GlobalFunctionSymbol function;

            // Function pointer is indicated as a special storage class following the 'P' (etc) denoting a pointer type

            // Function pointer types: 6, 7

            // Function with a single function pointer argument
            function = VerifyGlobalFunctionSymbolParsing("?f@@YAMP6GFD@Z@Z", "f", "float", new string[] { "short (__stdcall *)(char)" });
            Assert.AreEqual("float __cdecl f(short (__stdcall *)(char))", function.ToString());

            // Slight variation of storage modifiers on the function pointer and calling convention

            function = VerifyGlobalFunctionSymbolParsing("?fcv@@YAMS7AFD@Z@Z", "fcv", "float", new string[] { "short (__cdecl * const volatile)(char)" });
            Assert.AreEqual("float __cdecl fcv(short (__cdecl * const volatile)(char))", function.ToString());

            // Based function pointer types, _A, _B
            function = VerifyGlobalFunctionSymbolParsing("?fb@@YAMP_A2bp@@GFD@Z@Z", "fb", "float", new string[] { "short (__stdcall __based(bp) *)(char)" });
            Assert.AreEqual("float __cdecl fb(short (__stdcall __based(bp) *)(char))", function.ToString());

            function = VerifyGlobalFunctionSymbolParsing("?fbcv@@YAMS_B0GFD@Z@Z", "fbcv", "float", new string[] { "short (__stdcall __based(void) * const volatile)(char)" });
            Assert.AreEqual("float __cdecl fbcv(short (__stdcall __based(void) * const volatile)(char))", function.ToString());

            // TODO: Member function pointer types: 8, 9

            // TODO: Based Member function pointer types: _C, _D

            // TODO: Doubly indirected
        }

        [TestMethod]
        public void ExtendedFunctionTypeTest()
        {
            BaseSymbolNode[] templateArgs = VerifyGlobalTemplateFunc("??$Fn@$$A6AMN@Z@@YAHI@Z", "int Fn<float (double)>(unsigned int)", new string[] { "float __cdecl(double)" }, returnType: "int", funcArgs: new string[] { "unsigned int" });

        }

        [TestMethod]
        public void MemberFunctionTypesTest()
        {
            MemberFunctionSymbol function;

            // Just about the most simple case of a member function, but we are testing protection levels and member function type here
            for (int ch = 'A'; ch < 'Z'; ch++)
            {
                MemberProtectionLevel protection = MemberProtectionLevel.Private;
                MemberFunctionClassification memberClass = MemberFunctionClassification.Normal;
                bool invalid = false;
                string symbolFormat = "?f@C@@{0}AAHH@Z";
                string memberClassQualifier = "";

                // Member function encodings
                switch (ch)
                {
                    case 'A': // private member function
                    case 'B': // private far member function   
                        protection = MemberProtectionLevel.Private;
                        memberClass = MemberFunctionClassification.Normal;
                        break;
                    case 'C': // private static member function
                    case 'D': // private static far member function
                        protection = MemberProtectionLevel.Private;
                        memberClass = MemberFunctionClassification.Static;
                        memberClassQualifier = "static ";
                        symbolFormat = "?f@C@@{0}AHH@Z";
                        break;
                    case 'E': // private virtual member function
                    case 'F': // private virtual far member function
                        protection = MemberProtectionLevel.Private;
                        memberClass = MemberFunctionClassification.Virtual;
                        memberClassQualifier = "virtual ";
                        break;

                    case 'G':
                    case 'H':
                    case 'O':
                    case 'P':
                    case 'W':
                    case 'X':
                    case 'Y':
                    case 'Z':
                        invalid = true;
                        break;

                    case 'I': // protected member function       
                    case 'J': // protected far member function  
                        protection = MemberProtectionLevel.Protected;
                        memberClass = MemberFunctionClassification.Normal;
                        break;
                    case 'K': // protected static member function
                    case 'L': // protected static far member function
                        protection = MemberProtectionLevel.Protected;
                        memberClass = MemberFunctionClassification.Static;
                        memberClassQualifier = "static ";
                        symbolFormat = "?f@C@@{0}AHH@Z";
                        break;
                    case 'M': // protected virtual member function
                    case 'N': // protected virtual far member function
                        protection = MemberProtectionLevel.Protected;
                        memberClass = MemberFunctionClassification.Virtual;
                        memberClassQualifier = "virtual ";
                        break;

                    case 'Q': // public member function       
                    case 'R': // public far member function   
                        protection = MemberProtectionLevel.Public;
                        memberClass = MemberFunctionClassification.Normal;
                        break;
                    case 'S': // public static member function
                    case 'T': // public static far member function
                        protection = MemberProtectionLevel.Public;
                        memberClass = MemberFunctionClassification.Static;
                        memberClassQualifier = "static ";
                        symbolFormat = "?f@C@@{0}AHH@Z";
                        break;
                    case 'U': // public virtual member function
                    case 'V': // public virtual far member function
                        protection = MemberProtectionLevel.Public;
                        memberClass = MemberFunctionClassification.Virtual;
                        memberClassQualifier = "virtual ";
                        break;
                }
                string symbol = string.Format(symbolFormat, (char)ch);
                if (invalid)
                {
                    ExceptionAssert.Expect<InvalidSymbolNameException>(() => Parser.Parse(symbol));
                }
                else
                {
                    function = VerifyMemberFunctionSymbolParsing(symbol, "f", "C", "int", new String[] { "int" },
                        memberType: memberClass, protectionLevel: protection);
                    string expected = string.Format("{0}: {1}int __cdecl C::f(int)", protection.ToString().ToLowerInvariant(), memberClassQualifier);
                    string actual = function.ToString();
                    Assert.AreEqual(expected, actual);
                }
            }
        }

        private GlobalFunctionSymbol VerifyGlobalFunctionSymbolParsing(string symbolName, string functionName,
            string returnType, string[] paramTypeNames = null, CallingConvention callingConvention = CallingConvention.Cdecl, bool isVarVargs = false,
            bool isSaveRegs = false)
        {
            FunctionSymbol func = VerifyFunctionSymbolParsing(symbolName, functionName, returnType, paramTypeNames, callingConvention, isVarVargs, isSaveRegs);
            Assert.IsInstanceOfType(func, typeof(GlobalFunctionSymbol), "Failed to parse as global function");
            return (GlobalFunctionSymbol)func;
        }

        private FunctionSymbol VerifyFunctionSymbolParsing(string symbolName, string functionName, string returnType, string[] paramTypeNames, CallingConvention callingConvention, bool isVarVargs, bool isSaveRegs)
        {
            Symbol symbol = VerifySymbolUndecoration(symbolName);
            Assert.AreEqual(functionName, symbol.Name);
            Assert.IsInstanceOfType(symbol, typeof(FunctionSymbol), "Failed to parse as function");
            FunctionSymbol function = (FunctionSymbol)symbol;
            Assert.AreEqual(callingConvention, function.CallingConvention, "Incorrect calling convention");
            if (returnType == null)
            {
                Assert.IsNull(function.ReturnType);
            }
            else
            {
                Assert.AreEqual(symbolName, function.ReturnType.SymbolName);
                Assert.AreEqual(returnType, function.ReturnType.ToString(), "Incorrect return type");
            }
            Assert.AreEqual(isVarVargs, function.IsVarArgs);
            Assert.AreEqual(isSaveRegs, function.FunctionType.IsSaveRegs);
            if (paramTypeNames == null)
            {
                Assert.AreEqual(1, function.Parameters.Count());
                Assert.AreEqual("void", function.Parameters.First().ToString());
            }
            else
            {
                CollectionAssert.AreEqual(paramTypeNames, function.Parameters.Select(param => param.ToString()).ToArray());
            }
            return function;
        }

        private MemberFunctionSymbol VerifyMemberFunctionSymbolParsing(string symbolName, string functionName, string className,
            string returnType, string[] paramTypeNames = null, CallingConvention callingConvention = CallingConvention.Cdecl, bool isVarVargs = false,
            MemberProtectionLevel protectionLevel = MemberProtectionLevel.Private, MemberFunctionClassification memberType = MemberFunctionClassification.Normal,
            StorageClass memberStorageClass = StorageClass.None,
            bool isSaveRegs = false)
        {
            FunctionSymbol func = VerifyFunctionSymbolParsing(symbolName, functionName, returnType, paramTypeNames, callingConvention, isVarVargs, isSaveRegs);
            Assert.IsInstanceOfType(func, typeof(MemberFunctionSymbol), "Failed to parse as member function");
            MemberFunctionSymbol memberFunc = (MemberFunctionSymbol)func;
            Assert.AreEqual(className, memberFunc.TypeName, "Incorrect type name for member function");
            Assert.AreEqual(protectionLevel, memberFunc.ProtectionLevel);
            Assert.AreEqual(memberType, memberFunc.MemberClassification);
            Assert.AreEqual(memberStorageClass, memberFunc.StorageClassification);
            return memberFunc;
        }

        [TestMethod]
        public void NameBackReferenceTest()
        {
            Symbol symbol;

            // A long enough name with multiple id's to test each position and which demonstrates that the back reference is just 
            // to the name (we have the same function name as a type name, and the type name is therefore a back reference to the 
            // function name, which will always be the id with encoding '0' in a function symbol. Also covers multiple back
            // ref parts forming a composite name, and exceeding the maximum number of back refs (10); id10 and id11 cannot be encoded

            // struct id1::id2 __cdecl id1::id0(struct id1::id2,class id1::id3,class id4::id3,class id1::id0,class id4::id5,class id4::id6 *,struct id1::id2 &,class id7::id8::id9,class id7::id8::id9 * &,enum id7::id8::id10,enum id7::id8::id10 *,union id7::id8::id11,union id7::id8::id11 *,class id4::id6,class id4::id5)
            symbol = VerifySymbolUndecoration("?id0@id1@@YA?AUid2@1@U21@Vid3@1@V3id4@@V01@Vid5@4@PAVid6@4@AAU21@Vid9@id8@id7@@AAPAV789@W4id10@89@PAW4id10@89@Tid11@89@PATid11@89@V64@4@Z");
            Assert.AreEqual("id0", symbol.Name);
            Assert.AreEqual("id1", symbol.ScopeName);
            Assert.IsInstanceOfType(symbol, typeof(FunctionSymbol));
        }

        [TestMethod]
        [Description("Test parsing of symbols for involving function points with function pointer return and argument types")]
        public void NestedFunctionPointersTest()
        {
            Symbol symbol;

            // Complex case of multiply nested function pointers both as argument and return types
            // typedef int (__stdcall *fp)(char*);
            // typedef long (__cdecl *fp2)(wchar_t*);
            // typedef fp2 (__fastcall *fp3)(fp);
            // fp3 rfp(fp2 (__cdecl*)(fp3 ));
            symbol = Parser.Parse(@"?rfp@@YAP6IP6AJPA_W@ZP6GHPAD@Z@ZP6AP6AJ0@ZP6IP6AJ0@Z2@Z@Z@Z");
            // UnDecorateSymbolName gets this completely wrong - so much so that I don't want to emulate this particular bug
            // as the result is structurally incorrect
            string expected = "long (__cdecl *)(wchar_t *) (__fastcall *)(int (__stdcall *)(char *)) __cdecl rfp(long (__cdecl *)(wchar_t *) (__cdecl *)(long (__cdecl *)(wchar_t *) (__fastcall *)(int (__stdcall *)(char *))))";
            string actual = symbol.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        [Description("Test parsing of symbols for member functions of parametric types")]
        public void TemplatedMemberFunctionsTest()
        {
            Symbol symbol;

            // Note empty parameter list (void), which means there is no parameter list terminator
            symbol = VerifySymbolUndecoration(@"?xyz@?$def@V?$abc@H@@PAX@@QAEXXZ");
            Assert.AreEqual("public: void def<class abc<int>,void *>::xyz(void)", symbol.ToString(UndecorateOptions.NoCallingConvention));

            // With namespace qualification - note the namespace follows the parameter types
            // Note: Has some extra storage modifiers to cover bug in UndecorateSymbolName whereby
            // it includes these in the undecorated name even in name-only mode if they are on a template
            // argument, but not otherwise
            symbol = VerifySymbolUndecoration(@"?xyz@?$def@V?$abc@H@ns1@@PIFEBX@ns2@@QFIEAEXXZ");
            Assert.AreEqual("public: void ns2::def<class ns1::abc<int>,void const *>::xyz(void)", symbol.ToString(UndecorateOptions.NoCallingConvention | UndecorateOptions.NoMsftExtensions));

            // An interesting case with: 
            //  - Multiple namespace qualified templates
            //  - Nested template back ref to same templated type used earlier in template params list
            //  - Same templated type used as arg to function, but not a backref as different scope
            symbol = VerifySymbolUndecoration(@"?xyz@?$def@V?$abc@H@@AAPAHV1@@@QAEXV?$abc@H@@@Z");
            Assert.AreEqual("public: void def<class abc<int>,int * &,class abc<int> >::xyz(class abc<int>)", symbol.ToString(UndecorateOptions.NoCallingConvention));
        }

        [TestMethod]
        [Description("Test compiler generated RTTI metatype symbols")]
        public void RttiTest()
        {
            RttiNameNode rttiName;

            rttiName = VerifyRttiUndecoration(@"??_R0?AUAbc@@@8", "`struct Abc RTTI Type Descriptor'");
            Assert.AreEqual("`Abc RTTI Type Descriptor'", rttiName.Name);
            Assert.IsInstanceOfType(rttiName, typeof(RttiTypeDescriptorNode));
            var rttiTypeDescriptor = (RttiTypeDescriptorNode)rttiName;
            Assert.AreEqual("Abc", rttiTypeDescriptor.DescribedType.Name);

            rttiName = VerifyRttiUndecoration(@"??_R1A@?0B@C@Abc@@8", "Abc::`RTTI Base Class Descriptor at (0,-1,1,2)'");
            Assert.AreEqual("`RTTI Base Class Descriptor'", rttiName.Name);
            Assert.IsInstanceOfType(rttiName, typeof(RttiBaseClassDescriptorNode));
            var rttiBaseClass = (RttiBaseClassDescriptorNode)rttiName;
            CollectionAssert.Equals(new Int64[] { 0, -1, 1, 2 }, rttiBaseClass.Dimensions);

            rttiName = VerifyRttiUndecoration(@"??_R2Abc@@8", "Abc::`RTTI Base Class Array'");
            Assert.AreEqual("`RTTI Base Class Array'", rttiName.Name);

            rttiName = VerifyRttiUndecoration(@"??_R3Abc@@8", "Abc::`RTTI Class Hierarchy Descriptor'");
            Assert.AreEqual("`RTTI Class Hierarchy Descriptor'", rttiName.Name);

            rttiName = VerifyRttiUndecoration(@"??_R4Abc@@8", "Abc::`RTTI Complete Object Locator'");
            Assert.AreEqual("`RTTI Complete Object Locator'", rttiName.Name);

            ExpectParseError(@"??_R5Abc@@8", ParseErrors.InvalidRttiCode, '5', 5);
            ExpectParseError(@"??_RAAbc@@8", ParseErrors.InvalidRttiCode, 'A', 5);
            ExpectParseError(@"??_R_Abc@@8", ParseErrors.InvalidRttiCode, '_', 5);
        }

        private RttiNameNode VerifyRttiUndecoration(string symbolName, string unmangledName)
        {
            Symbol symbol = VerifySymbolUndecoration(symbolName);
            Assert.AreEqual(unmangledName, symbol.ToString());
            Assert.IsInstanceOfType(symbol, typeof(SpecialDataSymbol));
            var rttiSymbol = (SpecialDataSymbol)symbol;
            Assert.IsInstanceOfType(rttiSymbol.QualifiedName.Identifier, typeof(RttiNameNode));
            RttiNameNode rttiNode = (RttiNameNode)rttiSymbol.QualifiedName.Identifier;
            return rttiNode;
        }

        [TestMethod]
        public void VtblTest()
        {
            SpecialDataSymbol symbol;

            symbol = VerifyVtblUndecoration(@"?vtbl@@6EA@", "vtbl");
            symbol = VerifyVtblUndecoration(@"?vtbl@x@@6EAAbc@Def@@@", "x::vtbl{for `Def::Abc'}");
        }

        private SpecialDataSymbol VerifyVtblUndecoration(string symbolName, string unmangledName)
        {
            Symbol symbol = VerifySymbolUndecoration(symbolName);
            Assert.AreEqual(unmangledName, symbol.ToString());
            Assert.IsInstanceOfType(symbol, typeof(VtblSymbol));
            var vftableSymbol = (VtblSymbol)symbol;
            return vftableSymbol;
        }

        #endregion

        #region Helpers

        [Interop.DllImport("dbghelp.dll", SetLastError = true, PreserveSig = true)]
        private static extern int UnDecorateSymbolName(
            [Interop.In] [Interop.MarshalAs(Interop.UnmanagedType.LPStr)] string DecoratedName,
            [Interop.Out] StringBuilder UnDecoratedName,
            [Interop.In] [Interop.MarshalAs(Interop.UnmanagedType.U4)] int UndecoratedLength,
            [Interop.In] [Interop.MarshalAs(Interop.UnmanagedType.U4)] UndecorateOptions Flags);

        protected override String Undecorate(String decoratedName, UndecorateOptions options = UndecorateOptions.None)
        {
            StringBuilder buf = new StringBuilder(4096);
            int len = UnDecorateSymbolName(decoratedName, buf, buf.Capacity, options);
            if (len == 0)
            {
                throw new System.ComponentModel.Win32Exception(string.Format("Invalid symbol name: {0}", decoratedName));
            }

            string undecorateSymbolNameResult = buf.ToString(0, len);

            return undecorateSymbolNameResult;
        }


        #endregion
    }
}
