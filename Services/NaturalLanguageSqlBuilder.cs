using System.Text.RegularExpressions;
using NlpToSqlApi.Models;

namespace NlpToSqlApi.Services
{
    public interface INaturalLanguageSqlBuilder
    {
        SqlBuildResult Build(string question, DatabaseSchema schema, DatabaseType dbType);
    }

    /// <summary>
    /// Turns an English question into SQL using the real schema (tables, columns, foreign keys).
    /// Pure logic: no database access, so it can be unit tested.
    /// </summary>
    public class NaturalLanguageSqlBuilder : INaturalLanguageSqlBuilder
    {
        public SqlBuildResult Build(string question, DatabaseSchema schema, DatabaseType dbType)
        {
            if (string.IsNullOrWhiteSpace(question))
                return SqlBuildResult.Fail("Please type a question.");
            if (schema.Tables.Count == 0)
                return SqlBuildResult.Fail("No tables found in database.");

            try
            {
                return new Session(question, schema, dbType).Run();
            }
            catch (QuestionException ex)
            {
                return SqlBuildResult.Fail(ex.Message);
            }
        }

        private sealed class QuestionException : Exception
        {
            public QuestionException(string message) : base(message) { }
        }

        // ---------- model ----------

        private sealed class Tok
        {
            public string Text = "";   // lower-case
            public string Stem = "";
            public string Raw = "";    // original text (quoted value / number)
            public bool Quoted;
            public bool Number;
            public bool Word => !Quoted && !Number;
        }

        private sealed class Col
        {
            public string Name = "";
            public string[] Tokens = Array.Empty<string>();
            public bool IsPk, Numeric, Date, Text, Sensitive, Key;
            public List<(string Value, string[] Stems)> Values = new();
        }

        private sealed class Tbl
        {
            public string Name = "";
            public string[] Tokens = Array.Empty<string>();
            public List<Col> Cols = new();
            public Col? Pk => Cols.FirstOrDefault(c => c.IsPk) ?? Cols.FirstOrDefault(c => c.Key);
            public Col? Label;
            public Col? Label2;   // "last name" next to a "first name" label
        }

        private sealed class Fk
        {
            public string FromTable = "", FromCol = "", ToTable = "", ToCol = "";
            public string[] RoleTokens = Array.Empty<string>();
            public bool IsDefaultRole;
        }

        private sealed class Mention
        {
            public int Start, Len;
            public string Table = "";
            public Fk? Role;
            public int End => Start + Len;
            public Inst? Inst;
        }

        private sealed class ColMention
        {
            public int Start, Len;
            public Tbl Table = null!;
            public Col Col = null!;
            public Fk? Role;
            public Inst? Inst;
            public bool Used;
            public int End => Start + Len;
        }

        private sealed class Inst
        {
            public string Table = "";
            public string Alias = "";
            public Fk? Role;
            public Inst? Parent;
            public string ParentCol = "", OwnCol = "";
            public bool Shown;   // the question talks about it
            public bool Anti;    // "users with no bookings"
        }

        private sealed class OpMatch
        {
            public int Start, End;
            public string Op = "=";
            public bool Like, Between;
            public string LikeMode = "contains"; // contains | starts | ends
        }

        private sealed class Agg
        {
            public string Func = "";
            public Col? Col;
            public Inst? Inst;
            public bool Distinct;
            public bool Rank;   // "which X has the most Y"
        }

        // ---------- vocabulary ----------

        private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
        {
            "the","a","an","me","please","all","their","its","my","our","these","those","show","display","list","get","find",
            "give","see","view","fetch","select","what","which","who","whom","whose","where","when","how","are","is","was","were",
            "be","been","has","have","had","do","does","did","with","and","or","of","in","on","at","for","from","to","by","per",
            "each","every","as","than","that","this","it","them","there","then","can","could","would","should","i","you","we","they",
            "also","only","just","s","tell","about","having","whose",
            "count","number","total","sum","average","avg","mean","maximum","max","highest","largest","biggest","greatest","most",
            "minimum","min","lowest","smallest","least","fewest","many",
            "top","bottom","first","last","latest","newest","recent","oldest","earliest","order","sort","sorted","ordered",
            "ascending","descending","asc","desc","limit","group","grouped","distinct","unique",
            "more","greater","less","above","below","over","under","equal","equals","not","between","contains","containing","like",
            "named","called","starting","ending","starts","ends","begins","without","no","higher","lower","bigger","smaller","larger","fewer"
        };

        private static readonly HashSet<string> GenericWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "id","name","type","status","date","time","number","code","title","description","key","value"
        };

        private static readonly string[] MeasureHints = { "price", "cost", "amount", "fare", "salary", "revenue", "total" };

        private static readonly string[] Months =
            { "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december" };

        private static readonly string[] Sensitive = { "password", "hash", "salt", "secret", "token" };

        private static readonly (string[] Words, string Op, bool Like, string Mode)[] OpPhrases =
        {
            (new[]{"greater","than","or","equal","to"}, ">=", false, ""),
            (new[]{"less","than","or","equal","to"}, "<=", false, ""),
            (new[]{"not","equal","to"}, "<>", false, ""),
            (new[]{"different","from"}, "<>", false, ""),
            (new[]{"equal","to"}, "=", false, ""),
            (new[]{"at","least"}, ">=", false, ""),
            (new[]{"at","most"}, "<=", false, ""),
            (new[]{"no","less","than"}, ">=", false, ""),
            (new[]{"no","more","than"}, "<=", false, ""),
            (new[]{"more","than"}, ">", false, ""),
            (new[]{"greater","than"}, ">", false, ""),
            (new[]{"higher","than"}, ">", false, ""),
            (new[]{"bigger","than"}, ">", false, ""),
            (new[]{"larger","than"}, ">", false, ""),
            (new[]{"less","than"}, "<", false, ""),
            (new[]{"lower","than"}, "<", false, ""),
            (new[]{"smaller","than"}, "<", false, ""),
            (new[]{"fewer","than"}, "<", false, ""),
            (new[]{"starts","with"}, "", true, "starts"),
            (new[]{"starting","with"}, "", true, "starts"),
            (new[]{"begins","with"}, "", true, "starts"),
            (new[]{"ends","with"}, "", true, "ends"),
            (new[]{"ending","with"}, "", true, "ends"),
            (new[]{"above"}, ">", false, ""),
            (new[]{"over"}, ">", false, ""),
            (new[]{"exceeds"}, ">", false, ""),
            (new[]{"exceeding"}, ">", false, ""),
            (new[]{"below"}, "<", false, ""),
            (new[]{"under"}, "<", false, ""),
            (new[]{"equals"}, "=", false, ""),
            (new[]{"equal"}, "=", false, ""),
            (new[]{">="}, ">=", false, ""),
            (new[]{"<="}, "<=", false, ""),
            (new[]{"<>"}, "<>", false, ""),
            (new[]{"!="}, "<>", false, ""),
            (new[]{">"}, ">", false, ""),
            (new[]{"<"}, "<", false, ""),
            (new[]{"="}, "=", false, ""),
            (new[]{"contains"}, "", true, "contains"),
            (new[]{"containing"}, "", true, "contains"),
            (new[]{"contain"}, "", true, "contains"),
            (new[]{"like"}, "", true, "contains"),
            (new[]{"named"}, "=", false, ""),
            (new[]{"called"}, "=", false, ""),
        };

        private static readonly (string[] Words, string Func)[] AggPhrases =
        {
            (new[]{"how","many"}, "COUNT"),
            (new[]{"total","number","of"}, "COUNT"),
            (new[]{"number","of"}, "COUNT"),
            (new[]{"count","of"}, "COUNT"),
            (new[]{"count"}, "COUNT"),
            (new[]{"sum","of"}, "SUM"),
            (new[]{"sum"}, "SUM"),
            (new[]{"total"}, "SUM"),
            (new[]{"average"}, "AVG"),
            (new[]{"avg"}, "AVG"),
            (new[]{"mean"}, "AVG"),
            (new[]{"maximum"}, "MAX"),
            (new[]{"max"}, "MAX"),
            (new[]{"highest"}, "MAX"),
            (new[]{"largest"}, "MAX"),
            (new[]{"biggest"}, "MAX"),
            (new[]{"greatest"}, "MAX"),
            (new[]{"most"}, "MAX"),
            (new[]{"minimum"}, "MIN"),
            (new[]{"min"}, "MIN"),
            (new[]{"lowest"}, "MIN"),
            (new[]{"smallest"}, "MIN"),
            (new[]{"least"}, "MIN"),
            (new[]{"fewest"}, "MIN"),
        };

