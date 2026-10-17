using System.Runtime.CompilerServices;

// The session's lobby-to-snapshot mapping and state-transition table are internal; tests pin them.
[assembly: InternalsVisibleTo("Leeway.Tests.EditMode")]
