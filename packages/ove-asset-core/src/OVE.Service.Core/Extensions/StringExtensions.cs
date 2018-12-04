namespace OVE.Service.Core.Extensions {
    public static class StringExtensions {
        public static string EnsureTrailingSlash(this string input) => input + (input.EndsWith('/') ? "" : "/");
        public static string RemoveTrailingSlash(this string input) =>(!string.IsNullOrEmpty(input) && input.EndsWith("/") ? input.Substring(0,input.Length-1) : input);
    }
}