        // ---------- one build ----------

        private sealed class Session
        {
            private readonly string _question;
            private readonly DatabaseType _db;
            private readonly Dictionary<string, Tbl> _tables = new(StringComparer.OrdinalIgnoreCase);
            private readonly List<Fk> _fks = new();
            private readonly List<Tok> _t = new();
            private bool[] _used = Array.Empty<bool>();
            private Mention?[] _mentionAt = Array.Empty<Mention?>();
            private ColMention?[] _colAt = Array.Empty<ColMention?>();
            private readonly List<Mention> _mentions = new();
            private readonly List<ColMention> _colMentions = new();
            private readonly List<Inst> _insts = new();
            private Inst _primary = null!;
            private (string Op, string Number, Inst Inst)? _having;
            private readonly HashSet<string> _valueStems = new();
            private string[] _measureHint = Array.Empty<string>();

            public Session(string question, DatabaseSchema schema, DatabaseType db)
            {
                _question = question;
                _db = db;
                LoadSchema(schema);
                Tokenize();
            }

            // ----- schema -----

            private void LoadSchema(DatabaseSchema schema)
            {
                foreach (var (name, info) in schema.Tables)
                {
                    var tbl = new Tbl { Name = name, Tokens = Split(name) };
                    foreach (var c in info.Columns)
                    {
                        var type = (c.Type ?? "").ToLowerInvariant();
                        var tokens = Split(c.Name);
                        var col = new Col
                        {
                            Name = c.Name,
                            Tokens = tokens,
                            IsPk = c.PrimaryKey,
                            Numeric = new[] { "int", "decimal", "float", "double", "numeric", "real", "money", "number", "serial" }.Any(type.Contains),
                            Date = type.Contains("date") || type.Contains("time"),
                            Sensitive = tokens.Any(t => Sensitive.Any(s => t.StartsWith(s)))
                        };
                        col.Text = !col.Numeric && !col.Date && (type.Contains("char") || type.Contains("text") || type.Contains("string") || type == "");
                        col.Key = col.IsPk || (tokens.Length > 0 && tokens[^1] == "id");
                        if (!col.Sensitive && info.ColumnValues.TryGetValue(c.Name, out var values))
                            foreach (var v in values)
                            {
                                var stems = Regex.Matches(v, "[A-Za-z0-9]+").Select(m => Stem(m.Value)).ToArray();
                                if (stems.Length == 0) continue;
                                col.Values.Add((v, stems));
                                foreach (var st in stems) _valueStems.Add(st);
                            }
                        tbl.Cols.Add(col);
                    }
                    tbl.Label = tbl.Cols.FirstOrDefault(c => !c.Sensitive && c.Tokens.Length > 0 && c.Tokens[^1] == "name")
                                ?? tbl.Cols.FirstOrDefault(c => !c.Sensitive && c.Tokens.Contains("name"))
                                ?? tbl.Cols.FirstOrDefault(c => !c.Sensitive && c.Tokens.Contains("title"))
                                ?? tbl.Cols.FirstOrDefault(c => c.Text && !c.Sensitive && !c.Key)
                                ?? tbl.Pk;
                    if (tbl.Label != null && tbl.Label.Tokens.SequenceEqual(new[] { "first", "name" }))
                        tbl.Label2 = tbl.Cols.FirstOrDefault(c => !c.Sensitive && c.Tokens.SequenceEqual(new[] { "last", "name" }));
                    _tables[name] = tbl;
                }

                foreach (var (table, keys) in schema.Relationships)
                {
                    if (!_tables.ContainsKey(table)) continue;
                    foreach (var k in keys)
                    {
                        if (!_tables.ContainsKey(k.ReferencedTable) || string.Equals(table, k.ReferencedTable, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var colTokens = Split(k.Column);
                        var role = colTokens.Length > 0 && colTokens[^1] == "id" ? colTokens[..^1] : colTokens;
                        var target = _tables[k.ReferencedTable].Tokens;
                        _fks.Add(new Fk
                        {
                            FromTable = _tables[table].Name,
                            FromCol = k.Column,
                            ToTable = _tables[k.ReferencedTable].Name,
                            ToCol = k.ReferencedColumn,
                            RoleTokens = role,
                            IsDefaultRole = role.Length == 0 || role.SequenceEqual(target) || target.TakeLast(role.Length).SequenceEqual(role)
                        });
                    }
                }
            }

            // ----- text helpers -----

            private static string Stem(string w)
            {
                w = w.ToLowerInvariant();
                if (w.Length > 3 && w.EndsWith("ies")) return w[..^3] + "y";
                if (w.Length > 4 && (w.EndsWith("ches") || w.EndsWith("shes") || w.EndsWith("sses") || w.EndsWith("xes"))) return w[..^2];
                if (w.Length > 2 && w.EndsWith("s") && !w.EndsWith("ss")) return w[..^1];
                return w;
            }

            private static string[] Split(string identifier) =>
                Regex.Split(identifier, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|[_\s\-]+")
                     .Where(p => p.Length > 0).Select(Stem).ToArray();

            private void Tokenize()
            {
                var rx = new Regex(@"(?<![A-Za-z0-9])'([^']*)'|(?<![A-Za-z0-9])""([^""]*)""|(\d+(?:\.\d+)?)|(>=|<=|<>|!=|[<>=])|([A-Za-z_][A-Za-z0-9_]*)");
                foreach (Match m in rx.Matches(_question))
                {
                    var tok = new Tok();
                    if (m.Groups[1].Success || m.Groups[2].Success)
                    {
                        tok.Raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                        tok.Text = tok.Raw.ToLowerInvariant();
                        tok.Stem = tok.Text;
                        tok.Quoted = true;
                    }
                    else if (m.Groups[3].Success)
                    {
                        tok.Raw = tok.Text = tok.Stem = m.Groups[3].Value;
                        tok.Number = true;
                    }
                    else
                    {
                        tok.Raw = m.Value;
                        tok.Text = m.Value.ToLowerInvariant();
                        tok.Stem = Stem(m.Value);
                    }
                    _t.Add(tok);
                }
                _used = new bool[_t.Count];
                _mentionAt = new Mention?[_t.Count];
                _colAt = new ColMention?[_t.Count];
            }

            private bool IsWord(int i) => i >= 0 && i < _t.Count && _t[i].Word;
            private string Text(int i) => i >= 0 && i < _t.Count ? _t[i].Text : "";
            private bool IsBlocked(int i) => IsWord(i) && Blocked.Contains(_t[i].Text);

            private bool MatchWords(int i, string[] words)
            {
                if (i < 0 || i + words.Length > _t.Count) return false;
                for (int k = 0; k < words.Length; k++)
                {
                    var t = _t[i + k];
                    if (t.Quoted || t.Text != words[k]) return false;
                }
                return true;
            }

            private bool MatchStems(int i, string[] stems)
            {
                if (stems.Length == 0 || i + stems.Length > _t.Count) return false;
                for (int k = 0; k < stems.Length; k++)
                    if (!_t[i + k].Word || _t[i + k].Stem != stems[k]) return false;
                return true;
            }

            // ----- run -----

            public SqlBuildResult Run()
            {
                FindMentions();
                if (_mentions.Count == 0) InferTableFromColumns();
                if (_mentions.Count == 0)
                    throw new QuestionException("I could not find a table in your question. Available tables: " +
                                                string.Join(", ", _tables.Keys) + ".");

                FindColumnMentions();
                BuildInstances();
                return Compose();
            }

            // ----- 1. tables / roles -----

            private void FindMentions()
            {
                var raw = new List<Mention>();
                foreach (var tbl in _tables.Values)
                {
                    for (int i = 0; i < _t.Count; i++)
                        if (MatchStems(i, tbl.Tokens)) raw.Add(new Mention { Start = i, Len = tbl.Tokens.Length, Table = tbl.Name });

                    if (tbl.Tokens.Length > 1)
                    {
                        var last = tbl.Tokens[^1];
                        bool unique = _tables.Values.Count(o => o.Tokens.Length > 0 && o.Tokens[^1] == last) == 1 &&
                                      !_tables.Values.Any(o => o != tbl && o.Tokens.SequenceEqual(new[] { last }));
                        if (unique && !GenericWords.Contains(last))
                            for (int i = 0; i < _t.Count; i++)
                                if (MatchStems(i, new[] { last })) raw.Add(new Mention { Start = i, Len = 1, Table = tbl.Name });
                    }
                }

                // overlapping spans: keep the longest
                raw = raw.OrderByDescending(m => m.Len).ThenBy(m => m.Start).ToList();
                var kept = new List<Mention>();
                foreach (var m in raw)
                    if (!kept.Any(k => m.Start < k.End && k.Start < m.End)) kept.Add(m);

                // role mentions ("driver" -> Users through Bookings.DriverId)
                var mentionedTables = kept.Select(m => m.Table).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var fk in _fks.Where(f => !f.IsDefaultRole && mentionedTables.Contains(f.FromTable)))
                    for (int i = 0; i < _t.Count; i++)
                        if (MatchStems(i, fk.RoleTokens) && !kept.Any(k => i < k.End && k.Start < i + fk.RoleTokens.Length))
                            kept.Add(new Mention { Start = i, Len = fk.RoleTokens.Length, Table = fk.ToTable, Role = fk });

                // "vehicle type" next to Pricings is the column VehicleType, not the Vehicles table
                var tablesNow = kept.Select(m => m.Table).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var f in _fks.Where(f => kept.Any(k => k.Role == f))) tablesNow.Add(f.FromTable);
                kept.RemoveAll(m => tablesNow.Any(other =>
                    !string.Equals(other, m.Table, StringComparison.OrdinalIgnoreCase) &&
                    _tables[other].Cols.Any(c => c.Tokens.Length > m.Len && MatchStems(m.Start, c.Tokens) &&
                                                 c.Tokens.Take(m.Len).SequenceEqual(m.Role?.RoleTokens ?? _tables[m.Table].Tokens.TakeLast(m.Len).ToArray()))));

                foreach (var m in kept.OrderBy(m => m.Start))
                {
                    _mentions.Add(m);
                    for (int i = m.Start; i < m.End; i++) { _mentionAt[i] = m; _used[i] = true; }
                }
            }

            private void InferTableFromColumns()
            {
                var scores = new Dictionary<string, int>();
                void Add(string table, int points) => scores[table] = scores.GetValueOrDefault(table) + points;

                for (int i = 0; i < _t.Count; i++)
                {
                    if (!_t[i].Word) continue;
                    for (int len = Math.Min(4, _t.Count - i); len >= 2; len--)
                    {
                        if (Enumerable.Range(i, len).Any(k => !_t[k].Word)) continue;
                        var stems = Enumerable.Range(i, len).Select(k => _t[k].Stem).ToArray();
                        foreach (var tbl in _tables.Values)
                            if (tbl.Cols.Any(c => c.Tokens.SequenceEqual(stems))) Add(tbl.Name, len * 2);
                    }
                    if (IsBlocked(i) || GenericWords.Contains(_t[i].Stem)) continue;
                    foreach (var tbl in _tables.Values)
                        if (tbl.Cols.Any(c => c.Tokens.Length == 1 && c.Tokens[0] == _t[i].Stem)) Add(tbl.Name, 2);
                        else if (tbl.Cols.Any(c => c.Tokens.Contains(_t[i].Stem))) Add(tbl.Name, 1);
                }
                if (scores.Count == 0) return;
                var ranked = scores.OrderByDescending(x => x.Value).ToList();
                if (ranked.Count > 1 && ranked[0].Value == ranked[1].Value)
                    throw new QuestionException("Your question matches more than one table (" +
                        string.Join(", ", ranked.Where(r => r.Value == ranked[0].Value).Select(r => r.Key)) +
                        "). Please mention the table, for example \"" + string.Join(" ", _t.Select(t => t.Raw)) + " of " + ranked[0].Key.ToLowerInvariant() + "\".");
                _mentions.Add(new Mention { Start = 0, Len = 0, Table = ranked[0].Key });
            }

            // ----- 2. columns -----

            private IEnumerable<string> ScopeTables() =>
                _mentions.Select(m => m.Table).Distinct(StringComparer.OrdinalIgnoreCase);

            private void FindColumnMentions()
            {
                var scope = ScopeTables().ToList();
                var first = scope.First();

                for (int i = 0; i < _t.Count;)
                {
                    if (!_t[i].Word || _mentionAt[i] != null) { i++; continue; }

                    // a table/role word right before: "user name", "driver name"
                    Mention? qualifier = i > 0 ? _mentionAt[i - 1] : null;

                    ColMention? found = null;
                    for (int len = Math.Min(4, _t.Count - i); len >= 1 && found == null; len--)
                    {
                        if (Enumerable.Range(i, len).Any(k => !_t[k].Word || _mentionAt[k] != null)) continue;
                        var stems = Enumerable.Range(i, len).Select(k => _t[k].Stem).ToArray();
                        bool blocked = IsBlocked(i);
                        found = MatchColumn(stems, i, len, qualifier, scope, first, blocked);
                    }

                    if (found != null)
                    {
                        _colMentions.Add(found);
                        for (int k = found.Start; k < found.End; k++) { _colAt[k] = found; _used[k] = true; }
                        i = found.End;
                    }
                    else i++;
                }
            }

            private ColMention? MatchColumn(string[] stems, int start, int len, Mention? qualifier, List<string> scope, string firstTable, bool blocked)
            {
                // candidate tables in priority order
                var order = new List<(string Table, Fk? Role)>();
                if (qualifier != null) order.Add((qualifier.Table, qualifier.Role));
                foreach (var s in scope) order.Add((s, null));

                foreach (var (tableName, role) in order)
                {
                    var tbl = _tables[tableName];
                    var exact = tbl.Cols.FirstOrDefault(c => c.Tokens.SequenceEqual(stems));
                    if (exact != null && (!blocked || len >= 2))
                        return new ColMention { Start = start, Len = len, Table = tbl, Col = exact, Role = role };
                }
                // an exact column of a connected table the question did not name beats a partial match ("quantity" -> OrderItems.Quantity, not Products.StockQuantity)
                var outer = FindOutsideScope(stems, start, len, scope, firstTable, blocked, exactOnly: true);
                if (outer != null) return outer;

                if (!blocked)
                {
                    foreach (var (tableName, role) in order)
                    {
                        var tbl = _tables[tableName];
                        var partial = tbl.Cols.FirstOrDefault(c => c.Tokens.Length > len && !(len == 1 && stems[0] == "id") && ContainsRun(c.Tokens, stems));
                        if (partial != null)
                            return new ColMention { Start = start, Len = len, Table = tbl, Col = partial, Role = role };
                    }
                }

                return FindOutsideScope(stems, start, len, scope, firstTable, blocked, exactOnly: false);
            }

            // a column of a table the question did not name: only when it is clearly connected and unambiguous
            private ColMention? FindOutsideScope(string[] stems, int start, int len, List<string> scope, string firstTable, bool blocked, bool exactOnly)
            {
                var others = _tables.Values.Where(t => !scope.Contains(t.Name, StringComparer.OrdinalIgnoreCase)).ToList();
                var hits = new List<(Tbl Table, Col Col, int Dist)>();
                foreach (var tbl in others)
                    foreach (var c in tbl.Cols)
                    {
                        bool exact = c.Tokens.SequenceEqual(stems) && (!blocked || len >= 2) && (len >= 2 || !GenericWords.Contains(stems[0]));
                        bool part = !exactOnly && !blocked && len == 1 && !GenericWords.Contains(stems[0]) && c.Tokens.Length > 1 && c.Tokens.Contains(stems[0]);
                        if (!exact && !part) continue;
                        int dist = Distance(firstTable, tbl.Name);
                        if (dist >= 0) hits.Add((tbl, c, dist));
                    }
                if (hits.Count == 0) return null;
                int min = hits.Min(h => h.Dist);
                var best = hits.Where(h => h.Dist == min).ToList();
                if (best.Count > 1)
                {
                    if (exactOnly) return null;
                    throw new QuestionException($"'{string.Join(" ", stems)}' could mean " +
                        string.Join(" or ", best.Select(b => $"{b.Table.Name}.{b.Col.Name}")) + ". Please mention the table, for example \"" + best[0].Table.Name.ToLowerInvariant() + " " + string.Join(" ", stems) + "\".");
                }
                return new ColMention { Start = start, Len = len, Table = best[0].Table, Col = best[0].Col };
            }

            private static bool ContainsRun(string[] haystack, string[] needle)
            {
                for (int i = 0; i + needle.Length <= haystack.Length; i++)
                    if (haystack.Skip(i).Take(needle.Length).SequenceEqual(needle)) return true;
                return false;
            }

            // ----- 3. joins -----

            private int Distance(string from, string to)
            {
                if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 0;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from };
                var queue = new Queue<(string T, int D)>();
                queue.Enqueue((from, 0));
                while (queue.Count > 0)
                {
                    var (t, d) = queue.Dequeue();
                    foreach (var n in Neighbours(t))
                    {
                        if (!seen.Add(n)) continue;
                        if (string.Equals(n, to, StringComparison.OrdinalIgnoreCase)) return d + 1;
                        queue.Enqueue((n, d + 1));
                    }
                }
                return -1;
            }

