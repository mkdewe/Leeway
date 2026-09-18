using System.Linq;
using Leeway.CreatureEditor;
using Leeway.CreatureEditor.Authoring;
using NUnit.Framework;

namespace Leeway.Tests
{
    /// <summary>
    /// Wires the authoring audit into the test suite, so a broken part cannot enter the catalog quietly.
    /// </summary>
    /// <remarks>
    /// <para><b>Errors only, not warnings.</b> An audit error means the part will not work: it has no
    /// prefab, no visible mesh, carries a <c>NetworkObject</c> or a negative scale. Those are facts and
    /// there is no appealing them. Warnings can be a deliberate choice by the author — a
    /// <c>SegmentLength</c> mismatch can be resolved two ways, and which one is right depends on whether
    /// the model or the gameplay is meant to be the truth. A red suite over an unresolved design
    /// decision would quickly teach everyone to ignore it.</para>
    ///
    /// <para>The test calls <b>the same</b> code as the editor window. Restating the rules in its own
    /// words would mean the tool and the test could one day disagree — and then neither could be
    /// trusted.</para>
    /// </remarks>
    public class PartCatalogAuditTests
    {
        /// <summary>
        /// The phrase the "definition outside the catalog" finding is recognised by.
        /// </summary>
        /// <remarks>
        /// A constant rather than a literal buried in the query: matching on message text is a fragile
        /// seam, and it has already snapped once — translating the audit left the filter matching
        /// nothing, so the test went green while checking nothing at all. Keeping the phrase next to the
        /// explanation at least makes the coupling visible.
        /// </remarks>
        private const string OrphanPhrase = "not in the catalog";

        [Test]
        public void Catalog_HasNoAuthoringErrors()
        {
            CreaturePartCatalog catalog = PartAuthoringAudit.FindCatalog();
            Assert.IsNotNull(catalog, "The project has no part catalog.");

            string[] errors = PartAuthoringAudit.Inspect(catalog)
                .Where(issue => issue.Kind == PartIssueKind.Error)
                .Select(issue => issue.Message)
                .ToArray();

            CollectionAssert.IsEmpty(errors,
                "The catalog contains parts that will not work:\n" + string.Join("\n", errors));
        }

        /// <summary>
        /// A definition outside the catalog is invisible — the palette will not show it and no genome can
        /// resolve its identifier. It is the easiest step to miss when adding a part.
        /// </summary>
        [Test]
        public void EveryPartDefinitionInTheProject_IsRegisteredInTheCatalog()
        {
            CreaturePartCatalog catalog = PartAuthoringAudit.FindCatalog();
            Assert.IsNotNull(catalog, "The project has no part catalog.");

            string[] orphans = PartAuthoringAudit.Inspect(catalog)
                .Select(issue => issue.Message)
                .Where(message => message.Contains(OrphanPhrase))
                .ToArray();

            CollectionAssert.IsEmpty(orphans,
                "These definitions exist, but nobody can see them:\n" + string.Join("\n", orphans));
        }
    }
}
