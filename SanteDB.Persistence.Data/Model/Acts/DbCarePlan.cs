using SanteDB.Core.Model.Constants;
using SanteDB.OrmLite.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Model.Acts
{
    /// <summary>
    /// Represents a persistence class for storing extra metadata about a care plan
    /// </summary>
    [Table("cp_tbl")]
    public class DbCarePlan : DbActSubTable
    {
        /// <summary>
        /// Parent key
        /// </summary>
        [JoinFilter(PropertyName = nameof(DbActVersion.ClassConceptKey), Value = ActClassKeyStrings.CarePlan)]
        public override Guid ParentKey
        {
            get
            {
                return base.ParentKey;
            }
            set
            {
                base.ParentKey = value;
            }
        }

        /// <summary>
        /// Gets or sets the title of the column
        /// </summary>
        [Column("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the program identifier for the care plan
        /// </summary>
        [Column("prog")]
        public string Program { get; set; }
    }
}
