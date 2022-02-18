using UnityEngine;

namespace Autohand {
    [RequireComponent(typeof(Grabbable))]
    public class Climbable : MonoBehaviour{
        public Vector3 axis = Vector3.one;
    }
}
