using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

class CJsonHelper
{
    const string ms_blank = " \t\r\n";
    const string ms_number = "0123456789+-.eE";

    public static object Load(string json)
    {
        JsonParser p = new JsonParser(json);
        return p.Read();
    }

    static void Assert(bool con, string msg = "empty")
    {
        if (!con)
            throw new Exception("assert failed:" + msg);
    }

    class JsonParser
    {
        string m_json;
        int m_offset;

        public JsonParser(string json, int offset = 0)
        {
            m_json = json;
            m_offset = offset;
        }

        public object Read()
        {
            char c = FirstChar();

            switch (c)
            {
                case '\'':
                case '"':
                    return ReadString();
                case '[':
                    return ReadArray();
                case '{':
                    return ReadDict();
            }

            if (MatchString("true"))
                return true;
            if (MatchString("false"))
                return false;
            if (MatchString("null"))
                return null;

            if (ms_number.IndexOf(c) >= 0)
                return ReadNumber();

            Assert(false, "error char");
            return null;
        }

        object ReadNumber()
        {
            int i = m_offset;
            while (i < m_json.Length && ms_number.IndexOf(m_json[i]) >= 0)
                i++;
            string num = m_json.Substring(m_offset, i - m_offset);
            m_offset = i;
            if (num.IndexOfAny(new char[] { '.', 'e', 'E' }) >= 0)
                return double.Parse(num);
            if (num.Length >= 10)
            {
                long v = long.Parse(num);
                if (v >= int.MinValue && v <= int.MaxValue)
                    return (int)v;
                return v;
            }
            return int.Parse(num);
        }

        string ReadString()
        {
            char endC = m_json[m_offset];
            int i = m_offset + 1;
            while (true)
            {
                char c = m_json[i];
                if (c == endC)
                    break;
                if (c == '\\')
                    i++;
                i++;
            }
            string s = m_json.Substring(m_offset + 1, i - m_offset - 1);
            m_offset = i + 1;
            return System.Text.RegularExpressions.Regex.Unescape(s);
        }

        object[] ReadArray()
        {
            Assert(FirstChar() == '[');
            m_offset++;
            if (FirstChar() == ']')
            {
                m_offset++;
                return new object[0];
            }
            List<object> list = new List<object>();
            while (true)
            {
                list.Add(Read());

                char c = FirstChar();
                if (c == ',')
                {
                    m_offset++;
                    continue;
                }
                else if (c == ']')
                {
                    m_offset++;
                    return list.ToArray();
                }
                else
                {
                    Assert(false);
                    return null;
                }
            }
        }

        Dictionary<string, object> ReadDict()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            Assert(FirstChar() == '{');
            m_offset++;
            while (true)
            {
                if (FirstChar() == '}')
                {
                    m_offset++;
                    return dict;
                }

                object key = Read();
                Assert(FirstChar() == ':');
                m_offset++;
                object value = Read();
                dict.Add(key.ToString(), value);

                char c = FirstChar();
                if (c == '}')
                    continue;
                if (c == ',')
                {
                    m_offset++;
                    continue;
                }
                Assert(false);
                return null;
            }
        }

        char FirstChar()
        {
            Assert(m_json.Length > m_offset);

            char c = m_json[m_offset];

            // ignore blank
            while (ms_blank.IndexOf(c) >= 0)
                c = m_json[++m_offset];

            return c;
        }

        bool MatchString(string str)
        {
            str = str.ToLower();
            for (int i = 0; i < str.Length; i++)
            {
                char c = m_json[m_offset + i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)('a' - 'A' + c);
                if (c != str[i])
                    return false;
            }
            if (m_json.Length > m_offset + str.Length)
            {
                // 检测单词结束
                char c = m_json[m_offset + str.Length];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                    return false;
                if ("01234567890_".IndexOf(c) > 0)
                    return false;
            }
            m_offset += str.Length;
            return true;
        }
    }

    public static string Save(object obj)
    {
        if (obj == null)
            return "null";

        if (obj is byte || obj is int || obj is long || obj is uint || obj is float || obj is double) // my be more
            return obj.ToString();

        if (obj is bool)
            return (bool)obj ? "true" : "false";

        if (obj is string)
            return string.Format("\"{0}\"", obj.ToString().Replace(@"\", @"\\").Replace(@"""", @"\"""));

        if (obj is IDictionary)
        {
            List<string> list = new List<string>();
            foreach (DictionaryEntry de in obj as IDictionary)
                list.Add(Save(de.Key) + ":" + Save(de.Value));
            return string.Format("{{{0}}}", string.Join(",", list.ToArray()));
        }

        if (obj is IEnumerable)
        {
            List<string> list = new List<string>();
            foreach (object o in obj as IEnumerable)
                list.Add(Save(o));
            return string.Format("[{0}]", string.Join(",", list.ToArray()));
        }

        Assert(false, "unsupported data type");
        return "null";
    }

}
