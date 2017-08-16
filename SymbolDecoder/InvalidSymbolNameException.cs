using System;
using System.Runtime.Serialization;

namespace SymbolDecoder
{
    public class InvalidSymbolNameException : Exception
    {
        /// <summary>
        /// The native format mangled symbol name that is invalid
        /// </summary>
        public String Symbol { get; private set; }

        /// <summary>
        /// The character position in the symbol name where the error was detected
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        /// Initializes a new instance of the InvalidSymbolNameException class with serialized
        /// data.
        /// </summary>
        /// <param name="info">The object that holds the serialized object data.</param>
        /// <param name="context">The contextual information about the source or destination.</param>
        protected InvalidSymbolNameException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        /// <summary>
        /// Initializes a new instance of the System.ArgumentException class with a specified
        /// error parseErrorFormat and the name of the parameter that causes this exception.
        /// </summary>
        /// <param name="parseError">The error parseErrorFormat that explains the parsing error encountered.</param>
        /// <param name="position">The 1-based position in the symbolic name where the parsing error was detected</param>
        /// <param name="mangledName">The symbolic name that is invalid.</param>
        public InvalidSymbolNameException(string message, int position, string mangledName)
            : base(message)
        {
            this.Symbol = mangledName;
            this.Position = position;
        }
    }
}
