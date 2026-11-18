# Coordinate regression host

This host links the production spatial index, perception, geography, and navigation source files directly. Unity object lifecycles, transforms, time and vector math use deterministic managed adapters.

Run from the repository root:

```powershell
dotnet restore Tools/CoordinateRegression/CoordinateRegression.csproj --configfile Tools/FlowDocs/NuGet.Config
dotnet build Tools/CoordinateRegression/CoordinateRegression.csproj --no-restore
dotnet Tools/CoordinateRegression/bin/Debug/net10.0/CoordinateRegression.dll
```

The test compares spatial results with exact distance reference queries, checks observation isolation and budgets, and follows a production A* route around synthetic water. The one-thousand-entity check verifies update counts, not in-game frame rate.

For native collider immersion/drowning validation, run Unity's menu **Tools > Darwin Park > Validate Geography and Navigation**. This creates temporary fixtures and cleans them up. Its water checks require the Unity physics engine; they are not covered by the managed adapters.

Results are written to `Artifacts/GeographyNavigationReview/`. Do not present the managed-host milliseconds as Unity performance measurements.
