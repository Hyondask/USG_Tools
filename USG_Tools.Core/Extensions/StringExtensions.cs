namespace USG_Tools.Core.Extensions
{
    public static class StringExtensions
    {
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
