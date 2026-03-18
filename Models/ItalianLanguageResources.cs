// ✅ Fixed class definition
namespace NlpToSqlApi.Models
{
    public class ItalianLanguageResources
    {
        public Dictionary<string, string[]> OperationKeywords { get; } = new()
        {
            ["select"] = new[] { "mostra", "visualizza", "elenca", "trova", "dammi", "vedere", "vista" },
            ["count"] = new[] { "conta", "numero di", "quanti", "totale" },
            ["max"] = new[] { "massimo", "max", "più alto", "maggiore" },
            ["min"] = new[] { "minimo", "min", "più basso", "minore" },
            ["avg"] = new[] { "media", "avg" },
            ["sum"] = new[] { "somma", "totale" }
        };

        public Dictionary<string, string[]> AggregationKeywords { get; } = new()
        {
            ["count"] = new[] { "conta", "numero di", "quanti", "totale" },
            ["max"] = new[] { "massimo", "più alto", "maggiore", "più grande" },
            ["min"] = new[] { "minimo", "più basso", "minore", "più piccolo" },
            ["avg"] = new[] { "media", "medio", "valore medio" },
            ["sum"] = new[] { "somma", "totale", "aggiungi" }
        };


        public Dictionary<string, string[]> ComparisonKeywords { get; } = new()
        {
            [">"] = new[] { "più di", "maggiore di", "superiore a", "oltre" },
            ["<"] = new[] { "meno di", "inferiore a", "sotto" },
            [">="] = new[] { "almeno", "minimo di", "o più", "o superiore" },
            ["<="] = new[] { "al massimo", "massimo di", "o meno" },
            ["="] = new[] { "uguale a", "pari a", "è" },
            ["!="] = new[] { "diverso da", "non", "non uguale a" }
        };

        public string[] JoinKeywords { get; } = new[] { "con", "e", "incluso", "insieme a" };

        public Dictionary<string, string> SchemaTerms { get; } = new()
        {
            ["employee"] = "dipendente",
            ["department"] = "dipartimento",
            ["salary"] = "stipendio",
            ["customer"] = "cliente",
            ["order"] = "ordine",
            ["product"] = "prodotto",
            ["price"] = "prezzo",
            ["date"] = "data",
            ["name"] = "nome",
            ["id"] = "id"
        };
    }
}

public interface ILanguageResources
    {
        Dictionary<string, string[]> OperationKeywords { get; }
        Dictionary<string, string[]> ComparisonKeywords { get; }
        string[] JoinKeywords { get; }
        Dictionary<string, string> SchemaTerms { get; }
    }
