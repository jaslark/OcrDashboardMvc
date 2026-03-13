using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace OcrDashboardMvc.Database
{
    public interface ISqlApiProxyDatabase
    {
        Task<List<T>> FetchAsync<T>(string sql, params object[] args);
    }

    public class SqlApiProxyDatabase : ISqlApiProxyDatabase
    {
        private static readonly Regex PlaceholderRegex = new(@"@(\d+)", RegexOptions.Compiled);
        private static readonly JsonSerializerOptions JsonOptions;
        private readonly HttpClient _httpClient;
        private readonly ILogger<SqlApiProxyDatabase> _logger;

        static SqlApiProxyDatabase()
        {
            JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            JsonOptions.Converters.Add(new ExpandoObjectConverter());
        }

        public SqlApiProxyDatabase(HttpClient httpClient, ILogger<SqlApiProxyDatabase> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<T>> FetchAsync<T>(string sql, params object[] args)
        {
            var formattedSql = FormatSql(sql ?? string.Empty, args);
            var payload = new { sql = formattedSql };

            try
            {
                _logger.LogInformation("=== SQL QUERY ===");
                _logger.LogInformation("Formatted SQL: {FormattedSql}", formattedSql);

                var response = await _httpClient.PostAsJsonAsync("api/execute-sql", payload);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Response Status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SQL API failed: {StatusCode} - {Body}", response.StatusCode, content);
                    throw new HttpRequestException($"SQL API returned {response.StatusCode}: {content}");
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Empty response from SQL API");
                    return new List<T>();
                }

                // Try columns/rows format first
                var structuredRows = TryDeserializeColumnsRows<T>(content);
                if (structuredRows != null)
                {
                    return structuredRows;
                }

                // Fallback to direct deserialization
                var directResult = TryDeserializeDirect<T>(content);
                if (directResult != null)
                {
                    _logger.LogInformation("Deserialized {Count} rows from direct format", directResult.Count);
                    return directResult;
                }

                _logger.LogWarning("Could not deserialize response");
                return new List<T>();
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL API call failed for query: {Sql}", formattedSql);
                throw;
            }
        }

        private List<T>? TryDeserializeDirect<T>(string content)
        {
            try
            {
                return JsonSerializer.Deserialize<List<T>>(content, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogDebug("Direct deserialization failed: {Message}", ex.Message);
                return null;
            }
        }

        private List<T>? TryDeserializeColumnsRows<T>(string content)
        {
            try
            {
                var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (!root.TryGetProperty("columns", out var columnsElement) ||
                    !root.TryGetProperty("rows", out var rowsElement))
                {
                    return null;
                }

                var columns = columnsElement.Deserialize<string[]>(JsonOptions);
                if (columns == null || columns.Length == 0)
                    return null;

                // Parse rows as array of objects
                var rowsList = new List<Dictionary<string, object?>>();

                foreach (var rowElement in rowsElement.EnumerateArray())
                {
                    var rowDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                    if (rowElement.ValueKind == JsonValueKind.Object)
                    {
                        // Row is an object with property names
                        foreach (var prop in rowElement.EnumerateObject())
                        {
                            rowDict[prop.Name] = ConvertJsonElement(prop.Value);
                        }
                    }
                    else if (rowElement.ValueKind == JsonValueKind.Array)
                    {
                        // Row is an array of values
                        var values = rowElement.EnumerateArray().ToArray();
                        for (int i = 0; i < Math.Min(columns.Length, values.Length); i++)
                        {
                            rowDict[columns[i]] = ConvertJsonElement(values[i]);
                        }
                    }

                    rowsList.Add(rowDict);
                }

                if (rowsList.Count == 0)
                {
                    return new List<T>();
                }

                _logger.LogInformation("Parsed {Count} rows with {Columns} columns",
                    rowsList.Count, columns.Length);

                // ✅ Fixed: chỉ check typeof(object)
                if (typeof(T) == typeof(object))
                {
                    var expandoList = rowsList.Select(dict =>
                    {
                        var expando = new ExpandoObject();
                        var expandoDict = (IDictionary<string, object?>)expando;
                        foreach (var kvp in dict)
                        {
                            expandoDict[kvp.Key] = kvp.Value;
                        }
                        return (T)(object)expando;
                    }).ToList();
                    return expandoList;
                }

                // Convert to target type
                var serialized = JsonSerializer.Serialize(rowsList, JsonOptions);
                var result = JsonSerializer.Deserialize<List<T>>(serialized, JsonOptions);

                if (result != null && result.Count > 0)
                {
                    _logger.LogInformation("Successfully deserialized {Count} rows to {Type}",
                        result.Count, typeof(T).Name);
                    return result;
                }

                _logger.LogWarning("Deserialized 0 rows for type {Type}", typeof(T).Name);
                return new List<T>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Columns/Rows deserialization failed: {Message}", ex.Message);
                return null;
            }
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                JsonValueKind.String => ConvertStringValue(element.GetString()),
                JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => (double)decimalValue,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), JsonOptions),
                JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(element.GetRawText(), JsonOptions),
                _ => element.GetRawText()
            };
        }

        private static object? ConvertStringValue(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Try parse as number
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
            {
                if (Math.Abs(doubleValue % 1) < double.Epsilon && doubleValue >= int.MinValue && doubleValue <= int.MaxValue)
                    return (int)doubleValue;
                return doubleValue;
            }

            return value;
        }

        private sealed class ColumnsRowsResponse
        {
            [JsonPropertyName("columns")]
            public string[]? Columns { get; init; }

            [JsonPropertyName("rows")]
            public JsonElement[][]? Rows { get; init; }

            [JsonPropertyName("row_count")]
            public int RowCount { get; init; }
        }

        private sealed class ExpandoObjectConverter : JsonConverter<ExpandoObject>
        {
            public override ExpandoObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException("Expected JSON object.");
                }

                var expando = new ExpandoObject();
                var dict = (IDictionary<string, object?>)expando;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return expando;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException("Expected property name.");
                    }

                    var propertyName = reader.GetString() ?? string.Empty;
                    reader.Read();
                    dict[propertyName] = JsonSerializer.Deserialize(ref reader, typeof(object), options);
                }

                throw new JsonException("Incomplete JSON object.");
            }

            public override void Write(Utf8JsonWriter writer, ExpandoObject value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, (IDictionary<string, object?>)value, options);
            }
        }

        private static string FormatSql(string sql, object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return sql;
            }

            return PlaceholderRegex.Replace(sql, match =>
            {
                var indexText = match.Groups[1].Value;
                if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    throw new InvalidOperationException($"Invalid SQL placeholder '{match.Value}'.");
                }

                if (index < 0 || index >= args.Length)
                {
                    throw new InvalidOperationException($"Missing argument for SQL placeholder '{match.Value}'.");
                }

                return FormatArgument(args[index]);
            });
        }

        private static string FormatArgument(object? value)
        {
            return value switch
            {
                null => "NULL",
                DBNull => "NULL",
                string text => $"'{EscapeSqlString(text)}'",
                DateTime dt => $"'{dt:yyyy-MM-dd}'",
                DateTimeOffset dto => $"'{dto:yyyy-MM-dd}'",
                bool flag => flag ? "TRUE" : "FALSE",
                char ch => $"'{EscapeSqlString(ch.ToString())}'",
                Guid guid => $"'{guid:D}'",
                Enum enumValue => Convert.ToInt64(enumValue).ToString(CultureInfo.InvariantCulture),
                int intVal => intVal.ToString(CultureInfo.InvariantCulture),
                long longVal => longVal.ToString(CultureInfo.InvariantCulture),
                double doubleVal => doubleVal.ToString(CultureInfo.InvariantCulture),
                decimal decimalVal => decimalVal.ToString(CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL"
            };
        }

        private static string EscapeSqlString(string value)
        {
            return value.Replace("'", "''");
        }
    }
}