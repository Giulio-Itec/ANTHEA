namespace Materiali;

// Contratti della durabilità di ANTHEA (refactoring F2.9, passo E1): i record di Durability.cs e NtcCover.cs, invariati, in un file
// proprio. Li usano le facciate (Durability, NtcCover, MinimumConcrete, AtecapMix), il nucleo legacy (DurabilityLegacy), l'adattatore
// verso GPCChecker.Concrete (Anthea.Calculations.ConcreteDurabilityAdapter), la scheda Materiali, i muri e i dettagli della sezione c.a.
// Omonimi dei tipi CoverInput, CoverLine e CoverResult di GPC.Checkers.Concrete.Durability: i file che usano entrambi danno un alias
// alla libreria.
public record Exposure(string Code, string Description, double? MaxRatio, int MinStrength, int MinCement, double MinAir, int CoverColumn);
public record CoverInput(int Life, bool StrengthReduction, bool Slab, bool Quality, double Diameter, double Aggregate, double Deviation, bool Rough, int Abrasion, int Ground);
public record CoverLine(string Exposure, int StructuralClass, double Durability);
public record CoverResult(double Bond, double Durability, double Minimum, double Nominal, CoverLine[] Lines);
public record NtcCoverResult(string Environment,int Severity,double Cmin,double C0,double TableCover,double LifeExtra,double LowStrengthExtra,double QualityReduction,CoverResult Cover);
