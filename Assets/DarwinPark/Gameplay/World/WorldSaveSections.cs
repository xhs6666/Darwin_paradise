using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Simulation;
using DarwinPark.Genetics;
using UnityEngine;

namespace DarwinPark.UI
{
    internal static class WorldSaveSections
    {
        private const int Layout = 1;
        private static readonly object Gate = new();
        private static readonly string[] Names = { "terrain.json", "rules.json", "entities.json", "ecology.json", "body-parts.json" };

        [Serializable] private sealed class Manifest
        {
            public int sectionLayout;
            public string generation, id;
            public int entityCount;
            public WorldSaveData world;
        }

        [Serializable] private sealed class Terrain
        {
            public string terrainParameters, environmentParameters;
            public int oreResolution;
            public float oreTimer;
            public float[] ore;
        }

        [Serializable] private sealed class Rules
        {
            public string morphology, entityStateRegistry, ecologyBalanceSettings;
            public List<SavedGeneDefinition> definitions;
            public GeneTreeDocument tree;
            public WorldGameSettings gameSettings;
        }

        [Serializable] private sealed class Actors { public List<EntitySaveState> entities; }

        [Serializable] private sealed class Ecology
        {
            public ColonyWorldState colony;
            public PopulationMaintenanceState maintenance;
            public MutationRoundSave mutationRounds;
            public CorpseCleanupSave corpseCleanup;
        }

        [Serializable] private sealed class Bodies
        {
            public List<SavedBodyComposition> compositions = new();
            public List<int> references = new();
        }

        internal static WorldSaveData Read(string path)
        {
            lock (Gate)
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (json.IndexOf("\"sectionLayout\"", 0, Math.Min(256, json.Length), StringComparison.Ordinal) < 0)
                    return JsonUtility.FromJson<WorldSaveData>(json);

                var manifest = JsonUtility.FromJson<Manifest>(json);
                if (manifest?.world == null || manifest.sectionLayout != Layout || manifest.id != manifest.world.id ||
                    !Guid.TryParseExact(manifest.generation, "N", out _))
                    throw new InvalidDataException("存档分区清单无效");
                string folder = Generation(path, manifest.generation);
                WorldSaveManagement.RejectLink(Path.GetDirectoryName(folder));
                WorldSaveManagement.RejectLink(folder);
                var data = manifest.world;
                var terrain = ReadPart<Terrain>(folder, Names[0]);
                var rules = ReadPart<Rules>(folder, Names[1]);
                var actors = ReadPart<Actors>(folder, Names[2]);
                var ecology = ReadPart<Ecology>(folder, Names[3]);
                var bodies = ReadPart<Bodies>(folder, Names[4]);
                if (actors.entities == null || actors.entities.Count != manifest.entityCount ||
                    bodies.compositions == null || bodies.references == null || bodies.references.Count != actors.entities.Count)
                    throw new InvalidDataException("存档实体分区数量不匹配");

                data.terrainParameters = terrain.terrainParameters;
                data.environmentParameters = terrain.environmentParameters;
                data.oreResolution = terrain.oreResolution;
                data.oreTimer = terrain.oreTimer;
                data.ore = terrain.ore;
                data.morphology = rules.morphology;
                data.entityStateRegistry = rules.entityStateRegistry;
                data.ecologyBalanceSettings = rules.ecologyBalanceSettings;
                data.definitions = rules.definitions;
                data.tree = rules.tree;
                data.gameSettings = rules.gameSettings;
                data.entities = actors.entities;
                data.colony = ecology.colony;
                data.maintenance = ecology.maintenance;
                data.mutationRounds = ecology.mutationRounds;
                data.corpseCleanup = ecology.corpseCleanup;
                var shapes = JsonUtility.FromJson<MorphologySnapshot>(data.morphology);
                var partIds = new HashSet<string>();
                if (shapes?.parts != null)
                    foreach (var definition in shapes.parts)
                        if (definition != null) partIds.Add(definition.id);
                foreach (var composition in bodies.compositions)
                    if (composition == null || !composition.IsValid(partIds, shapes?.settings?.maximumParts ?? 0))
                        throw new InvalidDataException("存档部件构成无效");
                for (int i = 0; i < data.entities.Count; i++)
                {
                    int index = bodies.references[i];
                    if (data.entities[i] == null || index < -1 || index >= bodies.compositions.Count)
                        throw new InvalidDataException("存档实体或部件引用无效");
                    if (index >= 0) data.entities[i].BodyComposition = bodies.compositions[index];
                }
                return data;
            }
        }

        // ★ 适配：主线程生成不可变的分区文本，后台写盘不读取会继续变化的世界或 Unity 对象。
        internal sealed class PreparedWrite
        {
            internal readonly string Generation = Guid.NewGuid().ToString("N");
            internal readonly string[] Sections = new string[Names.Length];
            internal string Manifest;
        }

