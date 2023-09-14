using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.Core.i18n;
using SanteDB.Core.Cdss;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SanteDB.Persistence.Data.Cdss
{
    /// <summary>
    /// Represents a protocol asset base class
    /// </summary>
    internal abstract class AdoCdssAsset : ICdssAsset, IProtocolAssetWrapper
    {

        private readonly ICdssAsset m_wrappedAsset;

        /// <summary>
        /// Creates a new asset protocol definition from the specified database information
        /// </summary>
        public AdoCdssAsset(DbCdssAssetVersion dbVersionInformation, IEnumerable<DbCdssGroup> groups)
        {
            this.Uuid = dbVersionInformation.Key;
            this.Id = dbVersionInformation.Id;
            this.Name = dbVersionInformation.Name;
            this.Version = dbVersionInformation.VersionSequenceId.ToString();
            this.Oid = dbVersionInformation.Oid;
            this.Documentation = dbVersionInformation.Description;
            this.Groups = groups?.ToArray().Select(o => new AdoCdssAssetGroup(o)).ToArray();

            // Load the definition
            var handlerType = Type.GetType(dbVersionInformation.HandlerClass);
            if(handlerType == null)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.TYPE_NOT_FOUND, dbVersionInformation.HandlerClass));
            }
            var handlerInstance = handlerType.GetConstructor(Type.EmptyTypes).Invoke(new object[0]) as ICdssAsset;
            if(handlerInstance == null)
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.MAP_INCOMPATIBLE_TYPE, handlerType, typeof(ICdssAsset)));
            }
            using (var ms = new MemoryStream(dbVersionInformation.Definition))
            {
                handlerInstance.Load(ms);
                this.m_wrappedAsset = handlerInstance;
            }
        }

        /// <summary>
        /// Create a new instance of the asset with an existing asset
        /// </summary>
        public AdoCdssAsset(DbCdssAssetVersion dbVersionInformation, ICdssAsset existingAsset)
        {
            this.Uuid = dbVersionInformation.Key;
            this.Id = dbVersionInformation.Id;
            this.Name = dbVersionInformation.Name;
            this.Version = dbVersionInformation.VersionSequenceId.ToString();
            this.Oid = dbVersionInformation.Oid;
            this.Documentation = dbVersionInformation.Description;
            this.Groups = existingAsset.Groups;
            this.m_wrappedAsset = existingAsset;
        }

        /// <summary>
        /// Gets the wrapped asset
        /// </summary>
        public ICdssAsset Wrapped { get; }

        /// <inheritdoc/>
        public Guid Uuid { get; }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string Version { get; }

        /// <inheritdoc/>
        public string Oid { get; }

        /// <inheritdoc/>
        public string Documentation { get; }

        /// <inheritdoc/>
        public abstract CdssAssetClassification Classification { get; }

        /// <inheritdoc/>
        public IEnumerable<ICdssAssetGroup> Groups { get; }

        /// <inheritdoc/>
        public void Load(Stream definitionStream) => this.m_wrappedAsset.Load(definitionStream);

        /// <inheritdoc/>
        public void Save(Stream definitionStream) => this.m_wrappedAsset.Save(definitionStream);
    }
}
