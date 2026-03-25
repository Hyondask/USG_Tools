namespace USG_Tools.Core.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"> Строка, которую нужно спрятать </param>
        /// <returns>Возвращает ********** </returns>
        public static string MaskSecretData(this string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return "Empty";
            }
            return new string('*', 10);
        }

    }

}