            private IEnumerable<string> Neighbours(string table) =>
                _fks.Where(f => string.Equals(f.FromTable, table, StringComparison.OrdinalIgnoreCase)).Select(f => f.ToTable)
                    .Concat(_fks.Where(f => string.Equals(f.ToTable, table, StringComparison.OrdinalIgnoreCase)).Select(f => f.FromTable))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

            private Inst? FindInst(string table, Fk? role) =>
                role != null
                    ? _insts.FirstOrDefault(i => i.Role == role)
                    : _insts.FirstOrDefault(i => i.Role == null && string.Equals(i.Table, table, StringComparison.OrdinalIgnoreCase));

            private string NewAlias(string wanted)
            {
                var alias = wanted;
                int n = 2;
                while (_insts.Any(i => string.Equals(i.Alias, alias, StringComparison.OrdinalIgnoreCase)))
                    alias = wanted + n++;
                return alias;
            }

            private void BuildInstances()
            {
                var firstPlain = _mentions.FirstOrDefault(m => m.Role == null) ?? _mentions[0];
                _primary = new Inst { Table = firstPlain.Table, Alias = firstPlain.Table, Shown = true };
                _insts.Add(_primary);

                foreach (var m in _mentions.Where(m => m.Role == null))
                    m.Inst = Ensure(m.Table, null, true);
                foreach (var m in _mentions.Where(m => m.Role != null))
                    m.Inst = Ensure(m.Table, m.Role, true);

                foreach (var c in _colMentions)
                {
                    c.Inst = c.Role != null ? FindInst(c.Table.Name, c.Role) ?? Ensure(c.Table.Name, c.Role, true)
                                            : FindInst(c.Table.Name, null) ?? Ensure(c.Table.Name, null, true);
                    c.Inst.Shown = true;
                }
            }

