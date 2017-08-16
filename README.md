# SymbolDecoder

This is a decoder for C++ mangled names (symbols) in Microsoft C++ format. It can unmangle a name into something close to the original source code (like the [UnDecorateSymbolName](https://msdn.microsoft.com/en-us/library/windows/desktop/ms681400(v=vs.85).aspx) API), but it can also produce an AST (i.e. a parse tree). Since the information in a C++ symbol must be complete enough to be able to make an exact match at link time, the symbols contain detailed type information, albeit in a compressed form. Although UnDecorateSymbolName can make such symbols human readable, it is not very useful for tooling that wants to extract information from symbols. This decoder provides a parse tree to address that need.

It was more than 90% complete before the VS2015 and VS2017 C++ compiler releases. Most of the cases it did not cover were obscure, but I'm not sure what is missing from the latest compiler output for newer language features. There is an extensive suite of unit tests.

The decoder is written in C# and largely based on information gleaned from Agner Fog's [Calling conventions for different C++ compilers and operating systems](http://www.agner.org/optimize/calling_conventions.pdf). 
