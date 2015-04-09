using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avro.File;
using Latino.Workflows.Persistance.Avro.Utils;

namespace Latino.Workflows.Persistance.Avro
{
    public interface IValueLocationStrategy
    {
        string GetLocation(DateTime time, string key);
        bool TryGetKey(string location, out DateTime time, out string key);
        string NormalizeLocation(string location);
    }

    public class FilePerDayLocationStrategy : IValueLocationStrategy
    {
        public FilePerDayLocationStrategy(string rootDirectory)
        {
            RootDirectory = Preconditions.CheckNotNullArgument(rootDirectory);
        }

        public string RootDirectory { get; private set; }
        
        public string GetLocation(DateTime time, string key)
        {
            string fileName = string.Format("{0}.avro", time.Day.ToString("00"));
            string location = Path.Combine(RootDirectory, time.Year.ToString("0000"), time.Month.ToString("00"), fileName);
            return NormalizeLocation(location);
        }

        public bool TryGetKey(string location, out DateTime time, out string key)
        {
            if (!string.IsNullOrEmpty(location))
            {
                location = NormalizeLocation(location);
                if (location.StartsWith(RootDirectory) && location.ToLower().EndsWith(".avro"))
                {
                    string format = Path.Combine("yyyy", "MM", "dd");
                    var trimmed = location.Substring(RootDirectory.Length, location.Length - ".avro".Length - RootDirectory.Length);
                    if (trimmed.StartsWith(Path.PathSeparator.ToString())) { trimmed = trimmed.Substring(1); }
                    try
                    {
                        time = DateTime.ParseExact(trimmed, format, CultureInfo.InvariantCulture);
                        key = "";
                        return true;
                    }
                    catch (FormatException) { }
                }
            }
            time = DateTime.MinValue;
            key = null;
            return false;
        }

        public string NormalizeLocation(string location)
        {
            return new Uri(location).LocalPath.ToLower();
        }
    }

    public class AvroRepository<V>
    {
        private readonly Dictionary<string, AvroRandomAccessIndex<V>> mLocIndexes = new Dictionary<string, AvroRandomAccessIndex<V>>();
        private readonly Dictionary<Guid, string> mMasterIndex = new Dictionary<Guid, string>();

        public AvroRepository(IAvroFileValueDef<V> valueDef, string rootDirectory, Codec.Type codec)
            : this(valueDef, rootDirectory, new FilePerDayLocationStrategy(rootDirectory), codec)
        {
            Preconditions.CheckArgument(Directory.Exists(rootDirectory));
        }

        public AvroRepository(IAvroFileValueDef<V> valueDef, string rootDirectory, IValueLocationStrategy valueLocationStrategy, Codec.Type codec)
        {
            ValueDef = Preconditions.CheckNotNullArgument(valueDef);
            RootDirectory = Preconditions.CheckNotNullArgument(rootDirectory);
            ValueLocationStrategy = Preconditions.CheckNotNullArgument(valueLocationStrategy);
            Codec = codec;
        }

        public IAvroFileValueDef<V> ValueDef { get; private set; }
        public string RootDirectory { get; private set; }
        public IValueLocationStrategy ValueLocationStrategy { get; private set; }
        public Codec.Type Codec { get; private set; }

        public bool TryGetDocument(DateTime time, string key, out V value)
        {
            AvroRandomAccessReader<V> reader = GetReader(ValueLocationStrategy.GetLocation(time, key));
            if (reader != null)
            {
                value = reader.Get(key);
                return true;
            }
            value = default(V);
            return false;
        }

        public V GetValue(DateTime time, string key)
        {
            AvroRandomAccessReader<V> reader = GetReader(ValueLocationStrategy.GetLocation(time, key));
            if (reader == null) { throw new Exception(string.Format("Document could not be located: {0} - {1}", time, key)); }
            return reader.Get(key);
        }

        public void AddValue(DateTime time, V value)
        {
            string key = ValueDef.GetValueKey(value);
            string location = ValueLocationStrategy.GetLocation(time, key);
            using (AvroRandomAccessWriter<V> writer = GetWriter(location))
            {
                writer.Write(value);
            };
            lock (mMasterIndex)
            {
                mMasterIndex.Add(Guid.Parse(key), location);
            }
        }

        public void AddDocuments(DateTime time, IEnumerable<V> valueList)
        {
            var writers = new Dictionary<string, AvroRandomAccessWriter<V>>();
            try
            {
                foreach (V value in valueList)
                {
                    string key = ValueDef.GetValueKey(value);
                    string location = ValueLocationStrategy.GetLocation(time, key);

                    AvroRandomAccessWriter<V> writer;
                    if (!writers.TryGetValue(location, out writer))
                    {
                        writer = GetWriter(location);
                        writers.Add(location, writer);
                    }
                    writer.Write(value);
                    lock (mMasterIndex)
                    {
                        mMasterIndex.Add(Guid.Parse(key), location);
                    }
                }
            }
            finally
            {
                foreach (KeyValuePair<string, AvroRandomAccessWriter<V>> pair in writers)
                {
                    pair.Value.Dispose();
                }
            }
        }

        public void BuildMasterIndex()
        {
            lock (mMasterIndex)
            {
                mMasterIndex.Clear();
                foreach (string location in Directory.EnumerateFiles(RootDirectory, "*.avro", SearchOption.AllDirectories))
                {
                    DateTime time;
                    string key;
                    if (ValueLocationStrategy.TryGetKey(location, out time, out key))
                    {
                        try
                        {
                            foreach (string k in GetIndex(location).Get().Keys)
                            {
                                Guid guid = Guid.Parse(k);
                                string value = ValueLocationStrategy.GetLocation(time, k);
                                mMasterIndex.Add(guid, value);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        public bool TryGetDocument(string key, out V value)
        {
            lock (mMasterIndex)
            {
                Guid guid = Guid.Parse(key);
                string location;
                if (mMasterIndex.TryGetValue(guid, out location))
                {
                    return GetReader(location).TryGet(key, out value);
                }
                value = default(V);
                return false;
            }
        }

        private AvroRandomAccessIndex<V> GetIndex(string location)
        {
            lock (mLocIndexes)
            {
                return mLocIndexes.ContainsKey(location)
                    ? mLocIndexes[location]
                    : new AvroRandomAccessReader<V>(ValueDef, location).Index;
            }
        }

        private AvroRandomAccessReader<V> GetReader(string location)
        {
            if (File.Exists(location))
            {
                lock (mLocIndexes)
                {
                    AvroRandomAccessIndex<V> index;
                    mLocIndexes.TryGetValue(location, out index);
                    var reader = new AvroRandomAccessReader<V>(ValueDef, location, index);
                    if (index == null)
                    {
                        mLocIndexes.Add(location, reader.Index);
                    }
                    return reader;
                }
            }
            return null;
        }

        private AvroRandomAccessWriter<V> GetWriter(string location)
        {
            if (File.Exists(location))
            {
                lock (mLocIndexes)
                {
                    AvroRandomAccessIndex<V> index;
                    mLocIndexes.TryGetValue(location, out index);
                    var writer = new AvroRandomAccessWriter<V>(ValueDef, location, index);
                    if (index == null)
                    {
                        mLocIndexes.Add(location, writer.Index);
                    }
                    return writer;
                }
            }
            string directoryName = Path.GetDirectoryName(location);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return new AvroRandomAccessWriter<V>(ValueDef, location, Codec);
        }
    }
}