        internal static PreparedWrite Prepare(WorldSaveData data)
        {
            var write = new PreparedWrite();
            write.Sections[0] = JsonUtility.ToJson(new Terrain
            {
                terrainParameters = data.terrainParameters, environmentParameters = data.environmentParameters,
                oreResolution = data.oreResolution, oreTimer = data.oreTimer, ore = data.ore
            });
            write.Sections[1] = JsonUtility.ToJson(new Rules
            {
                morphology=data.morphology, entityStateRegistry=data.entityStateRegistry, ecologyBalanceSettings=data.ecologyBalanceSettings,
                definitions=data.definitions,tree=data.tree,gameSettings=data.gameSettings
            });
            write.Sections[2] = JsonUtility.ToJson(new Actors { entities = data.entities });
            write.Sections[3] = JsonUtility.ToJson(new Ecology
            {
                colony=data.colony, maintenance=data.maintenance, mutationRounds=data.mutationRounds, corpseCleanup=data.corpseCleanup
            });
            var bodies = new Bodies();
            var indices = new Dictionary<long, int>();
            foreach (var entity in data.entities)
            {
                var composition = entity.BodyComposition;
                if (composition == null)
                {
                    bodies.references.Add(-1);
                    continue;
                }
                if (!indices.TryGetValue(composition.Key, out int index))
                {
                    index = bodies.compositions.Count;
                    indices.Add(composition.Key, index);
                    bodies.compositions.Add(composition);
                }
                bodies.references.Add(index);
            }
            write.Sections[4] = JsonUtility.ToJson(bodies);
            var manifest = new Manifest
            {
                sectionLayout = Layout, generation = write.Generation, id=data.id, entityCount = data.entities.Count,
                world = new WorldSaveData
                {
                    version=data.version, contentEpoch=data.contentEpoch, savedWithRelease=data.savedWithRelease,
                    id=data.id, name=data.name, seed=data.seed, createdUtc=data.createdUtc, savedUtc=data.savedUtc,
                    gameTime=data.gameTime,timeScale=data.timeScale,sunAngle=data.sunAngle,running=data.running,
                    cameraPosition=data.cameraPosition,cameraRotation=data.cameraRotation,
                    cameraDistance=data.cameraDistance,planetRotation=data.planetRotation
                }
            };
            write.Manifest = JsonUtility.ToJson(manifest);
            return write;
        }

        internal static void Write(WorldSaveData data, string path) => Write(Prepare(data), path);

        internal static void Write(PreparedWrite write, string path)
        {
            lock (Gate)
            {
                using var writerLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                string folder = Generation(path, write.Generation);
                if(Directory.Exists(Path.GetDirectoryName(folder)))WorldSaveManagement.RejectLink(Path.GetDirectoryName(folder));
                string previous = ReadGeneration(path);
                string obsolete = ReadGeneration(path + ".bak");
                Directory.CreateDirectory(folder);
                for (int i = 0; i < Names.Length; i++)
                    WriteText(Path.Combine(folder, Names[i]), write.Sections[i]);
                WriteAtomicText(path, write.Manifest);
                if (obsolete != null && obsolete != previous) RemoveGeneration(path, obsolete);
            }
        }

        internal static void WriteAtomic<T>(string path, T value)
            => WriteAtomicText(path, JsonUtility.ToJson(value));

        private static void WriteAtomicText(string path, string json)
        {
            string temporary = path + ".tmp";
            WriteText(temporary, json);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

        private static T ReadPart<T>(string folder, string file) where T : class =>
            JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(folder, file), Encoding.UTF8)) ??
            throw new InvalidDataException("存档分区无效：" + file);

        private static void WriteText(string path, string json)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        private static string ReadGeneration(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var stream = File.OpenRead(path);
                if (stream.Length > 65536) return null;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var manifest = JsonUtility.FromJson<Manifest>(reader.ReadToEnd());
                return manifest?.sectionLayout == Layout && Guid.TryParseExact(manifest.generation, "N", out _)
                    ? manifest.generation : null;
            }
            catch (ArgumentException) { return null; }
        }

        private static string Generation(string path, string id) =>
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "sections", id);

        private static void RemoveGeneration(string path, string id)
        {
            string folder = Generation(path, id);
            try
            {
                WorldSaveManagement.RejectLink(Path.GetDirectoryName(folder));
                WorldSaveManagement.RejectLink(folder);
                foreach (string file in Names)
                {
                    string part = Path.Combine(folder, file);
                    if(File.Exists(part)){WorldSaveManagement.RejectLink(part);File.Delete(part);}
                }
                if (Directory.Exists(folder)) Directory.Delete(folder);
            }
            catch (IOException ex) { Debug.LogWarning("[存档] 旧分区保留：" + ex.Message); }
            catch (UnauthorizedAccessException ex) { Debug.LogWarning("[存档] 旧分区保留：" + ex.Message); }
        }
    }
}
