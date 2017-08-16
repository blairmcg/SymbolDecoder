using System;
using System.Collections.Generic;
using System.Text;

namespace SymbolDecoder
{
    public class CppNameBuilder
    {
        private int lastTemplateEnd = -1;
        private StringBuilder Output { get; set; }
        private readonly Stack<UndecorateOptions> options = new Stack<UndecorateOptions>();

        /// <summary>
        /// Separator used between name components, i.e. namespaces and type names
        /// </summary>
        public static readonly string NameSeparator = "::";

        /// <summary>
        /// Quote character used to indicate the start of a special name
        /// </summary>
        public static readonly char SpecialNameStart = '`';

        /// <summary>
        /// Quote character used to indicate the end of a special name
        /// </summary>
        public static readonly char SpecialNameEnd = '\'';

        /// <summary>
        /// Default constructor
        /// </summary>
        public CppNameBuilder() : this(UndecorateOptions.NoUndnameEmulation) { }

          /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Name undecoration option flags</param>
        public CppNameBuilder(UndecorateOptions options)
        {
            this.PushOptions(options);
            this.Output = new StringBuilder();
        }

        public override string ToString()
        {
            return this.Output.ToString();
        }

        #region Undecoration options

        /// <summary>
        /// Options used to control how the undecorated name is built.
        /// </summary>
        public UndecorateOptions Options
        {
            get
            {
                return this.options.Peek();
            }
        }

        /// <summary>
        /// Returns true if ALL of the options specified by the flag argument are set in the current options, otherwise false
        /// </summary>
        /// <param name="option">Unmangling options</param>
        public bool HasOptions(UndecorateOptions option)
        {
            return this.Options.HasFlag(option);
        }

        /// <summary>
        /// Make a new set of options current, but remembering the old set to return to later
        /// </summary>
        /// <param name="newOptions">New undecoration options</param>
        public void PushOptions(UndecorateOptions newOptions)
        {
            this.options.Push(newOptions);
        }

        /// <summary>
        /// Revert to a previous set of options
        /// </summary>
        /// <returns>Popped options that were current</returns>
        public UndecorateOptions PopOptions()
        {
            // The options stack cannot be empty or emptied
            if (this.options.Count < 2) throw new InvalidOperationException();

            return this.options.Pop();
        }

        /// <summary>
        /// Don't emulate UndecorateSymbolName bugs, such as mishandling of pointer/variable storage class, in
        /// order to produce a more precise rendering of symbols closer to the original source.
        /// </summary>
        public bool NoUndnameEmulation
        {
            get
            {
                return this.HasOptions(UndecorateOptions.NoUndnameEmulation);
            }
        }

        #endregion

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames")]
        [Flags]
        public enum Spacing
        {
            None,
            Leading = 1,
            Trailing = 2,
            Both = Leading | Trailing
        }

        public bool AppendMsKeyword(string keyword, Spacing spacing)
        {
            if (this.HasOptions(UndecorateOptions.NoMsftExtensions) || (keyword == "ptr64" && this.HasOptions(UndecorateOptions.NoPtr64)))
            {
                return false;
            }
            this.Space(Spacing.Leading, spacing);

            if (!this.HasOptions(UndecorateOptions.NoLeadingUnderscores))
            {
                this.Append("__");
            }
            this.Append(keyword);
            this.Space(Spacing.Trailing, spacing);

            return true;
        }

          private bool Space(Spacing spacePosition, Spacing desiredSpacing)
        {
            if (desiredSpacing.HasFlag(spacePosition))
            {
                Space();
                return true;
            }
            return false;
        }

        public void Space()
        {
            this.Append(' ');
        }

        /// <summary>
        /// Append a character to the output stream
        /// </summary>
        /// <param name="ch">Character to write</param>
        /// <param name="spacing">Spacing options</param>
        public void Append(char ch, Spacing spacing)
        {
            LeadingSpace(spacing);
            this.Append(ch);
            TrailingSpace(spacing);
        }

        public bool TrailingSpace(Spacing spacing)
        {
            return this.Space(Spacing.Trailing, spacing);
        }

        public bool LeadingSpace(Spacing spacing)
        {
            return this.Space(Spacing.Leading, spacing);
        }

        public void Append(char ch)
        {
            this.Output.Append(ch);
        }

        public void Append(Int64 i)
        {
            this.Output.Append(i);
        }

        /// <summary>
        /// Append a string to the output stream with optional leading and trailing spaces
        /// </summary>
        /// <param name="word">The string to write</param>
        /// <param name="spacing">Spacing options</param>
        public void Append(string word, Spacing spacing)
        {
            if (word == null) throw new ArgumentNullException(nameof(word));

            this.Space(Spacing.Leading, spacing);
            this.Append(word);
            this.Space(Spacing.Trailing, spacing);
        }

        public void Append(string s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            this.Output.Append(s);
        }

        /// <summary>
        /// Append class member access specifier prefix to the output stream (if enabled)
        /// </summary>
        /// <param name="memberAccess">The access level (private, protected or public)</param>
        /// <returns>Whether any output was emitted</returns>
        /// <remarks>Appears at the start of the output, so include a trailing space</remarks>
        public bool Append(MemberProtectionLevel memberAccess)
        {
            return Append(memberAccess, Spacing.Trailing);
        }

        /// <summary>
        /// Append class member access specifier prefix to the output stream (if enabled)
        /// </summary>
        /// <param name="memberAccess">The access level (private, protected or public)</param>
        /// <param name="spacing">Spacing required around the output</param>
        /// <returns>Whether any output was emitted</returns>
        public bool Append(MemberProtectionLevel memberAccess, Spacing spacing)
        {
            if (this.HasOptions(UndecorateOptions.NameOnly) || this.HasOptions(UndecorateOptions.NoMemberAccess)) return false;

            // Always appears first in the symbol, but is optional so needs trailing space
            this.Append((Enum)memberAccess, Spacing.None);
            this.Append(':', spacing);
            return true;
        }

