using ANTHEA.ModelWorkspace;

internal static class Sample
{
    // Deliberately discontinuous data: shared node 2 has Myy=20 in element 10 and -200 in element 11.
    internal static ModelSnapshot Model() => new()
    {
        Name = "Modello dimostrativo",
        Source = "Fixture sintetica indipendente",
        Nodes = [new(1, 0, 0, 0), new(2, 2, 0, 0), new(3, 2, 2, 0), new(4, 0, 2, 0), new(5, 4, 0, 0), new(6, 4, 2, 0)],
        Elements = [new(10, "PLATE", 1, [1, 2, 3, 4]), new(11, "PLATE", 1, [2, 5, 6, 3]), new(12, "BEAM", 2, [1, 4])],
        Plates = [new(1, "Piastra", .3, -.7)],
        Sections = [new(2, "Rettangolare", "SB", .25, .5, true)],
        Results = [new("SLU 01", false, [new(10, 1, [1, 10, 3, 4, 5, 6, 7, 8]), new(10, 2, [1, 20, 3, 4, 5, 6, 7, 8]),
            new(10, 3, [1, 30, 3, 4, 5, 6, 7, 8]), new(10, 4, [1, 40, 3, 4, 5, 6, 7, 8]), new(11, 2, [1, -200, 3, 4, 5, 6, 7, 8])])]
    };
}
