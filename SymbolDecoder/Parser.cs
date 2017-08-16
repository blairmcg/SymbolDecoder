using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SymbolDecoder
{
    public enum ParserOptions
    {
        None = 0,
        /// <summary>Parse symbols which are just name fragments rather than regarding these as in error</summary>
        /// <remarks>UndecorateSymbolName will treat partial symbols as erroneous</remarks>
        AllowNameFragments = 0x1,
    };

    public class Parser : IDisposable
    {
        #region Fields
        /// <summary>
        /// The symbolic name in raw/mangled/decorated form, as emitted by the C++ compiler
        /// </summary>
        private readonly String mangledName;

        private readonly ParserOptions options;
        private Lexer lexer;
        #endregion

        /// <summary>
        /// Constructor
        /// </suummary>
        private Parser(String mangledName, ParserOptions options)
        {
            if (string.IsNullOrEmpty(mangledName)) throw new ArgumentNullException(nameof(mangledName));

            this.mangledName = mangledName;
            this.options = options;
            PushBackRefs();
        }

        /// <summary>
        ///  Parses the a decorated/mangled name and returns an AST for it
        /// </summary>
        /// <param name="mangledname">The symbolic name in raw/mangled/decorated form, as emitted by the C++ compiler</param>
        /// <returns>AST for the symbol</returns>
        public static Symbol Parse(String mangledname)
        {
            return Parse(mangledname, ParserOptions.None);
        }

        /// <summary>
        ///  Parses the a decorated/mangled name and returns an AST for it
        /// </summary>
        /// <param name="mangledName">The symbolic name in raw/mangled/decorated form, as emitted by the C++ compiler</param>
        /// <param name="options">Parsing/undecoration options</param>
        /// <returns>AST for the symbol</returns>
        public static Symbol Parse(String mangledName, ParserOptions options)
        {
            using (Parser parser = new Parser(mangledName, options))
            {
                return parser.Parse();
            }
        }

        #region Back ref management

        private class StackScope<T> : IDisposable where T : class, new()
        {
            private readonly Stack<T> stack;
            private T frame;

            public StackScope(Stack<T> stack)
            {
                this.stack = stack;
                this.frame = new T();
                stack.Push(this.frame);
            }


            public void Dispose()
            {
                if (this.frame != null)
                {
                    T popped = stack.Pop();
                    Debug.Assert(object.ReferenceEquals(this.frame, popped));
                    this.frame = null;
                }
            }
        }

        private class NameContext
        {
            public readonly List<IdentifierNode> Names = new List<IdentifierNode>();
            public readonly List<TypeNode> ParameterTypes = new List<TypeNode>();
            public readonly List<BaseSymbolNode> TemplateArgs = new List<BaseSymbolNode>();
        }

        private readonly Stack<NameContext> backRefStack = new Stack<NameContext>();

        private NameContext BackRefs
        {
            get
            {
                return this.backRefStack.Peek();
            }
        }

        private IDisposable PushBackRefs()
        {
            return new StackScope<NameContext>(this.backRefStack);
        }


        #endregion

        #region Lexical Analysis

        #endregion

        #region Recursive descent parser

        private void ReportError(string parseErrorFormat)
        {
            lexer.ReportError(parseErrorFormat);
        }

        private void ReportError(string parseErrorFormat, int position, char ch)
        {
            lexer.ReportError(parseErrorFormat, position, ch);
        }

        /// <summary>
        /// Parse a symbol in it's mangled/decoreated form into an AST
        ///     symbol ::== '?' qualifiedName '@' typeInfo
        /// </summary>
        /// <returns></returns>
        private Symbol Parse()
        {
            using (lexer = new Lexer(this.mangledName))
            {
                Symbol symbol = ParseSymbol();

                // We should now have consumed all the input
                if (!lexer.AtEnd)
                {
                    this.ReportError(ParseErrors.NonsenseAtEndOfSymbol);
                }

                return symbol;
            }
        }

        /// <summary>
        /// Parse a qualified name
        /// </summary>
        private Symbol ParseSymbol()
        {
            // All symbol names must start with a leading '?'
            if (lexer.CurrentCharClass != CharacterClass.Special)
            {
                this.ReportError(ParseErrors.BadSymbolStart);
            }
            lexer.MoveNext();

            switch (lexer.CurrentCharClass)
            {
                case CharacterClass.Special:
                    if (lexer.Next.CharacterClass == CharacterClass.Terminator)
                    {
                        // ??@ indicates a truncated name, which cannot be parsed correctly
                        this.ReportError(ParseErrors.TruncatedSymbol);
                    }
                    break;
                case CharacterClass.Terminator:
                    // Reserved CodeView variant of form ?@<symbol>
                    lexer.MoveNext();
                    return this.ParseCvSymbol();
                default:
                    // Drop through and parse symbol body
                    break;
            }

            return ParseSymbolBody();
        }

        Symbol ParseCvSymbol()
        {
            if (lexer.CurrentCharClass != CharacterClass.Special)
            {
                this.ReportError(ParseErrors.BadSymbolStart);
            }
            lexer.MoveNext();
            Symbol symbolNode = ParseSymbolBody();
            symbolNode.IsReservedCodeViewName = true;
            return symbolNode;
        }

        /// <summary>
        /// Parse the remainder of the symbol after the opening '?' marker
        /// </summary>
        /// <returns>Symbol node which is the root of the AST parsed from the mangled name</returns>
        private Symbol ParseSymbolBody()
        {
            Symbol symbolNode = null;

            // Special case of ??...
            if (lexer.CurrentCharClass == CharacterClass.Special && lexer.Next.CharacterClass == CharacterClass.Special)
            {
                // TODO: Unit test needed for double encoded symbol with ??? prefix

                // 3rd question mark indicates double encoded name for the CLR, just strip off the wrapping
                // and recurse
                // Skip over the second and third '?'s
                lexer.MoveNext(); lexer.MoveNext();
                symbolNode = this.ParseSymbolBody();
                // TODO: Strip the rest of the name - in case nested - how do we know where it ends?
            }
            else
            {
                // Normal case is some qualified name, followed by indicator as to whether the symbol of a function or data item
                QualifiedNameNode qualifiedName = ParseQualifiedSymbolName();

                switch (lexer.CurrentCharClass)
                {
                    case CharacterClass.EOF:
                        // Not sure where these are encountered in practice, but the mangled names can be constructed 
                        // which are effectively just a type name and presumably these may be a valid symbols.
                        // As it isn't a variable or function, one might assume it represents a type name, but we don't really
                        // know specifically what this symbol represents
                        if (this.options.HasFlag(ParserOptions.AllowNameFragments))
                        {
                            symbolNode = new Symbol(this.mangledName, qualifiedName);
                        }
                        else
                        {
                            this.ReportError(ParseErrors.PrematureEndOfSymbol);
                        }
                        break;

                    case CharacterClass.Digit:
                        symbolNode = this.ParseDataSymbol(qualifiedName);
                        break;

                    case CharacterClass.UppercaseLetter:
                        symbolNode = this.ParseFunctionSymbol(qualifiedName);
                        break;

                    default:
                        this.ReportError(ParseErrors.InvalidSymbolTypeCode);
                        break;
                }
            }

            return symbolNode;
        }

        private QualifiedNameNode ParseQualifiedSymbolName()
        {
            NameNode symbolName;

            switch (lexer.CurrentCharClass)
            {
                case CharacterClass.Special:
                    lexer.MoveNext();
                    symbolName = ParseSpecialSymbolName();
                    break;

                case CharacterClass.Template:
                    lexer.MoveNext();
                    symbolName = this.ParseTemplateNameAndArgs(true);
                    break;

                default:
                    symbolName = ParseIdentifier(NameOptions.None);
                    break;
            }

            QualifiedNameNode qualifiedName = new QualifiedNameNode(symbolName);
            this.ParseQualifiers(qualifiedName);
            return qualifiedName;
        }

        private Symbol ParseDataSymbol(QualifiedNameNode qualifiedName)
        {
            DataSymbol dataNode = null;

            switch (lexer.CurrentChar)
            {
                case '0':
                case '1':
                case '2':
                    // Static class member variable
                    {
                        StaticMemberVariableSymbol staticMember = new StaticMemberVariableSymbol(this.mangledName, qualifiedName, (MemberProtectionLevel)lexer.Current.Base10);
                        ParseVariable(staticMember);
                        dataNode = staticMember;
                    }
                    break;
                case '3':
                    // Global variable
                    {
                        GlobalVariableSymbol globalVar = new GlobalVariableSymbol(this.mangledName, qualifiedName);
                        ParseVariable(globalVar);
                        dataNode = globalVar;
                    }
                    break;

                case '6':
                    // vtbl
                    dataNode = ParseVtblSymbol(qualifiedName);
                    break;

                case '4':
                // guard
                case '5':
                // local
                case '7':
                    // TODO: Unit test needed for guard, local, etc, data encodings
                    // vbtable?
                    throw new NotImplementedException();

                case '8':
                    // metatype, e.g. RTTI
                    dataNode = new SpecialDataSymbol(this.mangledName, qualifiedName);
                    lexer.MoveNext();
                    break;

                case '9':
                default:
                    this.ReportError(ParseErrors.InvalidDataEncoding);
                    break;
            }
            return dataNode;
        }

        private DataSymbol ParseVtblSymbol(QualifiedNameNode qualifiedName)
        {
            lexer.MoveNext();
            var vtbl = new VtblSymbol(this.mangledName, qualifiedName);
            this.ParseDataStorage(vtbl);
            if (lexer.CurrentCharClass != CharacterClass.Terminator)
            {
                // Qualified name may follow, e.g. "x::vtbl{for `Def::Abc'}", where Def::Abc is the name
                vtbl.TargetName = ParseQualifiedName();
            }
            switch (lexer.CurrentCharClass)
            {
                case CharacterClass.Terminator:
                    lexer.MoveNext();
                    break;
                default:
                    lexer.ErrorUnexpectedCharacter();
                    break;
            }

            return vtbl;
        }

        private void ParseVariable(VariableSymbol variableNode)
        {
            // Advance over the variable class code
            lexer.MoveNext();

            variableNode.VariableType = this.ParseType();

            // There follows storage encoding for the variable itself, although this seems to be redundant in the case of 
            // pointer variables as this is also encoded in the pointer (or in the target). Also it seems to be set to the 
            // storage class of the target for a pointer variable, which seems wrong to me.
            ParseDataStorage(variableNode);

        }

        private void ParseDataStorage(DataSymbol dataNode)
        {
            dataNode.StorageModifiers = ParseStorageModifiers();
            dataNode.Storage = this.ParseStorageClass();
        }

        private StorageClassNode ParseStorageClass()
        {
            StorageClass storageCode = ParseStorageCode();

            if (storageCode == StorageClass.None)
            {
                return null;
            }

            NameNode baseName = null;
            if (storageCode.HasFlag(StorageClass.Based))
            {
                baseName = this.ParseBaseName();
            }

            CompoundTypeNode memberType = null;
            if (storageCode.HasFlag(StorageClass.Member))
            {
                Debug.Assert(!storageCode.HasFlag(StorageClass.Based));
                memberType = this.ParseCompoundType(CompoundTypeClass.Unknown);
                if (storageCode.HasFlag(StorageClass.Function))
                {
                    memberType.Storage = ParseStorageClass();
                }
            }


            // Note that based and member are mutually exclusive
            return baseName != null
                ? new BasedStorageClassNode(storageCode, baseName)
                : new StorageClassNode(storageCode, memberType);

        }

        private FunctionSymbol ParseFunctionSymbol(QualifiedNameNode qualifiedName)
        {
            FunctionSymbol functionNode;
            int encoding;
            StorageClassNode functionStorage = null;

            switch (lexer.CurrentChar)
            {
                // Member function encodings

                case 'A': // private member function       
                case 'B': // private far member function   
                case 'E': // private virtual member function
                case 'F': // private virtual far member function

                // G AND H??

                case 'I': // protected member function       
                case 'J': // protected far member function   
                case 'M': // protected virtual member function
                case 'N': // protected virtual far member function

                // 'O' and 'P'?

                case 'Q': // public member function       
                case 'R': // public far member function   
                case 'U': // public virtual member function
                case 'V': // public virtual far member function
                    {
                        encoding = lexer.CurrentChar - 'A';
                        MemberFunctionSymbol memberFunction = NewMemberFunction(qualifiedName, encoding);
                        // Advance to the storage class
                        lexer.MoveNext();
                        // Weirdly the pointer-base modifier for ptr64 appears on a member function
                        memberFunction.StorageModifiers = ParseStorageModifiers();
                        functionStorage = this.ParseStorageClass();
                        // TODO: Check for member pointer storage which is not valid here
                        Debug.Assert(functionStorage == null || functionStorage.MemberType == null);
                        functionNode = memberFunction;
                    }
                    break;

                // Static member function encodings - note no storage class

                case 'C': // private static member function
                case 'D': // private static far member function
                case 'K': // protected static member function
                case 'L': // protected static far member function
                case 'S': // public static member function
                case 'T': // public static far member function
                    encoding = lexer.CurrentChar - 'A';
                    functionNode = NewMemberFunction(qualifiedName, encoding);
                    // Advance to the return type (static members do not have a storage class)
                    lexer.MoveNext();
                    break;

                // W and X??

                // Global (non-member) functions

                case 'Y':
                case 'Z':
                    {
                        encoding = lexer.CurrentChar - 'Y';
                        GlobalFunctionSymbol function = new GlobalFunctionSymbol(this.mangledName, qualifiedName);
                        functionNode = function;
                        // Advance to the return type
                        lexer.MoveNext();
                    }
                    break;

                default:
                    this.ReportError(ParseErrors.InvalidFunctionClass);
                    return null;
            }

            functionNode.IsFar = (encoding % 2) != 0;

            functionNode.FunctionType = ParseFunctionType(functionStorage, !(qualifiedName.Identifier is SpecialMemberFunctionNode));

            return functionNode;
        }

        private FunctionTypeNode ParseFunctionType(StorageClassNode functionStorage, bool returnTypeExpected)
        {
            // Parse calling convention, return type storage class, return type, parameter type list, 'Z' )
            // Note that parameter type list is terminated with '@' or 'X' (void, no parameters), or 'Z' (varargs)
            // The return type appears before the parameters

            FunctionTypeNode functionType = new FunctionTypeNode(ParseCallingConvention())
            {
                Storage = functionStorage
            };

            // The return type for constructors and destructors is replaced with @ as these do not have a return type
            if (lexer.CurrentCharClass == CharacterClass.Terminator)
            {
                if (returnTypeExpected)
                {
                    this.ReportError(ParseErrors.ExpectedReturnType);
                }
                lexer.MoveNext();
            }
            else
            {
                functionType.ReturnType = ParseReturnType();
            }

            ParseFunctionParameters(functionType);

            return functionType;
        }

        private void ParseFunctionParameters(FunctionTypeNode functionTypeNode)
        {
            ParseParameterTypes(functionTypeNode);

            // Function names finish with a 'Z' (though this seems unecessary)
            if (lexer.CurrentChar != 'Z')
            {
                this.ReportError(ParseErrors.UnterminatedFunction);
            }
            lexer.MoveNext();
        }

        private MemberFunctionSymbol NewMemberFunction(QualifiedNameNode qualifiedName, int encoding)
        {
            Debug.Assert(encoding >= 0 && encoding < ((int)MemberProtectionLevel.Public + 1) * 8);
            MemberFunctionSymbol memberFunction = new MemberFunctionSymbol(this.mangledName, qualifiedName)
            {
                ProtectionLevel = (MemberProtectionLevel)(encoding / 8),
                MemberClassification = (MemberFunctionClassification)(encoding % 8 / 2)
            };
            return memberFunction;
        }

        private void ParseParameterTypes(FunctionTypeNode functionTypeNode)
        {
            bool endOfParameterList = false;
            do
            {
                switch (lexer.CurrentCharClass)
                {
                    case CharacterClass.EOF:
                        this.ReportError(ParseErrors.UnterminatedParameterList);
                        return;

                    case CharacterClass.Digit:
                        // Back reference to previously encountered (in the scope of the template definition) user-defined type
                        functionTypeNode.AddParameter(this.ParseParameterTypeBackRef());
                        break;

                    case CharacterClass.Template:
                    case CharacterClass.Extend:
                    case CharacterClass.UppercaseLetter:
                        endOfParameterList = ParseParameterType(functionTypeNode);
                        break;

                    case CharacterClass.Terminator:
                        lexer.MoveNext();
                        endOfParameterList = true;
                        break;

                    default:
                        lexer.ErrorUnexpectedCharacter();
                        return;
                }
            } while (!endOfParameterList);

            // Except in the case of varargs there must always be at least one parameter specified, as if there are none there will always be at least an X for "void"
            if (!(functionTypeNode.IsVarArgs || functionTypeNode.Parameters.Any()))
            {
                this.ReportError(ParseErrors.EmptyParameterList);
            }
        }

        private bool ParseParameterType(FunctionTypeNode functionTypeNode)
        {
            bool lastParameter = false;
            // Encodings for common types
            char typeCode = lexer.CurrentChar;
            if (typeCode == 'Z')
            {
                functionTypeNode.IsVarArgs = true;
                lastParameter = true;
                lexer.MoveNext();
            }
            else
            {
                int startPos = lexer.Position;
                TypeNode paramType = ParseType();
                functionTypeNode.AddParameter(paramType);
                if (typeCode == 'X')
                {
                    // Parmeter type of void, which must be the only parameter type, implicitly ends the parameter list
                    Debug.Assert(functionTypeNode.Parameters.Count() == 1);
                    lastParameter = true;
                }
                else
                {
                    // Add it to the types table for back referencing if more than a one char encoding
                    if (lexer.Position - startPos > 1)
                    {
                        List<TypeNode> backRefTypes = BackRefs.ParameterTypes;
                        if (backRefTypes.Count < 10)
                        {
                            backRefTypes.Add(paramType);
                        }
                    }
                }
            }
            return lastParameter;
        }

        private TypeNode ParseReturnType()
        {
            StorageClassNode storage;
            if (lexer.CurrentCharClass == CharacterClass.Special)
            {
                lexer.MoveNext();
                storage = ParseStorageClass();
                // TODO: Should probably raise a parsing error here if the storage class is invalid
                Debug.Assert(storage == null || !(storage is BasedStorageClassNode) && !(storage.Classification.HasFlag(StorageClass.Member)));
            }
            else
            {
                storage = null;
            }

            TypeNode typeNode = ParseType();

            // TODO: I think the storage should be associated with the function itself, rather than 
            // overridding that associated with the return type, the same as for variables
            if (storage != null)
            {
                typeNode.Storage = storage;
            }

            return typeNode;
        }

        private int ParseCallingConvention()
        {
            int encoding;
            if (lexer.CurrentCharClass == CharacterClass.UppercaseLetter && lexer.CurrentChar <= 'P')
            {
                encoding = lexer.CurrentChar - 'A';
            }
            else
            {
                encoding = 0;
                this.ReportError(ParseErrors.InvalidCallingConvention);
            }
            lexer.MoveNext();

            return encoding;
        }

        /// <summary>
        /// Parse a qualified type name, including templated names and back refs
        /// </summary>
        private QualifiedNameNode ParseQualifiedName()
        {
            NameNode identifier = ParseName(NameOptions.None);

            QualifiedNameNode qualifiedName = new QualifiedNameNode(identifier);
            this.ParseQualifiers(qualifiedName);
            return qualifiedName;
        }

        [Flags]
        private enum NameOptions
        {
            None = 0,
            Forget = 1,
            AllowEmpty = 2,
        }

        private NameNode ParseName(NameOptions flags)
        {
            NameNode name;
            if (lexer.CurrentCharClass == CharacterClass.Special)
            {
                // Might be a templated name, but we don't allow any other type of special name here
                lexer.MoveNext();
                if (lexer.CurrentCharClass != CharacterClass.Template)
                {
                    name = null;
                    this.ReportError(ParseErrors.ExpectedTemplateName);
                }
                else
                {
                    lexer.MoveNext();
                    name = ParseTemplateName();
                }
            }
            else
            {
                name = ParseIdentifier(flags);
            }
            return name;
        }


        ///// <summary>
        ///// Parse a reserved name. These are indicated by an initial '@?' sequence, but are otherwise just basic name fragments
        /////     reservedName ::= '@' '?' basicName qualifiers*
        ///// </summary>
        ///// <returns></returns>
        //private SymbolNode ParseReservedSymbol()
        //{
        //    Debug.Assert(lexer.CurrentCharClass == CharacterClass.Terminator);
        //
        //    if (lexer.MoveNext().CharacterClass != CharacterClass.Special)
        //    {
        //        this.ReportError(ParseErrors.BadSymbolStart);
        //    }
        //    lexer.MoveNext();
        //
        //    // ** Check if this should go in the name table or not
        //    IdentifierNode specialIdentifier = ParseIdentifier();
        //    ReservedQualifiedNameNode scopedName = new ReservedQualifiedNameNode();
        //    scopedName.Identifier = specialIdentifier;
        //    ParseQualifiers(scopedName);
        //
        //    // ** Is this actually really just same as a standard symbol?
        //
        //    SymbolNode symbolNode = new SymbolNode(this.symbol);
        //    symbolNode.QualifiedName = scopedName;
        //    return symbolNode;
        //}


        /// <summary>
        /// Parse a special name encoding for an operator, compiler generated artefact, or a template
        /// </summary>
        private NameNode ParseSpecialSymbolName()
        {
            NameNode specialName;
            switch (lexer.CurrentCharClass)
            {
                case CharacterClass.Digit:
                case CharacterClass.UppercaseLetter:
                    specialName = ParseOperator();
                    break;

                case CharacterClass.Extend:
                    specialName = ParseExtendedSpecialName();
                    break;

                case CharacterClass.Template:
                    lexer.MoveNext();
                    specialName = ParseTemplateNameAndArgs(true);
                    break;

                default:
                    specialName = null;
                    this.ReportError(ParseErrors.InvalidSpecialNameCode);
                    break;
            }
            return specialName;
        }

        /// <summary>
        ///  Parse an operator encoding
        /// </summary>
        private NameNode ParseOperator()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.Digit || lexer.CurrentCharClass == CharacterClass.UppercaseLetter);

            NameNode nameNode;

            // There are a few special cases where we need a different node type
            switch (lexer.CurrentChar)
            {
                case '0':
                    // Constructor
                    nameNode = new ConstructorNode();
                    break;
                case '1':
                    // Destructor
                    nameNode = new DestructorNameNode();
                    break;
                case 'B':
                    // Cast operator, e.g. operator int
                    nameNode = new CastOperatorNameNode();
                    break;

                default:
                    {
                        OperatorNameNode op = new OperatorNameNode((Operator)lexer.Current.Base36);
                        // There are no gaps in the operator encodings - the special cases above don't have operator names as such, but can't get here in those cases
                        Debug.Assert(op.OperatorName != null);
                        nameNode = op;
                    }
                    break;
            }

            // Advance past the operator code
            lexer.MoveNext();

            return nameNode;
        }

        private NameNode ParseExtendedSpecialName()
        {
            NameNode extendedSpecialName;

            switch (lexer.MoveNext().Character)
            {
                case '0':   // /=
                case '1':   // %=
                case '2':   // >>=
                case '3':   // <<=
                case '4':   // &=
                case '5':   // |=
                case '6':   // ^=
                    extendedSpecialName = new OperatorNameNode((Operator)36 + lexer.Current.Base36);
                    lexer.MoveNext();
                    break;

                case '7':   // vftable
                case '8':   // vbtable
                case '9':   // vcall
                case 'A':   // typeof
                case 'B':   // local static guard
                case 'C':   // string
                case 'D':   // vbase destructor
                case 'E':   // vector deleting destructor
                case 'F':   // default constructor closure
                case 'G':   // scalar deleting destructor
                case 'H':   // vector constructor iterator
                case 'I':   // vector destructor iterator
                case 'J':   // vector vbase constructor iterator
                case 'K':   // virtual displacement map   
                case 'L':   // eh vector constructor iterator
                case 'M':   // eh vector destructor iterator
                case 'N':   // eh vector vbase constructor iterator
                case 'O':   // copy constructor closure
                case 'P':   // udt returning            
                case 'Q':   // EH                                  
                case 'S':   // local vftable
                case 'T':   // local vftable constructor closure
                case 'W':   // omni callsig
                case 'X':   // placement delete closure
                case 'Y':   // placement delete[] closure         
                case 'Z':   // unused
                    extendedSpecialName = new SpecialNameNode((CompilerSpecialName)lexer.Current.Base36 - 7);
                    lexer.MoveNext();
                    break;

                case 'U':   // new[]                               
                case 'V':   // delete[]         
                    // TODO: Need to add node classes for these, or extend OperatorNameNode to handle it?
                    throw new NotImplementedException();

                case 'R':   // `RTTI       
                    extendedSpecialName = ParseRtti();
                    break;

                case '_':
                    extendedSpecialName = ParseDoubleExtendedSpecialName();
                    break;

                default:
                    extendedSpecialName = null;
                    this.ReportError(ParseErrors.InvalidSpecialNameCode);
                    break;
            }
            // Advanced past the special name encoding
            return extendedSpecialName;
        }

        private RttiNameNode ParseRtti()
        {
            RttiNameNode rttiNode;

            switch (lexer.MoveNext().Character)
            {
                case '0':
                    lexer.MoveNext();
                    rttiNode = new RttiTypeDescriptorNode(this.ParseReturnType());
                    break;

                case '1':
                    lexer.MoveNext();
                    rttiNode = ParseRttiBaseClassDescriptor();
                    break;

                case '2':
                case '3':
                case '4':
                    rttiNode = new RttiNameNode((RttiCode)lexer.Current.Base10);
                    lexer.MoveNext();
                    break;

                default:
                    rttiNode = null;
                    this.ReportError(ParseErrors.InvalidRttiCode);
                    break;
            }
            return rttiNode;
        }

        private RttiBaseClassDescriptorNode ParseRttiBaseClassDescriptor()
        {
            List<Int64> dimensions = Enumerable.Range(0, 3).Select(i => ParseIntegerEncoding()).ToList();
            dimensions.Add(ParseUnsignedIntegerEncoding());
            return new RttiBaseClassDescriptorNode(dimensions);
        }

        private SpecialNameNode ParseDoubleExtendedSpecialName()
        {
            SpecialNameNode doubleExtendedSpecialName;

            switch (lexer.MoveNext().CharacterClass)
            {
                case CharacterClass.Digit:
                case CharacterClass.UppercaseLetter:
                    // TODO: Unit tests needed for valid double extended special names

                    doubleExtendedSpecialName = new SpecialNameNode((CompilerSpecialName)(36 - 7 + lexer.Current.Base36));
                    lexer.MoveNext();
                    break;

                default:
                    doubleExtendedSpecialName = null;
                    this.ReportError(ParseErrors.InvalidSpecialNameCode);
                    break;
            }
            return doubleExtendedSpecialName;
        }

        /// <summary>
        /// Parse a template name. This is an identifier, followed by the template parameters
        /// Note that the parameters appear before the qualifiers
        ///     templatedName ::= '$' name templateArguments
        /// </summary>
        private TemplateNameNode ParseTemplateName()
        {
            TemplateNameNode template = ParseTemplateNameAndArgs(false);

            // The full template name needs to be recorded in the outer scope at it can now be back referenced
            this.RememberIdentifier(template);

            return template;
        }

        private TemplateNameNode ParseTemplateNameAndArgs(bool allowUnterminatedArgList)
        {
            // lexer is positioned after the $

            // Templates are their own back ref scope
            using (this.PushBackRefs())
            {
                // The templates own name goes into the name list, but only the primary name
                // (although a template name can be fully qualified, the qualifiers follow the 
                // template parameters
                IdentifierNode identifier = ParseIdentifier(NameOptions.AllowEmpty);

                List<BaseSymbolNode> parms = ParseTemplateArgumentList(allowUnterminatedArgList);
                return new TemplateNameNode(identifier.Name, parms);
            }
        }

        /// <summary>
        /// Parse the template parameter list. This can include other types (potentially templates, and
        /// certain literal encodings (in C++ templates can be parameterised with literal values as well as types)
        /// </summary>
        /// <param name="allowUnterminated">Whether to tolerate EOF before encountering a terminator, which we do if parsing a top-level name</param>
        private List<BaseSymbolNode> ParseTemplateArgumentList(bool allowUnterminated)
        {
            List<BaseSymbolNode> parms = new List<BaseSymbolNode>();
            while (lexer.CurrentCharClass != CharacterClass.Terminator)
            {
                if (lexer.AtEnd)
                {
                    if (!allowUnterminated)
                    {
                        this.ReportError(ParseErrors.UnterminatedTemplateParameterList);
                    }
                    return parms;

                }
                parms.Add(ParseTemplateArgument());
            }
            // Position after the template terminator
            lexer.MoveNext();
            return parms;
        }

        private BaseSymbolNode ParseTemplateArgument()
        {
            BaseSymbolNode templateArg;
            int startPos = lexer.Position;
            switch (lexer.CurrentCharClass)
            {
                case CharacterClass.Digit:
                    // TODO: Unit test needed for template arg back refs

                    // Back reference to previously encountered (in the scope of the template definition) user-defined type
                    templateArg = this.ParseTemplateArgBackRef();
                    break;

                case CharacterClass.Extend:
                case CharacterClass.UppercaseLetter:
                    templateArg = ParseType();
                    break;

                case CharacterClass.Template:
                    if (lexer.Next.CharacterClass != CharacterClass.Template)
                    {
                        lexer.MoveNext();
                        templateArg = ParseConstTemplateArg();
                    }
                    else
                    {
                        templateArg = ParseType();
                    }
                    break;

                case CharacterClass.Special:
                    lexer.MoveNext();
                    templateArg = ParseTemplateArgIndexed();
                    break;

                default:
                    templateArg = null;
                    this.ReportError(ParseErrors.InvalidTemplateArgument);
                    break;

            }

            // If the encoding requires more than character, it will be replaced by a back ref if it appears againea
            if (lexer.Position - startPos > 1)
            {
                List<BaseSymbolNode> args = this.BackRefs.TemplateArgs;
                if (args.Count < 10)
                {
                    args.Add(templateArg);
                }
            }

            return templateArg;
        }



        private BaseSymbolNode ParseConstTemplateArg()
        {
            BaseSymbolNode constantNode = null;

            switch (lexer.CurrentChar)
            {
                case '0':
                    lexer.MoveNext();
                    constantNode = ParseIntegerTemplateArg();
                    break;

                case '1':
                    lexer.MoveNext();
                    constantNode = ParseTemplateArgAddress();
                    break;

                case '2':
                    lexer.MoveNext();
                    constantNode = ParseTemplateArgFloat();
                    break;

                case 'E':
                    // TODO: Unit test needed for symbol template arg
                    lexer.MoveNext();
                    constantNode = ParseSymbol();
                    break;

                case 'D':
                    lexer.MoveNext();
                    constantNode = ParseTemplateArgIndexed(false);
                    break;

                case 'Q':
                    lexer.MoveNext();
                    constantNode = ParseTemplateArgIndexed(true);
                    break;

                case 'R':
                    lexer.MoveNext();
                    constantNode = ParseTemplateArgIndexedNamed();
                    break;

                case 'F':
                case 'G':
                case 'H':
                case 'I':
                case 'J':
                    // TODO: Unit test needed for the template curly types
                    TemplateCurlyType curlyType = (TemplateCurlyType)lexer.CurrentChar;
                    lexer.MoveNext();
                    constantNode = ParseTemplateArgCurly(curlyType);
                    break;

                default:
                    this.ReportError(ParseErrors.InvalidTemplateConst);
                    break;
            }
            return constantNode;
        }

        private BaseSymbolNode ParseTemplateArgCurly(TemplateCurlyType curlyType)
        {
            // TODO: Unit test needed for template curly types

            TemplateParameterCurlyNode curlyNode = new TemplateParameterCurlyNode(curlyType);

            // Don't really know what these represent, but the format is straightforward enough
            // so we can parse it and generate the undercorated form, even if we don't know what
            // it represents.

            if (curlyType >= TemplateCurlyType.Mptmf)
            {
                curlyNode.AddPart(this.ParseSymbol());
            }

            if (curlyType == TemplateCurlyType.Gptmd || curlyType == TemplateCurlyType.Gptmf)
            {
                curlyNode.AddPart(this.ParseInteger());
            }

            if (curlyType != TemplateCurlyType.Mptmf)
            {
                curlyNode.AddPart(this.ParseInteger());
            }

            curlyNode.AddPart(this.ParseInteger());

            return curlyNode;
        }

        private BaseSymbolNode ParseTemplateArgIndexedNamed()
        {
            NameNode name = ParseName(NameOptions.Forget);
            Int64 index = ParseIntegerEncoding();
            return new NamedTemplateParameterNode(index, name);
        }

        private IndexedTemplateParameterNode ParseTemplateArgIndexed()
        {
            // Not entirely sure in which circumstances these would appear in compiled output - seems to be something
            // to do with template specialization. Anyway, we have very little information about these template arguments
            // in the mangled name

            IndexedTemplateParameterNode param = ParseTemplateArgIndexed(false);
            param.SignedDimensionBug = true;
            return param;
        }

        private IndexedTemplateParameterNode ParseTemplateArgIndexed(bool isNonType)
        {
            Int64 index = ParseIntegerEncoding();
            IndexedTemplateParameterNode param = new IndexedTemplateParameterNode(index, isNonType);
            return param;
        }

        private BaseSymbolNode ParseTemplateArgFloat()
        {
            //
            // template-floating-point-constant ::=
            //		<normalized-mantissa><exponent>
            //

            Int64 mantissa = ParseIntegerEncoding();
            Int64 exponent = ParseIntegerEncoding();

            double value = (mantissa / 10.0) * Math.Pow(10.0, exponent - 1);
            return new FloatingPointLiteralNode(value);
        }

        private AddressOfNode ParseTemplateArgAddress()
        {
            //
            // template-address-constant ::=
            //		'@'			// Null pointer
            //		<symbol-name>
            //

            Symbol target;
            if (lexer.CurrentCharClass == CharacterClass.Terminator)
            {
                target = null;
                lexer.MoveNext();
            }
            else
            {
                target = this.ParseSymbol();
            }

            return new AddressOfNode(target);
        }

        private BaseSymbolNode ParseIntegerTemplateArg()
        {
            Int64 multiplier = 1;
            if (lexer.CurrentCharClass == CharacterClass.Special)
            {
                multiplier = -1;
                lexer.MoveNext();
            }

            if (lexer.CurrentChar == 'Q')
            {
                // TODO: Try to generate this case for real - I think it is invalid
                // "`non-type-template-parameter"

                lexer.MoveNext();
                var parm = new IndexedTemplateParameterNode(ParseUnsignedIntegerEncoding() * multiplier, true)
                {
                    MissingCloseQuoteBug = true
                };
                return parm;
            }
            else
            {
                return new LiteralNode<Int64>(ParseUnsignedIntegerEncoding() * multiplier);
            }
        }

        private LiteralNode<Int64> ParseInteger()
        {
            return new LiteralNode<Int64>(ParseIntegerEncoding());
        }

        private Int64 ParseIntegerEncoding()
        {
            Int64 multiplier = 1;
            if (lexer.CurrentCharClass == CharacterClass.Special)
            {
                multiplier = -1;
                lexer.MoveNext();
            }

            return ParseUnsignedIntegerEncoding() * multiplier;
        }

        private Int64 ParseUnsignedIntegerEncoding()
        {
            // TODO: Test overflow cases

            Int64 dim = 0;

            if (lexer.CurrentCharClass == CharacterClass.Digit)
            {
                // Single integer code to represent 1..10
                dim = (Int64)(lexer.Current.Base10 + 1);
            }
            else
            {
                // Alpahnumeric encoding terminated by '@'
                do
                {
                    if (lexer.CurrentCharClass != CharacterClass.UppercaseLetter ||
                        lexer.CurrentChar > 'P')
                    {
                        // TODO: Unit test needed for invalid dimenion encoding
                        this.ReportError(ParseErrors.InvalidDimension);
                        break;
                    }
                    dim = (dim << 4) + (Int64)(lexer.CurrentChar - 'A');
                    lexer.MoveNext();
                }
                while (lexer.CurrentCharClass != CharacterClass.Terminator);
            }

            // Advance over the terminator or the single digit
            lexer.MoveNext();
            return dim;
        }

        private BaseSymbolNode ParseTemplateArgBackRef()
        {
            // TODO: Unit test needed

            Debug.Assert(lexer.CurrentCharClass == CharacterClass.Digit);
            int index = lexer.Current.Base10;
            Debug.Assert(index >= 0);
            List<BaseSymbolNode> args = this.BackRefs.TemplateArgs;
            if (index >= args.Count)
            {
                // TODO: Unit test needed
                this.ReportError(ParseErrors.InvalidBackRef);
            }
            lexer.MoveNext();
            return BaseSymbolNode.Copy(args[index]);
        }


        private TypeNode ParseType()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.UppercaseLetter
                || lexer.CurrentCharClass == CharacterClass.Template    // Special extended types
                || lexer.CurrentCharClass == CharacterClass.Extend);    // Extended primitive types

            TypeNode typeNode;

            switch (lexer.CurrentChar)
            {
                case 'A':
                    // Reference to another type
                    lexer.MoveNext();
                    typeNode = ParseReferenceType<ReferenceTypeNode>(false);
                    break;
                case 'B':
                    // Volatile reference to another type
                    lexer.MoveNext();
                    typeNode = ParseReferenceType<ReferenceTypeNode>(true);
                    break;
                case '$':
                    lexer.MoveNext();
                    typeNode = ParseSpecialExtendedType();
                    break;
                default:
                    typeNode = ParseNonRefType(null);
                    break;
            }

            return typeNode;
        }

        /// <summary>
        /// Parse special extended types represented as $$A, where A is one of a limited
        /// range of uppercase letter type codes, e.g. $$T for std::nullptr_t.
        /// </summary>
        /// <returns></returns>
        private TypeNode ParseSpecialExtendedType()
        {
            if (lexer.CurrentCharClass != CharacterClass.Template)
            {
                this.ReportError(ParseErrors.UnexpectedCharacter);
            }
            lexer.MoveNext();

            TypeNode extendedTypeNode = null;

            switch (lexer.CurrentChar)
            {
                case 'A':
                    // A function (not a pointer to a function). Templates can be parameterised by functions.
                    lexer.MoveNext();
                    extendedTypeNode = ParseExtendedFunctionType();
                    break;

                case 'B':     // PDT_ex_other	
                case 'C':     // PDT_ex_qualified
                    throw new NotImplementedException();

                case 'Q': // &&	
                    lexer.MoveNext();
                    extendedTypeNode = ParseReferenceType<RvalueReferenceTypeNode>(false);
                    break;

                case 'R': // volatile &&
                    lexer.MoveNext();
                    extendedTypeNode = ParseReferenceType<RvalueReferenceTypeNode>(true);
                    break;

                case 'T': // std::nullptr_t
                    lexer.MoveNext();
                    extendedTypeNode = new NullPtrTypeNode();
                    break;

                case 'D': // ellipsis?
                case 'S': // PDT_ex_nullptr	- seems to be obsolete		
                default:  // 'E' ... 'P' are used to encode managed-ness using a similar pattern (apparently)
                    this.ReportError(ParseErrors.InvalidExtendedTypeCode);
                    break;
            }

            return extendedTypeNode;
        }

        private TypeNode ParseExtendedFunctionType()
        {
            TypeNode extendedTypeNode;

            // Remember the current token in case needed for reporting invalid storage later
            Lexer.Token tok = lexer.Current;
            StorageClassNode functionStorage = ParseStorageClass();
            if (functionStorage == null || !functionStorage.IsFunction)
            {
                extendedTypeNode = null;
                lexer.ReportError(ParseErrors.InvalidFunctionStorage, tok.Position, tok.Character);
            }
            else
            {
                extendedTypeNode = ParseFunctionType(functionStorage, true);
            }
            return extendedTypeNode;
        }

        private TypeNode ParseNonRefType(StorageClassNode storage)
        {
            TypeNode typeNode;

            switch (lexer.CurrentChar)
            {
                case '_': // Extended primitive types
                    lexer.MoveNext();
                    typeNode = ParseTypeCode(1, ParseErrors.UnusedExtendedTypeCode);
                    break;
                case '$': // Special extended types
                    lexer.MoveNext();
                    typeNode = ParseSpecialExtendedType();
                    break;

                case 'P':
                case 'Q': // *const
                case 'R': // *volatile
                case 'S': // *const volatile
                          // Storage class of the pointer is encoded in the pointer code
                          // although only for const/volatile
                    StorageClass pointerStorageClass = (StorageClass)lexer.CurrentChar - 'P';
                    lexer.MoveNext();
                    typeNode = ParsePointerType(pointerStorageClass);
                    break;

                case 'T':
                case 'U':
                case 'V':
                    CompoundTypeClass typeClass = (CompoundTypeClass)(lexer.CurrentChar - 'T' + 1);
                    lexer.MoveNext();
                    typeNode = ParseCompoundType(typeClass);
                    break;

                case 'W':
                    lexer.MoveNext();
                    typeNode = ParseEnum();
                    break;

                default:
                    // Note that 'A', for reference, will throw a parse error here
                    typeNode = ParseTypeCode(0, ParseErrors.UnusedTypeCode);
                    break;
            }
            if (storage != null)
            {
                typeNode.Storage = storage;
            }
            return typeNode;
        }

        private TypeNode ParseEnum()
        {
            EnumBaseType baseTypeCode = ParseEnumBaseType();
            // Advance over the base type encoding
            lexer.MoveNext();

            QualifiedNameNode qualifiedName = this.ParseQualifiedName();

            return new EnumTypeNode(qualifiedName, baseTypeCode);
        }

        private EnumBaseType ParseEnumBaseType()
        {
            if (lexer.CurrentCharClass == CharacterClass.Digit)
            {
                int index = lexer.Current.Base10;
                if (index <= (int)EnumBaseType.UnsignedLong)
                {
                    return (EnumBaseType)index;
                }
            }
            this.ReportError(ParseErrors.InvalidEnumType);
            return EnumBaseType.Int;
        }

        private CompoundTypeNode ParseCompoundType(CompoundTypeClass typeClass)
        {
            // Expecting a qualified name for the type, possibly templatized or a back reference
            QualifiedNameNode qualifiedName = this.ParseQualifiedName();

            CompoundTypeNode typeNode = new CompoundTypeNode(typeClass, qualifiedName);

            return typeNode;
        }

        private PrimitiveTypeNode ParseTypeCode(int extended, string unusedErrorMessage)
        {
            Debug.Assert(extended >= 0 && extended <= 1);

            PrimitiveTypeNode typeNode = null;

            if (lexer.CurrentCharClass == CharacterClass.UppercaseLetter)
            {
                int index = lexer.CurrentChar - 'A';
                Debug.Assert(index >= 0 && index < 26);
                int encoding = extended * 26 + index;
                typeNode = new PrimitiveTypeNode((PrimitiveTypeCodes)encoding);
                if (typeNode.Name == null)
                {
                    this.ReportError(unusedErrorMessage);
                }
            }
            else
            {
                this.ReportError(ParseErrors.InvalidTypeEncoding);
            }

            lexer.MoveNext();
            return typeNode;
        }

        /// <summary>
        /// Parse a reference type:
        ///     refType ::= 'A' [prefix* modifier] storage-class type-code
        /// where storage-class is 'A' (default), 'B' const, 'C' volatile, or 'D' const volatile, 
        /// and type-code is one of the primitive type codes
        /// </summary>
        /// <param name="isVolatile">Is this a volatile reference?</param>
        private T ParseReferenceType<T>(bool isVolatile) where T : ReferenceTypeNode, new()
        {
            StorageClassNode referenceStorage = isVolatile ? new StorageClassNode(StorageClass.Volatile, null) : null;

            // The modifiers are for the reference itself. Some modifiers are not valid on references,
            // but we don't treat their presence as an error
            List<StorageModifierNode> referenceModifiers = ParseStorageModifiers();

            // TODO: Do we need to do anything with the unaligned modifier here

            // The storage class that follows is for the target
            StorageClassNode targetStorage = ParseNonPointerStorageClass();

            // Catch ref to ref immediately because it will otherwise give a unhelpful error
            if (lexer.CurrentChar == 'A')
            {
                this.ReportError(ParseErrors.DoubleReference);
            }

            int pos = lexer.Position;
            char ch = lexer.CurrentChar;
            TypeNode targetType = ParseNonRefType(targetStorage);

            // Detect ref to rvalue ref before parsing the target type to avoid looking ahead 3 positions 
            if (targetType is ReferenceTypeNode)
            {
                // Ref to rvalue ref
                this.ReportError(ParseErrors.DoubleReference, pos, ch);
            }

            T refNode = new T
            {
                TargetType = targetType,
                StorageModifiers = referenceModifiers,
                Storage = referenceStorage
            };
            return refNode;
        }

        private StorageClassNode ParseNonPointerStorageClass()
        {
            StorageClass storageCode = ParseStorageCode();
            if (storageCode == StorageClass.None)
            {
                return null;
            }

            // TODO: Unit test neded

            // Should only have const/volatile
            if (storageCode.HasFlag(~StorageClass.CvMask))
            {
                this.ReportError(ParseErrors.InvalidStorageModifierForNonPointer);
            }

            return new StorageClassNode(storageCode, null);
        }

        /// <summary>
        /// Parse the raw storage class encoding, which is encoded using 5 bits as follows
        ///  |------------- Member pointer
        ///  |  |---------- Huge pointer (historical)
        ///  |  | |-------- Far pointer (historical)
        ///  |  | | |------ Volatile
        ///  |  | | | |---- Const
        /// |16|8|4|2|1|
        ///
        /// This encoding seems to have got a bit messy over time and now has a number of unused
        /// flags from 16-bit days, and some re-used for other purposes.
        ///
        /// In 32 and 64 bit code the Huge and Far flags are not used for their
        /// original purpose, but instead set together for a based (relative) pointer
        /// </summary>
        private StorageClass ParseStorageCode()
        {
            StorageClass code;

            // Validate the encoding
            switch (lexer.CurrentChar)
            {
                case '2':   // based member variable
                case '3':   // const based member variable
                case '4':   // volatile based member variable
                case '5':   // const volatile based member variable
                    // Current compiler does not support based member variables as far as I can tell
                    // TODO: Unit test needed
                    code = (StorageClass)lexer.CurrentChar - '2' | StorageClass.Based | StorageClass.Member;
                    break;

                case '6':   // function
                case '7':   // far function
                    // Ignore far-ness as no longer relevant (compiler only ever emits '6')
                    code = StorageClass.Function;
                    break;

                case '8':   // member function
                case '9':   // far member function
                    code = StorageClass.Member | StorageClass.Function;
                    break;

                case 'A': // None (0)
                case 'B': // Const (0x1)
                case 'C': // Volatile (0x2)
                case 'D': // Const | Volatile (0x3)
                case 'M': // Based (0xC)
                case 'N': // Based const ? (0xD)
                case 'O': // Based volatile (0xE)
                case 'P': // Based const volatile (0xF)
                case 'Q': // Member pointer       (0x10)
                case 'R': // Member const?        (0x11)
                case 'S': // Member volatile?     (0x12)
                case 'T': // Member const volatile?   (0x13)
                    code = (StorageClass)lexer.CurrentChar - 'A';
                    // Only 5 bits can used, but in fact as this is A..Z, in practice we are limited
                    // to 0..26, which implies a pointer cannot be based and a member pointer
                    Debug.Assert(!code.HasFlag(StorageClass.Based) || !code.HasFlag(StorageClass.Member));
                    break;

                case '_': // Extended encoding for based functions - current compiler does not support these
                    lexer.MoveNext();
                    code = StorageClass.Function | StorageClass.Based;
                    switch (lexer.CurrentChar)
                    {
                        case 'A':
                        case 'B':
                            // Ignore far-ness
                            break;
                        case 'C':
                        case 'D':
                            // TODO: Unit test needed
                            code |= StorageClass.Member;
                            break;
                        default:
                            // TODO: Unit test needed
                            code = StorageClass.None;
                            this.ReportError(ParseErrors.InvalidStorageClass);
                            break;
                    }
                    break;

                case 'E': // Was Far (now used as modifier) (0x4)
                case 'F': // Was Far Const (now used as modifier) (0x5)
                case 'G': // Was Far Volatile (now unused) (0x6)
                case 'H': // Was Far Volatile Const (now unused) (0x7)
                case 'I': // Was Huge (now used as modifier) (0x8)
                case 'J': // Was Huge Cont (now unused) (0x9)
                case 'K': // was Huge Volatile (now unused) (0xA)
                case 'L': // Was Huge Volatile Const (now unused) (0xB)
                case 'U': // Was member Far (unused?) (0x14)
                case 'V': // Was Member Far Const (unused?) (0x15)
                case 'W': // Was Member Far Volatile (unused?) (0x16)
                case 'X': // Was Member Far Volatile Const (unused?) (0x17)
                case 'Y': // Was Member Huge ? (0x18)
                case 'Z': // Was Member Huge Const ? (0x19)
                default:
                    // TODO: Unit test needed
                    code = StorageClass.None;
                    this.ReportError(ParseErrors.InvalidStorageClass);
                    break;
            }


            // Advance over storage class code (there must be one)
            lexer.MoveNext();

            return code;
        }


        /// <summary>
        /// Parse the storage modifiers as a list of nodes (if any).
        /// </summary>
        /// <remarks>
        /// These can theoretically appear in any order and in any number
        /// Semantically there should only be one instance of each flag, but there's nothing
        /// particularly preventing more than one of each type in a symbol name. Shouldn't occur
        /// in practice, but UndecorateSymbolName decodes them all so we'll do the same.
        /// A 64-bit build will have a Ptr64 modifier on almost every pointer type.
        /// </remarks>
        private List<StorageModifierNode> ParseStorageModifiers()
        {
            var modifiers = new List<StorageModifierNode>();
            while (!lexer.AtEnd)
            {
                switch (lexer.CurrentChar)
                {
                    case 'E':
                        modifiers.Add(new StorageModifierNode(StorageModifiers.Ptr64));
                        break;
                    case 'F':
                        // This modifier must appear before the '*', the others afer it
                        modifiers.Add(new StorageModifierNode(StorageModifiers.Unaligned));
                        break;
                    case 'I':
                        modifiers.Add(new StorageModifierNode(StorageModifiers.Restrict));
                        break;
                    default:
                        // List terminated by any other character
                        return modifiers;
                }

                // Advance to next modifier (if any more)
                lexer.MoveNext();
            }
            return modifiers;
        }

        private TypeNode ParsePointerType(StorageClass pointerStorageClass)
        {
            List<StorageModifierNode> pointerModifiers = ParseStorageModifiers();

            StorageClassNode targetStorage = ParseStorageClass();
            TypeNode targetType = targetStorage != null && targetStorage.Classification.HasFlag(StorageClass.Function)
                ? ParseFunctionType(targetStorage, true)
                : ParseNonRefType(targetStorage);

            IndirectionTypeNode pointerNode = new PointerTypeNode(targetType)
            {
                StorageModifiers = pointerModifiers
            };
            if (pointerStorageClass != StorageClass.None)
            {
                pointerNode.Storage = new StorageClassNode(pointerStorageClass, null);
            }

            return pointerNode;
        }

        private NameNode ParseBaseName()
        {
            NameNode baseName;
            switch (lexer.CurrentChar)
            {
                case '0':   // void
                    lexer.MoveNext();
                    baseName = new PrimitiveTypeNode(PrimitiveTypeCodes.Void);
                    break;
                case '2': // historically __near, now used for scoped name
                    lexer.MoveNext();
                    baseName = ParseQualifiedName();
                    break;

                case '1': // __self (historic, 16-bit only)
                case '3': // NYI:__far (historic, 16-bit only)    
                case '4': // NYI:__huge	(historic, 16-bit only)
                case '5': // __based (reserved)
                case '6': // NYI:__segment	(historic, 16-bit only)
                case '7': // __segname("name")	(historic, 16-bit only)
                case '8': // NYI:<segment-address-of-variable>	(historic, 16-bit only)
                default:
                    baseName = null;
                    this.ReportError(ParseErrors.InvalidBasedPointerType);
                    break;
            }
            return baseName;
        }

        IdentifierNode ParseIdentifier(NameOptions nameOptions)
        {
            IdentifierNode identifier;

            if (lexer.CurrentCharClass == CharacterClass.Digit)
            {
                // Back reference to a previously seen name
                identifier = ParseNameBackRef();
            }
            else
            {
                string name = ParseNameFragment(nameOptions.HasFlag(NameOptions.AllowEmpty));

                identifier = new IdentifierNode(name);
                if (!nameOptions.HasFlag(NameOptions.Forget))
                {
                    RememberIdentifier(identifier);
                }
            }
            return identifier;
        }

        private void RememberIdentifier(IdentifierNode identifier)
        {
            List<IdentifierNode> names = this.BackRefs.Names;
            if (names.Count < 10)
            {
                names.Add(identifier);
            }
        }

        private string ParseNameFragment(bool allowEmpty)
        {
            int start = lexer.Position;
            while (lexer.CurrentCharClass != CharacterClass.Terminator)
            {
                if (lexer.AtEnd)
                {
                    this.ReportError(ParseErrors.UnterminatedName);
                }
                if (!lexer.Current.IsValidIdentifierCharacter)
                {
                    this.ReportError(ParseErrors.InvalidIdentifierChar);
                }
                lexer.MoveNext();
            }

            // Now positioned on the terminator, so the length is ...
            int len = lexer.Position - start;

            if (len == 0 && !allowEmpty)
            {
                this.ReportError(ParseErrors.EmptyName);
            }

            // Position just after the terminator
            lexer.MoveNext();

            // Position is 1-based
            return this.mangledName.Substring(start - 1, len);
        }

        /// <summary>
        ///  Parse qualifier list (i.e. the scope)
        ///     qualifers ::= qualifier* '@'
        /// </summary>
        /// <param name="qualifiedName">Qualified name to which the qualifiers are added</param>
        private void ParseQualifiers(QualifiedNameNode qualifiedName)
        {
            while (lexer.CurrentCharClass != CharacterClass.Terminator)
            {
                if (lexer.AtEnd)
                {
                    if (!this.options.HasFlag(ParserOptions.AllowNameFragments))
                    {
                        this.ReportError(ParseErrors.UnterminatedQualifiedName);
                    }
                    return;
                }
                qualifiedName.AddQualifier(ParseQualifier());
            }
            // Position after the name termiantor
            lexer.MoveNext();
            // Name and qualifiers now parsed, next is type info
        }

        /// <summary>
        /// Parse a single qualifier from the input stream
        ///     qualifier ::= name | specialQualifier | numberedNamespace | backReference
        /// </summary>
        private NameNode ParseQualifier()
        {
            Debug.Assert(!lexer.AtEnd);

            NameNode qualifier = lexer.CurrentCharClass == CharacterClass.Special
                                        ? this.ParseSpecialQualifier()
                                        : ParseIdentifier(NameOptions.None);
            return qualifier;
        }

        private NameNode ParseSpecialQualifier()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.Special);
            NameNode qualifier;
            switch (lexer.MoveNext().CharacterClass)
            {
                case CharacterClass.Anon:
                    // TODO: Unit test needed
                    qualifier = ParseAnonymousNamespace();
                    break;

                case CharacterClass.Special:
                    qualifier = ParseSpecialScope();
                    break;

                case CharacterClass.UppercaseLetter:
                    qualifier = ParseSpecialQualifierLetterPrefix();
                    break;

                case CharacterClass.Template:
                    lexer.MoveNext();
                    qualifier = this.ParseTemplateName();
                    break;

                case CharacterClass.Digit:
                    qualifier = this.ParseLexicalFrame();
                    break;

                default:
                    // TODO: Unit test needed
                    qualifier = null;
                    this.ReportError(ParseErrors.UnexpectedCharacter);
                    break;
            }

            return qualifier;
        }

        private NameNode ParseLexicalFrame()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.Digit);

            return new SpecialQualifierNode(this.ParseInteger());
        }


        private NameNode ParseSpecialQualifierLetterPrefix()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.UppercaseLetter);
            NameNode qualifier;

            switch (lexer.CurrentChar)
            {
                case 'A':
                    qualifier = ParseAnonymousNamespace();
                    break;

                case 'I':
                    // TODO: Unit test needed
                    qualifier = ParseInterfaceQualifierI();
                    break;

                case 'Q':
                    // TODO: Unit test needed
                    qualifier = ParseInterfaceQualifierQ();
                    break;

                default:
                    // TODO: Unit test needed
                    qualifier = null;
                    this.ReportError(ParseErrors.UnexpectedCharacter);
                    break;
            }

            return qualifier;
        }

        private NameNode ParseInterfaceQualifierQ()
        {
            this.ReportError(ParseErrors.UnimplementedInterfaceQualifier);
            return null;
        }

        private NameNode ParseInterfaceQualifierI()
        {
            this.ReportError(ParseErrors.UnimplementedInterfaceQualifier);
            return null;
        }

        /// <summary>
        /// Parse an anonymous namespace qualifier of the form 
        ///     anonymousNamespace ::= '?' ['%'|'A'] generated-namespace '@'
        /// The '?' prefix has been read, and we are position on the '%' or 'A'
        /// </summary>
        /// <returns></returns>
        private NameNode ParseAnonymousNamespace()
        {
            Debug.Assert(lexer.CurrentChar == '%' || lexer.CurrentChar == 'A');
            lexer.MoveNext();

            string name = ParseNameFragment(false);

            AnonymousNamespaceNode anon = new AnonymousNamespaceNode(name);
            RememberIdentifier(anon);

            return anon;
        }

        /// <summary>
        /// Parse a special scope, which is either an namespace of the form ??_?
        /// or a scope defined by a function (which will have its own mangled name)
        /// </summary>
        private NameNode ParseSpecialScope()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.Special);
            lexer.MoveNext();

            NameNode scope;

            if (lexer.CurrentCharClass == CharacterClass.Extend && lexer.Next.CharacterClass == CharacterClass.Special)
            {
                // TODO: Unit tests needed

                //
                // Anonymous namespace name (new style)
                //
                lexer.MoveNext();
                scope = ParseOperator();
                // There should be a name terminator
                if (lexer.CurrentCharClass != CharacterClass.Terminator)
                {
                    this.ReportError(ParseErrors.UnterminatedName);
                }
                lexer.MoveNext();
            }
            else
            {
                scope = new SpecialQualifierNode(this.ParseSymbolBody());
            }
            return scope;
        }

        /// <summary>
        ///  Parse a back reference and return the referenced type name (that must have previously appeared)
        /// </summary>
        private TypeNode ParseParameterTypeBackRef()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.Digit);
            int number = lexer.Current.Base10;

            List<TypeNode> types = this.BackRefs.ParameterTypes;
            if (number < 0 || number >= types.Count)
            {
                // Invalid back reference 
                // TODO: Unit test needed
                this.ReportError(ParseErrors.InvalidBackRef);
            }

            lexer.MoveNext();

            return BaseSymbolNode.Copy(types[number]);
        }

        /// <summary>
        ///  Parse a back reference and return the referenced namespace (that must have previously appeared)
        /// </summary>
        private IdentifierNode ParseNameBackRef()
        {
            Debug.Assert(lexer.CurrentCharClass == CharacterClass.Digit);
            int number = lexer.Current.Base10;

            List<IdentifierNode> names = this.BackRefs.Names;
            if (number < 0 || number >= names.Count)
            {
                // Invalid back reference 
                this.ReportError(ParseErrors.InvalidBackRef);
            }

            lexer.MoveNext();
            return BaseSymbolNode.Copy(names[number]);
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (lexer != null)
            {
                if (disposing)
                {
                    lexer.Dispose();
                }

                lexer = null;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #endregion
    }
}
