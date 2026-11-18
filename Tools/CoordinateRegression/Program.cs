using DarwinPark.Entities.Ecology;
using DarwinPark.Planet;
using UnityEngine;
var root=new GameObject("Planet");var planet=root.AddComponent<PlanetTerrainMeshBuilder>();EcologyQueries.Planet=planet;
var passed=new List<string>();
try
{
 DarwinPark.EditorTools.GlobalCoordinateRegression.Validate(planet,passed);
 passed.AddRange(GeographyAlgorithmChecks.Run(planet));
 Directory.CreateDirectory("Artifacts/GeographyNavigationReview");
 File.WriteAllLines("Artifacts/GeographyNavigationReview/managed-results.txt",passed.Prepend("Managed transform adapters; production index/perception/geography/navigation sources; not Unity physics."));
 Console.WriteLine($"PASS: {passed.Count} production-algorithm assertions (managed adapters; not Unity physics)");
 Console.WriteLine(File.ReadAllText("Artifacts/GeographyNavigationReview/index-metrics.txt"));
}
catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
