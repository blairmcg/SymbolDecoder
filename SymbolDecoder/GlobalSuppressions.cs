// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.
//
// To add a suppression to this file, right-click the message in the 
// Code Analysis results, point to "Suppress Message", and click 
// "In Suppression File".
// You do not need to add suppressions to this file manually.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Scope = "member", Target = "SymbolDecoder.CppNameBuilder.#Append(System.Int64)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "s", Scope = "member", Target = "SymbolDecoder.CppNameBuilder.#Append(System.String)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Undname", Scope = "member", Target = "SymbolDecoder.CppNameBuilder.#NoUndnameEmulation")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Cpp", Scope = "type", Target = "SymbolDecoder.CppNameBuilder")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ms", Scope = "member", Target = "SymbolDecoder.CppNameBuilder.#AppendMsKeyword(System.String,SymbolDecoder.CppNameBuilder+Spacing)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Ms", Scope = "member", Target = "SymbolDecoder.CppNameBuilder.#AppendMsKeyword(System.String,SymbolDecoder.CppNameBuilder+Spacing)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "ch", Scope = "member", Target = "SymbolDecoder.CppNameBuilder.#Append(System.Char)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Lexer", Scope = "type", Target = "SymbolDecoder.SymbolLexer")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "EOF", Scope = "member", Target = "SymbolDecoder.SymbolLexer.#EOF")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("SonarLint", "S1172:Unused method parameters should be removed", Justification = "Implementing a function with specific signature", Scope = "member", Target = "~M:SymbolDecoder.TypeNode.NullDisplayOn(SymbolDecoder.CppNameBuilder,SymbolDecoder.CppNameBuilder.Spacing)~System.Boolean")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("SonarLint", "S1066:Collapsible \"if\" statements should be merged", Justification = "Clearer as nested if's", Scope = "member", Target = "~M:SymbolDecoder.FunctionSymbol.DisplayBodyOn(SymbolDecoder.CppNameBuilder)")]