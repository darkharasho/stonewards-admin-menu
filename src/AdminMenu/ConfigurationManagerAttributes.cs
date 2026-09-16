namespace AdminMenu
{
    /// <summary>
    /// Tag for a setting's <see cref="BepInEx.Configuration.ConfigDescription"/> that tells the ModSettings menu
    /// (darkharasho-ModSettings) how to show it. Read by field name, so no reference to that mod is needed and the
    /// mod works the same without it. Uses BepInEx.ConfigurationManager's field names; unset fields are ignored.
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public int? Order;
    }
}
