using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The player's own presets: one JSON file per creature, in a folder on their disk.
    /// </summary>
    /// <remarks>
    /// <para><b>Files rather than assets</b>, because a player saving a creature in a shipped build
    /// cannot write into the project. <see cref="CreaturePreset"/> covers the other direction —
    /// creatures we ship — and both are the same <see cref="GenomeDocument"/>, so a preset can be
    /// promoted from one to the other by copying the text.</para>
    ///
    /// <para>Nothing here validates the creature. Loading gives a genome, and what the rules make of
    /// it is decided by <see cref="GenomeValidator"/> — the same gate a genome from the network goes
    /// through, because a file on disk is no more trustworthy than a packet.</para>
    ///
    /// <para>Every operation reports failure through a message instead of throwing: this touches a
    /// directory the player can rename, fill or lock at any moment, and none of that may take the
    /// editor down with it.</para>
    /// </remarks>
    public static class CreaturePresetStore
    {
        public const string Extension = ".creature.json";

        private const string FolderName = "Presets";

        /// <summary>Where the presets live. Created on the first save, not before.</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, FolderName);

        /// <summary>The names of the saved creatures, in alphabetical order.</summary>
        public static List<string> List()
        {
            var names = new List<string>();
            if (!Directory.Exists(Folder)) return names;

            try
            {
                foreach (string path in Directory.GetFiles(Folder, "*" + Extension))
                    names.Add(Path.GetFileName(path)[..^Extension.Length]);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read the preset folder \"{Folder}\": {exception.Message}");
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static string PathFor(string name) => Path.Combine(Folder, Sanitise(name) + Extension);

        public static bool Exists(string name) => File.Exists(PathFor(name));

        /// <summary>
        /// Writes a creature out under the given name, replacing a preset of the same name.
        /// </summary>
        /// <param name="keyOf">
        /// Resolves part identifiers to catalog keys — pass <c>catalog.KeyOf</c> so the file names its
        /// parts. <c>null</c> saves bare identifiers, which still load.
        /// </param>
        /// <param name="skinPng">
        /// The painted skin, or <c>null</c>. It is stored inside the preset rather than beside it, so a
        /// creature stays one shareable file — the whole point of the format.
        /// </param>
        public static bool TrySave(string name, CreatureGenome genome, Func<int, string> keyOf, byte[] skinPng, out string error)
        {
            error = null;

            if (genome == null)
            {
                error = "There is no creature to save.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Sanitise(name)))
            {
                error = "The preset needs a name.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(Folder);

                // Through a temporary file: a crash mid-write would otherwise leave a half-written
                // preset in place of the one that was there before.
                string path = PathFor(name);
                string temporary = path + ".tmp";

                GenomeDocument document = GenomeDocument.From(genome, name, keyOf);
                if (skinPng != null && skinPng.Length > 0) document.Skin = Convert.ToBase64String(skinPng);

                File.WriteAllText(temporary, document.ToJson(), Encoding.UTF8);

                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);

                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not save the preset: {exception.Message}";
                return false;
            }
        }

        public static bool TryLoad(string name, out CreatureGenome genome, out string error)
        {
            genome = null;

            if (!TryLoadDocument(name, out GenomeDocument document, out error)) return false;

            genome = document.ToGenome();
            return true;
        }

        /// <summary>
        /// The painted skin out of a document, as PNG bytes — <c>null</c> when the creature carries none.
        /// </summary>
        /// <remarks>
        /// A file the player may have edited by hand can hold anything in that field, so a base64 string
        /// that is not one comes back as "no skin" rather than as an exception in the middle of loading
        /// a creature.
        /// </remarks>
        public static byte[] SkinOf(GenomeDocument document)
        {
            if (string.IsNullOrEmpty(document?.Skin)) return null;

            try
            {
                return Convert.FromBase64String(document.Skin);
            }
            catch (FormatException)
            {
                Debug.LogWarning("The preset's skin is not valid base64 — loading the creature without it.");
                return null;
            }
        }

        public static bool TryLoadDocument(string name, out GenomeDocument document, out string error)
        {
            document = null;
            error = null;

            string path = PathFor(name);
            if (!File.Exists(path))
            {
                error = $"There is no preset called \"{name}\".";
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                error = $"Could not read the preset: {exception.Message}";
                return false;
            }

            return GenomeDocument.TryFromJson(json, out document, out error);
        }

        public static bool TryDelete(string name, out string error)
        {
            error = null;

            try
            {
                string path = PathFor(name);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not delete the preset: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// Turns what the player typed into something a file system will take.
        /// </summary>
        /// <remarks>
        /// The name is the file name — that is what makes the folder browsable and a preset shareable
        /// by sending one file. So the characters a path cannot hold are dropped rather than escaped:
        /// a preset called <c>"A/B"</c> is a small surprise, a preset written to another directory is
        /// a bug.
        /// </remarks>
        public static string Sanitise(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var builder = new StringBuilder(name.Length);
            foreach (char character in name.Trim())
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0) continue;
                builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
