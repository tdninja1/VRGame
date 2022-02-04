using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Interhaptics.HapticRenderer.Core
{
    public class HapticBodyPart : MonoBehaviour
    {
        [SerializeField]
        private HumanBodyBones m_body_part = HumanBodyBones.RightHand;

        public HumanBodyBones BodyPart
        {
            get => m_body_part;
        }

        private void Start()
        {
            Collider c;
            if ((c = GetComponent<Collider>()) != null)
                c.isTrigger = true;
        }
    }
}