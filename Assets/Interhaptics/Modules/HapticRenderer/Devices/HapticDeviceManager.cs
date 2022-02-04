using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Interhaptics.HapticRenderer.Devices
{
    public class HapticDeviceManager
    {
        public Dictionary<System.Type, object> m_haptic_providers = new Dictionary<System.Type, object>();
        
        public void Init()
        {
            HapticDevicesPreferences HDP = (HapticDevicesPreferences)Resources.Load("HDP");

            foreach (System.Reflection.Assembly assembly in Tools.ReflectionNames.GetCompatibleAssemblies())
            {

#if UNITY_EDITOR

                foreach (System.Type hapticProviderType in assembly.GetTypes().Where(t =>
                    t.GetInterfaces().Contains(typeof(Interfaces.IHapticProvider))))
                {
                    System.Reflection.MethodInfo method_platform_compatibility =
                    hapticProviderType.GetMethod(Tools.ReflectionNames.PLATFORM_COMPATIBILITIES_PROVIDER_METHOD_NAME);

                    if (method_platform_compatibility != null)
                    {
                        object instance = System.Activator.CreateInstance(hapticProviderType);
                        if (instance != null && ((IEnumerable<RuntimePlatform>)method_platform_compatibility.Invoke(instance, null)).Contains(UnityEngine.Application.platform))
                        {
                            m_haptic_providers.Add(hapticProviderType, instance);
                        }
                    }
                }
#else

                foreach (string s in HDP.Devices)
                {
                    System.Type hapticProviderType = assembly.GetType(s);
                    if (hapticProviderType != null)
                    {
                        object instance = System.Activator.CreateInstance(hapticProviderType);
                        if (instance != null)
                        {
                            System.Reflection.MethodInfo method_init = hapticProviderType.GetMethod("Init");

                            if (method_init != null && (bool)method_init.Invoke(instance, null))
                                m_haptic_providers.Add(hapticProviderType, instance);
                        }
                    }
                }
                
#endif
            }
        }
      
        private float[][] ToJaggedArray(float[,] _input)
        {
            int rows = _input.GetUpperBound(0) + 1;
            int cols = _input.GetUpperBound(1) + 1;

            float[][] output = new float[rows][];

            for (int i = 0; i < rows; i++)
            {
                float[] temp = new float[cols];
                float sum = 0;
                for (int j = 0; j < cols; j++)
                {
                    temp[j] = _input[i, j];
                    sum += temp[j];
                }
                if (sum == 0)
                    output[i] = new float[] { };
                else
                    output[i] = temp;
            }

            return output;
        }

        public void SendHaptics()
        {
            int row_count = System.Enum.GetValues(typeof(HumanBodyBones)).Length;
            int col_count = Core.HARWrapper.GetPerceptionCol();

            if (row_count < 1 || col_count < 1)
                return;

            float[,] pairs_haptic_buffers = new float[row_count, col_count];

            Core.HARWrapper.GetBPHapticsFeedback(pairs_haptic_buffers, row_count, col_count);

            float[][] haptic_buffers = ToJaggedArray(pairs_haptic_buffers);

            object[] param = new object[]
            {
                haptic_buffers
            };

            foreach(KeyValuePair<System.Type, object> hp in m_haptic_providers)
            {
                hp.Key.GetMethod(Tools.ReflectionNames.SEND_HAPTIC_PROVIDER_METHOD_NAME)
                    ?.Invoke(hp.Value, param);
            }
        }
    }
}
