using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avro;
using Avro.File;
using Avro.Generic;
using Avro.IO;
using Latino.Workflows.Persistance.Avro.Utils;

namespace Latino.Workflows.Persistance.Avro
{
    public interface IAvroFileValueDef<V>
    {
        Schema Schema { get; }
        string GetValueKey(V value);

        V GetValue(GenericRecord record);
        GenericRecord GetRecord(V value);
    }

    public abstract class AvroFileAccess<V> : IDisposable
    {
        public static readonly Schema IndexSchema = Schema.Parse("{\"type\": \"map\", \"values\": \"long\"}");

        protected AvroFileAccess(IAvroFileValueDef<V> valueDef, FileStream stream)
        {
            ValueDef = Preconditions.CheckNotNullArgument(valueDef);
            Stream = Preconditions.CheckNotNullArgument(stream);
        }

        public IAvroFileValueDef<V> ValueDef { get; private set; }
        public string FileName { get { return Stream == null ? null : Stream.Name; } }
        public string IndexFileName { get { return Path.Combine(Path.GetDirectoryName(FileName), Path.GetFileNameWithoutExtension(FileName) + "_index.avro"); } }
        public FileStream Stream { get; private set; }

        public virtual void Dispose()
        {
            if (Stream != null) { Stream.Dispose(); }
        }
    }

    public class AvroReader<V> : AvroFileAccess<V>, IEnumerable<V>, IEnumerator<V>
    {
        internal IFileReader<GenericRecord> mReader;

        public AvroReader(IAvroFileValueDef<V> valueDef, FileStream stream) : base(valueDef, stream)
        {
            Preconditions.CheckArgument(Stream.CanRead);
            Reset();
        }

        public AvroReader(IAvroFileValueDef<V> valueDef, string fileName, FileShare share = FileShare.None)
            : this(valueDef, new FileStream(fileName, FileMode.Open, FileAccess.Read, share))
        {
        }

        public IEnumerator<V> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool MoveNext()
        {
            Current = ValueDef.GetValue(mReader.Next());
            return mReader.HasNext();
        }

        public void Reset()
        {
            mReader = DataFileReader<GenericRecord>.OpenReader(Stream, ValueDef.Schema);
            mReader.Sync(0);
        }

        public V Current { get; private set; }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public override void Dispose()
        {
            if (mReader != null) { mReader.Dispose(); }
            base.Dispose();
        }
    }

    public class AvroWriter<V> : AvroFileAccess<V>
    {
        protected readonly IFileWriter<GenericRecord> mWriter;

        public AvroWriter(IAvroFileValueDef<V> valueDef, FileStream stream) : base(valueDef, stream)
        {
            Preconditions.CheckArgument(Stream.CanWrite);
            var datumWriter = new GenericDatumWriter<GenericRecord>(ValueDef.Schema);
            mWriter = DataFileWriter<GenericRecord>.ReopenWriter(datumWriter, Stream);
            Stream.Position = Stream.Length;
        }

        public AvroWriter(IAvroFileValueDef<V> valueDef, string fileName, FileShare share = FileShare.None)
            : this(valueDef, new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, share))
        {
        }

        public AvroWriter(IAvroFileValueDef<V> valueDef, FileStream stream, Codec.Type codec) : base(valueDef, stream)
        {
            Preconditions.CheckArgument(Stream.CanWrite);
            var datumWriter = new GenericDatumWriter<GenericRecord>(ValueDef.Schema);
            mWriter = DataFileWriter<GenericRecord>.OpenWriter(datumWriter, Stream, Codec.CreateCodec(codec));
            Stream.Position = Stream.Length;
        }

