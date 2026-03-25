using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Constants
{
    public static class PromptPatterns
    {
        // Ищем [ЛюбоеИмя] в конце строки
        public const string SystemView = @"\[.*\]\s*$";

        // Ищем -INSIDE перед > или ] в конце строки
        public const string InsideVsys = @"-INSIDE[>\]]\s*$";

        // Ищем <ЛюбоеИмя> в конце строки (User View)
        public const string UserView = @"<.*?>\s*$";
    }
}
