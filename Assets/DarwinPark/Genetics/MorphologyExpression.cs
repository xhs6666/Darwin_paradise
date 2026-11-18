using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DarwinPark.Genetics
{
    public static class MorphologyExpression
    {
        public static List<ExpressedPart> Resolve(Genome genome, GenomeEvaluation evaluation) =>
            new Resolver().Resolve(genome, evaluation, null);

        // Each view retains the inputs and result of its last successful expression.
        public sealed class Cache
        {
            internal object Catalog;
            internal string BodyPlan;
            internal List<(object source, float first, float second)> Inputs = new();
            internal List<(string slot, Vector3 scale, Vector3 position, Vector3 rotation)> Overrides = new();
            internal List<ExpressedPart> Parts;
        }

        // The client reuses one workspace while draining its main-thread appearance queue.
        public sealed class Resolver
        {
            private sealed class CatalogStamp { public long value; }
            private static readonly ConditionalWeakTable<PartCatalog,CatalogStamp> CatalogStamps=new();
            private readonly HashSet<string> _active = new();
            private readonly List<(object source, float first, float second)> _inputs = new();
            private readonly List<(string slot, Vector3 scale, Vector3 position, Vector3 rotation)> _overrides = new();
            private readonly Dictionary<string, string> _requested = new();
            private readonly Dictionary<string, ExpressedPart> _placed = new();
            private readonly Dictionary<string, List<(PartModifier rule, float factor)>> _modifiers = new();
            private readonly Stack<List<(PartModifier rule, float factor)>> _modifierLists = new();

            public List<ExpressedPart> Resolve(Genome genome, GenomeEvaluation evaluation, Cache cache, SavedBodyComposition saved = null)
            {
                var catalog = MorphologyRegistry.Settings;
                _active.Clear();
                _inputs.Clear();
                _overrides.Clear();
                foreach (var gene in evaluation.Genes)
                    if (gene.IsActive) _active.Add(gene.Definition.Id);

                foreach (var gene in evaluation.Genes)
                {
                    if (!gene.IsActive) continue;
                    foreach (var binding in gene.Definition.PartBindings)
                    {
                        if ((!string.IsNullOrEmpty(binding.stateId) && binding.stateId != gene.State?.StateId) ||
                            !Applies(binding.requireGene, binding.forbidGene)) continue;
                        _inputs.Add((binding, 0, 0));
                    }
                    foreach (var rule in gene.Definition.PartModifiers)
                    {
                        if (string.IsNullOrEmpty(rule.slotId) || !Applies(rule.requireGene, rule.forbidGene)) continue;
                        float value = gene.EffectiveStrength;
                        if (!string.IsNullOrEmpty(rule.attributeId))
                            evaluation.Attributes.TryGetValue(rule.attributeId, out value);
                        float factor = Mathf.Lerp(rule.minimum, rule.maximum, Mathf.InverseLerp(rule.rangeMin, rule.rangeMax, value));
                        _inputs.Add((rule, factor, 0));
                    }
                    if (gene.Definition.BodyColorWeight > 0 && gene.State != null)
                        _inputs.Add((gene.State, gene.EffectiveStrength, gene.Definition.BodyColorWeight * gene.Gene.CopyStrength));
                }
                foreach (string attribute in catalog.EfficiencyAttributes)
                {
                    evaluation.Attributes.TryGetValue(attribute, out float value);
                    _inputs.Add((attribute, value, 0));
                }
                foreach (var change in genome.BodyOverrides)
                    _overrides.Add((change.slotId, change.scale, change.position, change.rotation));

                if (cache?.Parts != null && cache.Catalog == catalog && cache.BodyPlan == genome.BodyPlanId &&
                    Same(cache.Inputs, _inputs) && Same(cache.Overrides, _overrides))
                    return cache.Parts;

                var parts = saved?.Parts != null && saved.Key == CompositionKey(genome.BodyPlanId)
                    ? saved.Parts : Assemble(MorphologyRegistry.Body(genome.BodyPlanId), evaluation);
                if (cache != null)
                {
                    cache.Catalog = catalog;
                    cache.BodyPlan = genome.BodyPlanId;
                    cache.Inputs.Clear();
                    cache.Inputs.AddRange(_inputs);
                    cache.Overrides.Clear();
                    cache.Overrides.AddRange(_overrides);
                    cache.Parts = parts;
                }
                return parts;
            }

            public SavedBodyComposition Capture(Genome genome, GenomeEvaluation evaluation, Cache cache = null)
            {
                var parts=Resolve(genome,evaluation,cache);
                return new SavedBodyComposition { Key=CompositionKey(genome.BodyPlanId), Parts=parts };
            }

            // Stable across processes. Include values that change the assembled result, in assembly order.
            private long CompositionKey(string bodyPlan)
            {
                ulong hash=14695981039346656037UL;
                void Add(int value){hash=unchecked((hash^(uint)value)*1099511628211UL);}
                void Number(float value)=>Add(System.BitConverter.SingleToInt32Bits(value));
                void Text(string value)
                {
                    Add(value?.Length??-1);
                    if(value!=null)foreach(char character in value)Add(character);
                }
                void Vector(Vector3 value){Number(value.x);Number(value.y);Number(value.z);}
                Add(1); // Change when the assembly rules change.
                long catalog=CatalogStamps.GetValue(MorphologyRegistry.Settings,_=>
                {
                    ulong stamp=14695981039346656037UL;
                    foreach(char character in MorphologyRegistry.Capture())
                        stamp=unchecked((stamp^character)*1099511628211UL);
                    return new CatalogStamp{value=unchecked((long)stamp)};
                }).value;
                Add(unchecked((int)catalog));Add(unchecked((int)(catalog>>32)));
                Text(bodyPlan);
                foreach(var input in _inputs)
                {
                    switch(input.source)
                    {
                        case PartBinding binding: Add(1);Text(binding.slotId);Text(binding.partId);break;
                        case PartModifier rule: Add(2);Text(rule.slotId);Text(rule.parameter);Number(input.first);break;
                        case GeneStateDefinition state:
                            Add(3);
                            var color=state.GetDisplayColor(input.first);
                            Number(color.r);Number(color.g);Number(color.b);Number(color.a);Number(input.second);
                            break;
                        case string attribute: Add(4);Text(attribute);Number(input.first);break;
                    }
                }
                Add(5);
                foreach(var change in _overrides)
                {Text(change.slot);Vector(change.scale);Vector(change.position);Vector(change.rotation);}
                return unchecked((long)hash);
            }

            private bool Applies(string required, string forbidden) =>
                (string.IsNullOrEmpty(required) || _active.Contains(required)) &&
                (string.IsNullOrEmpty(forbidden) || !_active.Contains(forbidden));

            private static bool Same<T>(List<T> previous, List<T> current) where T : System.IEquatable<T>
            {
                if (previous.Count != current.Count) return false;
                for (int i = 0; i < previous.Count; i++)
                    if (!previous[i].Equals(current[i])) return false;
                return true;
            }

            private List<ExpressedPart> Assemble(BodyDefinition body, GenomeEvaluation evaluation)
            {
                using var performance = Core.PerformanceTimings.Measure(Core.PerformanceTimings.Area.BodyResolve);
                _requested.Clear();
                _placed.Clear();
                foreach (var list in _modifiers.Values)
                {
                    list.Clear();
                    _modifierLists.Push(list);
                }
                _modifiers.Clear();
                _requested.Add("body", body.SlotsById["body"].partId);
                Color mixed = Color.clear;
                float weight = 0;
                foreach (var input in _inputs)
                {
                    switch (input.source)
                    {
                        case PartBinding binding:
                            if (!body.SlotsById.ContainsKey(binding.slotId))
                                throw new InvalidDataException("形体缺少槽位：" + binding.slotId);
                            if (_requested.TryGetValue(binding.slotId, out string prior) && prior != binding.partId)
                                throw new InvalidDataException("同一槽位请求了不同部件：" + binding.slotId);
                            _requested[binding.slotId] = binding.partId;
                            break;
                        case PartModifier rule:
                            if (!_modifiers.TryGetValue(rule.slotId, out var modifiers))
                            {
                                modifiers = _modifierLists.Count > 0 ? _modifierLists.Pop() : new();
                                _modifiers.Add(rule.slotId, modifiers);
                            }
                            modifiers.Add((rule, input.first));
                            break;
                        case GeneStateDefinition state:
                            mixed += state.GetDisplayColor(input.first) * input.second;
                            weight += input.second;
                            break;
                    }
                }

                // Parents precede their children in the validated catalog. Expand from leaves up.
                for (int i = body.slots.Count - 1; i >= 0; i--)
                {
                    var slot = body.slots[i];
                    if (_requested.ContainsKey(slot.id) && !string.IsNullOrEmpty(slot.parent))
                        _requested.TryAdd(slot.parent, body.SlotsById[slot.parent].partId);
                }
                var result = new List<ExpressedPart>(_requested.Count);
                foreach (var slot in body.slots)
                {
                    if (!_requested.TryGetValue(slot.id, out string id)) continue;
                    var definition = MorphologyRegistry.Part(id);
                    Vector3 scale = slot.scale;
                    if (_modifiers.TryGetValue(slot.id, out var modifiers))
                    {
                        foreach (var (rule, factor) in modifiers)
                        {
                            if (rule.parameter == "scaleX") scale.x *= factor;
                            else if (rule.parameter == "scaleY") scale.y *= factor;
                            else scale.z *= factor;
                        }
                    }
                    foreach (var change in _overrides)
                        if (change.slot == slot.id) scale = Vector3.Scale(scale, change.scale);
                    scale = new Vector3(
                        Mathf.Clamp(scale.x, definition.minimumScale.x, definition.maximumScale.x),
                        Mathf.Clamp(scale.y, definition.minimumScale.y, definition.maximumScale.y),
                        Mathf.Clamp(scale.z, definition.minimumScale.z, definition.maximumScale.z));

                    Color color = definition.ParsedColor;
                    if (!string.IsNullOrEmpty(definition.efficiencyAttribute))
                    {
                        evaluation.Attributes.TryGetValue(definition.efficiencyAttribute, out float efficiency);
                        color = Color.Lerp(color, definition.ParsedEfficientColor, Mathf.Clamp01(efficiency / definition.efficiencyMaximum));
                    }
                    if (slot.id == "body" && weight > 0) color = mixed / weight;

                    var part = new ExpressedPart
                    {
                        slotId = slot.id,
                        partId = id,
                        scale = scale,
                        color = color,
                        position = slot.position,
                        rotation = Quaternion.Euler(slot.rotation)
                    };
                    if (!string.IsNullOrEmpty(slot.parent))
                    {
                        var parent = _placed[slot.parent];
                        Vector3 offset = slot.position;
                        if (slot.attachment == "surface")
                        {
                            Vector3 direction = offset.normalized;
                            offset = Vector3.Scale(direction, parent.scale) * .5f;
                            Vector3 normal = new Vector3(direction.x / parent.scale.x, direction.y / parent.scale.y, direction.z / parent.scale.z).normalized;
                            offset += normal * slot.surfaceOffset;
                            part.rotation = parent.rotation * Quaternion.LookRotation(normal, Mathf.Abs(normal.y) > .95f ? Vector3.forward : Vector3.up) * part.rotation;
                        }
                        else part.rotation = parent.rotation * part.rotation;
                        part.position = parent.position + parent.rotation * offset;
                    }
                    foreach (var change in _overrides)
                    {
                        if (change.slot != slot.id) continue;
                        part.position += change.position;
                        part.rotation *= Quaternion.Euler(change.rotation);
                    }
                    _placed.Add(slot.id, part);
                    result.Add(part);
                }
                return result;
            }
        }
    }
}