            private Inst Ensure(string table, Fk? role, bool shown)
            {
                var existing = FindInst(table, role);
                if (existing != null) { existing.Shown |= shown; return existing; }

                if (role != null)
                {
                    var parent = FindInst(role.FromTable, null) ?? Ensure(role.FromTable, null, false);
                    var inst = new Inst
                    {
                        Table = table, Role = role, Parent = parent, Shown = shown,
                        Alias = NewAlias(string.Concat(role.RoleTokens.Select(t => char.ToUpperInvariant(t[0]) + t[1..]))),
                        ParentCol = role.FromCol, OwnCol = role.ToCol
                    };
                    _insts.Add(inst);
                    return inst;
                }

                // shortest path from anything already joined
                var sources = _insts.Select(i => i.Table).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<string>();
                foreach (var s in sources) { prev[s] = ""; queue.Enqueue(s); }
                string? hit = null;
                while (queue.Count > 0 && hit == null)
                {
                    var cur = queue.Dequeue();
                    foreach (var n in Neighbours(cur))
                    {
                        if (prev.ContainsKey(n)) continue;
                        prev[n] = cur;
                        if (string.Equals(n, table, StringComparison.OrdinalIgnoreCase)) { hit = n; break; }
                        queue.Enqueue(n);
                    }
                }
                if (hit == null)
                    throw new QuestionException($"I could not find a relationship between '{_primary.Table}' and '{table}', so I cannot join them.");

                var chain = new List<string>();
                for (var c = hit; !string.IsNullOrEmpty(prev[c]); c = prev[c]) chain.Insert(0, c);
                Inst last = null!;
                foreach (var step in chain)
                {
                    var from = prev[step];
                    var parent = FindInst(from, null) ?? _insts.First(i => string.Equals(i.Table, from, StringComparison.OrdinalIgnoreCase));
                    var inst = FindInst(step, null);
                    if (inst == null)
                    {
                        var (pCol, oCol) = PickEdge(parent.Table, step);
                        inst = new Inst { Table = step, Alias = NewAlias(step), Parent = parent, ParentCol = pCol, OwnCol = oCol };
                        _insts.Add(inst);
                    }
                    last = inst;
                }
                last.Shown |= shown;
                return last;
            }

            private (string ParentCol, string OwnCol) PickEdge(string parentTable, string childTable)
            {
                // the parent side holds the FK
                var fkOnParent = _fks.Where(f => Eq(f.FromTable, parentTable) && Eq(f.ToTable, childTable)).ToList();
                if (fkOnParent.Count > 0)
                {
                    var f = fkOnParent.FirstOrDefault(x => x.IsDefaultRole) ?? fkOnParent[0];
                    return (f.FromCol, f.ToCol);
                }
                // the new table holds the FK
                var fkOnChild = _fks.Where(f => Eq(f.FromTable, childTable) && Eq(f.ToTable, parentTable)).ToList();
                var g = fkOnChild.FirstOrDefault(x => x.IsDefaultRole) ?? fkOnChild[0];
                return (g.ToCol, g.FromCol);
            }

            private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

            // ----- 4. compose -----

            private string Q(string id) => _db switch
            {
                DatabaseType.MySQL => $"`{id}`",
                DatabaseType.PostgreSQL => $"\"{id}\"",
                _ => $"[{id}]"
            };

            private string Ref(Inst i, Col c) => $"{Q(i.Alias)}.{Q(c.Name)}";
            private Tbl TblOf(Inst i) => _tables[i.Table];

            private string OutName(Inst i, Col c) => ReferenceEquals(i, _primary) ? c.Name : $"{i.Alias}_{c.Name}";

            private string SelectCol(Inst i, Col c) =>
                ReferenceEquals(i, _primary) ? Ref(i, c) : $"{Ref(i, c)} AS {Q(OutName(i, c))}";

            private static string Lit(string s) => "'" + s.Replace("'", "''") + "'";

            private string LikeOp => _db == DatabaseType.PostgreSQL ? "ILIKE" : "LIKE";

