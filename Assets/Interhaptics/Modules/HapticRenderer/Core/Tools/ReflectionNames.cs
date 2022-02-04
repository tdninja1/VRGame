using System.Linq;


namespace Interhaptics.HapticRenderer.Tools
{

    public static class ReflectionNames
    {

        #region ASSEMBLY NAMES
        public const string DEFAULT_ASSEMBLY_NAME = "Assembly-CSharp";
        public const string ASSEMBLY_PREFIX_NAME_FOR_PROVIDERS = "Interhaptics.HapticProviders.";
        #endregion


        #region PROVIDERS METHODS NAMES
        // TRACKING CHARACTERISTICS
        public const string DESCRIPTION_PROVIDER_METHOD_NAME = "Description";
        public const string DISPLAY_NAME_PROVIDER_METHOD_NAME = "DisplayName";
        public const string DEVICE_CLASS_PROVIDER_METHOD_NAME = "DeviceClass";
        public const string PLATFORM_COMPATIBILITIES_PROVIDER_METHOD_NAME = "PlatformCompatibilities";
        public const string MANUFACTURER_PROVIDER_METHOD_NAME = "Manufacturer";
        public const string VERSION_PROVIDER_METHOD_NAME = "Version";

        // PROVIDER SETUP
        public const string INIT_PROVIDER_METHOD_NAME = "Init";
        public const string CLEANUP_PROVIDER_METHOD_NAME = "Cleanup";
        public const string CAN_EXPORT_PROVIDER_METHOD_NAME = "CanExport";

        // PROVIDER RENDERING
        public const string IS_PRESENT_PROVIDER_METHOD_NAME = "IsPresent";
        public const string GET_HAPTIC_BUFFERS_PROVIDER_METHOD_NAME = "GetHapticBuffers";
        public const string SEND_HAPTIC_PROVIDER_METHOD_NAME = "SendHaptic";
        #endregion


        #region PUBLIC METHODS
        /// <summary>
        ///     Get interhaptics assemblies in which a haptic provider can be
        /// </summary>
        /// <returns>An assembly collection</returns>
        public static System.Collections.Generic.IEnumerable<System.Reflection.Assembly>
            GetInterhapticsTrackingProviderAssemblies()
        {
            return GetAssemblies(assembly => assembly.FullName.StartsWith(ASSEMBLY_PREFIX_NAME_FOR_PROVIDERS));
        }

        /// <summary>
        ///     Get assemblies in which a haptic provider can be
        /// </summary>
        /// <returns>An assembly collection</returns>
        public static System.Collections.Generic.IEnumerable<System.Reflection.Assembly> GetCompatibleAssemblies()
        {
            return GetAssemblies(assembly =>
                assembly.FullName.StartsWith(ASSEMBLY_PREFIX_NAME_FOR_PROVIDERS) ||
                assembly.GetName().Name == DEFAULT_ASSEMBLY_NAME);
        }

        /// <summary>
        ///     Get assemblies in which a haptic provider can be depending on a parametrized checking method
        /// </summary>
        /// <param name="checker">A checking method</param>
        /// <returns>An assembly collection</returns>
        private static System.Collections.Generic.IEnumerable<System.Reflection.Assembly> GetAssemblies(
            System.Func<System.Reflection.Assembly, bool> checker)
        {
            return System.AppDomain.CurrentDomain.GetAssemblies().Where(checker);
        }
        #endregion

    }

}