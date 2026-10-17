using System.Collections.Generic;
using Unity.Services.Lobbies.Models;

namespace Leeway.Online.UnityServices
{
    /// <summary>
    /// Maps generic lobby data keys onto the five indexed string slots (S1…S5) UGS can query on.
    /// </summary>
    public sealed class UgsDataIndex
    {
        private static readonly DataObject.IndexOptions[] Slots =
        {
            DataObject.IndexOptions.S1, DataObject.IndexOptions.S2, DataObject.IndexOptions.S3,
            DataObject.IndexOptions.S4, DataObject.IndexOptions.S5,
        };

        private static readonly QueryFilter.FieldOptions[] Fields =
        {
            QueryFilter.FieldOptions.S1, QueryFilter.FieldOptions.S2, QueryFilter.FieldOptions.S3,
            QueryFilter.FieldOptions.S4, QueryFilter.FieldOptions.S5,
        };

        private readonly Dictionary<string, int> _slotByKey = new();

        public UgsDataIndex(IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                if (_slotByKey.Count == Slots.Length) break;
                if (!string.IsNullOrEmpty(key) && !_slotByKey.ContainsKey(key)) _slotByKey.Add(key, _slotByKey.Count);
            }
        }

        public bool TryGetIndex(string key, out DataObject.IndexOptions index)
        {
            bool found = _slotByKey.TryGetValue(key, out int slot);
            index = found ? Slots[slot] : default;
            return found;
        }

        public bool TryGetField(string key, out QueryFilter.FieldOptions field)
        {
            bool found = _slotByKey.TryGetValue(key, out int slot);
            field = found ? Fields[slot] : default;
            return found;
        }
    }
}
