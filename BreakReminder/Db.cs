using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace BreakReminder
{
    public static class Db
    {
        private const string _dbFileName = "BreakReminder.txt";

        public static T GetValue<T>(string key, T defaultValue = default)
        {
            var db = GetDb();

            if (db == null || !db.ContainsKey(key))
                return defaultValue;

            var value = db[key];

            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return (T)GetConverter<T>().ConvertFrom(value);
        }

        public static void SetValue<T>(string key, T value)
        {
            var db = GetDb();

            if ((db == null || !db.ContainsKey(key)) && Equals(value, default(T)))
                return;

            if (db == null)
                db = new Dictionary<string, string>();

            db[key] = GetConverter<T>().ConvertToString(value);

            File.WriteAllText(_dbFileName, JsonConvert.SerializeObject(db, Formatting.Indented));
            Thread.Sleep(500);
        }

        private static IDictionary<string, string> GetDb()
        {
            return !File.Exists(_dbFileName) ? null : JsonConvert.DeserializeObject<IDictionary<string, string>>(File.ReadAllText(_dbFileName));
        }

        private static TypeConverter GetConverter<T>()
        {
            var type = typeof(T);

            if (type.IsGenericType)
                type = Nullable.GetUnderlyingType(type) ?? type;

            return TypeDescriptor.GetConverter(type);
        }
    }
}
