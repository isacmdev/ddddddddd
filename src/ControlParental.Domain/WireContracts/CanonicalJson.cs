namespace ControlParental.Domain.WireContracts;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Portable canonical JSON authority used by policy hashes.</summary>
public static class CanonicalJson
{
    public static byte[] Utf8(JsonElement value)
    {
        var output = new StringBuilder();
        try { AppendValue(output, value); }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException or ArgumentOutOfRangeException)
        { throw new JsonException("Invalid canonical JSON value.", ex); }
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    /// <summary>Canonicalizes a complete UTF-8 JSON value with strict decoding.</summary>
    public static byte[] Utf8(ReadOnlySpan<byte> raw)
    {
        try
        {
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(raw);
            if (!HasValidUtf16Escapes(raw)) throw new JsonException("Unpaired UTF-16 surrogate is not valid canonical JSON.");
            using var document = JsonDocument.Parse(text);
            return Utf8(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new JsonException("Invalid UTF-8 JSON input.", ex);
        }
    }

    internal static bool HasValidUtf16Escapes(ReadOnlySpan<byte> raw)
    {
        var inString = false;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == (byte)'"') { inString = !inString; continue; }
            if (!inString || raw[i] != (byte)'\\') continue;
            if (++i >= raw.Length) return true;
            if (raw[i] != (byte)'u') continue;
            if (i + 4 >= raw.Length || !TryHex(raw.Slice(i + 1, 4), out var codeUnit)) return true;
            if (codeUnit is >= 0xDC00 and <= 0xDFFF) return false;
            if (codeUnit is >= 0xD800 and <= 0xDBFF)
            {
                if (i + 6 >= raw.Length || raw[i + 5] != (byte)'\\' || raw[i + 6] != (byte)'u'
                    || i + 10 >= raw.Length || !TryHex(raw.Slice(i + 7, 4), out var low)
                    || low is < 0xDC00 or > 0xDFFF) return false;
                i += 10;
            }
            else i += 4;
        }
        return true;
    }

    private static bool TryHex(ReadOnlySpan<byte> value, out int result)
    {
        result = 0;
        foreach (var digit in value)
        {
            var nibble = digit switch { >= (byte)'0' and <= (byte)'9' => digit - (byte)'0', >= (byte)'a' and <= (byte)'f' => digit - (byte)'a' + 10, >= (byte)'A' and <= (byte)'F' => digit - (byte)'A' + 10, _ => -1 };
            if (nibble < 0) return false;
            result = (result << 4) | nibble;
        }
        return true;
    }

    public static string Serialize(JsonElement value) => Encoding.UTF8.GetString(Utf8(value));
    public static string Sha256Hex(JsonElement value) => Convert.ToHexString(SHA256.HashData(Utf8(value))).ToLowerInvariant();

    /// <summary>Canonicalizes an object while omitting one member without a serialize/parse round trip.</summary>
    public static byte[] Utf8ObjectWithoutProperty(JsonElement value, string omittedProperty)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException("Expected an object.");
        var output = new StringBuilder();
        output.Append('{');
        var first = true;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON object member.");
            if (property.NameEquals(omittedProperty)) continue;
            if (!first) output.Append(',');
            first = false;
            AppendString(output, property.Name);
            output.Append(':');
            AppendValue(output, property.Value);
        }
        output.Append('}');
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    private static string NormalizeNumber(string raw)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(raw, @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$"))
            throw new JsonException("Invalid JSON number.");
        var negative = raw.StartsWith('-');
        var text = negative ? raw[1..] : raw;
        long exponent = 0;
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        if (exponentIndex >= 0)
        {
            if (!long.TryParse(text[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)
                || exponent is < -1_000_000 or > 1_000_000)
                throw new JsonException("Number exponent is outside the supported canonical range.");
            text = text[..exponentIndex];
        }

        var dot = text.IndexOf('.');
        var digits = dot >= 0 ? text.Remove(dot, 1) : text;
        var leadingZeroes = digits.Length - digits.TrimStart('0').Length;
        digits = digits.TrimStart('0');
        if (digits.Length == 0) return "0";
        var decimalPosition = (long)(dot >= 0 ? dot : text.Length) + exponent - leadingZeroes;
        var result = decimalPosition <= 0
            ? "0." + new string('0', checked((int)-decimalPosition)) + digits
            : decimalPosition >= digits.Length
                ? digits + new string('0', checked((int)(decimalPosition - digits.Length)))
                : digits[..checked((int)decimalPosition)] + "." + digits[checked((int)decimalPosition)..];
        if (result.Contains('.')) result = result.TrimEnd('0').TrimEnd('.');
        return negative ? "-" + result : result;
    }

    private static void AppendString(StringBuilder output, string value)
    {
        output.Append('"');
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            switch (character)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\f': output.Append("\\f"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    if (character < 0x20)
                        output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else if (char.IsSurrogate(character))
                    {
                        if (!char.IsHighSurrogate(character) || index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                            throw new JsonException("Unpaired UTF-16 surrogate is not valid canonical JSON.");
                        output.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                        output.Append("\\u").Append(((int)value[++index]).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                        output.Append(character);
                    break;
            }
        }
        output.Append('"');
    }

    private static void AppendValue(StringBuilder output, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{');
                var firstProperty = true;
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!names.Add(property.Name))
                        throw new JsonException("Duplicate JSON object member.");
                    if (!firstProperty) output.Append(',');
                    firstProperty = false;
                    AppendString(output, property.Name);
                    output.Append(':');
                    AppendValue(output, property.Value);
                }
                output.Append('}');
                break;
            case JsonValueKind.Array:
                output.Append('[');
                var firstItem = true;
                foreach (var item in value.EnumerateArray())
                {
                    if (!firstItem) output.Append(',');
                    firstItem = false;
                    AppendValue(output, item);
                }
                output.Append(']');
                break;
            case JsonValueKind.String:
                try { AppendString(output, value.GetString() ?? string.Empty); }
                catch (InvalidOperationException ex) { throw new JsonException("Invalid UTF-16 JSON string.", ex); }
                break;
            case JsonValueKind.Number: output.Append(NormalizeNumber(value.GetRawText())); break;
            case JsonValueKind.True: output.Append("true"); break;
            case JsonValueKind.False: output.Append("false"); break;
            case JsonValueKind.Null: output.Append("null"); break;
            default: throw new JsonException("Undefined JSON value");
        }
    }
}
