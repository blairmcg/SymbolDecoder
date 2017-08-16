using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SymbolDecoder
{
    /// <summary>
    /// Classification of the characters in the symbolic name lexicon
    /// </summary>
    public enum CharacterClass : byte
    {
        Invalid = 0,
        EOF,
        Digit,
        UppercaseLetter,
        LowercaseLetter,
        Terminator,     // '@'
        Special,        // '?'
        Template,       // '$'
        Extend,         // '_'
        LessThan,
        GreaterThan,
        Anon,           // '%'
        Minus,          // '-'
        HighAnsi,
    };

    /// <summary>
    /// Simple lexical analyzer for Microsoft C++ format mangled symbol names
    /// </summary>
    /// <remarks>The input stream is already tokenised so there is little lexical analysis that can be done. In particular
    /// we are unable to recognise even identifiers lexically because the encoded representations of various other attributes
    /// of the symbol use the some of same characters that can appear in identifiers. We could recognise a few special character
    /// sequences (e.g. the ?$ sequence that indicates a template name), but it's not really worth doing. All multi-character
    /// sequences are instead recognised in the parser. Nevertheless it is useful to classify the input to make the parser code
    /// clearer and simpler</remarks>
    public class Lexer : IDisposable
    {
        #region Fields
        private readonly String symbolName;
        private StringReader inputStream;
        private static readonly CharacterClass[] characterClasses = new CharacterClass[256];
        #endregion

        #region Constants
        public const byte EOF = 0x1A;  // Ascii EOF character
        #endregion

        #region Construct/Desctruct
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="symbolName">The managled name to analyzer</param>
        public Lexer(string symbolName)
        {
            if (string.IsNullOrEmpty(symbolName)) throw new ArgumentNullException(nameof(symbolName));

            this.symbolName = symbolName;
            Reset();
        }

        /// <summary>
        /// IDisposable implementation (necessary only because of use of StringBuilder)
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && this.inputStream != null)
            {
                this.inputStream.Dispose();
                this.inputStream = null;
            }

        }

        static Lexer()
        {
            InitializeCharacterClasses();
        }

        #endregion

        #region Properties
        /// <summary>
        ///  The token at the current position in the input stream
        /// </summary>
        public Token Current
        {
            get { return this.current; }
        }
        private Token current;

        /// <summary>
        /// The token at the next position in the input stream
        /// </summary>
        public Token Next
        {
            get
            {
                int ch = this.inputStream.Peek();
                return new Token(ch, this.Position + 1);
            }
        }

        /// <summary>
        /// Are we at the end of the input "stream"?
        /// </summary>
        public bool AtEnd { get { return this.Current.CharacterClass == CharacterClass.EOF; } }

        // Convenience properties for quick access to attributes of the current token
        public char CurrentChar { get { return this.Current.Character; } }
        public int Position { get { return this.Current.Position; } }
        public CharacterClass CurrentCharClass { get { return this.Current.CharacterClass; } }
        #endregion

        /// <summary>
        /// A token from the input stream
        /// </summary>
        public struct Token
        {
            private readonly byte ch;
            public readonly CharacterClass CharacterClass;
            private readonly short position;

            public Token(int codePoint, int position)
            {
                Debug.Assert(codePoint >= -1 || codePoint < 256);
                this.ch = codePoint < 0 ? EOF : (byte)codePoint;
                this.CharacterClass = Classify(this.ch);
                Debug.Assert(position > 0 && position < 0x7FFF);
                this.position = (short)position;
            }

            public char Character
            {
                get
                {
                    return (char)ch;
                }
            }

            /// <summary>
            /// Returns the character classification for the argument
            /// </summary>
            /// <param name="ch">The character to classify</param>
            public static CharacterClass Classify(int ch)
            {
                return (uint)ch >= characterClasses.Length
                    ?  CharacterClass.Invalid
                    : characterClasses[ch];
            }

            /// <summary>
            /// Answer the decimal equivalent of the current character if it is assumed to be a base-10 digit
            /// </summary>
            public int Base10
            {
                get
                {
                    Debug.Assert(this.CharacterClass == CharacterClass.Digit);
                    return this.Character - '0';
                }
            }

            /// <summary>
            /// Answer the decimal equivalent of the current character if it is assumed to be a base-36 digit
            /// </summary>
            public int Base36
            {
                get
                {
                    Debug.Assert(this.CharacterClass == CharacterClass.UppercaseLetter || this.CharacterClass == CharacterClass.Digit);
                    return this.Character >= 'A' ? (this.Character - 'A') + 10 : this.Character - '0';
                }
            }

            public int Position
            {
                get
                {
                    return this.position;
                }
            }

            public bool IsValidIdentifierCharacter
            {
                get
                {
                    switch (this.CharacterClass)
                    {
                        case CharacterClass.Digit:
                        case CharacterClass.UppercaseLetter:
                        case CharacterClass.LowercaseLetter:
                        case CharacterClass.Extend:
                        case CharacterClass.LessThan:
                        case CharacterClass.GreaterThan:
                        case CharacterClass.Minus:
                        case CharacterClass.HighAnsi:
                            return true;
                        default:
                            return false;
                    }
                }
            }
        }

        #region Helpers

        private static void InitializeCharacterClasses()
        {
            characterClasses['_'] = CharacterClass.Extend;
            characterClasses['@'] = CharacterClass.Terminator;
            characterClasses['$'] = CharacterClass.Template;
            characterClasses['?'] = CharacterClass.Special;
            characterClasses['%'] = CharacterClass.Anon;

            // '-' can appear in names, but has no other syntactic significance
            characterClasses['-'] = CharacterClass.Minus;
            characterClasses['<'] = CharacterClass.LessThan;
            characterClasses['>'] = CharacterClass.GreaterThan;

            for (int ch = '0'; ch <= '9'; ch++)
            {
                characterClasses[ch] = CharacterClass.Digit;
            }
            for (int ch = 'A'; ch <= 'Z'; ch++)
            {
                characterClasses[ch] = CharacterClass.UppercaseLetter;
            }
            for (int ch = 'a'; ch <= 'z'; ch++)
            {
                characterClasses[ch] = CharacterClass.LowercaseLetter;
            }

            for (int ch = 0x80; ch <= 0xFE; ch++)
            {
                characterClasses[ch] = CharacterClass.HighAnsi;
            }

            characterClasses[EOF] = CharacterClass.EOF;

            // Every other entry in the table is 0, i.e. CharacterClass.Invalid
        }

        #endregion

        #region Operations

        /// <summary>
        /// Position at the start of the input stream
        /// </summary>
        private void Reset()
        {
            this.Dispose();
            this.inputStream = new StringReader(this.symbolName);
            this.MoveNext();
        }

        /// <summary>
        ///  Move to the next character (token) in the input "stream"
        /// </summary>
        /// <returns>Returns that next character</returns>
        public Token MoveNext()
        {
            if (this.AtEnd)
            {
                // Already at the end of the symbol before lexing/parsing complete, so the symbol is truncated or malformed
                this.ReportError(ParseErrors.PrematureEndOfSymbol);
            }
            this.current = new Token(this.inputStream.Read(), this.Position + 1);
            if (this.CurrentCharClass == CharacterClass.Invalid)
            {
                this.ReportError(ParseErrors.InvalidCharacter);
            }

            return this.current;
        }

        /// <summary>
        /// Report a syntax error encountered attempting to parse the mangled symbol
        /// </summary>
        /// <param name="parseErrorFormat">Error message format string with optional substitutions for the current character and position in the stream of characters</param>
        public void ReportError(string parseErrorFormat)
        {
            this.ReportError(parseErrorFormat, this.Position, this.CurrentChar);
        }

        /// <summary>
        /// Report a syntax error encountered attempting to parse the mangled symbol
        /// </summary>
        /// <param name="parseErrorFormat">Error message format string with optional substitutions for the current character and position in the stream of characters</param>
        /// <param name="position">Position of the error in the symbol, or -1 if the current lexer position</param>
        /// <param name="ch">Character in error, or null character if current lexer character</param>
        public void ReportError(string parseErrorFormat, int position, char ch)
        {
            string parseErrorMessage = string.Format(CultureInfo.CurrentCulture, parseErrorFormat, ch, position);

            throw new InvalidSymbolNameException(
                string.Format(CultureInfo.CurrentCulture, ParseErrors.SymbolParseErrorFormat,
                    this.symbolName,
                    parseErrorMessage,
                    position),
                position,
                this.symbolName);
        }

        /// <summary>
        /// Report that the current character was unexpected at this point in the input stream
        /// </summary>
        public void ErrorUnexpectedCharacter()
        {
            this.ReportError(ParseErrors.UnexpectedCharacter);
        }
        #endregion


    }
}
