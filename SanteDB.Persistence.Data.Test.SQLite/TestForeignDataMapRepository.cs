using SanteDB.Core.Data.Import.Definition;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Persistence.Data.Test.SQLite
{
    public class TestForeignDataMapRepository : IRepositoryService<ForeignDataMap>
    {

        public string ServiceName => "Test Foreign Data Map Repo";

        public ForeignDataMap Delete(Guid key)
        {
            throw new NotSupportedException();
        }

        public IQueryResultSet<ForeignDataMap> Find(Expression<Func<ForeignDataMap, bool>> query)
        {
            return
                typeof(TestForeignDataMapRepository).Assembly.GetManifestResourceNames()
                .Where(t => t.EndsWith("Map.xml"))
                .Select(o =>
                {
                    using (var ms = typeof(TestForeignDataMapRepository).Assembly.GetManifestResourceStream(o))
                    {
                        return ForeignDataMap.Load(ms);
                    }
                })
                .Where(query.Compile())
                .AsResultSet();
        }

        public ForeignDataMap Get(Guid key) => this.Get(key, Guid.Empty);

        public ForeignDataMap Get(Guid key, Guid versionKey)
        {
            return this.Find(o => o.Key == key).FirstOrDefault();
        }

        public ForeignDataMap Insert(ForeignDataMap data)
        {
            throw new NotSupportedException();
        }

        public ForeignDataMap Save(ForeignDataMap data)
        {
            throw new NotSupportedException();
        }
    }
}
