using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerTipDotAssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Definition
        {
            public string GraphName, GraphPath;
            public TipDotGraphType GraphType;
            public TipDotGraphScope GraphScope;
            public TipDotNodeGraph Graph;
        }

        [SerializeField] private Definition[] definitions = Array.Empty<Definition>();
        public IReadOnlyList<Definition> Definitions => definitions;

        public static OfficialPartnerTipDotAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerTipDotAssetCatalog>("Partners/OfficialPartnerTipDotAssetCatalog");
            if (!value) throw new InvalidOperationException("Official partner tip graph catalog is not packaged");
            return value;
        }

        public PartnerTipDotGraphInfo[] GetAllGraphs() => definitions.Select(value => new PartnerTipDotGraphInfo {
            GraphName = value.GraphName, GraphType = value.GraphType, GraphScope = value.GraphScope }).ToArray();

        public TipDotGraphType? GetGraphType(string graphName)
            => definitions.FirstOrDefault(value => value.GraphName == graphName)?.GraphType;

        public TipDotNodeGraph LoadGraph(string graphName)
        {
            var definition = definitions.FirstOrDefault(value => value.GraphName == graphName);
            if (definition == null) return null;
            if (!definition.Graph)
                throw new InvalidOperationException("Official tip graph has no native asset binding: " + definition.GraphPath);
            // Original linkers/manager use TipDotInfo, not the graph author's
            // serialized Type/Scope fields. Keep both original inputs intact.
            if (definition.Graph.name != definition.GraphName)
                throw new InvalidOperationException("Official tip graph/configuration identity differs: " + definition.GraphPath);
            return definition.Graph;
        }
    }
}
