This directory contains files ported from System.Net.Http.Json which are marked internal
but which are needed. They are JsonContent<T> and its supporting types, which allow the
use of a JsonTypeInfo<T> when building a HttpContent object.

If some method to create a JsonContent<T> object is ever made accessible in
System.Text.Json we can remove these types. It appears this work is currently planned
for .NET 7: https://github.com/dotnet/runtime/issues/51544
