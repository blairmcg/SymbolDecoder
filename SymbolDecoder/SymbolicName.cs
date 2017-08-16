using System;

namespace SymbolDecoder
{
    /// <summary>
    /// Class to represent mangled C++ names
    /// </summary>
    public class SymbolicName
    {
        /// <summary>
        /// Constructor
        /// </suummary>
        public SymbolicName(String symbolName)
        {
            if (string.IsNullOrEmpty(symbolName)) throw new ArgumentNullException(nameof(symbolName));
            this.SymbolName = symbolName;
        }

        /// <summary>
        /// The unqualified name from the symbol (effectively the final name component, e.g. the function or variable name)
        /// </summary>
        public String Name
        {
            get
            {
                return this.ParseTree.QualifiedName.Identifier.ToString();
            }
        }

        /// <summary>
        /// The symbolic name in raw/mangled/decorated form, as emitted by the C++ compiler
        /// </summary>
        public String SymbolName { get; private set; }

        /// <summary>
        /// The undecorated conversion of the symbolic name respecting the requested name formatting options.
        /// </summary>
        /// <param name="options">Flags controlling the undecorated representation</param>
        public string ToString(UndecorateOptions options)
        {
            return this.ParseTree.ToString(options);
        }

        /// <summary>
        /// The complete undecorated conversion of the symbolic name. This should be the same as the fully qualified name
        /// in the C++ source, except in the case of compiler generated functions which have no source equivalent,
        /// however these will be translated to a standard human-readable form
        /// </summary>
        public override String ToString()
        {
            return this.ParseTree.ToString();
        }


        private Symbol ast;

        public Symbol ParseTree
        {
            get
            {
                if (this.ast == null)
                {
                    this.ast = Parser.Parse(this.SymbolName);
                }
                return this.ast;
            }
        }
    }
}