            private SqlBuildResult Compose()
            {
                var conds = new List<string>();
                var aggs = new List<Agg>();
                var groupCols = new List<(Inst Inst, Col Col)>();
                var orderBy = new List<string>();
                int? limit = null;
                bool distinct = false;
                bool explicitGroup = false;
                bool rank = false;
                bool rankAsc = false;

                // "users with no bookings" / "users without bookings"
                foreach (var m in _mentions.Where(m => m.Inst != null && !ReferenceEquals(m.Inst, _primary)))
                {
                    // also "customers who have not placed any orders", "customers who never ordered"
                    for (int p = m.Start - 1; p >= 0 && p >= m.Start - 5 && IsWord(p); p--)
                    {
                        var w = Text(p);
                        if (w is "without" or "no" or "not" or "never" or "t") { m.Inst!.Anti = true; _used[p] = true; break; }
                        if (!(w is "any" or "ever" or "a" or "an" or "have" or "has" or "had" or "did" or "does" or "do" or "who" or "that" or "which"
                                  or "placed" or "made" or "ordered" or "bought" or "purchased" or "got" or "haven" or "hasn" or "didn" or "doesn" or "don"))
                            break;
                    }
                }

                // ---- top N / latest / oldest ----
                string? sortIntent = null;   // "top" | "bottom" | "latest" | "oldest"
                bool superlative = false;
                for (int i = 0; i < _t.Count; i++)
                {
                    if (_used[i] || !IsWord(i)) continue;
                    var w = Text(i);
                    bool numNext = i + 1 < _t.Count && _t[i + 1].Number;
                    if (numNext && (w is "top" or "first" or "bottom" or "last" or "latest" or "newest" or "recent" or "oldest" or "earliest" or "limit"
                                      or "highest" or "largest" or "biggest" or "greatest" or "lowest" or "smallest"))
                    {
                        limit = (int)Math.Min(1000, double.Parse(_t[i + 1].Text, System.Globalization.CultureInfo.InvariantCulture));
                        sortIntent = w switch { "top" or "highest" or "largest" or "biggest" or "greatest" => "top", "bottom" or "lowest" or "smallest" => "bottom", "latest" or "newest" or "recent" => "latest", "oldest" or "earliest" => "oldest", "last" => "latest", _ => sortIntent };
                        _used[i] = _used[i + 1] = true;
                    }
                    else if (_t[i].Word && i > 0 && _t[i - 1].Number && !_used[i - 1] && w is "highest" or "largest" or "biggest" or "greatest" or "most" or "lowest" or "smallest" or "least")
                    {
                        limit = (int)Math.Min(1000, double.Parse(_t[i - 1].Text, System.Globalization.CultureInfo.InvariantCulture));
                        sortIntent = w is "lowest" or "smallest" or "least" ? "bottom" : "top";
                        _used[i] = _used[i - 1] = true;
                    }
                    else if (w is "latest" or "newest" or "recent") { sortIntent = "latest"; _used[i] = true; }
                    else if (w is "oldest" or "earliest") { sortIntent = "oldest"; _used[i] = true; }
                    else if (w is "distinct" or "unique") { distinct = true; _used[i] = true; }
                    else if (w is "expensive" or "costly" or "priciest" or "cheapest" or "cheap" or "cheaper")
                    {
                        // "most expensive", "least expensive", "cheapest": sort by a price-like column
                        bool most = i > 0 && Text(i - 1) == "most", least = i > 0 && Text(i - 1) == "least";
                        bool cheap = w is "cheapest" or "cheap" or "cheaper";
                        sortIntent = cheap || least ? "bottom" : "top";
                        _measureHint = MeasureHints;
                        if (most || least) _used[i - 1] = true;
                        if (most || least || w is "priciest" or "cheapest") superlative = true;
                        _used[i] = true;
                    }
                }
                if (superlative && limit == null) limit = 1;

                // ---- conditions ----
                BuildConditions(conds);

                // ---- aggregates ----
                var aggTargets = new HashSet<ColMention>();
                for (int i = 0; i < _t.Count; i++)
                {
                    if (!IsWord(i)) continue;
                    var colHere = _colAt[i];
                    bool totalIsColumn = colHere != null && colHere.Start == i && !colHere.Used && Text(i) == "total" &&
                                         !(i > 0 && Text(i - 1) is "by" or "with" or "and" or "or" or "between" or "where" or "sorted" or "ordered");
                    if (_used[i] && !totalIsColumn) continue;

                    foreach (var (words, func) in AggPhrases)
                    {
                        if (!MatchWords(i, words)) continue;
                        if (func is "MAX" or "MIN" && limit != null && sortIntent is "top" or "bottom")
                        {
                            for (int k = i; k < i + words.Length; k++) _used[k] = true;
                            break;
                        }
                        // "total fare": the aggregate keyword is also the start of the column name
                        int j = i + words.Length;
                        if (colHere != null && colHere.Start == i && words.Length == 1) j = i;   // column starts at the keyword
                        if (words.Length > 1 || colHere == null) for (int k = i; k < i + words.Length; k++) _used[k] = true;

                        while (j < _t.Count && (Text(j) is "of" or "the" or "a" or "an" or "all" or "distinct" or "unique" or "different")) { if (Text(j) is "distinct" or "unique" or "different") distinct = true; j++; }

                        var agg = new Agg { Func = func, Distinct = false };
                        var cm = j < _t.Count ? _colAt[j] : null;
                        var mm = j < _t.Count ? _mentionAt[j] : null;

                        if (cm != null && !cm.Used)
                        {
                            agg.Col = cm.Col; agg.Inst = cm.Inst; cm.Used = true;
                            agg.Distinct = func == "COUNT" && distinct;
                            if (func == "COUNT") distinct = false;
                        }
                        if (agg.Col == null)
                        {
                            var measure = func == "SUM" && mm != null
                                ? _colMentions.FirstOrDefault(c => !c.Used && c.Start >= j && c.Start <= mm.End + 2 && c.Col.Numeric && !c.Col.Key)
                                : null;
                            if (measure != null)
                            {
                                agg.Col = measure.Col; agg.Inst = measure.Inst; aggTargets.Add(measure);
                            }
                            else if (func == "COUNT" || func == "SUM" && mm != null)
                            {
                                agg.Func = "COUNT";
                            }
                            else if (mm != null && mm.Inst != null && !ReferenceEquals(mm.Inst, _primary) && func is "MAX" or "MIN")
                            {
                                agg.Func = "COUNT"; agg.Rank = true; rank = true; rankAsc = func == "MIN";
                                agg.Inst = mm.Inst;
                            }
                            else
                            {
                                // "max and min fare": use the next (or previous) column in the question
                                var next = _colMentions.FirstOrDefault(c => c.Start >= j && !c.Used) ?? _colMentions.LastOrDefault(c => c.End <= i && !c.Used);
                                if (next != null) { agg.Col = next.Col; agg.Inst = next.Inst; aggTargets.Add(next); }
                                else
                                {
                                    var fallback = HintedMeasure(_primary);
                                    if (fallback == null)
                                        throw new QuestionException($"Which column should I use for {func.ToLowerInvariant()}? Please name it, for example \"{func.ToLowerInvariant()} {(TblOf(_primary).Cols.FirstOrDefault(c => c.Numeric)?.Name ?? "column").ToLowerInvariant()}\".");
                                    agg.Col = fallback; agg.Inst = _primary;
                                }
                            }
                        }
                        if (!aggs.Any(a => a.Func == agg.Func && a.Col == agg.Col && a.Rank == agg.Rank)) aggs.Add(agg);
                        break;
                    }
                }

                foreach (var target in aggTargets) target.Used = true;

                // ---- group by / order by ----
                for (int i = 0; i < _t.Count; i++)
                {
                    if (!IsWord(i) || _used[i]) continue;
                    var w = Text(i);
                    int len = 0; bool isOrder = false, isGroup = false;
                    if ((w is "order" or "sort") && Text(i + 1) == "by") { len = 2; isOrder = true; }
                    else if (w is "sorted" or "ordered" && Text(i + 1) == "by") { len = 2; isOrder = true; }
                    else if (w is "group" or "grouped" && Text(i + 1) == "by") { len = 2; isGroup = true; }
                    else if (w == "for" && Text(i + 1) == "each") { len = 2; isGroup = true; }
                    else if (w is "per" or "each") { len = 1; isGroup = true; }
                    else if (w == "by") { len = 1; if (aggs.Count > 0 && !rank) isGroup = true; else isOrder = true; }
                    if (len == 0) continue;
                    // do not re-read tokens already consumed (e.g. "top 5 ... by")
                    int j = i + len;
                    while (j < _t.Count && Text(j) is "the" or "a" or "an" or "each" or "every") j++;
                    var cm = j < _t.Count ? _colAt[j] : null;
                    var mm = j < _t.Count ? _mentionAt[j] : null;
                    if (isGroup)
                    {
                        if (mm?.Inst != null)
                        {
                            var tb = TblOf(mm.Inst);
                            if (tb.Pk != null) groupCols.Add((mm.Inst, tb.Pk));
                            if (tb.Label != null && tb.Label != tb.Pk) groupCols.Add((mm.Inst, tb.Label));
                            if (tb.Label2 != null) groupCols.Add((mm.Inst, tb.Label2));
                            for (int k = i; k < i + len; k++) _used[k] = true;
                            explicitGroup = true;
                        }
                        else if (cm != null)
                        {
                            groupCols.Add((cm.Inst!, cm.Col)); cm.Used = true;
                            for (int k = i; k < i + len; k++) _used[k] = true;
                            explicitGroup = true;
                        }
                    }
                    else if (isOrder && cm != null)
                    {
                        bool desc = DirectionAfter(cm.End, out var dir) ? dir : sortIntent is "top" or "latest";
                        orderBy.Add($"{Ref(cm.Inst!, cm.Col)} {(desc ? "DESC" : "ASC")}");
                        cm.Used = true;
                        for (int k = i; k < i + len; k++) _used[k] = true;
                    }
                    else if (isOrder && mm?.Inst != null && TblOf(mm.Inst).Label != null)
                    {
                        bool desc = DirectionAfter(mm.End, out var dir) ? dir : sortIntent is "top" or "latest";
                        orderBy.Add($"{Ref(mm.Inst, TblOf(mm.Inst).Label!)} {(desc ? "DESC" : "ASC")}");
                        for (int k = i; k < i + len; k++) _used[k] = true;
                    }
                }
                if (!explicitGroup && aggs.Count > 0 && aggs.All(a => a.Inst != null && !ReferenceEquals(a.Inst, _primary)) &&
                    (sortIntent is "top" or "bottom" || _t.Any(t => t.Word && t.Text is "by" or "per")))
                {
                    var tb = TblOf(_primary);
                    if (tb.Pk != null) groupCols.Add((_primary, tb.Pk));
                    if (tb.Label != null && tb.Label != tb.Pk) groupCols.Add((_primary, tb.Label));
                    if (tb.Label2 != null) groupCols.Add((_primary, tb.Label2));
                    explicitGroup = true;
                }
                if (explicitGroup && aggs.Count == 0) aggs.Add(new Agg { Func = "COUNT" });

                // ---- sort for top N / latest ----
                if (orderBy.Count == 0 && sortIntent != null && aggs.Count == 0)
                {
                    if (sortIntent is "latest" or "oldest")
                    {
                        var cd = _colMentions.FirstOrDefault(c => !c.Used && c.Col.Date);
                        (Inst, Col?) dateCol = cd != null
                            ? (cd.Inst!, cd.Col)
                            : (_primary, TblOf(_primary).Cols.FirstOrDefault(c => c.Date) ?? TblOf(_primary).Pk);
                        if (dateCol.Item2 != null)
                            orderBy.Add($"{Ref(dateCol.Item1, dateCol.Item2)} {(sortIntent == "latest" ? "DESC" : "ASC")}");
                        foreach (var c in _colMentions.Where(c => c.Col == dateCol.Item2 && c.Inst == dateCol.Item1)) c.Used = true;
                    }
                    else
                    {
                        var target = _colMentions.FirstOrDefault(c => !c.Used && c.Col.Numeric && !c.Col.Key);
                        (Inst Inst, Col? Col) t = target != null ? (target.Inst!, target.Col) : (_primary, HintedMeasure(_primary));
                        if (t.Col != null) orderBy.Add($"{Ref(t.Inst, t.Col)} {(sortIntent == "top" ? "DESC" : "ASC")}");
                        if (target != null) target.Used = true;
                    }
                }

                // ---- select list ----
                var select = new List<string>();
                var colsUsed = new List<string>();
                bool scalarAgg = false;

                var plain = _colMentions.Where(c => !c.Used && !c.Inst!.Anti).ToList();
                if (rank || _having != null)
                {
                    var tb = TblOf(_primary);
                    var keys = new List<Col>();
                    if (tb.Pk != null) keys.Add(tb.Pk);
                    if (tb.Label != null && tb.Label != tb.Pk) keys.Add(tb.Label);
                    if (tb.Label2 != null) keys.Add(tb.Label2);
                    foreach (var k in keys) { select.Add(Ref(_primary, k)); colsUsed.Add($"{_primary.Table}.{k.Name}"); }
                    groupCols.Clear();
                    foreach (var k in keys) groupCols.Add((_primary, k));
                    select.Add($"COUNT(*) AS {Q("Count")}");
                    if (rank)
                    {
                        orderBy.Clear();
                        orderBy.Add($"COUNT(*) {(rankAsc ? "ASC" : "DESC")}");
                        limit ??= 1;
                    }
                }
                else if (aggs.Count > 0)
                {
                    if (!explicitGroup) foreach (var c in plain) { groupCols.Add((c.Inst!, c.Col)); c.Used = true; }
                    var seen = new HashSet<string>();
                    foreach (var (inst, col) in groupCols)
                        if (seen.Add(Ref(inst, col))) { select.Add(SelectCol(inst, col)); colsUsed.Add($"{inst.Table}.{col.Name}"); }
                    foreach (var a in aggs)
                    {
                        string expr, name;
                        if (a.Col == null) { expr = "COUNT(*)"; name = "Count"; }
                        else
                        {
                            expr = $"{a.Func}({(a.Distinct ? "DISTINCT " : "")}{Ref(a.Inst ?? _primary, a.Col)})";
                            name = $"{(a.Func[0] + a.Func[1..].ToLowerInvariant())}_{a.Col.Name}";
                            colsUsed.Add($"{(a.Inst ?? _primary).Table}.{a.Col.Name}");
                        }
                        select.Add($"{expr} AS {Q(name)}");
                    }
                    var groupSeen = new HashSet<string>();
                    groupCols = groupCols.Where(g => groupSeen.Add(Ref(g.Inst, g.Col))).ToList();
                    scalarAgg = groupCols.Count == 0;
                    if (orderBy.Count == 0 && groupCols.Count > 0 && sortIntent is "top" or "bottom")
                    {
                        var first = select[^1];
                        orderBy.Add($"{first[..first.LastIndexOf(" AS ", StringComparison.Ordinal)]} {(sortIntent == "top" ? "DESC" : "ASC")}");
                    }
                }
                else if (plain.Count > 0)
                {
                    var seen = new HashSet<string>();
                    if (!plain.Any(c => ReferenceEquals(c.Inst, _primary)))
                        foreach (var pc in TblOf(_primary).Cols.Where(c => !c.Sensitive))
                        { select.Add(Ref(_primary, pc)); seen.Add(Ref(_primary, pc)); colsUsed.Add($"{_primary.Table}.{pc.Name}"); }
                    foreach (var c in plain)
                    {
                        if (seen.Add(Ref(c.Inst!, c.Col))) { select.Add(SelectCol(c.Inst!, c.Col)); colsUsed.Add($"{c.Inst!.Table}.{c.Col.Name}"); }
                        // "customer names": a plain "name" means first and last name together
                        var label2 = TblOf(c.Inst!).Label2;
                        if (c.Col == TblOf(c.Inst!).Label && label2 != null && c.Len == 1 && seen.Add(Ref(c.Inst!, label2)))
                        { select.Add(SelectCol(c.Inst!, label2)); colsUsed.Add($"{c.Inst!.Table}.{label2.Name}"); }
                    }
                }
                else
                {
                    foreach (var c in TblOf(_primary).Cols.Where(c => !c.Sensitive)) { select.Add(Ref(_primary, c)); colsUsed.Add($"{_primary.Table}.{c.Name}"); }
                    foreach (var inst in _insts.Where(i => !ReferenceEquals(i, _primary) && i.Shown && !i.Anti))
                    {
                        var tb = TblOf(inst);
                        bool nameLike = tb.Label != null && (tb.Label.Tokens.Contains("name") || tb.Label.Tokens.Contains("title"));
                        var cols = nameLike ? new List<Col> { tb.Label! } : tb.Cols.Where(c => !c.Key && !c.Sensitive).Take(6).ToList();
                        if (cols.Count == 0 && tb.Label != null) cols.Add(tb.Label);
                        if (nameLike && tb.Label2 != null) cols.Add(tb.Label2);
                        foreach (var c in cols) { select.Add(SelectCol(inst, c)); colsUsed.Add($"{inst.Table}.{c.Name}"); }
                    }
                    if (select.Count == 0) select.Add("*");
                }

                // ---- anti joins ----
                foreach (var inst in _insts.Where(i => i.Anti))
                {
                    var key = TblOf(inst).Pk ?? TblOf(inst).Cols.First();
                    conds.Add($"{Ref(inst, key)} IS NULL");
                }

                if (limit == null && !scalarAgg) limit = 100;

                // ---- SQL text ----
                var sb = new System.Text.StringBuilder("SELECT ");
                if (distinct && aggs.Count == 0) sb.Append("DISTINCT ");
                if (_db == DatabaseType.MSSQL && limit != null) sb.Append($"TOP {limit} ");
                sb.Append(string.Join(", ", select));
                sb.Append($"\nFROM {Q(_primary.Table)}");
                foreach (var inst in _insts.Where(i => !ReferenceEquals(i, _primary)))
                {
                    var alias = Eq(inst.Alias, inst.Table) ? "" : $" AS {Q(inst.Alias)}";
                    var kind = inst.Anti ? "LEFT JOIN" : "INNER JOIN";
                    sb.Append($"\n{kind} {Q(inst.Table)}{alias} ON {Q(inst.Parent!.Alias)}.{Q(inst.ParentCol)} = {Q(inst.Alias)}.{Q(inst.OwnCol)}");
                }
                if (conds.Count > 0) sb.Append($"\nWHERE {string.Join(" AND ", conds)}");
                if (groupCols.Count > 0) sb.Append($"\nGROUP BY {string.Join(", ", groupCols.Select(g => Ref(g.Inst, g.Col)))}");
                if (_having != null) sb.Append($"\nHAVING COUNT(*) {_having.Value.Op} {_having.Value.Number}");
                if (orderBy.Count > 0) sb.Append($"\nORDER BY {string.Join(", ", orderBy)}");
                if (_db != DatabaseType.MSSQL && limit != null) sb.Append($"\nLIMIT {limit}");

                return new SqlBuildResult
                {
                    Success = true,
                    Sql = sb.ToString(),
                    Operation = aggs.Count > 0 || _having != null ? "aggregate" : _insts.Count > 1 ? "join" : "select",
                    TablesUsed = _insts.Select(i => i.Table).Distinct().ToList(),
                    ColumnsUsed = colsUsed.Distinct().ToList(),
                    HasJoins = _insts.Count > 1,
                    HasAggregations = aggs.Count > 0 || _having != null
                };
            }

