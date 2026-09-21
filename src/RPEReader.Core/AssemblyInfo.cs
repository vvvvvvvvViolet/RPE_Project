using System.Runtime.CompilerServices;

// The test project verifies a few internal helpers directly (CSV escaping,
// the XML-to-node mapper) rather than only through their public callers.
[assembly: InternalsVisibleTo("RPEReader.Core.Tests")]
