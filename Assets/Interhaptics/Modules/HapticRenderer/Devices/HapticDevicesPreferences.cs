using System.Collections.Generic;
using UnityEngine;

namespace Interhaptics.HapticRenderer.Devices
{
    public class HapticDevicesPreferences : ScriptableObject
    {
        #region STATIC CONST PATHS
        public static readonly string PATH_TO_PREFERENCES = "Assets/Interhaptics/Modules/HapticRenderer/Devices/Resources/HDP.asset";
        #endregion

        [SerializeField, HideInInspector]
        private List<string> m_devices = new List<string>();
        public List<string> Devices
        {
            get => m_devices;
        }

        public void AddDevice(string s)
        {
            m_devices.Add(s);
        }
    }
}
