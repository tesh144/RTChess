/*
 * MiniJSON by Darrell Bethea — https://gist.github.com/darrenclark/6111279
 * MIT Licence
 *
 * Placed in LittleCafe.Editor namespace so it doesn't clash with any other
 * MiniJSON copy that may be present elsewhere in the project.
 *
 * Usage:
 *   var obj = MiniJson.Deserialize(jsonString) as Dictionary<string,object>;
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LittleCafe.Editor
{
    internal static class MiniJson
    {
        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Parses a JSON string and returns an object graph.</summary>
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        /// <summary>Serializes an object graph to a JSON string.</summary>
        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }

        // ─────────────────────────────────────────────────────────────────
        // Parser
        // ─────────────────────────────────────────────────────────────────

        private sealed class Parser : IDisposable
        {
            const string WORD_BREAK = "{}[],:\"";

            public static bool IsWordBreak(char c) => char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;

            enum Token { None, CurlyOpen, CurlyClose, SquaredOpen, SquaredClose, Colon, Comma, String, Number, True, False, Null }

            StringReader json;

            Parser(string jsonString) { json = new StringReader(jsonString); }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                    return instance.ParseValue();
            }

            public void Dispose() { json.Dispose(); }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                json.Read(); // {
                while (true)
                {
                    switch (NextToken)
                    {
                        case Token.None:    return null;
                        case Token.CurlyClose: return table;
                        default:
                            string name = ParseString();
                            if (name == null) return null;
                            if (NextToken != Token.Colon) return null;
                            json.Read();
                            table[name] = ParseValue();
                            break;
                    }
                }
            }

            List<object> ParseArray()
            {
                var array = new List<object>();
                json.Read(); // [
                bool parsing = true;
                while (parsing)
                {
                    var nextToken = NextToken;
                    switch (nextToken)
                    {
                        case Token.None:         parsing = false; break;
                        case Token.SquaredClose: parsing = false; break;
                        case Token.Comma:        break;
                        default:
                            array.Add(ParseByToken(nextToken));
                            break;
                    }
                }
                return array;
            }

            object ParseValue()
            {
                var nextToken = NextToken;
                return ParseByToken(nextToken);
            }

            object ParseByToken(Token token)
            {
                switch (token)
                {
                    case Token.String:      return ParseString();
                    case Token.Number:      return ParseNumber();
                    case Token.CurlyOpen:   return ParseObject();
                    case Token.SquaredOpen: return ParseArray();
                    case Token.True:        return true;
                    case Token.False:       return false;
                    case Token.Null:        return null;
                    default:                return null;
                }
            }

            string ParseString()
            {
                var s = new StringBuilder();
                json.Read(); // "
                bool parsing = true;
                while (parsing)
                {
                    if (json.Peek() == -1) { parsing = false; break; }
                    char c = NextChar;
                    switch (c)
                    {
                        case '"':  parsing = false; break;
                        case '\\':
                            if (json.Peek() == -1) { parsing = false; break; }
                            char escaped = NextChar;
                            switch (escaped)
                            {
                                case '"':  s.Append('"');  break;
                                case '\\': s.Append('\\'); break;
                                case '/':  s.Append('/');  break;
                                case 'b':  s.Append('\b'); break;
                                case 'f':  s.Append('\f'); break;
                                case 'n':  s.Append('\n'); break;
                                case 'r':  s.Append('\r'); break;
                                case 't':  s.Append('\t'); break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default:
                            s.Append(c);
                            break;
                    }
                }
                return s.ToString();
            }

            object ParseNumber()
            {
                string number = NextWord;
                if (number.IndexOf('.') == -1)
                {
                    long parsedLong;
                    if (long.TryParse(number, out parsedLong)) return parsedLong;
                }
                double parsedDouble;
                if (double.TryParse(number, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsedDouble))
                    return parsedDouble;
                return 0;
            }

            void EatWhitespace() { while (char.IsWhiteSpace(PeekChar)) json.Read(); }

            char PeekChar => Convert.ToChar(json.Peek());
            char NextChar => Convert.ToChar(json.Read());

            string NextWord
            {
                get
                {
                    var word = new StringBuilder();
                    while (!IsWordBreak(PeekChar))
                    {
                        word.Append(NextChar);
                        if (json.Peek() == -1) break;
                    }
                    return word.ToString();
                }
            }

            Token NextToken
            {
                get
                {
                    EatWhitespace();
                    if (json.Peek() == -1) return Token.None;
                    switch (PeekChar)
                    {
                        case '{': return Token.CurlyOpen;
                        case '}': json.Read(); return Token.CurlyClose;
                        case '[': return Token.SquaredOpen;
                        case ']': json.Read(); return Token.SquaredClose;
                        case ',': json.Read(); return Token.Comma;
                        case '"': return Token.String;
                        case ':': return Token.Colon;
                        case '0': case '1': case '2': case '3': case '4':
                        case '5': case '6': case '7': case '8': case '9':
                        case '-': return Token.Number;
                        default:
                            string word = NextWord;
                            switch (word)
                            {
                                case "false": return Token.False;
                                case "true":  return Token.True;
                                case "null":  return Token.Null;
                            }
                            return Token.None;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Serializer
        // ─────────────────────────────────────────────────────────────────

        private sealed class Serializer
        {
            StringBuilder builder;

            Serializer() { builder = new StringBuilder(); }

            public static string Serialize(object obj)
            {
                var instance = new Serializer();
                instance.SerializeValue(obj);
                return instance.builder.ToString();
            }

            void SerializeValue(object value)
            {
                if (value == null) { builder.Append("null"); return; }
                if (value is string s) { SerializeString(s); return; }
                if (value is bool b) { builder.Append(b ? "true" : "false"); return; }
                if (value is IList list) { SerializeArray(list); return; }
                if (value is IDictionary dict) { SerializeObject(dict); return; }
                if (value is char c) { SerializeString(new string(c, 1)); return; }
                SerializeOther(value);
            }

            void SerializeObject(IDictionary obj)
            {
                bool first = true;
                builder.Append('{');
                foreach (object e in obj.Keys)
                {
                    if (!first) builder.Append(',');
                    SerializeString(e.ToString());
                    builder.Append(':');
                    SerializeValue(obj[e]);
                    first = false;
                }
                builder.Append('}');
            }

            void SerializeArray(IList arr)
            {
                builder.Append('[');
                bool first = true;
                foreach (object obj in arr)
                {
                    if (!first) builder.Append(',');
                    SerializeValue(obj);
                    first = false;
                }
                builder.Append(']');
            }

            void SerializeString(string str)
            {
                builder.Append('"');
                foreach (char c in str)
                {
                    switch (c)
                    {
                        case '"':  builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b");  break;
                        case '\f': builder.Append("\\f");  break;
                        case '\n': builder.Append("\\n");  break;
                        case '\r': builder.Append("\\r");  break;
                        case '\t': builder.Append("\\t");  break;
                        default:
                            int codepoint = Convert.ToInt32(c);
                            if (codepoint >= 32 && codepoint <= 126)
                                builder.Append(c);
                            else
                                builder.AppendFormat("\\u{0:x4}", codepoint);
                            break;
                    }
                }
                builder.Append('"');
            }

            void SerializeOther(object value)
            {
                if (value is float  f) { builder.Append(f.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); return; }
                if (value is double d) { builder.Append(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); return; }
                if (value is decimal dec) { builder.Append(dec.ToString(System.Globalization.CultureInfo.InvariantCulture)); return; }
                builder.Append(value);
            }
        }
    }
}
