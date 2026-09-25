using System.Reflection;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
foreach(var a in new[]{typeof(SectionSolver).Assembly,typeof(Shape2d).Assembly,typeof(GPC.Model.Sections.Concrete.ReinforcedConcreteSection).Assembly})
foreach(var t in a.GetTypes().Where(t => t.Name.Contains("StrainPlane") || t.Name=="StressAnalysisResult" || t.Name=="SectionSolver" || t.Name=="Shape2d" || t.Name=="ConcreteMaterialEuropeanCommon")) {
 Console.WriteLine("TYPE "+t.FullName);
 foreach(var m in t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)) Console.WriteLine(m);
}
