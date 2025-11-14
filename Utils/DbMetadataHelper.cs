using System;
using System.Collections.Generic;
using System.Linq;

namespace Kursovaya.Utils
{
    public static class DbMetadataHelper
    {
        // Возвращает список разрешённых значений для Vehicles.Status из CHECK-ограничения
        public static string[] GetAllowedVehicleStatuses(user149_dbEntities db)
        {
            const string sql = @"
SELECT cc.definition
FROM sys.check_constraints cc
JOIN sys.objects o ON cc.parent_object_id = o.object_id
WHERE o.name = N'Vehicles' AND cc.name = N'CK_Vehicles_Status';
";
            try
            {
                var def = db.Database.SqlQuery<string>(sql).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(def))
                    return new[] { "В наличии", "В резерве", "Продано" }; // запасной вариант

                var list = new List<string>();

                // Вариант 1: ... IN (N'В наличии',N'В резерве',N'Продано')
                var upper = def.ToUpperInvariant();
                int inPos = upper.IndexOf("IN (", StringComparison.Ordinal);
                if (inPos >= 0)
                {
                    int end = def.IndexOf(')', inPos + 3);
                    if (end > inPos)
                    {
                        var inside = def.Substring(inPos + 3, end - (inPos + 3));
                        foreach (var part in inside.Split(','))
                        {
                            var s = part.Trim();
                            if (s.StartsWith("N'")) s = s.Substring(2);
                            if (s.StartsWith("'")) s = s.Substring(1);
                            if (s.EndsWith("'")) s = s.Substring(0, s.Length - 1);
                            if (!string.IsNullOrWhiteSpace(s))
                                list.Add(s);
                        }
                    }
                }
                else
                {
                    // Вариант 2: ... [Status]='В наличии' OR [Status]='В резерве' OR ...
                    var tokens = def.Split('\'');
                    for (int i = 1; i < tokens.Length; i += 2)
                    {
                        var s = tokens[i].Trim();
                        if (!string.IsNullOrWhiteSpace(s))
                            list.Add(s);
                    }
                }

                return list.Distinct().ToArray();
            }
            catch
            {
                return new[] { "В наличии", "В резерве", "Продано" };
            }
        }
    }
}