            private Col? HintedMeasure(Inst inst) =>
                (_measureHint.Length > 0
                    ? TblOf(inst).Cols.FirstOrDefault(c => c.Numeric && !c.Key && !c.Sensitive && c.Tokens.Any(t => _measureHint.Contains(t)))
                    : null) ?? DefaultMeasure(inst);

            private Col? DefaultMeasure(Inst inst) =>
                TblOf(inst).Cols.FirstOrDefault(c => c.Numeric && !c.Key && !c.Sensitive);

            private bool DirectionAfter(int index, out bool desc)
            {
                desc = false;
                for (int k = index; k < Math.Min(index + 3, _t.Count); k++)
                {
                    var w = Text(k);
                    if (w is "desc" or "descending") { desc = true; _used[k] = true; return true; }
                    if (w is "asc" or "ascending") { desc = false; _used[k] = true; return true; }
                    if (w == "high" && Text(k + 1) == "to") { desc = true; return true; }
                }
                return false;
            }

            // ----- conditions -----

            private void BuildConditions(List<string> conds)
            {
                // "<table> <number>" / "<table> <name>": bookings of user 2, users with role admin
                foreach (var m in _mentions.Where(m => m.Inst != null && m.Len > 0))
                {
                    int j = m.End;
                    if (j >= _t.Count || _used[j]) continue;
                    var inst = m.Inst!; var tb = TblOf(inst);
                    if (_t[j].Number && tb.Pk != null && tb.Pk.Numeric)
                    { conds.Add($"{Ref(inst, tb.Pk)} = {_t[j].Text}"); _used[j] = true; }
                    else if (_t[j].Word && !IsBlocked(j) && !LooksLikeVerb(_t[j].Text) && !_valueStems.Contains(_t[j].Stem) && _mentionAt[j] == null && _colAt[j] == null && tb.Label != null && tb.Label.Text)
                    { conds.Add($"{Ref(inst, tb.Label)} {LikeOp} {Lit("%" + _t[j].Raw + "%")}"); _used[j] = true; }
                    else if (_t[j].Quoted && tb.Label != null)
                    { conds.Add($"{Ref(inst, tb.Label)} {LikeOp} {Lit("%" + _t[j].Raw + "%")}"); _used[j] = true; }
                }

                // "from X to Y" for source/destination style columns
                foreach (var (kw, names) in new[] { ("from", new[] { "source", "origin", "from" }), ("to", new[] { "destination", "dest", "to" }) })
                    for (int i = 0; i + 1 < _t.Count; i++)
                    {
                        if (_used[i] || !IsWord(i) || Text(i) != kw) continue;
                        int j = i + 1;
                        if (_used[j] || !(_t[j].Quoted || _t[j].Word && !IsBlocked(j) && _mentionAt[j] == null && _colAt[j] == null)) continue;
                        var tb = TblOf(_primary);
                        var col = tb.Cols.FirstOrDefault(c => c.Text && c.Tokens.Length == 1 && names.Contains(c.Tokens[0]));
                        if (col == null) continue;
                        conds.Add($"LOWER({Ref(_primary, col)}) = {Lit(_t[j].Raw.ToLowerInvariant())}");
                        _used[i] = _used[j] = true;
                    }

                for (int i = 0; i < _t.Count; i++)
                {
                    var op = TryOperator(i);
                    if (op == null) continue;

                    // left side: the column (or table label) right before the operator
                    ColMention? lhs = _colMentions.Where(c => !c.Used && c.End <= op.Start && op.Start - c.End <= 1).OrderByDescending(c => c.End).FirstOrDefault();
                    Mention? lhsTable = lhs == null && op.Start > 0 ? _mentionAt[op.Start - 1] : null;
                    Inst? inst; Col? col;
                    if (lhs != null) { inst = lhs.Inst; col = lhs.Col; }
                    else if (lhsTable?.Inst != null) { inst = lhsTable.Inst; col = TblOf(inst).Label; }
                    else
                    {
                        // "users with more than 2 bookings"
                        int n0 = op.End;
                        if (!op.Like && !op.Between && n0 + 1 < _t.Count && _t[n0].Number && _mentionAt[n0 + 1]?.Inst is { } counted && !ReferenceEquals(counted, _primary))
                        {
                            _having = (op.Op, _t[n0].Text, counted);
                            MarkUsed(op.Start, n0 + 1);
                            i = n0;
                        }
                        continue;
                    }
                    if (col == null || inst == null) continue;

                    if (op.Between)
                    {
                        int a = op.End;
                        if (a + 2 < _t.Count && _t[a].Number && Text(a + 1) == "and" && _t[a + 2].Number)
                        {
                            conds.Add($"{Ref(inst, col)} BETWEEN {_t[a].Text} AND {_t[a + 2].Text}");
                            if (lhs != null) lhs.Used = true;
                            MarkUsed(op.Start, a + 3);
                            i = a + 2;
                        }
                        continue;
                    }

                    int v = op.End;
                    while (v < _t.Count && !_t[v].Quoted && !_t[v].Number && Text(v) is "the" or "a" or "an" or "to") v++;
                    if (v >= _t.Count) continue;
                    var val = _t[v];
                    bool valueOk = val.Number || val.Quoted || (val.Word && !IsBlocked(v) && _mentionAt[v] == null && _colAt[v] == null);
                    if (!valueOk) continue;

                    string? cond = null;
                    if (op.Like && (col.Text || val.Quoted))
                    {
                        var pat = op.LikeMode switch { "starts" => val.Raw + "%", "ends" => "%" + val.Raw, _ => "%" + val.Raw + "%" };
                        cond = $"{Ref(inst, col)} {LikeOp} {Lit(pat)}";
                    }
                    else if (!op.Like && col.Numeric && val.Number)
                        cond = $"{Ref(inst, col)} {op.Op} {val.Text}";
                    else if (!op.Like && col.Text && op.Op is "=" or "<>")
                        cond = $"LOWER({Ref(inst, col)}) {op.Op} {Lit(val.Raw.ToLowerInvariant())}";
                    else if (!op.Like && col.Date && val.Quoted)
                        cond = $"{Ref(inst, col)} {op.Op} {Lit(val.Raw)}";
                    if (cond == null) continue;

                    conds.Add(cond);
                    if (lhs != null) lhs.Used = true;
                    MarkUsed(op.Start, v + 1);
                    i = v;
                }

                MatchValues(conds);
                MatchYears(conds);

                // "status 1": a column directly followed by a number
                foreach (var c in _colMentions.Where(c => !c.Used).ToList())
                {
                    int j = c.End;
                    if (j < _t.Count && !_used[j] && _t[j].Number && c.Col.Numeric)
                    { conds.Add($"{Ref(c.Inst!, c.Col)} = {_t[j].Text}"); c.Used = true; _used[j] = true; }
                }
            }

