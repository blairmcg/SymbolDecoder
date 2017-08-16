using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace SymbolDecoder
{
    /// <summary>
    /// Base class of symbol AST nodes
    /// </summary>
    public abstract class BaseSymbolNode
    {
        #region Fields
        /// <summary>
        /// Pointer to parent node
        /// </summary>
        private BaseSymbolNode parent;
        #endregion

        #region Properties
        /// <summary>
        /// Parent node of this node - note that even symbols themselves may appear nested in the definitions of
        /// other symbols, so all symbol nodes may have a parent
        /// </summary>
        /// <remarks>Null only for a root symbol node</remarks>
        public BaseSymbolNode Parent
        {
            get
            {
                return this.parent;
            }
            internal set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (this.parent != null && this.parent != value) throw new InvalidOperationException();
                this.parent = value;
            }
        }

        /// <summary>
        /// The raw/mangled/symbolic representation of the artefact name
        /// </summary>
        public virtual string SymbolName
        {
            get
            {
                // Walk up to the symbol node at the root which stores the symbolic name
                return this.SymbolNode.SymbolName;
            }
        }

        /// <summary>
        /// The top node of the AST sub-tree that represents the root of the symbol of which this node
        /// is a part. The symbol itself may not be the root of the entire tree, as symbols can be nested.
        /// </summary>
        public virtual Symbol SymbolNode
        {
            get
            {
                return this.Parent.SymbolNode;
            }
        }

        #endregion

        #region Displaying
        /// <summary>
        /// Append the undecorated textual representation of this node to the StringBuilder argument
        /// </summary>
        /// <param name="builder">C++ name builder</param>
        public void DisplayOn(CppNameBuilder builder)
        {
            this.DisplayOn(builder, CppNameBuilder.Spacing.None);
        }

        public abstract bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing);

        /// <summary>
        /// Build the complete undecorated representation of this mangled name
        /// </summary>
        /// <returns>The undecorated representation</returns>
        public override string ToString()
        {
            return this.ToString(UndecorateOptions.NoUndnameEmulation | UndecorateOptions.NoPtr64);
        }

        /// <summary>
        /// Bulid an undecorated representation of this mangled name, controlled by the options to include/exclude
        /// certain components of the complete name.
        /// </summary>
        /// <param name="options">The options to apply when printing the name - note these are the same flags as used with the UndecorateSymbliName Windows API</param>
        /// <returns>Undecorated string representation with the requested format</returns>
        public string ToString(UndecorateOptions options)
        {
            CppNameBuilder context = new CppNameBuilder(options);
            this.DisplayOn(context);
            return context.ToString();
        }
        #endregion

        #region Copying

        // Unfortunately we need to be able to copy nodes to implement the back ref mechanism in the 
        // symbolic names. Since the back refs can be to arbitrarily complex template names we have to be
        // able to copy pretty much all nodes, although most of the time we're just copying simple
        // identifiers.
        // An alternative approach which would avoid the need to add a copying mechanism would be to 
        // store the original text interval, and then re-parse to create the copy.

        /// <summary>
        /// Creates a clone of the specified node which is a copy deep enough only to orphan it from 
        /// that objects parent. Essentially this means that the parent link is cut, and any childe nodes 
        /// must be copied in the same way.
        /// </summary>
        /// <param name="source">The object to copy</param>
        /// <typeparam name="T">The type of symbol node being copied</typeparam>
        /// <returns>A copy of the node</returns>
        public static T Copy<T>(T source) where T : BaseSymbolNode
        {
            return source == null ? null : (T)source.DeepenShallowCopy((T)source.MemberwiseClone());
        }

        /// <summary>
        /// Deepen the memberwise copy of this object which is the argument so as to create an independent
        /// copy which can be safely added to the AST elsewhere
        /// </summary>
        /// <param name="shallowCopy">The copy to deepen</param>
        /// <returns>The deepened copy, usually the argument</returns>
        protected virtual BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            shallowCopy.parent = null;
            return shallowCopy;
        }

        #endregion
    }

    /// <summary>
    /// AST node representing an entire symbol, i.e. root of the AST for a symbol
    /// </summary>
    public class Symbol : BaseSymbolNode
    {
        #region Fields
        private readonly string symbolicName;
        private QualifiedNameNode qualifiedName;
        #endregion

        #region Constructors
        public Symbol(string symbolicName, QualifiedNameNode qualifiedName)
        {
            if (symbolicName == null) throw new ArgumentNullException(nameof(symbolicName));
            this.symbolicName = symbolicName;
            this.QualifiedName = qualifiedName;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The raw/mangled/decorated symbol name
        /// </summary>
        public override string SymbolName
        {
            get
            {
                return this.symbolicName;
            }
        }

        /// <summary>
        /// The root of this symbol's AST
        /// </summary>
        public override Symbol SymbolNode
        {
            get
            {
                return this;
            }
        }

        /// <summary>
        ///  Node reprenting the fully qualified name of the symbol
        /// </summary>
        public QualifiedNameNode QualifiedName
        {
            get
            {
                return this.qualifiedName;
            }
            private set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                this.qualifiedName = value;
                value.Parent = this;
            }
        }

        /// <summary>
        /// The primary (unqualified) name of the represented symbol
        /// </summary>
        public string Name
        {
            get
            {
                return this.QualifiedName.Identifier.Name;
            }
        }

        /// <summary>
        /// The scoping qualifiers of the symbol in C++ format (i.e. with double-colon separators).
        /// Note tht the individual components of the scope name may be either namespaces or class names
        /// as these are not distinguished. In the case of a class member symbol, however, we know that
        /// at least the final component is a type name.
        /// </summary>
        public string ScopeName
        {
            get
            {
                return this.QualifiedName.ScopeName;
            }
        }

        /// <summary>
        /// Is this a reserved CodeView name?
        /// </summary>
        /// <remarks>Not really sure what these are, and don't know how to cause compiler to generate one</remarks>
        public bool IsReservedCodeViewName { get; internal set; }

        /// <summary>
        /// Operator code for the function, or Operator.None if not an operator
        /// </summary>
        public Operator Operator
        {
            get
            {
                OperatorNameNode op = this.QualifiedName.Identifier as OperatorNameNode;
                return op == null ? Operator.None : op.Operator;
            }
        }


        #endregion

        #region Displaying
        public override sealed bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.LeadingSpace(spacing);
            if (this.IsReservedCodeViewName)
            {
                builder.Append("CV:", CppNameBuilder.Spacing.Trailing);
            }
            DisplayBodyOn(builder);
            builder.TrailingSpace(spacing);
            return true;
        }

        protected virtual void DisplayBodyOn(CppNameBuilder builder)
        {
            this.QualifiedName.DisplayOn(builder, CppNameBuilder.Spacing.None);
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is Symbol);
            Symbol copy = (Symbol)deepenedCopy;
            copy.QualifiedName = Copy(copy.qualifiedName);
            return copy;
        }
        #endregion
    }

    public abstract class NameNode : BaseSymbolNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        protected NameNode() { }
        #endregion

        #region Properties
        /// <summary>
        /// The short name string for this named node
        /// </summary>
        public abstract string Name { get; }
        #endregion

        #region Comparison

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this))
            {
                return true;
            }

            NameNode otherName = obj as NameNode;
            return otherName != null && otherName.Name == this.Name;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append(this.Name, spacing);
            return true;
        }
        #endregion
    }

    /// <summary>
    /// AST node representing a scoped name from a symbol. Essentially a qualified name is a primary identifier,
    /// and a list of qualifying names. The qualifying names may be the identifiers of namespaces and/or classes.
    /// Class names may be templatized.
    /// </summary>
    public class QualifiedNameNode : NameNode
    {
        #region Fields
        private NameNode identifier;
        private List<NameNode> qualifiers;
        private string scopeName;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="identifier">The final part of the qualified name</param>
        public QualifiedNameNode(NameNode identifier)
        {
            this.Identifier = identifier;
            this.SetQualifiers(null);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Node representing the final part of the qualified name, i.e. the unqualified name
        /// In many cases this will be simple identifier node, but it may be a complex structure
        /// if representing a templated artefact.
        /// </summary>
        public NameNode Identifier
        {
            get
            {
                return this.identifier;
            }
            internal set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                this.identifier = value;
                value.Parent = this;
            }
        }

        /// <summary>
        /// The nesting scope of the qualified name (that is class and enclosing namespace(s), both of which are option)
        /// in the nesting order, innermost towards outermost
        /// </summary>
        public IReadOnlyCollection<NameNode> Qualifiers { get { return this.qualifiers; } }

        /// <summary>
        /// The scope of this qualified name (i.e. the fully qualified name excluding the identifier)
        /// </summary>
        /// <remarks>Empty if at global scope</remarks>
        public string ScopeName
        {
            get
            {
                // Store for immediate access after first time
                if (scopeName == null)
                {
                    CppNameBuilder builder = new CppNameBuilder(UndecorateOptions.NameOnly | UndecorateOptions.NoUndnameEmulation | UndecorateOptions.NoPtr64);
                    this.DisplayScopeOn(builder);
                    scopeName = builder.ToString();
                }
                return scopeName;
            }
        }

        /// <summary>
        ///  The identifier string for this named node
        /// </summary>
        public override string Name
        {
            get
            {
                CppNameBuilder builder = new CppNameBuilder(UndecorateOptions.NameOnly | UndecorateOptions.NoUndnameEmulation | UndecorateOptions.NoPtr64);
                this.DisplayOn(builder);
                return builder.ToString();
            }
        }

        #endregion

        #region Helpers
        private void SetQualifiers(IEnumerable<NameNode> quals)
        {
            this.qualifiers = new List<NameNode>();
            if (quals != null)
            {
                foreach (NameNode qualifier in quals)
                {
                    AddQualifier(qualifier);
                }
            }
        }

        /// <summary>
        /// Add a qualifying name to the end of the list of qualifiers associated with this name
        /// </summary>
        /// <param name="qualifier"></param>
        internal void AddQualifier(NameNode qualifier)
        {
            this.qualifiers.Add(qualifier);
            qualifier.Parent = this;
        }
        #endregion

        #region Comparison

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            QualifiedNameNode otherName = obj as QualifiedNameNode;
            return otherName != null && otherName.Identifier.Equals(this.Identifier) &&
                otherName.ScopeName == this.ScopeName;
        }

        public override int GetHashCode()
        {
            return this.Identifier.GetHashCode() + this.ScopeName.GetHashCode();
        }

        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.LeadingSpace(spacing);
            if (DisplayScopeOn(builder))
            {
                builder.Append(CppNameBuilder.NameSeparator);
            }
            this.Identifier.DisplayOn(builder, spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }

        private bool DisplayScopeOn(CppNameBuilder context)
        {
            // Note that the qualifiers may be non-trivial "names" such as templated class instances
            return context.AppendAllWithSeparators(this.Qualifiers.Reverse(), CppNameBuilder.NameSeparator);
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is QualifiedNameNode);
            QualifiedNameNode copy = (QualifiedNameNode)deepenedCopy;
            copy.Identifier = Copy(copy.Identifier);
            copy.SetQualifiers(copy.Qualifiers.Select(each => Copy(each)));
            return shallowCopy;
        }
        #endregion
    }


    /// <summary>
    /// Node to represent a basic identifier (partial name)
    /// </summary>
    public class IdentifierNode : NameNode
    {
        #region Fields
        private readonly string name;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">The identifier name, can be empty in case of template name</param>
        public IdentifierNode(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            this.name = name;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The short name of the identifier
        /// </summary>
        public override string Name
        {
            get
            {
                return this.name;
            }
        }
        #endregion
    }

    internal class AnonymousNamespaceNode : IdentifierNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">The compiler generated namespace name</param>
        public AnonymousNamespaceNode(string name)
            : base(name)
        {
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            if (builder.HasOptions(UndecorateOptions.NoUndnameEmulation))
            {
                builder.Append(CppNameBuilder.SpecialNameStart, spacing & CppNameBuilder.Spacing.Leading);
                builder.Append("Anon$");
                base.DisplayOn(builder, spacing);
                builder.Append(CppNameBuilder.SpecialNameEnd, spacing & CppNameBuilder.Spacing.Leading);
            }
            else
            {
                builder.AppendSpecialName("anonymous namespace", spacing);
            }
            return true;
        }
        #endregion
    }

    /// <summary>
    /// Represents a namespace qualifier that is itself a full symbol. Occurs, for example, for a symbol associated 
    /// with a type declared within a function, in which case the name of the function forms part of the scope of 
    /// nested artefact.
    /// </summary>
    public class SpecialQualifierNode : NameNode
    {
        #region Fields
        BaseSymbolNode scopeNode;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        public SpecialQualifierNode(BaseSymbolNode scopeNode)
        {
            this.scopeNode = scopeNode;
        }
        #endregion

        #region Properties

        public BaseSymbolNode Scope
        {
            get
            {
                return this.scopeNode;
            }
            private set
            {
                value.Parent = this;
                this.scopeNode = value;
            }
        }

        public override string Name
        {
            get
            {
                return this.ToString(UndecorateOptions.NameOnly);
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append(CppNameBuilder.SpecialNameStart, spacing & CppNameBuilder.Spacing.Leading);
            this.Scope.DisplayOn(builder);
            builder.Append(CppNameBuilder.SpecialNameEnd, spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is SpecialQualifierNode);
            SpecialQualifierNode copy = (SpecialQualifierNode)deepenedCopy;
            copy.Scope = Copy(this.Scope);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// Class of nodes that perform a special purpose operation on a type, e.g. constructors and destructors
    /// </summary>
    public abstract class SpecialMemberFunctionNode : NameNode
    {
        protected NameNode DeclaringType
        {
            get
            {
                QualifiedNameNode parent = (QualifiedNameNode)this.Parent;
                return parent.Qualifiers.First();
            }
        }
    }

    /// <summary>
    /// Node to represent the special case of a constructor, the name of which is the name of the type it constructs
    /// </summary>
    public class ConstructorNode : SpecialMemberFunctionNode
    {
        #region Properties
        /// <summary>
        /// The short name of the destructor, i.e. <class-name>
        /// </summary>
        public override string Name
        {
            get
            {
                return this.DeclaringType.Name;
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            return this.DeclaringType.DisplayOn(builder, spacing);
        }
        #endregion
    }

    /// <summary>
    /// Node to represent the special case of a destructor, the name of which is based on the name of its defining type
    /// </summary>
    internal class DestructorNameNode : SpecialMemberFunctionNode
    {
        #region Properties
        /// <summary>
        /// The short name of the destructor, i.e. ~<class-name>
        /// </summary>
        public override string Name
        {
            get
            {
                return "~" + this.DeclaringType.Name;
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append("~", spacing & CppNameBuilder.Spacing.Leading);
            this.DeclaringType.DisplayOn(builder, spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }
        #endregion
    }

    public enum CompilerSpecialName
    {
        Vftable = 0,                               // _7
        Vbtable,                                   // _8
        Vcall,                                     // _9
        Typeof,                                    // _A
        LocalStaticGuard,                          // _B
        String,                                    // _C
        VbaseDestructor,                           // _D
        VectorDeletingDestructor,                  // _E
        DefaultConstructorClosure,                 // _F
        ScalarDeletingDestructor,                  // _G
        VectorConstructorIterator,                 // _H
        VectorDestructorIterator,                  // _I
        VectorVbaseConstructorIterator,            // _J
        VirtualDisplacementMap,                    // _K
        EhVectorConstructorIterator,               // _L
        EhVectorDestructorIterator,                // _M
        EhVectorVbaseConstructorIterator,          // _N
        CopyConstructorClosure,                    // _O
        UdtReturning,                              // _P
        Eh,                                        // _Q
        Rtti,                                      // _R
        LocalVftable,                              // _S
        LocalVftableConstructorClosure,            // _T
        NewArray,                                  // _U
        DeleteArray,                               // _V
        OmniCallsig,                               // _W
        PlacementDeleteClosure,                    // _X
        PlacementDeleteArrayClosure,               // _Y
        // 3rd range with double underscore prefix
        ManagedVectorConstructorIterator = 36 - 7 + 10,// __A
        ManagedVectorDestructorIterator,           // __B
        EhVectorCopyConstructorIterator,           // __C
        EhVectorVbaseCopyConstructorIterator,      // __D
        DynamicInitializer,                        // __E
        DynamicAtExitDestructor,                   // __F
        VectorCopyConstructorIterator,             // __G
        VectorVbaseCopyConstructorIterator,        // __H
        ManagedVectorCopyCconstructorIterator,     // __I
        LocalStaticThreadGuard,                    // __J
    }

    /// <summary>
    /// Node to represent special encoded names, lot of compiler generated functions and data elements for meta-information, etc.
    /// </summary>
    public class SpecialNameNode : NameNode
    {
        #region Constants
        private readonly static string[] SpecialNames = new string[] {
            "vftable",                                  // _7
	        "vbtable",                                  // _8
	        "vcall",                                    // _9
	        "typeof",                                   // _A
	        "local static guard",                       // _B
	        "string",                                   // _C
	        "vbase destructor",                         // _D
	        "vector deleting destructor",               // _E
	        "default constructor closure",              // _F
	        "scalar deleting destructor",               // _G
	        "vector constructor iterator",              // _H
	        "vector destructor iterator",               // _I
	        "vector vbase constructor iterator",        // _J
	        "virtual displacement map",                 // _K
	        "eh vector constructor iterator",           // _L
	        "eh vector destructor iterator",            // _M
	        "eh vector vbase constructor iterator",     // _N
	        "copy constructor closure",                 // _O
	        "udt returning",                            // _P
	        "EH",                                       // _Q
	        "RTTI",                                     // _R
	        "local vftable",                            // _S
	        "local vftable constructor closure",        // _T
	        "new[]",                                    // _U
	        "delete[]",                                 // _V
	        "omni callsig",                             // _W
	        "placement delete closure",                 // _X
	        "placement delete[] closure",               // _Y
            "<unknown: _Z>",                            // _Z unused
            // 3rd range with double underscore prefix
            "<unknown: __0>",                           // __0
            "<unknown: __1>",                           // __1
            "<unknown: __2>",                           // __2
            "<unknown: __3>",                           // __3
            "<unknown: __4>",                           // __4
            "<unknown: __5>",                           // __5
            "<unknown: __6>",                           // __6
            "<unknown: __7>",                           // __7
            "<unknown: __8>",                           // __8
            "<unknown: __9>",                           // __9
            "managed vector constructor iterator",      // __A
            "managed vector destructor iterator",       // __B
            "eh vector copy constructor iterator",      // __C
            "eh vector vbase copy constructor iterator",// __D
            "dynamic initializer for ",                 // __E
            "dynamic atexit destructor for ",           // __F
            "vector copy constructor iterator",         // __G
            "vector vbase copy constructor iterator",   // __H
            "managed vector copy constructor iterator", // __I
            "local static thread guard",                // __J
	        "<unknown: __K>",                           // __K unused
	        "<unknown: __L>",                           // __L unused
	        "<unknown: __M>",                           // __M unused
	        "<unknown: __N>",                           // __N unused
	        "<unknown: __O>",                           // __O unused
	        "<unknown: __P>",                           // __P unused
	        "<unknown: __Q>",                           // __Q unused
	        "<unknown: __R>",                           // __R unused
	        "<unknown: __S>",                           // __S unused
	        "<unknown: __T>",                           // __T unused
	        "<unknown: __U>",                           // __U unused
	        "<unknown: __V>",                           // __V unused
	        "<unknown: __W>",                           // __W unused
	        "<unknown: __X>",                           // __X unused
	        "<unknown: __Y>",                           // __Y unused
            "<unknown: __Z>",                           // __Z unused
        };
        #endregion

        #region Properties
        public CompilerSpecialName SpecialNameCode { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="nameCode">Code for the special name</param>
        public SpecialNameNode(CompilerSpecialName nameCode)
        {
            // Be tolerant of currently unused encodings. This seems most likely mode of future expansion for future compiler generated functions
            if ((int)nameCode < 0 || (int)nameCode >= SpecialNames.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(nameCode));
            }
            this.SpecialNameCode = nameCode;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The decoded special name
        /// </summary>
        public override string Name
        {
            get
            {
                return CppNameBuilder.QuoteSpecialName(SpecialNames[(int)this.SpecialNameCode]);
            }
        }
        #endregion
    }

    public enum RttiCode
    {
        TypeDescriptor,             // _R0
        BaseClassDescriptor,        // _R1
        BaseClassArray,             // _R2
        ClassHierarchyDescriptor,   // _R3
        CompleteObjectLocator,      // _R4
    }

    public class RttiNameNode : NameNode
    {
        #region Constants
        private readonly static string[] RttiNames = new string[] {
            "Type Descriptor",              // _R0
	        "Base Class Descriptor",        // _R1
	        "Base Class Array",             // _R2
	        "Class Hierarchy Descriptor",   // _R3
	        "Complete Object Locator",      // _R4
        };
        #endregion

        #region Fields
        private readonly RttiCode rttiCode;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="characterCode">The encoding used for the RTTI metadata</param>
        public RttiNameNode(RttiCode rttiCode)
        {
            if ((int)rttiCode < 0 || (int)rttiCode >= RttiNames.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(rttiCode));
            }
            this.rttiCode = rttiCode;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The short name of the RTTI information
        /// </summary>
        public override string Name
        {
            get
            {
                // The RTTI name table contains a leading space separator
                return CppNameBuilder.QuoteSpecialName(this.SpecialName);
            }
        }

        protected string SpecialName
        {
            get
            {
                return "RTTI " + RttiNames[(int)rttiCode];
            }
        }


        #endregion
    }

    public class RttiTypeDescriptorNode : RttiNameNode
    {
        #region Fields
        TypeNode describedType;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="characterCode">The encoding used for the operator (in the scope of a range)</param>
        /// <param name="range">The range of the encoding</param>
        public RttiTypeDescriptorNode(TypeNode describedType)
            : base(RttiCode.TypeDescriptor)
        {
            this.DescribedType = describedType;
        }
        #endregion

        #region Properties

        public TypeNode DescribedType
        {
            get
            {
                return this.describedType;
            }
            private set
            {
                value.Parent = this;
                this.describedType = value;
            }
        }

        /// <summary>
        /// The short name of the RTTI information
        /// </summary>
        public override string Name
        {
            get
            {
                return this.ToString(UndecorateOptions.NameOnly | UndecorateOptions.NoUndnameEmulation);
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            if (builder.NoUndnameEmulation)
            {
                builder.Append(CppNameBuilder.SpecialNameStart, spacing & CppNameBuilder.Spacing.Leading);
            }
            else
            {
                builder.LeadingSpace(spacing);
            }
            this.DescribedType.DisplayOn(builder, CppNameBuilder.Spacing.Trailing);
            if (!builder.NoUndnameEmulation)
            {
                builder.Append(CppNameBuilder.SpecialNameStart);
            }
            builder.Append(this.SpecialName);
            builder.Append(CppNameBuilder.SpecialNameEnd, spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is RttiTypeDescriptorNode);
            RttiTypeDescriptorNode copy = (RttiTypeDescriptorNode)deepenedCopy;
            copy.DescribedType = Copy(this.DescribedType);
            return shallowCopy;
        }
        #endregion
    }

    public class RttiBaseClassDescriptorNode : RttiNameNode
    {
        #region Fields
        #endregion

        #region Construtors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="characterCode">The encoding used for the operator (in the scope of a range)</param>
        /// <param name="range">The range of the encoding</param>
        public RttiBaseClassDescriptorNode(IEnumerable<Int64> dimensions)
            : base(RttiCode.BaseClassDescriptor)
        {
            this.Dimensions = dimensions.ToArray();
        }
        #endregion

        #region Properties

        public Int64[] Dimensions { get; private set; }

        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append(CppNameBuilder.SpecialNameStart, spacing & CppNameBuilder.Spacing.Leading);
            builder.Append(this.SpecialName);
            builder.Append(" at (");
            bool first = true;
            foreach (Int64 dim in this.Dimensions)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(',');
                }
                builder.Append(dim);
            }
            builder.Append(')');
            builder.Append(CppNameBuilder.SpecialNameEnd, spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }
        #endregion

    }

    public enum Operator
    {
        None = 0,
        New = 2,
        Delete,
        Assign,
        RightShift,
        LeftShift,
        LogicalNot,
        Equals,
        NotEqual,
        Subscript,
        Cast,      // User defined cast operator is a special case
        MemberAccess,
        Multiply,
        Increment,
        Decrement,
        Subtract,
        Add,
        AddressOf,
        PointerToMember,
        Divide,
        Modulus,
        LessThan,
        LessOrEqual,
        GreaterThan,
        GreaterOrEqual,
        Comma,
        Call,
        BitwiseNot,
        BitwiseXor,
        BitwiseOr,
        LogicalAnd,
        LogicalOr,
        MultiplyLvalue,
        AddLvalue,
        SubtractLvalue,
        DivideLvalue,
        ModulusLvalue,
        RightShiftLvalue,
        LeftLeftLValue,
        BitwiseAndLvalue,
        BitwiseOrLvalue,
        BitwiseXorLvalue,
    };

    /// <summary>
    /// Node to represent operators, which have special encodings in the symbol name
    /// </summary>
    public class OperatorNameNode : NameNode
    {
        #region Constants
        // ctor and dtor are encoded as '0' and '1'
        // Operators are enoded as 2..9 then A..Z (codes 0 and 1 are for constructors and destructors respectively
        // There is a 2nd range of operators encoded with a '_' prefix, and other special name encodings follow
        // but these are represented by SpecialNameNodes
        private readonly static string[] OperatorNames = new string[] {
            null,       // 0 constructor, special case
            null,       // 1 destructor, special case
        	"new",      // 2
	        "delete",   // 3
	        "=",        // 4
	        ">>",       // 5
	        "<<",       // 6
	        "!",        // 7
	        "==",       // 8
	        "!=",       // 9
	        "[]",       // A
	        "<cast>",   // B (Cast operator, a special case)
	        "->",       // C
	        "*",        // D
	        "++",       // E
	        "--",       // F
	        "-",        // G
	        "+",        // H
	        "&",        // I
	        "->*",      // J
	        "/",        // K
	        "%",        // L
	        "<",        // M
	        "<=",       // N
	        ">",        // O
	        ">=",       // P
	        ",",        // Q
	        "()",       // R
	        "~",        // S    
	        "^",        // T
	        "|",        // U   
	        "&&",       // V
	        "||",       // W
	        "*=",       // X
	        "+=",       // Y
	        "-=",       // Z

            // 2nd range of encodings
   	        "/=",       // _0
	        "%=",       // _1
	        ">>=",      // _2
	        "<<=",      // _3
	        "&=",       // _4
	        "|=",       // _5
	        "^=",       // _6
        };

        internal static readonly string OperatorKeyword = "operator";
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="encoding">The encoding used for the operator</param>
        public OperatorNameNode(Operator op)
        {
            // Check enum is in in range as the parser is converting this from a character encoding
            if ((int)op < 0 || (int)op >= OperatorNames.Length) throw new ArgumentOutOfRangeException(nameof(op));

            this.Operator = op;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Operator code for the function, or Operator.None if not an operator
        /// </summary>
        public Operator Operator { get; private set; }

        public string OperatorName
        {
            get
            {
                return OperatorNames[(int)this.Operator];
            }
        }

        /// <summary>
        /// The short name of the operator
        /// </summary>
        public override string Name
        {
            get
            {
                return this.ToString(UndecorateOptions.NameOnly);
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append(OperatorKeyword, spacing & CppNameBuilder.Spacing.Leading);
            string operatorSymbol = this.OperatorName;
            Debug.Assert(!string.IsNullOrEmpty(operatorSymbol)); Debug.Assert(!Char.IsLetter(operatorSymbol, 0));
            builder.Append(operatorSymbol, spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }
        #endregion
    }

    /// <summary>
    /// AST node representing a user-defined cast operation, e.g. operator int.
    /// </summary>
    internal class CastOperatorNameNode : OperatorNameNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        public CastOperatorNameNode() : base(Operator.Cast) { }
        #endregion

        #region Properties
        /// <summary>
        /// The target type of the cast (i.e. the type resulting from the cast operation)
        /// </summary>
        BaseSymbolNode TargetType
        {
            get
            {
                // Walk up to symbol at the top to get return type
                FunctionSymbol function = (FunctionSymbol)this.SymbolNode;
                return function.ReturnType;
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append(OperatorNameNode.OperatorKeyword, spacing | CppNameBuilder.Spacing.Trailing);
            this.TargetType.DisplayOn(builder, spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }
        #endregion
    }

    /// <summary>
    /// Node to represent template instances
    /// </summary>
    public class TemplateNameNode : IdentifierNode
    {
        #region Fields
        /// <summary>
        /// The collection of arguments that are parameterizing the template
        /// </summary>
        private List<BaseSymbolNode> arguments;
        #endregion

        #region Constructors
        public TemplateNameNode(string name, IEnumerable<BaseSymbolNode> args)
            : base(name)
        {
            this.SetArguments(args);
        }
        #endregion

        #region Properties
        /// <summary>
        /// The ordered list of template arguments used to instantiate the underlying parametric type 
        /// </summary>
        /// <remarks>Note that in C++ parametric types can be parameterised by certain
        /// literals, such as integers and strings, as well as other types)</remarks>
        public IEnumerable<BaseSymbolNode> Arguments
        {
            get
            {
                return this.arguments ?? Enumerable.Empty<BaseSymbolNode>();
            }
        }

        #endregion

        #region Helpers
        private void SetArguments(IEnumerable<BaseSymbolNode> args)
        {
            this.arguments = null;
            if (args != null)
            {
                foreach (BaseSymbolNode arg in args)
                {
                    AddArgument(arg);
                }
            }
        }

        internal void AddArgument(BaseSymbolNode arg)
        {
            if (this.arguments == null)
            {
                this.arguments = new List<BaseSymbolNode>();
            }
            this.arguments.Add(arg);
            arg.Parent = this;
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            base.DisplayOn(builder, spacing & CppNameBuilder.Spacing.Leading);
            builder.AppendTemplateArguments(this.Arguments);
            builder.TrailingSpace(spacing);
            return true;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is TemplateNameNode);
            TemplateNameNode copy = (TemplateNameNode)deepenedCopy;
            copy.SetArguments(copy.Arguments.Select(each => Copy(each)));
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// Node to represent type names that appear as function and template parameters, return and variable types, in symbols
    /// </summary>
    public abstract class TypeNode : NameNode
    {
        #region Fields
        private StorageClassNode storage;
        #endregion

        #region Properties
        /// <summary>
        ///  Storage information such as const, volatile, and various MS extensions. Null if no special storage attributes
        /// </summary>
        public StorageClassNode Storage
        {
            get
            {
                return this.storage;
            }

            internal set
            {
                this.storage = value;
                if (value != null)
                {
                    value.Parent = this;
                }
            }
        }

        /// <summary>
        /// Storage class flags for this type, if any
        /// </summary>
        public StorageClass StorageClassification
        {
            get
            {
                return this.Storage == null ? StorageClass.None : this.Storage.Classification;
            }
        }

        /// <summary>
        /// The name of the type
        /// </summary>
        public override string Name
        {
            get
            {
                return this.ToString(UndecorateOptions.NameOnly);
            }
        }

        /// <summary>
        /// Is this type a pointer or reference type?
        /// </summary>
        public virtual bool IsIndirection
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region Displaying

        protected static bool NullDisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing) => false;

        /// <summary>
        /// Append the undecorated textual representation of this node to the StringBuilder argument
        /// </summary>
        /// <param name="builder">C++ name builder</param>
        /// <param name="spacing">Whitespace options</param>
        /// <remarks>Subclasses should override DisplayOnAround instead.</remarks>
        public sealed override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            return this.DisplayOnAround(builder, spacing, NullDisplayOn);
        }

        internal virtual bool DisplayOnAround(CppNameBuilder builder, CppNameBuilder.Spacing spacing, Func<CppNameBuilder, CppNameBuilder.Spacing, bool> displayOp)
        {
            base.DisplayOn(builder, spacing & CppNameBuilder.Spacing.Leading);
            if (this.StorageClassification != StorageClass.None)
            {
                this.Storage.DisplayOn(builder, CppNameBuilder.Spacing.Leading);
            }
            displayOp?.Invoke(builder, CppNameBuilder.Spacing.Leading);
            builder.TrailingSpace(spacing);
            return true;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is TypeNode);
            TypeNode copy = (TypeNode)deepenedCopy;
            copy.Storage = Copy(this.storage);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// Storage class flags in the encoding format used in the mangled names
    /// </summary>
    [Flags]
    public enum StorageClass
    {
        None = 0,
        Const = 0x1,
        Volatile = 0x2,
        CvMask = 0x3,
        Based = 0xC,        // i.e. far + huge, for a based pointer
        Member = 0x10,      // pointer to member
        Function = 0x20,    // function pointer
    }

    /// <summary>
    /// Node to represent storage classification information associated with functions and variables
    /// </summary>
    public class StorageClassNode : BaseSymbolNode
    {
        #region Fields
        private CompoundTypeNode memberType;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="classification">Storage classification flags</param>
        /// <param name="memberType">Type whose member is pointed at for member pointers, otherwise null</param>
        public StorageClassNode(StorageClass classification, CompoundTypeNode memberType)
        {
            if (memberType == null && classification.HasFlag(StorageClass.Member))
            {
                throw new ArgumentNullException(nameof(memberType));
            }

            this.Classification = classification;
            this.MemberType = memberType;
        }
        #endregion

        #region Properties

        /// <summary>
        /// Storage class flags (whether const, volatile, etc)
        /// </summary>
        public StorageClass Classification { get; internal set; }

        /// <summary>
        /// Is this storage class node describing the storage for a function?
        /// </summary>
        public bool IsFunction
        {
            get
            {
                return this.Classification.HasFlag(StorageClass.Function);
            }
        }

        /// <summary>
        /// The type pointed at when this is the storage class for a member, otherwise null
        /// </summary>
        public CompoundTypeNode MemberType
        {
            get
            {
                return this.memberType;
            }
            private set
            {
                this.memberType = value;
                if (this.memberType != null)
                {
                    this.memberType.Parent = this;
                }
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            bool output = false;
            StorageClass storage = this.Classification;
            // The first keyword should have a leading space only if requested
            CppNameBuilder.Spacing s = spacing & CppNameBuilder.Spacing.Leading;
            for (StorageClass storageFlag = StorageClass.Const; storageFlag <= StorageClass.Volatile; storageFlag = (StorageClass)((uint)storageFlag << 1))
            {
                if (storage.HasFlag(storageFlag))
                {
                    // Modifier, leading space always required
                    output |= builder.Append((Enum)storageFlag, s);
                    // Subsequent output will require a leading space
                    s |= CppNameBuilder.Spacing.Leading;
                }
            }

            //Debug.Assert(this.MemberTypeName != null == storage.HasFlag(StorageClass.Member));
            //
            //if (storage.HasFlag(StorageClass.Member))
            //{
            //    output |= this.MemberTypeName.DisplayOn(builder, s);
            //    builder.Append(CppNameBuilder.NameSeparator);
            //    // Not trailing space wanted
            //    output = false;
            //}

            // If there was output emit a trailing space if one was requested
            if (output)
            {
                builder.TrailingSpace(spacing);
            }
            return output;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is StorageClassNode);
            StorageClassNode copy = (StorageClassNode)deepenedCopy;
            copy.MemberType = Copy(this.MemberType);
            return shallowCopy;
        }
        #endregion

    }

    /// <summary>
    /// StorageClassNode to represented based (relative) pointers
    /// </summary>
    public class BasedStorageClassNode : StorageClassNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="classification">Storage class flags</param>
        /// <param name="baseName">Non-null name of base variable </param>
        public BasedStorageClassNode(StorageClass classification, NameNode baseName)
            : base(classification, null)
        {
            this.BaseName = baseName;
        }
        #endregion

        #region Properties
        private NameNode baseName;
        /// <summary>
        /// The name of the variable to which the pointer is relative. Cannot be null
        /// </summary>
        public NameNode BaseName
        {
            get
            {
                return this.baseName;
            }
            private set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                this.baseName = value;
                this.baseName.Parent = this;
            }
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            CppNameBuilder.Spacing requestedLeadingSpacing = spacing & CppNameBuilder.Spacing.Leading;

            // Base class may not append any output if it has no flags or they are omitted by options
            bool output = base.DisplayOn(builder, requestedLeadingSpacing);

            if (builder.AppendMsKeyword("based", output ? CppNameBuilder.Spacing.Leading : requestedLeadingSpacing))
            {
                builder.Append('(');
                this.BaseName.DisplayOn(builder);
                builder.Append(')');
                output = true;
            }
            if (output)
            {
                builder.TrailingSpace(spacing);
            }
            return output;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is BasedStorageClassNode);
            BasedStorageClassNode copy = (BasedStorageClassNode)deepenedCopy;
            copy.BaseName = Copy(this.BaseName);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// MS specific storage flags
    /// </summary>
    [Flags]
    public enum StorageModifiers
    {
        None = 0,
        Ptr64 = 0x1,      // 'E'
        Unaligned = 0x2,  // 'F'
        Restrict = 0x4    // 'I'
    }

    /// <summary>
    /// Nodes to represent MS specific storage modifiers, e.g. __ptr64
    /// </summary>
    /// <remarks>Storage modifiers can theoretically appear in any order, so to reproduce
    /// that in the undecorated form we use a simple node rather than just the consolidated flags</remarks>
    public class StorageModifierNode : NameNode
    {
        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="modifier">Storage modifier flag</param>
        public StorageModifierNode(StorageModifiers modifier)
        {
            this.Modifier = modifier;
        }

        #endregion

        #region Properties
        /// <summary>
        /// The storage modifier flag this node represents
        /// </summary>
        public StorageModifiers Modifier { get; private set; }

        /// <summary>
        /// Whether this storage modifier should be displayed before the indirection
        /// symbol. Most are displayed after it.
        /// </summary>
        internal bool IsPrefix { get { return this.Modifier == StorageModifiers.Unaligned; } }
        #endregion

        #region Displaying
        public override string Name
        {
            get
            {
                return CppNameBuilder.GetEnumValueName(this.Modifier);
            }
        }

        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            return builder.AppendMsKeyword(this.Name, spacing);
        }
        #endregion
    }

    public enum PrimitiveTypeCodes
    {
        // A - ReferenceModifier,
        // B - VolatileModifier,
        SignedChar = 'C' - 'A', // C
        Char,                   // D
        UnsignedChar,           // E
        Short,                  // F
        UnsignedShort,          // G
        Int,                    // H
        UnsignedInt,            // I
        Long,                   // J
        UnsignedLong,           // K
        // L - unused
        Float = 'M' - 'A',      // M
        Double,                 // N
        LongDouble,             // O
        // P - pointer
        // Q - const pointer
        // R - volatile pointer
        // S - const volatile pointer
        // T - union
        // U - struct
        // V - class
        // W - enum
        Void = 'X' - 'A',      // X (or coclass?)
        // Y - cointerface
        // Z - terminator

        // Extended type encodings
        // _A
        // _B
        // _C
        Int8 = 'D' - 'A' + 26, // _D
        UnsignedInt8,          // _E
        Int16,                 // _F
        UnsignedInt16,         // _G
        Int32,                 // _H
        UnsignedInt32,         // _I
        Int64,                 // _J
        UnsignedInt64,         // _K
        Int128,                // _J
        UnsignedInt128,        // _K
        Bool,                  // _N
        // _O - Array
        // _P
        // _Q
        // _R
        // _S
        // _T
        // _U
        // _V
        WCharT = 'W' - 'A' + 26,  // _W
        // _X - coclass
        // _Y - cointerface
        // _Z unused
    }

    /// <summary>
    /// Node to represent built-in primitive types with special encodings
    /// </summary>
    public class PrimitiveTypeNode : TypeNode
    {
        #region Constants
        private readonly static string[] TypeNames = new string[] {
            null,               // A - reference modifier
	        null,               // B - volatile modifier
	        "signed char",      // C
	        "char",             // D
	        "unsigned char",    // E
	        "short",            // F
	        "unsigned short",   // G
	        "int",              // H
	        "unsigned int",     // I
	        "long",             // J
	        "unsigned long",    // K
	        null,               // L - unused
	        "float",            // M
	        "double",           // N
	        "long double",      // O
	        null,               // P - pointer
	        null,               // Q - const pointer
	        null,               // R - volatile pointer
	        null,               // S - const volatile pointer
	        null,               // T - union
	        null,               // U - struct
	        null,               // V - class
	        null,               // W - enum
	        "void",             // X (or coclass?)
	        null,               // Y - cointerface
	        null,               // Z - terminator

            // Extended type encodings
	        null,               // _A
	        null,               // _B
	        null,               // _C
	        "__int8",           // _D
	        "unsigned __int8",  // _E
	        "__int16",          // _F
	        "unsigned __int16", // _G
	        "__int32",          // _H
	        "unsigned __int32", // _I
	        "__int64",          // _J
	        "unsigned __int64", // _K
	        "__int128",         // _J
	        "unsigned __int128",// _K
	        "bool",             // _N
	        null,               // _O - Array
	        null,               // _P
	        null,               // _Q
	        null,               // _R
	        null,               // _S
	        null,               // _T
	        null,               // _U
	        null,               // _V
	        "wchar_t",          // _W
	        null,               // _X - coclass
	        null,               // _Y - cointerface
            null,               // _Z unused
            };
        #endregion

        #region Fields
        private readonly PrimitiveTypeCodes encoding;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="encoding">Encoding for the type name</param>
        public PrimitiveTypeNode(PrimitiveTypeCodes encoding)
        {
            // Normally one wouldn't bother checking an enum for being in range, but the parser is calculating it from a character encoding
            // so we check to catch logic errors there, especially as this is used to index into an array of names
            if ((int)encoding < 0 || (int)encoding >= TypeNames.Length) throw new ArgumentOutOfRangeException(nameof(encoding));
            this.encoding = encoding;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The decoded name
        /// </summary>
        public override string Name
        {
            get
            {
                return TypeNames[(int)this.encoding];
            }
        }
        #endregion
    }

    /// <summary>
    /// Distinguishes between different variations of compound (non-scalar) type
    /// </summary>
    public enum CompoundTypeClass
    {
        Unknown = 0,
        Union,
        Struct,
        Class,
        Enum
    }

    /// <summary>
    /// Node for types that are pointers or references to ther types
    /// </summary>
    public abstract class IndirectionTypeNode : TypeNode
    {
        #region Fields
        private TypeNode targetType;
        private List<StorageModifierNode> storageModifiers;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="targetType">The pointed-at or referenced type</param>
        protected IndirectionTypeNode(TypeNode targetType)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            this.TargetType = targetType;
        }

        protected IndirectionTypeNode()
        {
        }
        #endregion

        #region Properties
        /// <summary>
        /// The type which is the target of the indirection
        /// </summary>
        public TypeNode TargetType
        {
            get
            {
                return this.targetType;
            }
            internal set
            {
                this.targetType = value;
                value.Parent = this;
            }
        }

        /// <summary>
        /// The ordered list of MS specific storage modifiers, or null if none
        /// </summary>
        /// <remarks>This is normally an empty list, and most should contain no more than three modifiers</remarks>
        public IEnumerable<StorageModifierNode> StorageModifiers
        {
            get
            {
                return this.storageModifiers ?? Enumerable.Empty<StorageModifierNode>();
            }
            internal set
            {
                // Don't retain the modifier list if empty
                if (value == null || !value.Any())
                {
                    this.storageModifiers = null;
                }
                else
                {
                    this.storageModifiers = new List<StorageModifierNode>();
                    foreach (StorageModifierNode modifier in value)
                    {
                        modifier.Parent = this;
                        this.storageModifiers.Add(modifier);
                    }
                }
            }
        }

        /// <summary>
        /// Is this type a pointer or reference type?
        /// </summary>
        public override bool IsIndirection
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Is this a pointer to a function?
        /// </summary>
        public virtual bool IsFunctionPointer => false;
        #endregion

        #region Displaying

        protected abstract void DisplayIndirectionOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing);

        internal sealed override bool DisplayOnAround(CppNameBuilder builder, CppNameBuilder.Spacing spacing, Func<CppNameBuilder, CppNameBuilder.Spacing, bool> displayOp)
        {
            return this.TargetType.DisplayOnAround(builder, spacing, (b, s) =>
            {
                CppNameBuilder.Spacing leadingSpacing = s & CppNameBuilder.Spacing.Leading;
                IEnumerable<StorageModifierNode> modifiers = this.StorageModifiers;
                Debug.Assert(modifiers.All(each => each.Parent == this));
                if (b.AppendMsKeywords(modifiers.Where(each => each.IsPrefix)))
                {
                    leadingSpacing |= CppNameBuilder.Spacing.Leading;
                }
                this.DisplayIndirectionOn(b, leadingSpacing);
                // UndecorateSymbolName does not correctly decode some const/volatile pointers/refs as it 
                // ignores the storage class specified on the indirection itself. An example of a case that 
                // undname undecorates incorrectly is a const pointer to member, e.g ?pcia@@3QQAbc@@HQ1@, 
                // which should be "int Abc::* const pcia"
                // Undname does correctly emit the pointer storage classifiers in the case of function pointers
                if (this.Storage != null && (builder.NoUndnameEmulation || this.IsFunctionPointer))
                {
                    this.Storage.DisplayOn(builder, CppNameBuilder.Spacing.Leading);
                }
                b.AppendMsKeywords(modifiers.Where(each => !each.IsPrefix), CppNameBuilder.Spacing.Leading);
                displayOp?.Invoke(b, s | CppNameBuilder.Spacing.Leading);
                //displayOp(b, s & CppNameBuilder.Spacing.Trailing);
                return true;
            });
        }

        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is IndirectionTypeNode);
            IndirectionTypeNode copy = (IndirectionTypeNode)deepenedCopy;
            copy.TargetType = Copy(this.targetType);
            copy.StorageModifiers = this.StorageModifiers.Select(each => Copy(each));
            return shallowCopy;
        }

        #endregion
    }

    /// <summary>
    /// Node to represent references to other (non-reference) types
    /// </summary>
    public class ReferenceTypeNode : IndirectionTypeNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        public ReferenceTypeNode()
        {
        }
        #endregion

        #region Displaying

        protected override void DisplayIndirectionOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append('&', spacing);
        }
        #endregion
    }

    /// <summary>
    /// Node to represent pointers to other types
    /// </summary>
    public class PointerTypeNode : IndirectionTypeNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="targetType">The target type of the pointer</param>
        public PointerTypeNode(TypeNode targetType) : base(targetType) { }
        #endregion

        #region Properties
        public override bool IsFunctionPointer => this.TargetType.StorageClassification.HasFlag(StorageClass.Function);
        #endregion

        #region Displaying
        protected override void DisplayIndirectionOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.LeadingSpace(spacing);
            StorageClassNode targetStorage = this.TargetType.Storage;
            if (targetStorage != null && targetStorage.MemberType != null)
            {
                targetStorage.MemberType.DisplayOn(builder);
                builder.Append(CppNameBuilder.NameSeparator);
            }
            builder.Append('*', spacing & CppNameBuilder.Spacing.Trailing);
        }

        #endregion
    }

    public class RvalueReferenceTypeNode : ReferenceTypeNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        public RvalueReferenceTypeNode()
        {
        }
        #endregion

        #region Displaying
        protected override void DisplayIndirectionOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append("&&", spacing);
        }
        #endregion
    }

    /// <summary>
    /// Node to represent compound, user-defined, types such as classes, structs, unions 
    /// but not enums (which are user-defined, but not really compound)
    /// </summary>
    public class CompoundTypeNode : TypeNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="typeClass">Type of compoung type (struct, class, etc)</param>
        /// <param name="qualifiedName">Fully qualified name</param>
        public CompoundTypeNode(CompoundTypeClass typeClass, QualifiedNameNode qualifiedName)
        {
            if (qualifiedName == null) throw new ArgumentNullException(nameof(qualifiedName));

            this.CompoundTypeClass = typeClass;
            this.QualifiedName = qualifiedName;
            qualifiedName.Parent = this;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The broad type of this compound type, i.e. struct, class or union, if known
        /// </summary>
        public CompoundTypeClass CompoundTypeClass { get; private set; }

        /// <summary>
        /// The fully qualified name of this compound type
        /// </summary>
        public QualifiedNameNode QualifiedName { get; private set; }

        /// <summary>
        /// The unqualified name of this type
        /// </summary>
        public override string Name
        {
            get
            {
                return this.QualifiedName.Identifier.Name;
            }
        }
        #endregion

        #region Displaying
        internal override bool DisplayOnAround(CppNameBuilder builder, CppNameBuilder.Spacing spacing, Func<CppNameBuilder, CppNameBuilder.Spacing, bool> displayOp)
        {
            builder.LeadingSpace(spacing);
            if (this.CompoundTypeClass != CompoundTypeClass.Unknown && !builder.HasOptions(UndecorateOptions.NoCompoundTypeClass))
            {
                DisplayCompoundTypeClassOn(builder);
            }
            this.QualifiedName.DisplayOn(builder);
            if (this.StorageClassification != StorageClass.None)
            {
                this.Storage.DisplayOn(builder, CppNameBuilder.Spacing.Leading);
            }
            displayOp?.Invoke(builder, CppNameBuilder.Spacing.Leading);
            builder.TrailingSpace(spacing);
            return true;
        }

        protected virtual void DisplayCompoundTypeClassOn(CppNameBuilder builder)
        {
            if (!builder.HasOptions(UndecorateOptions.NameOnly))
            {
                builder.Append((Enum)this.CompoundTypeClass, CppNameBuilder.Spacing.Trailing);
            }
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is CompoundTypeNode);
            CompoundTypeNode copy = (CompoundTypeNode)deepenedCopy;
            copy.QualifiedName = Copy(this.QualifiedName);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// Base integer type of an enum type
    /// </summary>
    public enum EnumBaseType
    {
        Char = 0,
        UnsignedChar,
        Short,
        UnsignedShort,
        Int,
        UnsignedInt,
        Long,
        UnsignedLong,
    }

    /// <summary>
    /// Node to represent enumerated types
    /// </summary>
    public class EnumTypeNode : CompoundTypeNode
    {
        #region Constants
        private readonly static string[] TypeNames = new string[] {
            "char",
            "unsigned char",
            "short",
            "unsigned short",
            "int",
            "unsigned int",
            "long",
            "unsigned long" };
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="qualifiedName">The fully qualified name of the type</param>
        /// <param name="baseTypeCode">The base type of the num</param>
        public EnumTypeNode(QualifiedNameNode qualifiedName, EnumBaseType baseTypeCode)
            : base(CompoundTypeClass.Enum, qualifiedName)
        {
            this.BaseType = baseTypeCode;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The base integral type of this enum type
        /// </summary>
        public EnumBaseType BaseType { get; private set; }

        /// <summary>
        /// The name of the base type of this enum type
        /// </summary>
        public string BaseTypeName
        {
            get
            {
                return TypeNames[(int)this.BaseType];
            }
        }
        #endregion

        #region Displaying
        protected override void DisplayCompoundTypeClassOn(CppNameBuilder builder)
        {
            // Undname displays "enum" prefix even in name only mode (doesn't have this bug for other compound types)
            if (!builder.HasOptions(UndecorateOptions.NameOnly | UndecorateOptions.NoUndnameEmulation))
            {
                builder.Append((Enum)this.CompoundTypeClass, CppNameBuilder.Spacing.Trailing);

                if (this.BaseType != EnumBaseType.Int)
                {
                    builder.Append(this.BaseTypeName, CppNameBuilder.Spacing.Trailing);
                }
            }
        }
        #endregion
    }


    /// <summary>
    /// The calling convention for a function, which includes how arguments are passed on the stack,
    /// how the return value is returned, etc.
    /// </summary>
    public enum CallingConvention
    {
        Cdecl,      // A, the default and only available convention for 64-bit code
        Pascal,     // C
        ThisCall,   // E
        StdCall,    // G
        FastCall,   // I
        Interrupt,  // K
        ClrCall,    // M
        Eabi,       // O Dunno what this is
    }

    /// <summary>
    /// Node to represent the type information describing a function, i.e. the return type, parameter types,
    /// calling convention and throw types. Common between symbol nodes representing entire functions, and 
    /// function pointers appearing within symbols anywhere there can be type information
    /// </summary>
    public class FunctionTypeNode : TypeNode
    {
        #region Fields
        private TypeNode returnType;
        private List<TypeNode> parameters;
        private readonly int callingConventionEncoding;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbol">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the function</param>
        internal FunctionTypeNode(int callingConventionEncoding)
            : base()
        {
            this.callingConventionEncoding = callingConventionEncoding;
            this.SetParameters(null);
        }
        #endregion


        #region Helpers
        /// <summary>
        /// Add a parsed type node to the parameter list of the function
        /// </summary>
        /// <param name="param">The new parameter</param>
        /// <remarks>Intended only for use by the parser</remarks>
        internal void AddParameter(TypeNode param)
        {
            if (this.parameters == null)
            {
                this.parameters = new List<TypeNode>();
            }
            this.parameters.Add(param);
            param.Parent = this;
        }

        private void SetParameters(IEnumerable<TypeNode> parms)
        {
            this.parameters = null;
            if (parms != null)
            {
                foreach (TypeNode param in parms)
                {
                    this.AddParameter(param);
                }
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// The return type of the function
        /// </summary>
        /// <remarks>Can be null in the special case of constructors and destructors</remarks>
        public TypeNode ReturnType
        {
            get
            {
                return this.returnType;
            }

            internal set
            {
                this.returnType = value;
                if (value != null)
                {
                    value.Parent = this;
                }
            }
        }

        /// <summary>
        /// The types of the parameters of the function
        /// </summary>
        public IEnumerable<TypeNode> Parameters
        {
            get
            {
                return this.parameters ?? Enumerable.Empty<TypeNode>();
            }
        }

        /// <summary>
        /// The functions calling convention
        /// </summary>
        public CallingConvention CallingConvention
        {
            get
            {
                return (CallingConvention)(this.callingConventionEncoding / 2);
            }
        }

        /// <summary>
        /// Is the function marked with __saveregs?
        /// </summary>
        /// <remarks>Obsolete: This was only relevant in 16-bit code, and is no longer used</remarks>
        public bool IsSaveRegs
        {
            get
            {
                return this.callingConventionEncoding % 2 != 0;
            }
        }

        /// <summary>
        /// Does this function have ellipsis?
        /// </summary>
        public bool IsVarArgs { get; set; }

        #endregion

        #region Displaying

        internal override bool DisplayOnAround(CppNameBuilder builder, CppNameBuilder.Spacing spacing, Func<CppNameBuilder, CppNameBuilder.Spacing, bool> displayOp)
        {
            builder.LeadingSpace(spacing);
            // Constructors and destructors have no return type
            if (this.ReturnType != null)
            {
                this.ReturnType.DisplayOn(builder, CppNameBuilder.Spacing.Trailing);
            }
            bool needsParenthesis = displayOp != NullDisplayOn;
            if (needsParenthesis)
            {
                builder.Append('(');
            }
            // Bug in undname, forgets space after calling convention
            bool spaceAfterCC = false;
            // UndecorateName only applies the NoCallingConvention option at the top-level, but it seems reasonable to strip it out from any function node
            if (!builder.HasOptions(UndecorateOptions.NoCallingConvention | UndecorateOptions.NoUndnameEmulation))
            {
                spaceAfterCC = builder.AppendMsKeyword(CppNameBuilder.GetEnumValueName(this.CallingConvention), CppNameBuilder.Spacing.None);
            }

            if (this.StorageClassification != StorageClass.None)
            {
                spaceAfterCC |= this.Storage.DisplayOn(builder, CppNameBuilder.Spacing.Leading);
            }

            // Display the name and indirection as provided from the parent(s)
            displayOp?.Invoke(builder, spaceAfterCC ? CppNameBuilder.Spacing.Leading : CppNameBuilder.Spacing.None);

            if (needsParenthesis)
            {
                builder.Append(')');
            }
            builder.AppendFunctionParameters(this.Parameters, this.IsVarArgs);
            builder.TrailingSpace(spacing);
            return true;
        }

        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is FunctionTypeNode);
            FunctionTypeNode copy = (FunctionTypeNode)deepenedCopy;
            copy.ReturnType = Copy(this.returnType);
            copy.SetParameters(this.Parameters.Select(each => Copy(each)));
            return shallowCopy;
        }
        #endregion

    }

    /// <summary>
    /// Node to represent literal values
    /// </summary>
    /// <typeparam name="T">The type of the underlying literal value</typeparam>
    public class LiteralNode<T> : BaseSymbolNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">The literal value</param>
        public LiteralNode(T value)
        {
            this.Value = value;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The literal value
        /// </summary>
        public T Value { get; private set; }
        #endregion

        #region Displaying

        /// <summary>
        /// The literal value
        /// </summary>
        public override string ToString()
        {
            return this.Value.ToString();
        }

        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append(this.ToString(), spacing);
            return true;
        }
        #endregion
    }

    internal class FloatingPointLiteralNode : LiteralNode<double>
    {
        #region Constructors
        public FloatingPointLiteralNode(double value)
            : base(value)
        {
        }
        #endregion

        #region Displaying
        public override string ToString()
        {
            return this.Value.ToString("0.############e0", CultureInfo.InvariantCulture);
        }
        #endregion
    }

    /// <summary>
    /// Node to represent the address of another symbol. Can appears as a template argument.
    /// </summary>
    internal class AddressOfNode : BaseSymbolNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="target">Symbol who's adress is taken (can be null)</param>
        public AddressOfNode(Symbol target)
        {
            this.Target = target;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The symbol of which this represents the address. Can be null.
        /// </summary>
        public Symbol Target { get; private set; }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            if (this.Target == null)
            {
                builder.Append("NULL", spacing);
            }
            else
            {
                builder.Append('&', spacing | CppNameBuilder.Spacing.Trailing);
                this.Target.DisplayOn(builder, spacing & CppNameBuilder.Spacing.Trailing);
            }
            return true;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is AddressOfNode);
            AddressOfNode copy = (AddressOfNode)deepenedCopy;
            copy.Target = Copy(this.Target);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// Represents some weird template parameter cases that I don't understand, but which we don't 
    /// really care about as long as we can parse them
    /// </summary>
    public abstract class TemplateParameterNode : NameNode
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="index">index encoding</param>
        protected TemplateParameterNode(Int64 index)
        {
            this.Index = index;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The encoded index, whatever that is
        /// </summary>
        public Int64 Index { get; private set; }
        #endregion
    }

    public class IndexedTemplateParameterNode : TemplateParameterNode
    {
        #region Fields
        [Flags]
        private enum Options
        {
            None = 0,
            NonType = 1,
            SignedDimensionBug = 2,
            MissingCloseQuoteBug = 4
        }

        private Options options;

        #endregion

        public IndexedTemplateParameterNode(Int64 index, bool isNonType)
            : base(index)
        {
            this.IsNonType = isNonType;
        }

        #region Properties

        /// <summary>
        /// Don't really know what a non-type template parameter is, but this says whether it is one or not
        /// </summary>
        public bool IsNonType
        {
            get
            {
                return options.HasFlag(Options.NonType);
            }
            private set
            {
                this.SetOption(Options.NonType, value);
            }
        }

        internal bool MissingCloseQuoteBug
        {
            get
            {
                return options.HasFlag(Options.MissingCloseQuoteBug);
            }
            set
            {
                this.SetOption(Options.MissingCloseQuoteBug, value);
            }
        }


        internal bool SignedDimensionBug
        {
            get
            {
                return options.HasFlag(Options.SignedDimensionBug);
            }
            set
            {
                this.SetOption(Options.SignedDimensionBug, value);
            }
        }

        public override string Name
        {
            get
            {
                CppNameBuilder builder = new CppNameBuilder();
                this.DisplayOn(builder);
                return builder.ToString();
            }
        }

        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {

            builder.Append(CppNameBuilder.SpecialNameStart, spacing & CppNameBuilder.Spacing.Leading);
            if (this.IsNonType)
            {
                builder.Append("non-type-");
            }
            builder.Append("template-parameter");
            if (this.SignedDimensionBug || builder.NoUndnameEmulation) builder.Append('-');
            builder.Append(this.Index);
            if (!this.MissingCloseQuoteBug || builder.NoUndnameEmulation) builder.Append('\'');
            builder.TrailingSpace(spacing);
            return true;
        }
        #endregion

        #region Private Helpers

        private void SetOption(Options mask, bool value)
        {
            this.options = value ? this.options | mask : this.options & ~mask;
        }

        #endregion
    }

    internal class NamedTemplateParameterNode : TemplateParameterNode
    {
        #region Fields
        private NameNode Identifier { get; set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="index">Encoded index</param>
        /// <param name="identifier">Name of template parameter</param>
        public NamedTemplateParameterNode(Int64 index, NameNode identifier)
            : base(index)
        {
            this.Identifier = identifier;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The name of the template parameter
        /// </summary>
        public override string Name
        {
            get
            {
                return this.Identifier.Name;
            }
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is NamedTemplateParameterNode);
            NamedTemplateParameterNode copy = (NamedTemplateParameterNode)deepenedCopy;
            copy.Identifier = Copy(this.Identifier);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// ?
    /// </summary>
    internal enum TemplateCurlyType
    {
        Vptmd = 'F',
        Gptmd = 'G',
        Mptmf = 'H',
        Vptmf = 'I',
        Gptmf = 'J'
    }

    /// <summary>
    /// Supports a complex template argument that I don't fully understand, but which we need to at 
    /// least part. Undname prints these with curly brackets
    /// </summary>
    internal class TemplateParameterCurlyNode : BaseSymbolNode
    {
        #region Fields
        private List<BaseSymbolNode> parts;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">?</param>
        public TemplateParameterCurlyNode(TemplateCurlyType type)
        {
            this.SetParts(null);
            this.CurlyType = type;
        }
        #endregion

        #region Properties
        /// <summary>
        /// ?
        /// </summary>
        public TemplateCurlyType CurlyType { get; private set; }
        #endregion

        #region Helpers
        private void SetParts(IEnumerable<BaseSymbolNode> partNodes)
        {
            this.parts = new List<BaseSymbolNode>();
            if (partNodes != null)
            {
                foreach (BaseSymbolNode part in partNodes)
                {
                    this.AddPart(part);
                }
            }
        }

        internal void AddPart(BaseSymbolNode part)
        {
            parts.Add(part);
            part.Parent = this;
        }
        #endregion

        #region Displaying
        public override bool DisplayOn(CppNameBuilder builder, CppNameBuilder.Spacing spacing)
        {
            builder.Append('{', spacing & CppNameBuilder.Spacing.Leading);
            builder.AppendAllWithSeparators(this.parts, ",");
            builder.Append('}', spacing & CppNameBuilder.Spacing.Trailing);
            return true;
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is TemplateParameterCurlyNode);
            TemplateParameterCurlyNode copy = (TemplateParameterCurlyNode)deepenedCopy;
            copy.SetParts(this.parts.Select(each => Copy(each)));
            return shallowCopy;
        }
        #endregion
    }
    /// <summary>
    /// Node to represent a symbol that is the mangled name of a function, global or member
    /// </summary>
    public abstract class FunctionSymbol : Symbol
    {
        #region Fields
        private FunctionTypeNode functionType;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the function</param>
        protected FunctionSymbol(string symbolicName, QualifiedNameNode qualifiedName)
            : base(symbolicName, qualifiedName)
        {
        }
        #endregion

        #region Properties

        public FunctionTypeNode FunctionType
        {
            get
            {
                return this.functionType;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                this.functionType = value;
                value.Parent = this;
            }
        }

        /// <summary>
        /// The return type of the function
        /// </summary>
        /// <remarks>Can be null in the special case of constructors and destructors</remarks>
        public TypeNode ReturnType
        {
            get
            {
                return this.FunctionType.ReturnType;
            }
        }

        /// <summary>
        /// The types of the parameters of the function
        /// </summary>
        public IEnumerable<TypeNode> Parameters
        {
            get
            {
                return this.FunctionType.Parameters;
            }
        }

        /// <summary>
        /// The functions calling convention
        /// </summary>
        public CallingConvention CallingConvention
        {
            get
            {
                return this.FunctionType.CallingConvention;
            }
        }

        /// <summary>
        /// Does this function have ellipsis?
        /// </summary>
        public bool IsVarArgs
        {
            get
            {
                return this.FunctionType.IsVarArgs;
            }
        }

        /// <summary>
        /// Is this a "far" function (as opposed to near)?
        /// </summary>
        /// <remarks>Obsolete: This was only relevant in 16-bit code, and is no longer used</remarks>
        public bool IsFar { get; internal set; }

        #endregion

        #region Displaying
        protected override void DisplayBodyOn(CppNameBuilder builder)
        {
            FunctionTypeNode funcType = this.FunctionType;

            // Cast operators do not require a return type as this is implied
            // TODO: Temporary hack - need to restructure
            if (!(this.QualifiedName.Identifier is CastOperatorNameNode))
            {
                // Note that the NoReturnType option only applies at the top-level, not to any embedded function types (e.g. for function pointers)
                if (!(this.ReturnType == null || builder.HasOptions(UndecorateOptions.NameOnly) || builder.HasOptions(UndecorateOptions.NoReturnType)))
                {
                    this.ReturnType.DisplayOn(builder, CppNameBuilder.Spacing.Trailing);
                }
            }

            builder.Append(funcType.CallingConvention, CppNameBuilder.Spacing.Trailing);

            base.DisplayBodyOn(builder);

            if (!builder.HasOptions(UndecorateOptions.NameOnly))
            {
                builder.AppendFunctionParameters(funcType.Parameters, funcType.IsVarArgs);
            }
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is FunctionSymbol);
            FunctionSymbol copy = (FunctionSymbol)deepenedCopy;
            copy.FunctionType = Copy(this.functionType);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// Root symbol node to represent a non-member function
    /// </summary>
    public class GlobalFunctionSymbol : FunctionSymbol
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the function</param>
        public GlobalFunctionSymbol(string symbolicName, QualifiedNameNode qualifiedName)
            : base(symbolicName, qualifiedName)
        {
        }
        #endregion
    }

    /// <summary>
    /// The type of member function, e.g. virtual
    /// </summary>
    public enum MemberFunctionClassification
    {
        Normal = 0,
        Static,
        Virtual,
    }

    /// <summary>
    /// The C++ access level for a member function, e.g. private
    /// </summary>
    public enum MemberProtectionLevel
    {
        Private = 0,
        Protected,
        Public,
    }

    /// <summary>
    /// Specialized function node to represent member functions.
    /// </summary>
    public class MemberFunctionSymbol : FunctionSymbol
    {
        #region Fields
        private List<StorageModifierNode> storageModifiers;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the function</param>
        public MemberFunctionSymbol(string symbolicName, QualifiedNameNode qualifiedName)
            : base(symbolicName, qualifiedName)
        {
        }
        #endregion

        #region Properties
        /// <summary>
        /// The fully-qualified name of the type of which this function is a member
        /// </summary>
        public string TypeName
        {
            get
            {
                // The scope for a member function is a type
                return this.ScopeName;
            }
        }

        /// <summary>
        /// Is this a normal member function, or virtual or static?
        /// </summary>
        public MemberFunctionClassification MemberClassification { get; internal set; }
        /// <summary>
        /// Is this member function, public or private?
        /// </summary>
        public MemberProtectionLevel ProtectionLevel { get; internal set; }


        /// <summary>
        /// The ordered list of MS specific storage modifiers for the member function pointer, or null if none
        /// </summary>
        /// <remarks>This is normally an empty list, and most should contain no more than three modifiers</remarks>
        public IEnumerable<StorageModifierNode> StorageModifiers
        {
            get
            {
                return this.storageModifiers ?? Enumerable.Empty<StorageModifierNode>();
            }
            internal set
            {
                // Don't retain the modifier list if empty
                if (value == null || !value.Any())
                {
                    this.storageModifiers = null;
                }
                else
                {
                    this.storageModifiers = new List<StorageModifierNode>();
                    foreach (StorageModifierNode modifier in value)
                    {
                        modifier.Parent = this;
                        this.storageModifiers.Add(modifier);
                    }
                }
            }
        }

        /// <summary>
        /// Storage class flags for this member
        /// </summary>
        public StorageClass StorageClassification
        {
            get
            {
                // Should always be set once the full variable symbol is parsed
                return this.FunctionType.StorageClassification;
            }
        }

        public StorageClassNode Storage
        {
            get
            {
                return this.FunctionType.Storage;
            }
        }

        #endregion

        #region Displaying
        protected override void DisplayBodyOn(CppNameBuilder builder)
        {
            // Member functions are prefixed with public, private, etc, and also may be virtual or static
            builder.Append(this.ProtectionLevel);
            builder.Append(this.MemberClassification);

            // This wil emit the qualified name and function parameters - there must be some output
            base.DisplayBodyOn(builder);

            if (!(builder.HasOptions(UndecorateOptions.NameOnly) || builder.HasOptions(UndecorateOptions.NoMemberStorageClass)))
            {
                if (this.Storage != null)
                {
                    this.Storage.DisplayOn(builder, CppNameBuilder.Spacing.Leading);
                }
                builder.AppendMsKeywords(this.StorageModifiers);
            }
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is MemberFunctionSymbol);
            MemberFunctionSymbol copy = (MemberFunctionSymbol)deepenedCopy;
            copy.StorageModifiers = this.StorageModifiers.Select(each => Copy(each));
            return shallowCopy;
        }

        #endregion
    }

    // Class of nodes to represent data symbols, usually variables, but there are many other special types
    public abstract class DataSymbol : Symbol
    {
        #region Fields
        private StorageClassNode storage;
        private List<StorageModifierNode> storageModifiers;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the variable</param>
        protected DataSymbol(string symbolicName, QualifiedNameNode qualifiedName) : base(symbolicName, qualifiedName) { }
        #endregion

        #region Properties

        /// <summary>
        /// Storage attributes for the variable itself
        /// </summary>
        public StorageClassNode Storage
        {
            get
            {
                return this.storage;
            }

            internal set
            {
                this.storage = value;
                if (value != null)
                {
                    value.Parent = this;
                }
            }
        }

        /// <summary>
        /// Storage class flags for this variable
        /// </summary>
        public StorageClass StorageClassification
        {
            get
            {
                return this.Storage == null ? StorageClass.None : this.Storage.Classification;
            }
        }
        /// <summary>
        /// The ordered list of MS specific storage modifiers for the member function pointer, or null if none
        /// </summary>
        /// <remarks>This is normally an empty list, and most should contain no more than three modifiers</remarks>
        public IEnumerable<StorageModifierNode> StorageModifiers
        {
            get
            {
                return this.storageModifiers ?? Enumerable.Empty<StorageModifierNode>();
            }
            internal set
            {
                // Don't retain the modifier list if empty
                if (value == null || !value.Any())
                {
                    this.storageModifiers = null;
                }
                else
                {
                    this.storageModifiers = new List<StorageModifierNode>();
                    foreach (StorageModifierNode modifier in value)
                    {
                        modifier.Parent = this;
                        this.storageModifiers.Add(modifier);
                    }
                }
            }
        }


        /// <summary>
        /// The set of storage modifier flags specified for this variable
        /// </summary>
        public StorageModifiers StorageModifierFlags
        {
            get
            {
                // Should always be set once the full variable symbol is parsed
                return this.StorageModifiers.Aggregate(global::SymbolDecoder.StorageModifiers.None, (modifiers, each) => modifiers | each.Modifier);
            }
        }
        #endregion

        #region Displaying
        protected override void DisplayBodyOn(CppNameBuilder builder)
        {
            if (!builder.HasOptions(UndecorateOptions.NameOnly))
            {
                if (this.Storage != null)
                {
                    // TODO: Unit test needed for DataSymbol with storage
                    this.Storage.DisplayOn(builder, CppNameBuilder.Spacing.Trailing);
                }
                Debug.Assert(this.StorageModifiers.All(each => each.Parent == this));
                builder.AppendMsKeywords(this.StorageModifiers, CppNameBuilder.Spacing.Trailing);
            }
            UndecorateOptions options = builder.Options;
            if (options.HasFlag(UndecorateOptions.NameOnly))
            {
                // Replace NameOnly with NoCompoundTypeClass (i.e. we still suppress struct & class prefixes)
                options = options | UndecorateOptions.NoCompoundTypeClass;
            }
            builder.PushOptions(options);
            base.DisplayBodyOn(builder);
            builder.PopOptions();
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is DataSymbol);
            DataSymbol copy = (DataSymbol)deepenedCopy;
            copy.Storage = Copy(this.storage);
            copy.StorageModifiers = this.StorageModifiers.Select(each => Copy(each));
            return shallowCopy;
        }
        #endregion
    }

    public class SpecialDataSymbol : DataSymbol
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the variable</param>
        public SpecialDataSymbol(string symbolicName, QualifiedNameNode qualifiedName) : base(symbolicName, qualifiedName) { }
        #endregion
    }

    public class VtblSymbol : SpecialDataSymbol
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the variable</param>
        public VtblSymbol(string symbolicName, QualifiedNameNode qualifiedName) : base(symbolicName, qualifiedName) { } 
        #endregion

        #region Properties
        public QualifiedNameNode TargetName { get; set; }
        #endregion

        #region Displaying
        protected override void DisplayBodyOn(CppNameBuilder builder)
        {
            base.DisplayBodyOn(builder);
            if (this.TargetName != null && !builder.HasOptions(UndecorateOptions.NameOnly))
            {
                builder.Append("{for `");
                this.TargetName.DisplayOn(builder);
                builder.Append("'}");
            }
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is VtblSymbol);
            VtblSymbol copy = (VtblSymbol)deepenedCopy;
            copy.TargetName = Copy(this.TargetName);
            return shallowCopy;
        }
        #endregion

    }

    /// <summary>
    /// Node to represent symbols that are the names of variables, be they global or static member variables.
    /// </summary>
    public abstract class VariableSymbol : DataSymbol
    {
        #region Fields
        private TypeNode type;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the variable</param>
        protected VariableSymbol(string symbolicName, QualifiedNameNode qualifiedName) : base(symbolicName, qualifiedName) { }
        #endregion

        #region Properties
        /// <summary>
        /// The type of the variable
        /// </summary>
        public TypeNode VariableType
        {
            get
            {
                // Variables must always have a type
                Debug.Assert(this.type != null);
                return this.type;
            }

            internal set
            {
                this.type = value;
                value.Parent = this;
            }
        }

        /// <summary>
        /// Is this a "far" variable (as opposed to near)?
        /// </summary>
        /// <remarks>Obsolete - only used in 16-bit code</remarks>
        [Obsolete]
        public bool IsFar { get; internal set; }
        #endregion

        #region Displaying
        protected override void DisplayBodyOn(CppNameBuilder builder)
        {
            if (builder.HasOptions(UndecorateOptions.NameOnly) || this.type == null)
            {
                this.QualifiedName.DisplayOn(builder, CppNameBuilder.Spacing.None);
            }
            else
            {
                this.VariableType.DisplayOnAround(builder, CppNameBuilder.Spacing.None, (b, s) =>
                {
                    // Don't call the base as we need to work around the compiler bug that sets the storage of the variable
                    // to be the same as the target of a pointer (i.e. if we had "const char* blah", the compiler will encode 
                    // this as "const char* const blah", which is wrong as the pointer variable itself is not const, only the pointed
                    // at thing
                    if (!(b.NoUndnameEmulation && this.VariableType.IsIndirection))
                    {
                        if (this.Storage != null)
                        {
                            this.Storage.DisplayOn(b, CppNameBuilder.Spacing.Leading);
                        }
                        b.AppendMsKeywords(this.StorageModifiers, CppNameBuilder.Spacing.Leading);

                    }
                    // Finally print the qualified name of the variable afer the type
                    this.QualifiedName.DisplayOn(builder, s | CppNameBuilder.Spacing.Leading);
                    return true;
                });
            }
        }
        #endregion

        #region Copying
        protected override BaseSymbolNode DeepenShallowCopy(BaseSymbolNode shallowCopy)
        {
            BaseSymbolNode deepenedCopy = base.DeepenShallowCopy(shallowCopy);
            Debug.Assert(deepenedCopy is VariableSymbol);
            VariableSymbol copy = (VariableSymbol)deepenedCopy;
            copy.VariableType = Copy(this.type);
            return shallowCopy;
        }
        #endregion
    }

    /// <summary>
    /// Specialized variable node to represent static member variables
    /// </summary>
    public class StaticMemberVariableSymbol : VariableSymbol
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the variable</param>
        public StaticMemberVariableSymbol(string symbolicName, QualifiedNameNode qualifiedName, MemberProtectionLevel protectionLevel)
            : base(symbolicName, qualifiedName)
        {
            this.ProtectionLevel = protectionLevel;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The name of the type of which this variable is a static member
        /// </summary>
        public string TypeName
        {
            get
            {
                // In this case we know the scope is a type name
                return this.ScopeName;
            }
        }

        /// <summary>
        /// Access control, i.e. public, private or protected
        /// </summary>
        public MemberProtectionLevel ProtectionLevel { get; private set; }
        #endregion

        #region Displaying
        protected override void DisplayBodyOn(CppNameBuilder builder)
        {
            builder.Append(this.ProtectionLevel);
            builder.Append(MemberFunctionClassification.Static);
            base.DisplayBodyOn(builder);
        }
        #endregion
    }

    /// <summary>
    /// Specialized variable node to represent global variables
    /// </summary>
    public class GlobalVariableSymbol : VariableSymbol
    {
        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolicName">Mangled name</param>
        /// <param name="qualifiedName">The fully qualified name of the variable</param>
        public GlobalVariableSymbol(string symbolicName, QualifiedNameNode qualifiedName) : base(symbolicName, qualifiedName) { }
        #endregion
    }

    public class NullPtrTypeNode : TypeNode
    {
        public override string Name
        {
            get
            {
                return "std::nullptr_t";
            }
        }
    }
}

