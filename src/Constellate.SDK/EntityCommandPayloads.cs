namespace Constellate.SDK
{
    /// <summary>
    /// Transitional compatibility file retained to preserve the historical SDK anchor
    /// while the payload records themselves now live in narrower family files:
    ///
    /// - WorldEntityAndPanelCommandPayloads.cs
    /// - InteractionAndNavigationCommandPayloads.cs
    /// - ViewCameraBridgePayloads.cs
    ///
    /// The payload record names, namespace, and consumer-facing types remain unchanged;
    /// only the file-family organization has been narrowed so future cleanup can reason
    /// about the real command domains without one broad mixed bundle.
    /// </summary>
    internal static class EntityCommandPayloadsFileMarker
    {
    }
}