            private static bool LooksLikeVerb(string w) =>
                (w.Length > 4 && w.EndsWith("ed")) || w is "made" or "sold" or "bought" or "paid" or "done" or "taken";

            private static bool Capital(string raw) => raw.Length > 0 && char.IsUpper(raw[0]);

            // ----- values: "customers from India", "cancelled orders", "employees in Engineering" -----

            private void MatchValues(List<string> conds)
            {
                for (int i = 0; i < _t.Count; i++)
                {
                    if (_used[i] || _mentionAt[i] != null || _colAt[i] != null) continue;
                    if (!_t[i].Word && !_t[i].Quoted) continue;
                    if (_t[i].Word && IsBlocked(i)) continue;

                    var candidates = new List<(Inst? Inst, Tbl Table, Col Col, string Value, int Len, int Dist)>();
                    foreach (var tbl in _tables.Values)
                        foreach (var col in tbl.Cols)
                            foreach (var (value, stems) in col.Values)
                            {
                                int len = ValueMatchLength(i, value, stems);
                                if (len == 0) continue;
                                if (len == 1 && stems.Length == 1 && (stems[0].Length < 3 || GenericWords.Contains(stems[0]))) continue;
                                int dist = _insts.Select(x => Distance(x.Table, tbl.Name)).Where(d => d >= 0).DefaultIfEmpty(-1).Min();
                                if (dist < 0) continue;
                                // a far table needs a capitalised or quoted word, so "total sales" does not filter on a Sales department
                                if (dist > 1 && !(dist <= 3 && (_t[i].Quoted || Capital(_t[i].Raw)))) continue;
                                candidates.Add((FindInst(tbl.Name, null), tbl, col, value, len, dist));
                            }
                    if (candidates.Count == 0) continue;

                    int bestLen = candidates.Max(c => c.Len);
                    var pick = candidates.Where(c => c.Len == bestLen).OrderBy(c => c.Dist).First();
                    var inst = pick.Inst ?? Ensure(pick.Table.Name, null, false);

                    bool negate = i > 0 && !_used[i - 1] && Text(i - 1) == "not";
                    if (negate) _used[i - 1] = true;
                    conds.Add($"LOWER({Ref(inst, pick.Col)}) {(negate ? "<>" : "=")} {Lit(pick.Value.ToLowerInvariant())}");
                    MarkUsed(i, i + bestLen);
                    i += bestLen - 1;
                }
            }

