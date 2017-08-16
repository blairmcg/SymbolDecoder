using System;

namespace SymbolDecoder
{
    /// <summary>
    /// Flags to control undecoration of symbols into readable representations
    /// </summary>
    /// <remarks>For convenience these are the same as the options accepted by the UndecorateSymbolName API, though
    /// some attempt has been made to give them more meaningful names.</remarks>
    [Flags]
    public enum UndecorateOptions
    {
       /// <summary>Include the complete signature with all decorations</summary>
        None = 0x000,
        /// <summary>Omit leading underscores from Microsoft extended keywords</summary>
        NoLeadingUnderscores = 0x001,
        /// <summary>Omit Microsoft extended keywords</summary>
        NoMsftExtensions = 0x002,
        /// <summary>Omit the return type for primary declaration</summary>
        NoReturnType = 0x004,
        /// <summary>Omit of the declaration model</summary>
        NoAllocationModel = 0x008,
        /// <summary>Omit the declaration language specifier</summary>
        NoCallingConvention = 0x010,
        /// <summary>Omit all member storage modifiers, e.g. const, volatile, etc</summary>
        NoMemberStorageClass = 0x060,
        /// <summary>Omit member access specifier such as "public: "</summary>
        NoMemberAccess = 0x080,
        /// <summary>Omit exception signature for functions and pointers to functions</summary>
        NoThrowSignatures = 0x100,
        /// <summary>Omit member type, i.e. 'static' or 'virtual'ness of members</summary>
        NoMemberType = 0x200,
        /// <summary>Omit Microsoft model for UDT returns</summary>
        NoReturnUdtModel = 0x400,
        /// <summary>Undecorate 32-bit decorated names</summary>
        Decode32 = 0x800,
        /// <summary>Include only the name for primary declaration; i.e. just [scope::]name.  Does expand template params</summary>
        NameOnly = 0x1000,
        /// <summary>The symbol is a type name only (not a function or variable)</summary>
        TypeOnly = 0x2000,
        // <summary>Omit special names v-table, vcall, vector xxx, metatype, etc</summary>
        // This one is probably wrongly defined in the MSDN docs as the UnDecorateSymbol API appears to use the undname CRT functinon,
        // which itself uses this flag for a different purpose (configures to use callback function for template parameters)
        //NoSpecialNames = 0x4000,
        /// <summary>Omit enum/struct/class/union prefix on parameter types</summary>
        NoCompoundTypeClass = 0x8000,
        /// <summary>Omit ptr64 from output</summary>
        NoPtr64 = 0x20000,

        // The following flags are extras not supported by UndecorateSymbolName/undname

        /// <sumary>Don't emulate the bugs in the UndecorateSymbolName API and produce more correct undecoration closer to the original source</summary>
        /// <remarks>Obviously UndecorateSymbolName does not support this flag!</remarks>
        NoUndnameEmulation = 0x10000000,
    };
}
