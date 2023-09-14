using Newtonsoft.Json;
using SanteDB.Core.Cdss;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.IO;

namespace SanteDB.Persistence.Data.Cdss
{
    /// <summary>
    /// Represents a protocol asset group constructed from ADO information
    /// </summary>
    internal class AdoCdssAssetGroup : ICdssAssetGroup
    {

        /// <summary>
        /// Create a new asset group
        /// </summary>
        public AdoCdssAssetGroup(DbCdssGroup group)
        {
            this.Uuid = group.Key;
            this.Name = group.Name;
            this.Oid = group.Oid;
        }

        /// <inheritdoc/>
        public Guid Uuid { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string Oid { get; }

    }
}