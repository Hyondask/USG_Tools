using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Constants
{

    /// <summary>
    /// Паттерны RegExp выражений
    /// </summary>
    public static class PromptPatterns
    {
        // Ищем [ЛюбоеИмя] в конце строки
        public const string HuaweiSystemView = @"\[.*\]\s*$";

        // Ищем -INSIDE перед > или ] в конце строки
        public const string HuaweiInsideVsys = @"-INSIDE[>\]]\s*$";

        // Ищем <ЛюбоеИмя> в конце строки (User View)
        public const string HuaweiUserView = @"<.*?>\s*$";
    }
}
