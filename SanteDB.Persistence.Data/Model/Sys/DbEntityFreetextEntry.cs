using SanteDB.OrmLite.Attributes;
using SanteDB.Persistence.Data.Model.Entities;
using System;

namespace SanteDB.Persistence.Data.Model.Sys
{
    /// <summary>
    /// Internal pointer table to entries in the full text index. Used for transaction deletion operations.
    /// </summary>
    [Table("ft_ent_systbl")]
    internal class DbEntityFreetextEntry
    {
        /// <summary>
        /// The entity id for the entry.
        /// </summary>
        [Column("ent_id"), PrimaryKey, ForeignKey(typeof(DbEntity), nameof(DbEntity.Key))]
        public Guid Key { get; set; }

        //Note the terms vector is not represented here. To use the full text index, use the FreeText 
    }
}
