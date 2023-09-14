using SanteDB.Core.BusinessRules;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Cdss;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Text;
using SanteDB.Core.Model;

namespace SanteDB.Persistence.Data.Cdss
{
    /// <summary>
    /// ADO clinical protocol
    /// </summary>
    internal class AdoCdssProtocolAsset : AdoCdssAsset, ICdssProtocolAsset
    {
        private readonly ICdssProtocolAsset m_wrapped;


        /// <inheritdoc/>
        public AdoCdssProtocolAsset(DbCdssAssetVersion dbVersionInformation) : base(dbVersionInformation, (IEnumerable<DbCdssGroup>)null)
        {
            this.m_wrapped = base.Wrapped as ICdssProtocolAsset;
        }

        /// <inheritdoc/>
        public AdoCdssProtocolAsset(DbCdssAssetVersion dbVersionInformation, IEnumerable<DbCdssGroup> groups) : base(dbVersionInformation, groups)
        {
            this.m_wrapped = base.Wrapped as ICdssProtocolAsset;
        }

        /// <inheritdoc/>
        public AdoCdssProtocolAsset(DbCdssAssetVersion dbVersionInformation, ICdssProtocolAsset existingAsset) : base(dbVersionInformation, existingAsset)
        {
            this.m_wrapped = existingAsset;
        }

        /// <inheritdoc/>
        public override CdssAssetClassification Classification => CdssAssetClassification.DecisionSupportProtocol;

        /// <inheritdoc/>
        public IEnumerable<Act> ComputeProposals(IdentifiedData patient, IDictionary<string, object> parameters) => this.m_wrapped.ComputeProposals(patient, parameters);

        /// <inheritdoc/>
        public IEnumerable<DetectedIssue> Analyze(IdentifiedData collectedSample) => this.m_wrapped.Analyze(collectedSample);

        /// <inheritdoc/>
        public void Prepare(Patient p, IDictionary<string, object> parameters) => this.m_wrapped.Prepare(p, parameters);
    }
}
