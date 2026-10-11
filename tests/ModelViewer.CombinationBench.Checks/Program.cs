ExperimentSelfChecks.Run();
Console.WriteLine("PASS independent hand fixtures: signed sum, min/max, negative factor over nested envelope, SIMD/tail and cyclic dependency rejection.");
if(args.Length>0){Directory.CreateDirectory(args[0]);File.WriteAllText(Path.Combine(args[0],"esito.txt"),"PASS independent combination experiment fixtures");}