        /// <summary>
        /// Append MS extension calling convention keyword to the output stream (if enabled)
        /// </summary>
        /// <param name="callingConvention">Function calling convention</param>
        /// <returns>Whether any output was emitted</returns>
        public bool Append(CallingConvention callingConvention)
        {
            return Append(callingConvention, Spacing.None);
        }

        /// <summary>
        /// Append MS extension calling convention keyword to the output stream (if enabled)
        /// </summary>
        /// <param name="callingConvention">Function calling convention</param>
        /// <param name="spacing">Spacing required around the output</param>
        /// <returns>Whether any output was emitted</returns>
        public bool Append(CallingConvention callingConvention, Spacing spacing)
        {
            // Appears before function name, which can be at start of output if no return type
            // so we need to allow a space before it.
            return !this.HasOptions(UndecorateOptions.NameOnly)
                && !this.HasOptions(UndecorateOptions.NoCallingConvention)
                && this.AppendMsKeyword(GetEnumValueName(callingConvention), spacing);
        }

        public bool Append(Enum value, Spacing spacing)
        {
            this.Append(GetEnumValueName(value), spacing);
            return true;
        }

        public bool Append(MemberFunctionClassification memberFunctionType)
        {
            return Append(memberFunctionType, Spacing.Trailing);
        }

        public bool Append(MemberFunctionClassification memberFunctionType, Spacing spacing)
        {
            return memberFunctionType != MemberFunctionClassification.Normal
                && !this.HasOptions(UndecorateOptions.NameOnly)
                && !this.HasOptions(UndecorateOptions.NoMemberType)
                && this.Append((Enum)memberFunctionType, spacing);
        }

        /// <summary>
        /// Emit a compiler generated name to the output stream in quoted form
        /// </summary>
        /// <param name="name">The special compiler generated name</param>
        /// <param name="spacing">Spacing requested around the output</param>
        /// <returns>Whether any output was emitted</returns>
        public bool AppendSpecialName(string name, CppNameBuilder.Spacing spacing)
        {
            this.LeadingSpace(spacing);
            AppendSpecialNameOn(name, this.Output);
            this.TrailingSpace(spacing);
            return true;
        }

        /// <summary>
        /// Append a list of items to the output stream with the specified separator between each.
        /// No leading or trailing separators or spaces are emitted.
        /// </summary>
        /// <typeparam name="T">The type of object to be printed</typeparam>
        /// <param name="items">The sequence of objects to be printed</param>
        /// <param name="separator">The separator to emit between objects</param>
        /// <returns>Whether or not any output was emitted</returns>
        public bool AppendAllWithSeparators<T>(IEnumerable<T> items, string separator) where T : BaseSymbolNode
        {
            bool needSeparator = false;
            foreach (T item in items)
            {
                if (needSeparator)
                {
                    this.Append(separator);
                }
                needSeparator |= item.DisplayOn(this, Spacing.None);
            }
            return needSeparator;
        }

        /// <summary>
        /// Separator between parameters of function, or a parameteric type instance. Note no spacing
        /// </summary>
        private const string ParameterSeparator = ",";

        internal bool AppendFunctionParameters(IEnumerable<TypeNode> parameters, bool ellipsis)
        {
            if (this.HasOptions(UndecorateOptions.TypeOnly)) return false;

            this.Append('(');
            bool needSeparator = this.AppendAllWithSeparators(parameters, ParameterSeparator);
            // Varargs functions may finish with ellipsis
            if (ellipsis)
            {
                if (needSeparator)
                {
                    this.Append(ParameterSeparator);
                }
                this.Append("...");
            }
            this.Append(')');
            return true;
        }

        internal bool AppendTemplateArguments(IEnumerable<BaseSymbolNode> arguments)
        {
            // Template arguments are always included, even in NameOnly mode

            this.Append('<');
            this.AppendAllWithSeparators(arguments, ParameterSeparator);
            // A little tweak we need to reproduce is that a space is always inserted between template close brackets
            // if they would otherwise be adjacent.
            this.Append('>', this.lastTemplateEnd == this.Output.Length ? Spacing.Leading : Spacing.None);
            this.lastTemplateEnd = this.Output.Length;
            return true;
        }

        internal bool AppendMsKeywords<T>(IEnumerable<T> keywords, Spacing spacing = Spacing.Leading) where T : NameNode
        {
            bool output = false;
            // The first keyword should have a leading space only if requested
            Spacing s = spacing & Spacing.Leading;
            foreach (T keyword in keywords)
            {
                output |= this.AppendMsKeyword(keyword.Name, s);
                // Subsequent keywords will require a leading space
                s = Spacing.Leading;
            }
            // If there was output emit a trailing space if one was requested
            if (output)
            {
                this.Space(Spacing.Trailing, spacing);
            }
            return output;
        }

        #region Helpers

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification ="The value is required for output purposes, not normalization")]
        internal static string GetEnumValueName(Enum value)
        {
            return value.ToString().ToLowerInvariant();
        }

        public static void AppendSpecialNameOn(string name, StringBuilder output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            output.Append(SpecialNameStart);
            output.Append(name);
            output.Append(SpecialNameEnd);
        }

        public static string QuoteSpecialName(string name)
        {
            StringBuilder output = new StringBuilder();
            AppendSpecialNameOn(name, output);
            return output.ToString();
        }

        #endregion

    }

}
