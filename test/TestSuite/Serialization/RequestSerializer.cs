﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using Amazon.Util;

namespace AWS.SignatureVersion4.TestSuite.Serialization
{
    public class RequestSerializer
    {
        private readonly string filePath;
        private readonly Regex methodAndPathAndQueryRegex;
        private readonly Regex headerRegex;

        public RequestSerializer(string filePath)
        {
            this.filePath = filePath;

            methodAndPathAndQueryRegex = new Regex(@"^(?<method>\w+) (?<pathAndQuery>(?!HTTP/1.1).+) HTTP/1.1$");
            headerRegex = new Regex(@"^(?<headerName>[\w-]+):(?<headerValue>.+)$");
        }

        public HttpRequestMessage Deserialize()
        {
            var buffer = new FileBuffer(filePath);
            var to = new HttpRequestMessage();

            // Method, path and query are defined on the first row
            var (method, pathAndQuery) = ParseMethodAndPathAndQuery(buffer);
            to.Method = method;
            to.RequestUri = new UriBuilder("https://example.amazonaws.com" + pathAndQuery).Uri;

            // The content, if existing, is defined in two last rows
            to.Content = ParseContent(buffer);

            // Everything else is headers
            foreach (var (name, value) in ParseHeaders(buffer))
            {
                // Skip the 'X-Amz-Date' header, since it is defined in the test context, and it is
                // the responsibility of the signing algorithm to add it to the request
                if (name == HeaderKeys.XAmzDateHeader) continue;

                to.Headers.Add(name, value);
            }

            return to;
        }

        private (HttpMethod, string) ParseMethodAndPathAndQuery(FileBuffer buffer)
        {
            var row = buffer.PopFirst();

            var match = methodAndPathAndQueryRegex.Match(row);
            if (!match.Success)
            {
                throw new ArgumentException($"The row '{row}' does not match the expected method, path and query format");
            }

            var method = new HttpMethod(match.Groups["method"].Value);
            var pathAndQuery = match.Groups["pathAndQuery"].Value;

            return (method, pathAndQuery);
        }

        private static HttpContent ParseContent(FileBuffer buffer)
        {
            // If the second last row is a empty string, then the last row is the request content
            if (buffer.Length >= 2 && buffer[buffer.Length - 2] == string.Empty)
            {
                var content = buffer.PopLast();

                // Lets also remove the empty row
                buffer.PopLast();

                return new StringContent(content);
            }

            return null;
        }

        private KeyValuePair<string, string>[] ParseHeaders(FileBuffer buffer)
        {
            var headers = new List<KeyValuePair<string, string>>();
            string row;
            string headerName = null;

            while (!string.IsNullOrEmpty(row = buffer.TryPopFirst()))
            {
                var match = headerRegex.Match(row);

                if (match.Success)
                {
                    headerName = match.Groups["headerName"].Value;
                    var headerValue = match.Groups["headerValue"].Value;

                    headers.Add(new KeyValuePair<string, string>(headerName, headerValue));
                }
                else if (headerName != null)
                {
                    // Assume this is a multi-line header value, where the name was defined on a
                    // previous row
                    headers.Add(new KeyValuePair<string, string>(headerName, row));
                }
                else
                {
                    throw new Exception($"The row '{row}' does not match the expected header format");
                }
            }

            return headers.ToArray();
        }
    }
}
