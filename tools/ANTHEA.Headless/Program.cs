// Entry point for document and module services, implemented in the following F3 steps.
if (args.Length == 0 || args is ["--help"] or ["help"])
{
    Console.WriteLine("ANTHEA · servizi senza interfaccia");
    Console.WriteLine("Uso: ANTHEA.Headless --help");
    Console.WriteLine("I comandi per documenti, calcoli e report vengono aggiunti con i servizi F3.");
    return 0;
}
Console.Error.WriteLine("Comando non disponibile. Usare --help.");
return 2;
