using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Leeway.CreatureEditor;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>
    /// Makes sure the message bus comes up complete, no matter which scene loaded first.
    /// </summary>
    /// <remarks>
    /// Every <c>LifetimeScope</c> bails out early on <c>GlobalMessagePipe.IsInitialized</c>, so the
    /// brokers are installed by <b>whichever scene loaded first</b>. Were it to register only its own
    /// messages, the first <c>GetPublisher</c> from another phase would throw at runtime — far away
    /// from where the cause lies. This test is cheap and it catches a regression that otherwise only
    /// shows up on an additive scene load.
    /// </remarks>
    public class MessageBrokerRegistrationTests
    {
        /// <summary>
        /// The cell- and creature-phase messages live in <c>Assembly-CSharp</c>, which the test
        /// assembly cannot reference (the dependency only runs the other way). Hence the reflection —
        /// without it those two brokers would be left with no coverage at all.
        /// </summary>
        private static readonly string[] AssemblyCSharpMessages =
        {
            "Leeway.Creature.LocalCellChangedMessage",
            "Leeway.Creature.LocalCreatureChangedMessage",
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Application.runInBackground = true;
            yield return CreatureEditorTestScene.EnsureLoaded();
        }

        [UnityTest]
        public IEnumerator Pipe_IsInitialized()
        {
            Assert.IsTrue(GlobalMessagePipe.IsInitialized, "The MessagePipe bus did not come up with the scene.");
            yield break;
        }

        [UnityTest]
        public IEnumerator EditorBrokers_AllResolve()
        {
            Assert.DoesNotThrow(() => GlobalMessagePipe.GetPublisher<LocalCreatureBodyChangedMessage>(),
                "The editor body broker is not registered.");
            Assert.DoesNotThrow(() => GlobalMessagePipe.GetPublisher<CreatureEditorSessionChangedMessage>(),
                "The editor session broker is not registered.");
            Assert.DoesNotThrow(() => GlobalMessagePipe.GetPublisher<CreatureEditorModeChangedMessage>(),
                "The editor mode broker is not registered.");
            Assert.DoesNotThrow(() => GlobalMessagePipe.GetPublisher<CreatureEditorSelectionChangedMessage>(),
                "The selection broker is not registered.");
            Assert.DoesNotThrow(() => GlobalMessagePipe.GetPublisher<CreatureEditorPartSelectionChangedMessage>(),
                "The part selection broker is not registered.");
            Assert.DoesNotThrow(() => GlobalMessagePipe.GetPublisher<GenomeCommitResultMessage>(),
                "The commit result broker is not registered.");
            Assert.DoesNotThrow(() => GlobalMessagePipe.GetPublisher<CreatureBodyRebuiltMessage>(),
                "The body rebuild broker is not registered.");
            yield break;
        }

        /// <summary>
        /// The heart of the test: the editor scene put the bus up, and yet the brokers of <b>other
        /// phases</b> still have to resolve.
        /// </summary>
        [UnityTest]
        public IEnumerator OtherPhaseBrokers_ResolveEvenThoughEditorSceneStartedThePipe()
        {
            foreach (string typeName in AssemblyCSharpMessages)
            {
                Type messageType = ResolveType(typeName);
                Assert.IsNotNull(messageType, $"Type {typeName} was not found — was it renamed, or moved to another namespace?");

                Assert.DoesNotThrow(() => GetPublisherOf(messageType),
                    $"Broker {typeName} is not registered — the bus came up incomplete.");
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Subscribers_ReceiveWhatPublishersSend()
        {
            int received = 0;
            using (GlobalMessagePipe.GetSubscriber<CreatureEditorSelectionChangedMessage>().Subscribe(_ => received++))
            {
                GlobalMessagePipe.GetPublisher<CreatureEditorSelectionChangedMessage>()
                    .Publish(new CreatureEditorSelectionChangedMessage(3));
            }

            Assert.AreEqual(1, received, "The message did not reach the subscriber — the brokers are registered but disconnected.");
            yield break;
        }

        private static Type ResolveType(string fullName)
            => AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, throwOnError: false))
                .FirstOrDefault(type => type != null);

        private static void GetPublisherOf(Type messageType)
        {
            MethodInfo generic = typeof(GlobalMessagePipe)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(GlobalMessagePipe.GetPublisher)
                            && m.IsGenericMethodDefinition
                            && m.GetGenericArguments().Length == 1
                            && m.GetParameters().Length == 0);

            try
            {
                generic.MakeGenericMethod(messageType).Invoke(null, null);
            }
            catch (TargetInvocationException e)
            {
                // Reflection wraps the real error — without this the test message would be useless.
                throw e.InnerException ?? e;
            }
        }
    }
}
