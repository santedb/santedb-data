using SanteDB.Core.Cdss;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Cdss
{
    /// <summary>
    /// A protocol asset library loaded from ADO.NET
    /// </summary>
    internal class AdoCdssLibraryAsset : AdoCdssAsset, ICdssLibraryAsset
    {
        // Wrapped asset library
        private readonly ICdssLibraryAsset m_wrapped;

        /// <inheritdoc/>
        public AdoCdssLibraryAsset(DbCdssAssetVersion dbVersionInformation) : base(dbVersionInformation, (IEnumerable<DbCdssGroup>)null)
        {
            this.m_wrapped = base.Wrapped as ICdssLibraryAsset;
        }

        /// <inheritdoc/>
        public AdoCdssLibraryAsset(DbCdssAssetVersion dbVersionInformation, IEnumerable<DbCdssGroup> groups) : base(dbVersionInformation, groups)
        {
            this.m_wrapped = base.Wrapped as ICdssLibraryAsset;
        }

        /// <inheritdoc/>
        public AdoCdssLibraryAsset(DbCdssAssetVersion dbVersionInformation, ICdssLibraryAsset protocolAssetLibrary) : base(dbVersionInformation, protocolAssetLibrary)
        {
            this.m_wrapped = protocolAssetLibrary;
        }

        /// <inheritdoc/>
        public override CdssAssetClassification Classification => CdssAssetClassification.DecisionSupportLibrary;

        /// <inheritdoc/>
        public TResolved ResolveElement<TResolved>(string elementName) => this.m_wrapped.ResolveElement<TResolved>(elementName);
    }
}
