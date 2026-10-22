namespace X.Desktop;

/// <summary>
/// The extended «Stratigrafia» card of the pile sheets (vertical and horizontal pile and micropile, historic and MVVM views) keeps its
/// «Profilo stratigrafico» at its right (user request of 22/10/2026): the drawing of the pile in the soil layers stays visible while the
/// layers are edited. The profile takes <see cref="ProfileShare"/> of the width, between <see cref="MinimumProfile"/> and
/// <see cref="MaximumProfile"/>; when the table would be narrower than <see cref="MinimumTable"/> the stratigraphy stays alone, as before.
/// </summary>
internal static class ExtendedStratigraphy
{
    public const double ProfileShare = .24, MinimumProfile = 300, MaximumProfile = 440, MinimumTable = 480;

    /// <summary>The widths of the table and of the profile in <paramref name="width"/> with <paramref name="gap"/> between them; null for the table alone.</summary>
    public static (double Table, double Profile)? Split(double width, double gap)
    {
        double profile = Math.Clamp(width * ProfileShare, MinimumProfile, MaximumProfile), table = width - gap - profile;
        return table >= MinimumTable ? (table, profile) : null;
    }
}
