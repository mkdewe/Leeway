using System.Runtime.CompilerServices;

// The EditorPad <-> CreatureBody contract (the overlap counter, recognising the collider) is
// deliberately internal — it is an implementation detail of that pair, not API for the rest of the
// game. Tests have to see it, because that is exactly where the rule worth guarding lives.
[assembly: InternalsVisibleTo("Leeway.Tests.EditMode")]
