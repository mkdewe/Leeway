using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RootMotion.Demos
{
    public class DynamicAnchorDemoLayerSetup : MonoBehaviour
    {

        public int headLayer = 10;
        public int handLayer = 11;
        public int characterControllerLayer = 8;
        public bool ignoreHandToHandCollisions = true;

        private void Start()
        {
            Physics.IgnoreLayerCollision(headLayer, handLayer, true);
            if (ignoreHandToHandCollisions) Physics.IgnoreLayerCollision(handLayer, handLayer, true);
            Physics.IgnoreLayerCollision(headLayer, characterControllerLayer, true);
            Physics.IgnoreLayerCollision(handLayer, characterControllerLayer, true);
        }
    }
}