        public AvroWriter(IAvroFileValueDef<V> valueDef, string fileName, Codec.Type codec, FileShare share = FileShare.None)
            : this(valueDef, new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, share), codec)
        {
        }

        public virtual void Write(V value)
        {
            Preconditions.CheckNotNullArgument(value);
            GenericRecord record = ValueDef.GetRecord(value);
            mWriter.Append(record);
            mWriter.Sync();
        }

        public override void Dispose()
        {
            if (mWriter != null) { mWriter.Dispose(); }
            base.Dispose();
        }
    }

    public class AvroRandomAccessIndex<V>
    {
        internal AvroRandomAccessIndex(string fileName)
        {
            FileName = Preconditions.CheckNotNullArgument(fileName);
            Index = new Dictionary<string, object>();
        }

        public string FileName { get; private set; }
        
        private bool IsIndexWritePending { get; set; }
        private Dictionary<string, object> Index { get; set; }

        public Dictionary<string, object> Get()
        {
            lock (Index)
            {
                return Index.ToDictionary(kv => kv.Key, kv => kv.Value);
            }
        }

        internal bool TryGetEntry(string key, out long position)
        {
            lock (Index)
            {
                object value;
                if (Index.TryGetValue(key, out value))
                {
                    position = (long)value;
                    return true;
                }
            }
            position = -1;
            return false;
        }

        internal void Read()
        {
            lock (Index)
            {
                using (var indexStream = new FileStream(FileName, FileMode.Open))
                {
                    Decoder indexDecoder = new BinaryDecoder(indexStream);
                    var indexReader = new GenericReader<Dictionary<string, object>>(AvroFileAccess<V>.IndexSchema, AvroFileAccess<V>.IndexSchema);
                    Index = indexReader.Read(new Dictionary<string, object>(), indexDecoder);
                }
            }
        }

        internal void Write()
        {
            if (IsIndexWritePending)
            {
                lock (Index)
                {
                    using (var indexStream = new FileStream(FileName, FileMode.Create))
                    {
                        var indexEncoder = new BinaryEncoder(indexStream);
                        var indexWriter = new GenericWriter<Dictionary<string, object>>(AvroFileAccess<V>.IndexSchema);
                        indexWriter.Write(Index, indexEncoder);
                    }
                }
            }
        }

        internal void AddEntry(string key, long position)
        {
            lock (Index)
            {
                Index.Add(key, position);
                IsIndexWritePending = true;
            }
        }

        internal void Build(AvroFileAccess<V> fileAccess)
        {
            Preconditions.CheckNotNullArgument(fileAccess);
            Preconditions.CheckArgument(fileAccess.Stream.Name == FileName);
            Preconditions.CheckArgument(fileAccess.Stream.CanRead);

            lock (Index)
            {
                Index = new Dictionary<string, object>();
                long oldPosition = fileAccess.Stream.Position;
                try
                {
                    fileAccess.Stream.Position = 0;
                    IFileReader<GenericRecord> reader = DataFileReader<GenericRecord>.OpenReader(fileAccess.Stream, fileAccess.ValueDef.Schema);
                    while (reader.HasNext())
                    {
                        long position = reader.PreviousSync();
                        GenericRecord record = reader.Next();
                        V value = fileAccess.ValueDef.GetValue(record);
                        Index.Add(fileAccess.ValueDef.GetValueKey(value), position);
                        IsIndexWritePending = true;
                    }
                }
                finally
                {
                    fileAccess.Stream.Position = oldPosition;
                }
            }
        }
    }

    public class AvroRandomAccessReader<V> : AvroReader<V>
    {
        public AvroRandomAccessReader(IAvroFileValueDef<V> valueDef, FileStream stream, AvroRandomAccessIndex<V> index = null)
            : base(valueDef, stream)
        {
            Preconditions.CheckArgument(Stream.CanSeek);
            Index = index ?? new AvroRandomAccessIndex<V>(IndexFileName);
            if (index == null)
            {
                Preconditions.CheckState(File.Exists(IndexFileName), "Random access index is not available");
                Index.Read();
            }
        }

        public AvroRandomAccessReader(IAvroFileValueDef<V> valueDef, string fileName, AvroRandomAccessIndex<V> index = null, FileShare share = FileShare.None) 
            : this(valueDef, new FileStream(fileName, FileMode.Open, FileAccess.Read, share), index)
        {
        }

        public AvroRandomAccessIndex<V> Index { get; private set; }
        public V this[string key] { get { return Get(key); } }

        public bool TryGet(string key, out V value)
        {
            Preconditions.CheckNotNullArgument(key);
            long position;
            if (Index.TryGetEntry(key, out position))
            {
                if (position == 0) { mReader.Sync(position); } // lib issue workaround
                else { mReader.Seek(position); }
                value = ValueDef.GetValue(mReader.Next());
                return true;
            }
            value = default(V);
            return false;
        }

        public V Get(string key)
        {
            V value;
            if (TryGet(key, out value)) { return value; }
            throw new Exception("Key not found: " + key);
        }
    }

    public class AvroRandomAccessWriter<V> : AvroWriter<V>
    {
        public AvroRandomAccessWriter(IAvroFileValueDef<V> valueDef, FileStream stream, Codec.Type codec)
            : base(valueDef, stream, codec)
        {
            Preconditions.CheckArgument(Stream.CanSeek);
            Index = new AvroRandomAccessIndex<V>(IndexFileName);
        }

        public AvroRandomAccessWriter(IAvroFileValueDef<V> valueDef, FileStream stream, AvroRandomAccessIndex<V> index)
            : base(valueDef, stream)
        {
            Preconditions.CheckArgument(Stream.CanSeek);
            Index = index ?? new AvroRandomAccessIndex<V>(IndexFileName);
            if (index == null)
            {
                if (File.Exists(IndexFileName)) { Index.Read(); } else { BuildIndex(); }
            }
        }

        public AvroRandomAccessWriter(IAvroFileValueDef<V> valueDef, string fileName, Codec.Type codec, FileShare share = FileShare.None)
            : base(valueDef, new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, share), codec)
        {
            Index = new AvroRandomAccessIndex<V>(IndexFileName);
        }

        public AvroRandomAccessWriter(IAvroFileValueDef<V> valueDef, string fileName, AvroRandomAccessIndex<V> index = null, FileShare share = FileShare.None)
            : base(valueDef, new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, share))
        {
            Index = index ?? new AvroRandomAccessIndex<V>(IndexFileName);
            if (index == null)
            {
                if (File.Exists(IndexFileName)) { Index.Read(); } else { BuildIndex(); }
            }
        }

        public AvroRandomAccessIndex<V> Index { get; private set; }

        public void BuildIndex()
        {
            Index.Build(this);
        }

        public override void Write(V value)
        {
            Preconditions.CheckNotNullArgument(value);
            GenericRecord record = ValueDef.GetRecord(value);
            long position = mWriter.Sync();
            mWriter.Append(record);
            Index.AddEntry(ValueDef.GetValueKey(value), position);
        }

        public void WriteIndex()
        {
            Index.Write();
        }

        public override void Dispose()
        {
            WriteIndex(); 
            base.Dispose();
        }
    }
}