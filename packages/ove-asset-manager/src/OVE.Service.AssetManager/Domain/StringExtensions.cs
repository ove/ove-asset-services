namespace OVE.Service.AssetManager.Domain {
    public static class StringExtensions {
        public static string EnsureTrailingSlash(this string input) => input + (input.EndsWith('/') ? "" : "/");
    }
}