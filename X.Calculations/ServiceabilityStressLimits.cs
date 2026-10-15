namespace Anthea.Calculations;

/// <summary>Tasso, stato e limiti tensionali SLE di uno stato tensionale (MPa, valori assoluti dei limiti).</summary>
/// <param name="Ratio">Tasso delle combinazioni con limiti (rara: CLS e acciaio; quasi permanente: CLS); null per le altre.</param>
/// <param name="Status">«Entro limiti tensionali», «Oltre limiti tensionali» o, senza tasso, «Stato tensionale calcolato».</param>
/// <param name="ConcreteStressLimit">Limite del calcestruzzo della combinazione, con il fattore dei getti sottili; null senza limite.</param>
/// <param name="SteelStressLimit">Limite dell'armatura k3·fyk della prima barra, mostrato per ogni stato.</param>
public sealed record ServiceabilityStressLimits(double? Ratio, string Status, double? ConcreteStressLimit, double SteelStressLimit);
