namespace Interface10Server;

// Other server mods that do the same things: ours step aside for them.
internal static class Compat
{
    // Hj's Improved Customization UI (server part ImprovedCustomizationUI.Server): it changes the head and sells the
    // other side's clothing itself, so our HeadCustomization and ClothingSides stay off (its client takes the
    // character screen's tab and Ragman's toggles, see the client's Compat).
    internal static bool HjCustomization =>
        AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "ImprovedCustomizationUI.Server");
}
