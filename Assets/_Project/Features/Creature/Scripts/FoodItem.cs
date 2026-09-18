using System;
using FishNet.Object;
using UnityEngine;

namespace Leeway.Creature
{
    /// <summary>A collectable piece of food on the ground. The server validates consumption and despawns the object.</summary>
    public class FoodItem : NetworkBehaviour
    {
        [SerializeField] private float _foodAmount = 15f;

        public event Action<FoodItem> Collected;

        [Server]
        public void Consume(CreatureEntity collector)
        {
            collector.AddFood(_foodAmount);
            Collected?.Invoke(this);
            NetworkObject.Despawn();
        }
    }
}
