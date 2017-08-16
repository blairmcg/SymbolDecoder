using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SymbolDecoder.UnitTests
{
    /// <summary>
    /// Basic unit tests of the symbol node class hierarchy. Note that most coverage is through the 
    /// SymbolParser tests.
    /// </summary>
    [TestClass]
    public class SymbolNodesTests
    {
        #region Test Methods

        [TestMethod]
        public void ConstructorsArgsTest()
        {
            ExceptionAssert.Expect<ArgumentNullException>(() => new IdentifierNode(null));
            ExceptionAssert.Expect<ArgumentNullException>(() => new CompoundTypeNode(CompoundTypeClass.Class, null));

            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new PrimitiveTypeNode((PrimitiveTypeCodes)(-1)));
            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new PrimitiveTypeNode((PrimitiveTypeCodes)52));
            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new PrimitiveTypeNode((PrimitiveTypeCodes)53));

            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new OperatorNameNode((Operator)(-1)));
            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new OperatorNameNode((Operator)43));
            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new OperatorNameNode((Operator)44));

            ExceptionAssert.Expect<ArgumentNullException>(() => new QualifiedNameNode(null));
            ExceptionAssert.Expect<ArgumentNullException>(() => new PointerTypeNode(null));

            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new RttiNameNode((RttiCode)(-1)));
            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new RttiNameNode((RttiCode)5));

            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new SpecialNameNode(CompilerSpecialName.Vftable - 1));
            ExceptionAssert.Expect<ArgumentOutOfRangeException>(() => new SpecialNameNode(CompilerSpecialName.LocalStaticThreadGuard + ('Z' - 'K' + 1) + 1));

            ExceptionAssert.Expect<ArgumentNullException>(() => new StorageClassNode(StorageClass.Member, null));

            ExceptionAssert.Expect<ArgumentNullException>(() => new BasedStorageClassNode(StorageClass.None, null));

        }

        [Description("Test setting node parent to null, existing parent, setting to different parent, etc")]
        [TestMethod]
        public void SetNodeParentTest()
        {
            var child1 = new IdentifierNode("blah");
            var child2 = new IdentifierNode("wibble");

            var parent1 = new QualifiedNameNode(child1);
            Assert.AreSame(parent1, child1.Parent);
            var parent2 = new QualifiedNameNode(child2);
            Assert.AreSame(parent2, child2.Parent);

            // Parent cannot be nulled once set
            ExceptionAssert.Expect<ArgumentNullException>(() => child1.Parent = null);

            // Ok to set to same parent though
            child1.Parent = parent1;

            // Changing parent directly is an error
            ExceptionAssert.Expect<InvalidOperationException>(() => child2.Parent = parent1);
        }

        [TestMethod]
        public void SetStorageModifiersTest()
        {
            var variable = new GlobalVariableSymbol("blah", new QualifiedNameNode(new IdentifierNode("blah")));
            VerifySetStorageModifiers(new PrivateObject(variable, new PrivateType(typeof(DataSymbol))));

            var pointerNode = new PointerTypeNode(new PrimitiveTypeNode(PrimitiveTypeCodes.Int));
            VerifySetStorageModifiers(new PrivateObject(pointerNode, new PrivateType(typeof(IndirectionTypeNode))));

            var memberFunc = new MemberFunctionSymbol("blah", new QualifiedNameNode(new IdentifierNode("blah")));
            VerifySetStorageModifiers(new PrivateObject(memberFunc));

        }

        private static void VerifySetStorageModifiers(PrivateObject obj)
        {
            IEnumerable<StorageModifierNode> modifiers = (IEnumerable<StorageModifierNode>)obj.GetProperty("StorageModifiers");
            Assert.IsNotNull(modifiers);
            Assert.AreEqual(0, modifiers.Count());
            // Here testing an internal implementation detail, that the empty list is not stored
            Assert.IsNull(obj.GetField("storageModifiers"));

            // Set to null
            obj.SetProperty("StorageModifiers", null);
            modifiers = (IEnumerable<StorageModifierNode>)obj.GetProperty("StorageModifiers");
            Assert.IsNotNull(modifiers);
            Assert.AreEqual(0, modifiers.Count());
            Assert.IsNull(obj.GetField("storageModifiers"));

            // Set to empty list
            obj.SetProperty("StorageModifiers", new List<StorageModifierNode>());
            modifiers = (IEnumerable<StorageModifierNode>)obj.GetProperty("StorageModifiers");
            Assert.IsNotNull(modifiers);
            Assert.AreEqual(0, modifiers.Count());
            Assert.IsNull(obj.GetField("storageModifiers"));
        }

        [Description("Test setting function symbol type node to null")]
        [TestMethod]
        public void SetFunctionSymbolType()
        {
            var func = new GlobalFunctionSymbol("Blah", new QualifiedNameNode(new IdentifierNode("Blah")));
            ExceptionAssert.Expect<ArgumentNullException>(() => func.FunctionType = null);
        }

        [Description("Test Equals and GetHashCode")]
        [TestMethod]
        public void NameNodeComparisonTest()
        {
            var ident1 = new IdentifierNode("Blah");
            var ident2 = new IdentifierNode("blah");
            var ident3 = new IdentifierNode("Frobble");
            var ident4 = new IdentifierNode("Blah");

            VerifyComparison(ident1, ident2, ident3, ident4);
        }

        private static void VerifyComparison(NameNode ident1, NameNode ident2, NameNode ident3, NameNode ident1a)
        {
            Assert.IsTrue(ident1.Equals(ident1));
            Assert.IsTrue(ident1.Equals(ident1a));
            Assert.IsFalse(ident1.Equals(null));
            Assert.IsFalse(ident1.Equals(new Object()));
            Assert.IsFalse(ident1.Equals(ident2));
            Assert.IsFalse(ident1.Equals(ident3));
            Assert.AreEqual(ident1.GetHashCode(), ident1a.GetHashCode());
            Assert.AreNotEqual(ident1.GetHashCode(), ident2.GetHashCode());
            Assert.AreNotEqual(ident1.GetHashCode(), ident3.GetHashCode());
        }

        [TestMethod]
        public void QualifiedNameNodeComparisonTest()
        {
            var ident1 = new QualifiedNameNode(new IdentifierNode("Blah"));
            var scope1 = new List<NameNode>();
            scope1.Add(new IdentifierNode("Ns1"));
            scope1.Add(new IdentifierNode("Class1"));
            new PrivateObject(ident1).Invoke("SetQualifiers", scope1);
            var ident2 = new QualifiedNameNode(new IdentifierNode("Blah"));
            
            var ident3 = new QualifiedNameNode(new IdentifierNode("Frobble"));
            var ident4 = QualifiedNameNode.Copy(ident1);
            VerifyComparison(ident1, ident2, ident3, ident4);
        }

        [TestMethod]
        public void SpecialNameNodeNameTest()
        {
            SpecialNameNode specialName;
            // Test the enum values where there are gaps to check they map correctly
            specialName = new SpecialNameNode(CompilerSpecialName.Vftable);
            Assert.AreEqual("`vftable'", specialName.Name);
            specialName = new SpecialNameNode(CompilerSpecialName.ManagedVectorConstructorIterator);
            Assert.AreEqual("`managed vector constructor iterator'", specialName.Name);

            // Unused at time of writing, but quite likely to be added as new compiler generated functions are added
            specialName = new SpecialNameNode(CompilerSpecialName.PlacementDeleteArrayClosure + 1);
            Assert.AreEqual("`<unknown: _Z>'", specialName.Name);
            specialName = new SpecialNameNode(CompilerSpecialName.PlacementDeleteArrayClosure + 2);
            Assert.AreEqual("`<unknown: __0>'", specialName.Name);
            specialName = new SpecialNameNode(CompilerSpecialName.LocalStaticThreadGuard + 1);
            Assert.AreEqual("`<unknown: __K>'", specialName.Name);
            specialName = new SpecialNameNode(CompilerSpecialName.LocalStaticThreadGuard + ('Z' - 'K' + 1));
            Assert.AreEqual("`<unknown: __Z>'", specialName.Name);
        }
        #endregion

    }
}
