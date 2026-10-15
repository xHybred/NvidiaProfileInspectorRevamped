using System;

namespace nvidiaProfileInspector.Common.CustomSettings
{
    [Serializable]
    public class CustomSettingValue
    {
        public uint SettingValue
        {
            get { return Convert.ToUInt32(HexValue.Trim(), 16); }
        }

        public ulong QwordSettingValue
        {
            get { return Convert.ToUInt64(HexValue.Trim(), 16); }
        }

        public string UserfriendlyName { get; set; }

        public string HexValue { get; set; }

        /// <summary>Hides this value from dropdowns (opt-in per value).
        /// A value currently used by a profile is still shown (keep-if-active).</summary>
        public bool ValueHidden { get; set; }

    }
}
