using System.ComponentModel;

namespace OtomatikMetinGenisletici.Models
{
    /// <summary>
    /// Text expansion için kullanılabilecek tuş kombinasyonları
    /// </summary>
    public enum ExpansionTriggerKey
    {
        [Description("Space")]
        Space = 0,

        [Description("Enter")]
        Enter = 1,

        [Description("Ctrl + Space")]
        CtrlSpace = 2,

        [Description("Shift + Space")]
        ShiftSpace = 3,

        [Description("Alt + Space")]
        AltSpace = 4,

        [Description("Ctrl + Enter")]
        CtrlEnter = 5,

        [Description("Shift + Enter")]
        ShiftEnter = 6,

        [Description("Alt + Enter")]
        AltEnter = 7
    }

    /// <summary>
    /// Tuş kombinasyonu bilgilerini tutan yardımcı sınıf
    /// </summary>
    public static class ExpansionTriggerKeyHelper
    {
        /// <summary>
        /// Enum değerinin açıklamasını döndürür
        /// </summary>
        public static string GetDescription(ExpansionTriggerKey key)
        {
            var field = key.GetType().GetField(key.ToString());
            if (field != null)
            {
                var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
                if (attribute != null)
                    return attribute.Description;
            }
            return key.ToString();
        }

        /// <summary>
        /// Tüm mevcut tuş kombinasyonlarını döndürür
        /// </summary>
        public static Dictionary<ExpansionTriggerKey, string> GetAllTriggerKeys()
        {
            var result = new Dictionary<ExpansionTriggerKey, string>();
            foreach (ExpansionTriggerKey key in Enum.GetValues<ExpansionTriggerKey>())
            {
                result[key] = GetDescription(key);
            }
            return result;
        }

        /// <summary>
        /// Tuş kombinasyonunun modifier tuşları olup olmadığını kontrol eder
        /// </summary>
        public static bool HasModifiers(ExpansionTriggerKey key)
        {
            return key switch
            {
                ExpansionTriggerKey.CtrlSpace or 
                ExpansionTriggerKey.ShiftSpace or 
                ExpansionTriggerKey.AltSpace or 
                ExpansionTriggerKey.CtrlEnter or 
                ExpansionTriggerKey.ShiftEnter or 
                ExpansionTriggerKey.AltEnter => true,
                _ => false
            };
        }

        /// <summary>
        /// Tuş kombinasyonunun ana tuşunu döndürür
        /// </summary>
        public static string GetMainKey(ExpansionTriggerKey key)
        {
            return key switch
            {
                ExpansionTriggerKey.Space or
                ExpansionTriggerKey.CtrlSpace or
                ExpansionTriggerKey.ShiftSpace or
                ExpansionTriggerKey.AltSpace => "Space",

                ExpansionTriggerKey.Enter or
                ExpansionTriggerKey.CtrlEnter or
                ExpansionTriggerKey.ShiftEnter or
                ExpansionTriggerKey.AltEnter => "Enter",

                _ => "Space"
            };
        }

        /// <summary>
        /// Tuş kombinasyonunun modifier tuşlarını döndürür
        /// </summary>
        public static List<string> GetModifiers(ExpansionTriggerKey key)
        {
            var modifiers = new List<string>();
            
            switch (key)
            {
                case ExpansionTriggerKey.CtrlSpace:
                case ExpansionTriggerKey.CtrlEnter:
                    modifiers.Add("Ctrl");
                    break;
                    
                case ExpansionTriggerKey.ShiftSpace:
                case ExpansionTriggerKey.ShiftEnter:
                    modifiers.Add("Shift");
                    break;
                    
                case ExpansionTriggerKey.AltSpace:
                case ExpansionTriggerKey.AltEnter:
                    modifiers.Add("Alt");
                    break;
            }
            
            return modifiers;
        }
    }
}
