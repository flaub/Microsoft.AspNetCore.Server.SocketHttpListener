using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	public class HeaderDictionary : IHeaderDictionary
	{
        const string CONTENT_LENGTH = "Content-Length";

        private readonly NameValueCollection _collection;
        private long? _contentLength;
        private StringValues _contentLengthText;

        public HeaderDictionary(NameValueCollection collection)
		{
			_collection = collection;
		}

		public int Count => _collection.Count;
		public bool IsReadOnly => false;

		public StringValues this[string key]
		{
			get { return _collection.GetValues(key); }
			set { _collection[key] = value; }
		}

		public ICollection<string> Keys => _collection.Keys.OfType<string>().ToList();

		public ICollection<StringValues> Values => _collection.Keys.OfType<string>()
			.Select(x => _collection.GetValues(x))
			.Cast<StringValues>()
			.ToList();

        public long? ContentLength
        {
            get
            {
                long value;
                var rawValue = this[CONTENT_LENGTH];

                if (_contentLengthText.Equals(rawValue))
                {
                    return _contentLength;
                }

                if (rawValue.Count == 1 &&
                    !string.IsNullOrWhiteSpace(rawValue[0]) &&
                    HeaderUtilities.TryParseNonNegativeInt64(new StringSegment(rawValue[0]).Trim(), out value))
                {
                    _contentLengthText = rawValue;
                    _contentLength = value;
                    return value;
                }

                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    if (value.Value < 0)
                    {
                        throw new ArgumentOutOfRangeException("value", value.Value, "Cannot be negative.");
                    }
                    _contentLengthText = HeaderUtilities.FormatNonNegativeInt64(value.Value);
                    this[CONTENT_LENGTH] = _contentLengthText;
                    _contentLength = value;
                }
                else
                {
                    Remove(CONTENT_LENGTH);
                    _contentLengthText = StringValues.Empty;
                    _contentLength = null;
                }
            }
        }

        public void Add(KeyValuePair<string, StringValues> item) => Add(item.Key, item.Value);

		public void Add(string key, StringValues value)
		{
			_collection.Add(key, value);
		}

		public void Clear()
		{
			_collection.Clear();
		}

		public bool Contains(KeyValuePair<string, StringValues> item) => ContainsKey(item.Key);

		public bool ContainsKey(string key)
		{
			return _collection.Keys.OfType<string>().Contains(key);
		}

		public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex)
		{
			for (var i = arrayIndex; i < array.Length - arrayIndex; i++)
			{
				var key = _collection.GetKey(i);
				var values = _collection.GetValues(i);
				array[i] = new KeyValuePair<string, StringValues>(key, values);
			}
		}

		public bool Remove(KeyValuePair<string, StringValues> item) => Remove(item.Key);

		public bool Remove(string key)
		{
			if (!ContainsKey(key))
				return false;

			_collection.Remove(key);
			return true;
		}

		public bool TryGetValue(string key, out StringValues value)
		{
			if (ContainsKey(key))
			{
				value = _collection.GetValues(key);
				return true;
			}

			value = default(StringValues);
			return false;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
		{
			foreach (string key in _collection.Keys)
				yield return new KeyValuePair<string, StringValues>(key, _collection.GetValues(key));
		}
	}
}