            private int ValueMatchLength(int i, string value, string[] stems)
            {
                if (_t[i].Quoted) return _t[i].Text == value.ToLowerInvariant() ? 1 : 0;
                if (i + stems.Length > _t.Count) return 0;
                for (int k = 0; k < stems.Length; k++)
                {
                    var t = _t[i + k];
                    if (t.Quoted || _used[i + k] || _mentionAt[i + k] != null || _colAt[i + k] != null) return 0;
                    if (t.Stem != stems[k]) return 0;
                }
                return stems.Length;
            }

            // ----- years: "orders in 2024", "hired after 2020", "in March 2024" -----

            private string DatePart(string part, string expr) => _db switch
            {
                DatabaseType.PostgreSQL => $"EXTRACT({part} FROM {expr})",
                DatabaseType.SQLite => $"CAST(strftime('{(part == "YEAR" ? "%Y" : "%m")}', {expr}) AS INTEGER)",
                _ => $"{part}({expr})"
            };

            private void MatchYears(List<string> conds)
            {
                for (int i = 0; i < _t.Count; i++)
                {
                    var t = _t[i];
                    if (!t.Number || _used[i] || t.Text.Length != 4 || t.Text.Contains('.')) continue;
                    int year = int.Parse(t.Text);
                    if (year < 1900 || year > 2100) continue;

                    // date column: one named in the question, else the first date column of the tables involved
                    var named = _colMentions.FirstOrDefault(c => !c.Used && c.Col.Date && (c.End == i || c.End == i - 1 || c.End == i - 2));
                    Inst? inst; Col? col;
                    if (named != null) { inst = named.Inst; col = named.Col; named.Used = true; }
                    else
                    {
                        inst = _insts.FirstOrDefault(x => TblOf(x).Cols.Any(c => c.Date));
                        col = inst == null ? null : TblOf(inst).Cols.First(c => c.Date);
                    }
                    if (inst == null || col == null) continue;

                    string op = "=";
                    int prev = i - 1;
                    int month = 0;
                    if (prev >= 0 && IsWord(prev) && !_used[prev] && Array.IndexOf(Months, Text(prev)) is var mi and >= 0) { month = mi + 1; _used[prev] = true; prev--; }
                    if (prev >= 0 && IsWord(prev) && !_used[prev])
                    {
                        switch (Text(prev))
                        {
                            case "after": op = ">"; _used[prev] = true; break;
                            case "before": op = "<"; _used[prev] = true; break;
                            case "since": op = ">="; _used[prev] = true; break;
                            case "in" or "during" or "of" or "from": _used[prev] = true; break;
                        }
                    }

                    if (month > 0) conds.Add($"{DatePart("MONTH", Ref(inst, col))} = {month}");
                    conds.Add($"{DatePart("YEAR", Ref(inst, col))} {op} {year}");
                    _used[i] = true;
                }
            }

            private void MarkUsed(int from, int to)
            {
                for (int k = from; k < to && k < _t.Count; k++) _used[k] = true;
            }

            private OpMatch? TryOperator(int i)
            {
                if (i >= _t.Count || _used[i] || !_t[i].Word) return null;

                int j = i;
                bool copula = Text(i) is "is" or "are" or "was" or "were" or "be";
                if (copula) j = i + 1;
                if (j >= _t.Count) return null;
                if (_used[j]) return null;

                if (Text(j) == "between") return new OpMatch { Start = i, End = j + 1, Between = true };

                foreach (var (words, op, like, mode) in OpPhrases)
                {
                    if (!MatchWords(j, words)) continue;
                    return new OpMatch { Start = i, End = j + words.Length, Op = op, Like = like, LikeMode = mode };
                }

                if (copula && Text(j) == "not" && j + 1 < _t.Count && !_t[j + 1].Quoted && (_t[j + 1].Number || _t[j + 1].Word && !IsBlocked(j + 1)))
                    return new OpMatch { Start = i, End = j + 1, Op = "<>" };

                // bare "is": only straight after a column and followed by a value
                if (copula && i > 0 && _colAt[i - 1] != null && j < _t.Count && (_t[j].Number || _t[j].Quoted || _t[j].Word && !IsBlocked(j) && _mentionAt[j] == null && _colAt[j] == null))
                    return new OpMatch { Start = i, End = j, Op = "=" };

                return null;
            }
        }
    }
}
