using System;
using Leeway.Gravity;
using Leeway.Planet;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>Dedicated sphere fixtures own their field; pause the live planet while they run.</summary>
    internal sealed class PlanetTestGravityScope : IDisposable
    {
        private readonly PlanetPlayableWorld _playable;
        private readonly GravityWorld _field;
        private readonly bool _wasSuspended;
        private readonly bool _fieldWasEnabled;
        public PlanetTestGravityScope()
        {
            _playable=UnityEngine.Object.FindFirstObjectByType<PlanetPlayableWorld>(FindObjectsInactive.Include);
            if(_playable!=null){_wasSuspended=_playable.Suspended;_playable.Suspended=true;}
            _field=UnityEngine.Object.FindFirstObjectByType<GravityWorld>(FindObjectsInactive.Include);
            if(_field!=null){_fieldWasEnabled=_field.enabled;_field.enabled=false;}
        }
        public void Dispose()
        {
            if(_field!=null)_field.enabled=_fieldWasEnabled;
            if(_playable!=null)_playable.Suspended=_wasSuspended;
        }
    }
}
