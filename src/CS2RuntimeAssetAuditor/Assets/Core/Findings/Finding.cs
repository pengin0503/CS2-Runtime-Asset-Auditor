using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Assets.Core.Findings
{
    public sealed class Finding
    {
        public Finding(string ruleId, FindingStatus status, FindingCategory category, string title, string explanation, IEnumerable<string> evidence, FindingBasis basis, string ruleVersion)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("Rule ID is required.", nameof(ruleId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(explanation)) throw new ArgumentException("Explanation is required.", nameof(explanation));
            if (string.IsNullOrWhiteSpace(ruleVersion)) throw new ArgumentException("Rule version is required.", nameof(ruleVersion));
            RuleId = ruleId;
            Status = status;
            Category = category;
            Title = title;
            Explanation = explanation;
            Evidence = Array.AsReadOnly((evidence ?? throw new ArgumentNullException(nameof(evidence))).ToArray());
            Basis = basis;
            RuleVersion = ruleVersion;
        }
        public string RuleId { get; }
        public FindingStatus Status { get; }
        public FindingCategory Category { get; }
        public string Title { get; }
        public string Explanation { get; }
        public IReadOnlyList<string> Evidence { get; }
        public FindingBasis Basis { get; }
        public string RuleVersion { get; }
    }
}
