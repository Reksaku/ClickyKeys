namespace ClickyKeys
{
    /// <summary>
    /// Build-time constants that must NOT be overridable by the user's
    /// <c>config.json</c>. Anything the user could otherwise tamper with to
    /// silently suppress an update check — or pretend to be running a
    /// different channel — lives here and is baked into the binary.
    ///
    /// <para>
    /// To produce a non-dev build, change <see cref="Distribution"/> before
    /// shipping (or wrap it in <c>#if DISTRO_STORE</c> / <c>#if DISTRO_GITHUB</c>
    /// preprocessor directives if the same source tree has to produce
    /// multiple distribution artifacts).
    /// </para>
    /// </summary>
    public static class BuildInfo
    {
        public const DistributionType Distribution = DistributionType.dev;

        /// <summary>
        /// Version baked into this build. Compared at startup against the
        /// version persisted in <c>config.json</c>; when this constant is
        /// strictly greater than the config value an update is detected,
        /// the tutorial is replayed (via <c>ShowTutorial(0)</c>) and the
        /// config is bumped to match. Bump this every release alongside
        /// <c>FileVersion</c>/<c>AssemblyVersion</c> in the .csproj.
        /// </summary>
        public const string Version = "2.4.0";
    }
}
