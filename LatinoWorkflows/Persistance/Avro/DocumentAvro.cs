using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avro;
using Avro.File;
using Avro.Generic;
using Latino.Workflows.TextMining;
using Latino.Workflows.Persistance.Avro.Utils;

namespace Latino.Workflows.Persistance.Avro
{
    public class DocAvroFileValueDef : IAvroFileValueDef<Document>
    {
        public static DocAvroFileValueDef Instance = new DocAvroFileValueDef();

        public Schema Schema { get { return Schema.Parse(GetManifestResourceString("news_document.avsc")); } }

        public string GetValueKey(Document value)
        {
            return value.Features.GetFeatureValue("guid");
        }

        private static string GetManifestResourceString(string resName)
        {
            string fullResName = Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(res => res.EndsWith(resName));
            if (fullResName == null) { throw new Exception(String.Format("The specified resource name does not exist")); }
            Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullResName);
            return resStream == null ? null : new StreamReader(resStream).ReadToEnd();
        }

        public GenericRecord GetRecord(Document doc)
        {
            Preconditions.CheckNotNullArgument(doc);
            var recordSchema = (RecordSchema)Schema;
            var record = new GenericRecord(recordSchema);
            record.Add("name", doc.Name);
            record.Add("text", doc.Text);

            var features = new Dictionary<string, object>();
            foreach (KeyValuePair<string, string> feature in doc.Features)
            {
                features[feature.Key] = feature.Value;
            }
            record.Add("features", features);

            var annSchema = (RecordSchema)((ArraySchema)((UnionSchema)recordSchema["annotations"].Schema).Schemas[1]).ItemSchema;
            var annRecords = new List<GenericRecord>();
            foreach (Annotation annotation in doc.Annotations)
            {
                var annRecord = new GenericRecord(annSchema);
                annRecord.Add("type", annotation.Type);
                annRecord.Add("spanStart", annotation.SpanStart);
                annRecord.Add("spanEnd", annotation.SpanEnd);
                features = new Dictionary<string, object>();
                foreach (KeyValuePair<string, string> feature in annotation.Features)
                {
                    features[feature.Key] = feature.Value;
                }
                annRecord.Add("features", features);
                annRecords.Add(annRecord);
            }
            record.Add("annotations", annRecords.ToArray());

            return record;
        }

        public Document GetValue(GenericRecord record)
        {
            Preconditions.CheckNotNullArgument(record);
            var doc = new Document(record["name"] as string, record["text"] as string);
            var features = record["features"] as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> feature in features)
            {
                doc.Features.SetFeatureValue(feature.Key, feature.Value as string);
            }
            var annotations = (Object[])record["annotations"];
            foreach (GenericRecord annRecord in annotations)
            {
                var type = annRecord["type"] as string;
                var spanStart = (int)annRecord["spanStart"];
                var spanEnd = (int)annRecord["spanEnd"];
                var annotation = new Annotation(spanStart, spanEnd, type);
                features = annRecord["features"] as Dictionary<string, object>;
                foreach (KeyValuePair<string, object> feature in features)
                {
                    annotation.Features.SetFeatureValue(feature.Key, feature.Value as string);
                }
                doc.AddAnnotation(annotation);
            }
            return doc;
        }
    }

    public class DocumentAvroRepository : AvroRepository<Document>
    {
        public DocumentAvroRepository(string rootDirectory, Codec.Type codec) 
            : base(DocAvroFileValueDef.Instance, rootDirectory, codec)
        {
        }

        public DocumentAvroRepository(string rootDirectory, IValueLocationStrategy valueLocationStrategy, Codec.Type codec)
            : base(DocAvroFileValueDef.Instance, rootDirectory, valueLocationStrategy, codec)
        {
        }
    }

    public class DocAvroReader : AvroReader<Document>
    {
        public DocAvroReader(FileStream stream) : base(DocAvroFileValueDef.Instance, stream)
        {
        }

        public DocAvroReader(string fileName, FileShare share = FileShare.None) : base(DocAvroFileValueDef.Instance, fileName, share)
        {
        }
    }

    public class DocAvroWriter : AvroWriter<Document>
    {
        private DocAvroWriter(FileStream stream) 
            : base(DocAvroFileValueDef.Instance, stream)
        {
        }

        private DocAvroWriter(FileStream stream, Codec.Type codec) 
            : base(DocAvroFileValueDef.Instance, stream, codec)
        {
        }

        public DocAvroWriter(string fileName, FileShare share = FileShare.None)
            : base(DocAvroFileValueDef.Instance, fileName, share)
        {
        }

        public DocAvroWriter(string fileName, Codec.Type codec)
            : base(DocAvroFileValueDef.Instance, fileName, codec)
        {
        }

        public static DocAvroWriter OpenFile(FileStream stream)
        {
            return new DocAvroWriter(stream);
        }

        public static DocAvroWriter OpenFile(string fileName)
        {
            return new DocAvroWriter(fileName);
        }

        public static DocAvroWriter CreateFile(FileStream stream, Codec.Type codec = Codec.Type.Null)
        {
            return new DocAvroWriter(stream, codec);
        }

        public static DocAvroWriter CreateFile(string fileName, Codec.Type codec = Codec.Type.Null)
        {
            return new DocAvroWriter(fileName, codec);
        }
    }

    public class DocumentAvroRAReader : AvroRandomAccessReader<Document>
    {
        public DocumentAvroRAReader(FileStream stream, AvroRandomAccessIndex<Document> index = null) 
            : base(DocAvroFileValueDef.Instance, stream, index)
        {
        }

        public DocumentAvroRAReader(string fileName, AvroRandomAccessIndex<Document> index = null, FileShare share = FileShare.None)
            : base(DocAvroFileValueDef.Instance, fileName, index, share)
        {
        }
    }

    public class DocumentAvroRAWriter : AvroRandomAccessWriter<Document>
    {
        protected DocumentAvroRAWriter(FileStream stream, Codec.Type codec)
            : base(DocAvroFileValueDef.Instance, stream, codec)
        {
        }

        protected DocumentAvroRAWriter(FileStream stream, AvroRandomAccessIndex<Document> index = null)
            : base(DocAvroFileValueDef.Instance, stream, index)
        {
        }

        public DocumentAvroRAWriter(string fileName, Codec.Type codec)
            : base(DocAvroFileValueDef.Instance, fileName, codec)
        {
        }

        public DocumentAvroRAWriter(string fileName, AvroRandomAccessIndex<Document> index = null, FileShare share = FileShare.None)
            : base(DocAvroFileValueDef.Instance, fileName, index, share)
        {
        }

        public static DocumentAvroRAWriter OpenFile(FileStream stream, AvroRandomAccessIndex<Document> index = null)
        {
            return new DocumentAvroRAWriter(stream, index);
        }

        public static DocumentAvroRAWriter OpenFile(string fileName, AvroRandomAccessIndex<Document> index = null, FileShare share = FileShare.None)
        {
            return new DocumentAvroRAWriter(fileName, index, share);
        }

        public static DocumentAvroRAWriter CreateFile(FileStream stream, Codec.Type codec = Codec.Type.Null)
        {
            return new DocumentAvroRAWriter(stream, codec);
        }

        public static DocumentAvroRAWriter CreateFile(string fileName, Codec.Type codec = Codec.Type.Null)
        {
            return new DocumentAvroRAWriter(fileName, codec);
        }
    }
}