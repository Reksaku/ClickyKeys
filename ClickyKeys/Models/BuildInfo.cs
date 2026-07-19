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
        /// strictly greater than the config value an update is detected, the
        /// changelog is shown and the config is bumped to match. Bump this
        /// every release alongside <c>FileVersion</c>/<c>AssemblyVersion</c>
        /// in the .csproj.
        /// </summary>
        public const string Version = "2.4.4";

        /// <summary>
        /// When <c>true</c>, the first launch after an update replays the
        /// tutorial once — even for users who already completed it (i.e. it
        /// overrides <c>Configuration.ShowTutorial == false</c>). The tutorial
        /// opens after the changelog window is closed, so the two don't compete
        /// for attention, and finishing it marks the tutorial as seen again.
        ///
        /// <para>
        /// Turn this on for releases that meaningfully change the tutorial or
        /// the features it walks through; leave it off for routine updates so
        /// returning users aren't shown the walkthrough again.
        /// </para>
        /// </summary>
        public const bool ForceTutorialOnUpdate = true;
    }
}
