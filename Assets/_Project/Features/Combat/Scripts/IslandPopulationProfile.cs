using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;
namespace Leeway.Combat
{
    [CreateAssetMenu(fileName="IslandPopulation",menuName="Leeway/World/Island population")]
    public sealed class IslandPopulationProfile:ScriptableObject
    {
        [SerializeField] private PopulationRule[] _species=PopulationRule.Defaults();
        [SerializeField,Range(0,48)] private int _populationCap=32;
        [SerializeField,Min(10)] private float _safeHubRadius=28;
        public IReadOnlyList<PopulationRule> Species=>_species;
        public int PopulationCap=>_populationCap;
        public float SafeHubRadius=>_safeHubRadius;
    }
}
