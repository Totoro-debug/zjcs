using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Zjcs.Offline.Equipment
{
    public sealed class OfficialEquipmentJson
    {
        private readonly string text;
        private int position;
        private bool allowNonFinite;

        private OfficialEquipmentJson(string text) { this.text = text; }

        public static object Parse(string text, bool allowNonFinite = false)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var parser = new OfficialEquipmentJson(text);
            parser.allowNonFinite = allowNonFinite;
            object value = parser.Value();
            parser.Space();
            if (parser.position != text.Length) throw parser.Error("Trailing JSON content");
            return value;
        }

        private object Value()
        {
            Space();
            if (position >= text.Length) throw Error("Unexpected end of JSON");
            // Unity animation curves serialize stepped tangents as Infinity.
            if (allowNonFinite)
            {
                if (text[position] == 'I') { Literal("Infinity"); return double.PositiveInfinity; }
                if (text[position] == 'N') { Literal("NaN"); return double.NaN; }
                if (text[position] == '-' && position + 1 < text.Length && text[position + 1] == 'I')
                { Literal("-Infinity"); return double.NegativeInfinity; }
            }
            switch (text[position])
            {
                case '{': return ObjectValue();
                case '[': return ArrayValue();
                case '"': return StringValue();
                case 't': Literal("true"); return true;
                case 'f': Literal("false"); return false;
                case 'n': Literal("null"); return null;
                default: return NumberValue();
            }
        }

        private Dictionary<string, object> ObjectValue()
        {
            position++;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            Space();
            if (Take('}')) return result;
            while (true)
            {
                Space();
                if (position >= text.Length || text[position] != '"') throw Error("Expected object key");
                string key = StringValue();
                Space(); Expect(':');
                if (result.ContainsKey(key)) throw Error("Duplicate object key " + key);
                result.Add(key, Value());
                Space();
                if (Take('}')) return result;
                Expect(',');
            }
        }

        private List<object> ArrayValue()
        {
            position++;
            var result = new List<object>();
            Space();
            if (Take(']')) return result;
            while (true)
            {
                result.Add(Value());
                Space();
                if (Take(']')) return result;
                Expect(',');
            }
        }

        private string StringValue()
        {
            Expect('"');
            var result = new StringBuilder();
            while (position < text.Length)
            {
                char value = text[position++];
                if (value == '"') return result.ToString();
                if (value < 0x20) throw Error("Control character in string");
                if (value != '\\') { result.Append(value); continue; }
                if (position >= text.Length) throw Error("Incomplete string escape");
                char escaped = text[position++];
                switch (escaped)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length) throw Error("Incomplete Unicode escape");
                        result.Append((char)int.Parse(text.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        position += 4;
                        break;
                    default: throw Error("Invalid string escape");
                }
            }
            throw Error("Unterminated string");
        }

        private object NumberValue()
        {
            int start = position;
            Take('-');
            while (position < text.Length && char.IsDigit(text[position])) position++;
            bool floating = false;
            if (Take('.'))
            {
                floating = true;
                while (position < text.Length && char.IsDigit(text[position])) position++;
            }
            if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
            {
                floating = true;
                position++;
                if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                while (position < text.Length && char.IsDigit(text[position])) position++;
            }
            if (start == position) throw Error("Invalid JSON value");
            string number = text.Substring(start, position - start);
            if (floating) return double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
            return long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? (object)value : double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private void Literal(string value)
        {
            if (position + value.Length > text.Length
                || string.CompareOrdinal(text, position, value, 0, value.Length) != 0)
                throw Error("Invalid JSON literal");
            position += value.Length;
        }

        private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private bool Take(char value) { if (position < text.Length && text[position] == value) { position++; return true; } return false; }
        private void Expect(char value) { if (!Take(value)) throw Error("Expected '" + value + "'"); }
        private FormatException Error(string message) { return new FormatException(message + " at offset " + position); }
    }
}
