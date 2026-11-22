using System;
using System.Collections.Generic;
using Hollowcrawl.Items;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Hollowcrawl.Content
{
    /// <summary>Shared JSON settings for content and saves: camelCase, string enums, ParamBag/EffectSpec converters.</summary>
    public static class JsonContent
    {
        public static JsonSerializerSettings CreateSettings()
        {
            var s = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false } },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Formatting = Formatting.None,
            };
            s.Converters.Add(new StringEnumConverter());
            s.Converters.Add(new EffectSpecConverter());
            s.Converters.Add(new ParamBagConverter());
            return s;
        }

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, CreateSettings());
        public static string Serialize(object o, bool indented = true)
        {
            var s = CreateSettings();
            s.Formatting = indented ? Formatting.Indented : Formatting.None;
            return JsonConvert.SerializeObject(o, s);
        }

        /// <summary>Converts a JToken to plain CLR values (long, double, bool, string, string[], object[]).</summary>
        public static object ToPlain(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Integer: return token.Value<long>();
                case JTokenType.Float: return token.Value<double>();
                case JTokenType.Boolean: return token.Value<bool>();
                case JTokenType.String: return token.Value<string>();
                case JTokenType.Null: return null;
                case JTokenType.Array:
                    var arr = (JArray)token;
                    bool allStrings = true;
                    foreach (var t in arr) if (t.Type != JTokenType.String) { allStrings = false; break; }
                    if (allStrings) { var sa = new string[arr.Count]; for (int i = 0; i < arr.Count; i++) sa[i] = (string)arr[i]; return sa; }
                    var oa = new object[arr.Count]; for (int i = 0; i < arr.Count; i++) oa[i] = ToPlain(arr[i]); return oa;
                default: return token.ToString(Formatting.None);
            }
        }
    }

    /// <summary>
    /// Effect entries are written flat for readability:
    ///   { "effect": "proj_element", "type": "Fire", "status": "burn", "chance": 0.5, "condition": "health_full" }
    /// "effect" selects the handler; "condition" and "id" are reserved; every other key becomes a parameter.
    /// </summary>
    public sealed class EffectSpecConverter : JsonConverter<EffectSpec>
    {
        public override EffectSpec ReadJson(JsonReader reader, Type objectType, EffectSpec existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var obj = JObject.Load(reader);
            var spec = new EffectSpec();
            foreach (var prop in obj.Properties())
            {
                switch (prop.Name)
                {
                    case "effect": spec.Type = (string)prop.Value; break;
                    case "condition": spec.Condition = (string)prop.Value; break;
                    case "id": spec.Id = (string)prop.Value; break;
                    default: spec.Params.Set(prop.Name, JsonContent.ToPlain(prop.Value)); break;
                }
            }
            return spec;
        }

        public override void WriteJson(JsonWriter writer, EffectSpec value, JsonSerializer serializer)
        {
            var obj = new JObject { ["effect"] = value.Type };
            if (value.Id != null) obj["id"] = value.Id;
            if (value.Condition != null) obj["condition"] = value.Condition;
            foreach (var k in value.Params.Keys) obj[k] = JToken.FromObject(value.Params.GetRaw(k) ?? JValue.CreateNull());
            obj.WriteTo(writer);
        }
    }

    public sealed class ParamBagConverter : JsonConverter<ParamBag>
    {
        public override ParamBag ReadJson(JsonReader reader, Type objectType, ParamBag existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return new ParamBag();
            var obj = JObject.Load(reader);
            var bag = new ParamBag();
            foreach (var p in obj.Properties()) bag.Set(p.Name, JsonContent.ToPlain(p.Value));
            return bag;
        }

        public override void WriteJson(JsonWriter writer, ParamBag value, JsonSerializer serializer)
        {
            var obj = new JObject();
            foreach (var k in value.Keys) obj[k] = JToken.FromObject(value.GetRaw(k) ?? JValue.CreateNull());
            obj.WriteTo(writer);
        }
    }
}
